using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace OFS.Sdk;

/// <summary>
/// Internal fixed-size format shared by the runtime and manager. The active
/// commit is written last so an interrupted update cannot expose a partial
/// payload as valid crash evidence.
/// </summary>
internal static class HotCrashBreadcrumbCodec
{
    internal const string FileName = "hot-callback.bin";
    internal const int FileSize = 16 * 1024;
    internal const int CommitOffset = 56;
    internal const int PayloadOffset = 64;
    private const int FormatVersion = 1;
    private static ReadOnlySpan<byte> Magic => "OFSHOT01"u8;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static byte[] CreateInactiveTemplate(ModLoadJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        var errors = ModSafetyDocuments.Validate(journal);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(string.Join(" ", errors));
        }
        if (!journal.Phase.StartsWith("callback:hot:", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Hot breadcrumb phases must start with 'callback:hot:'.",
                nameof(journal));
        }

        var payload = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
        if (payload.Length > FileSize - PayloadOffset)
        {
            throw new InvalidDataException("Hot callback breadcrumb payload is too large.");
        }

        var result = new byte[PayloadOffset + payload.Length];
        Magic.CopyTo(result);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(8), FormatVersion);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(12), payload.Length);
        SHA256.HashData(payload).CopyTo(result, 16);
        payload.CopyTo(result, PayloadOffset);
        // CommitOffset remains zero until the mapped writer publishes it.
        return result;
    }

    internal static bool TryReadActive(
        ReadOnlySpan<byte> source,
        out ModLoadJournal? journal,
        out string error)
    {
        journal = null;
        error = string.Empty;
        if (source.Length == 0)
        {
            return false;
        }
        if (source.Length != FileSize)
        {
            error = $"Hot callback breadcrumb has invalid size {source.Length}.";
            return false;
        }

        var commit = BinaryPrimitives.ReadInt32LittleEndian(source[CommitOffset..]);
        if (commit == 0)
        {
            return false;
        }
        if (commit != 1)
        {
            error = $"Hot callback breadcrumb has invalid commit state {commit}.";
            return false;
        }
        if (!source[..Magic.Length].SequenceEqual(Magic))
        {
            error = "Hot callback breadcrumb magic is invalid.";
            return false;
        }
        var version = BinaryPrimitives.ReadInt32LittleEndian(source[8..]);
        if (version != FormatVersion)
        {
            error = $"Unsupported hot callback breadcrumb format {version}.";
            return false;
        }
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(source[12..]);
        if (payloadLength <= 0 || payloadLength > FileSize - PayloadOffset)
        {
            error = $"Hot callback breadcrumb payload length {payloadLength} is invalid.";
            return false;
        }
        var payload = source.Slice(PayloadOffset, payloadLength);
        Span<byte> actualHash = stackalloc byte[32];
        SHA256.HashData(payload, actualHash);
        if (!CryptographicOperations.FixedTimeEquals(source.Slice(16, 32), actualHash))
        {
            error = "Hot callback breadcrumb payload hash is invalid.";
            return false;
        }

        try
        {
            journal = JsonSerializer.Deserialize<ModLoadJournal>(payload, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Hot callback breadcrumb JSON is invalid: {exception.Message}";
            return false;
        }
        var errors = ModSafetyDocuments.Validate(journal);
        if (errors.Count != 0 ||
            !journal!.Phase.StartsWith("callback:hot:", StringComparison.Ordinal))
        {
            error = errors.Count == 0
                ? "Hot callback breadcrumb phase is invalid."
                : string.Join(" ", errors);
            journal = null;
            return false;
        }
        return true;
    }
}
