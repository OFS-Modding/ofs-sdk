using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace OFS.Sdk;

internal enum CatalogThumbnailFormat
{
    Png,
    Jpeg,
}

internal sealed record CatalogThumbnailFile(
    string Path,
    CatalogThumbnailFormat Format,
    int Width,
    int Height,
    bool FromCache);

internal sealed class CatalogThumbnailStore
{
    internal const int MaximumDimension = 1024;
    internal const long MaximumPixels = 1024L * 1024L;
    private const int MaximumCachedFiles = 128;
    private const long MaximumCacheBytes = 64L * 1024L * 1024L;
    private readonly string _root;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, Lazy<Task<CatalogThumbnailFile>>> _inflight =
        new(StringComparer.OrdinalIgnoreCase);

    public CatalogThumbnailStore(string root, HttpClient http)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(http);
        _root = Path.GetFullPath(root);
        _http = http;
    }

    public async Task<CatalogThumbnailFile> GetOrFetchAsync(
        ModCatalogThumbnail thumbnail,
        CancellationToken cancellationToken = default)
    {
        ValidateMetadata(thumbnail);
        var hash = thumbnail.Sha256.ToLowerInvariant();
        var lazy = _inflight.GetOrAdd(
            hash,
            _ => new Lazy<Task<CatalogThumbnailFile>>(
                () => GetOrFetchCoreAsync(thumbnail, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazy.Value.ConfigureAwait(false);
        }
        catch
        {
            _inflight.TryRemove(new KeyValuePair<string, Lazy<Task<CatalogThumbnailFile>>>(hash, lazy));
            throw;
        }
    }

    public static (CatalogThumbnailFormat Format, int Width, int Height) Inspect(byte[] bytes)
        => InspectRaster(bytes, MaximumDimension, MaximumPixels, "Thumbnail");

    internal static (CatalogThumbnailFormat Format, int Width, int Height) InspectRaster(
        byte[] bytes,
        int maximumDimension,
        long maximumPixels,
        string subject = "Image")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (maximumDimension <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDimension));
        if (maximumPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumPixels));
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var result = TryInspectPng(bytes, out var width, out var height)
            ? (CatalogThumbnailFormat.Png, width, height)
            : TryInspectJpeg(bytes, out width, out height)
                ? (CatalogThumbnailFormat.Jpeg, width, height)
                : throw new InvalidDataException($"{subject} must be a PNG or JPEG image.");
        if (result.width <= 0 || result.width > maximumDimension ||
            result.height <= 0 || result.height > maximumDimension ||
            (long)result.width * result.height > maximumPixels)
        {
            throw new InvalidDataException(
                $"{subject} dimensions {result.width}x{result.height} exceed the " +
                $"{maximumDimension}px/{maximumPixels} pixel limit.");
        }
        return (result.Item1, result.width, result.height);
    }

    private async Task<CatalogThumbnailFile> GetOrFetchCoreAsync(
        ModCatalogThumbnail thumbnail,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);
        var destination = ResolveCachePath(thumbnail.Sha256);
        var cached = await TryReadVerifiedAsync(destination, thumbnail, true, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            File.SetLastAccessTimeUtc(destination, DateTime.UtcNow);
            return cached;
        }

        var temporary = destination + $".tmp-{Guid.NewGuid():N}";
        try
        {
            var uri = new Uri(thumbnail.Url, UriKind.Absolute);
            using var response = await _http.GetAsync(
                    uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException("HTTPS downgrade detected while downloading thumbnail.");
            }
            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength != thumbnail.Bytes)
            {
                throw new InvalidDataException(
                    $"Thumbnail Content-Length is {contentLength}; expected {thumbnail.Bytes}.");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
            using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long total = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                total = checked(total + read);
                if (total > thumbnail.Bytes || total > ModCatalogValidator.MaximumThumbnailBytes)
                {
                    throw new InvalidDataException("Thumbnail exceeded its declared size.");
                }
                incrementalHash.AppendData(buffer.AsSpan(0, read));
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            await output.DisposeAsync().ConfigureAwait(false);
            var actualHash = Convert.ToHexString(incrementalHash.GetHashAndReset());
            if (total != thumbnail.Bytes ||
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actualHash),
                    Convert.FromHexString(thumbnail.Sha256)))
            {
                throw new InvalidDataException("Thumbnail failed SHA-256/size verification.");
            }

            var verified = await TryReadVerifiedAsync(
                    temporary,
                    thumbnail,
                    false,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidDataException("Downloaded thumbnail could not be verified.");
            try
            {
                File.Move(temporary, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                File.Delete(temporary);
            }
            Prune(destination);
            return verified with { Path = destination };
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task<CatalogThumbnailFile?> TryReadVerifiedAsync(
        string path,
        ModCatalogThumbnail thumbnail,
        bool fromCache,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != thumbnail.Bytes)
        {
            return null;
        }
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var actualHash = SHA256.HashData(bytes);
        if (!CryptographicOperations.FixedTimeEquals(
                actualHash,
                Convert.FromHexString(thumbnail.Sha256)))
        {
            if (fromCache)
            {
                File.Delete(path);
            }
            return null;
        }
        var inspected = Inspect(bytes);
        return new CatalogThumbnailFile(
            path,
            inspected.Format,
            inspected.Width,
            inspected.Height,
            fromCache);
    }

    private string ResolveCachePath(string hash)
    {
        var normalizedRoot = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(_root, hash.ToLowerInvariant() + ".img"));
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Thumbnail cache path escaped its root.");
        }
        return path;
    }

    private void Prune(string protectedPath)
    {
        var files = Directory.EnumerateFiles(_root, "*.img", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => file.Name.Length == 68 &&
                           file.Name.AsSpan(0, 64).ToString().All(Uri.IsHexDigit))
            .OrderByDescending(file => file.LastAccessTimeUtc)
            .ThenBy(file => file.Name, StringComparer.Ordinal)
            .ToArray();
        long retainedBytes = 0;
        for (var index = 0; index < files.Length; index++)
        {
            var keep = index < MaximumCachedFiles &&
                       retainedBytes + files[index].Length <= MaximumCacheBytes;
            if (keep || string.Equals(files[index].FullName, protectedPath, StringComparison.OrdinalIgnoreCase))
            {
                retainedBytes += files[index].Length;
                continue;
            }
            try
            {
                files[index].Delete();
            }
            catch (IOException)
            {
                // Another fetch may currently be reading this immutable cache entry.
            }
        }
    }

    private static void ValidateMetadata(ModCatalogThumbnail thumbnail)
    {
        ArgumentNullException.ThrowIfNull(thumbnail);
        var probe = new ModCatalog
        {
            GeneratedAtUtc = DateTimeOffset.UnixEpoch,
            GameBuild = "probe",
            FrameworkVersion = "0.1.0",
            Mods =
            [
                new ModCatalogEntry
                {
                    Id = "ofs.thumbnail-probe",
                    Name = "Thumbnail Probe",
                    Version = "0.1.0",
                    SdkVersion = "0.1.0",
                    GameBuilds = ["probe"],
                    Thumbnail = thumbnail,
                    Package = new ModCatalogPackage
                    {
                        Url = "https://example.invalid/probe.ofmod",
                        Bytes = 1,
                        Sha256 = new string('0', 64),
                    },
                },
            ],
        };
        var errors = ModCatalogValidator.Validate(probe)
            .Where(error => error.Contains("thumbnail", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidDataException(string.Join(" ", errors));
        }
    }

    private static bool TryInspectPng(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 24 || !bytes[..8].SequenceEqual(signature) ||
            !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return false;
        }
        width = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bytes.Slice(16, 4)));
        height = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bytes.Slice(20, 4)));
        return true;
    }

    private static bool TryInspectJpeg(ReadOnlySpan<byte> bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return false;
        }
        var offset = 2;
        while (offset + 4 <= bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                return false;
            }
            while (offset < bytes.Length && bytes[offset] == 0xFF) offset++;
            if (offset >= bytes.Length) return false;
            var marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9) continue;
            if (marker == 0xDA) return false;
            if (offset + 2 > bytes.Length) return false;
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            if (length < 2 || offset + length > bytes.Length) return false;
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or
                0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (length < 7) return false;
                height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                return true;
            }
            offset += length;
        }
        return false;
    }
}
