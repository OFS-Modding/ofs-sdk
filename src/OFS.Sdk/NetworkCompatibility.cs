using System.Security.Cryptography;
using System.Text;

namespace OFS.Sdk;

public sealed record NetworkModIdentity(
    string Id,
    string Version,
    string Multiplayer);

public sealed record NetworkCompatibilityProfile(
    int ProtocolVersion,
    string GameBuild,
    string FrameworkVersion,
    IReadOnlyList<NetworkModIdentity> RequiredMods,
    IReadOnlyList<NetworkModIdentity> IncompatibleMods,
    string Fingerprint)
{
    public bool BlocksMultiplayer => IncompatibleMods.Count != 0;
    public bool RequiresExactMatch => RequiredMods.Count != 0;
}

public enum NetworkCompatibilityStatus
{
    Unknown = 0,
    Compatible = 1,
    CompatibleLegacyHost = 2,
    MissingHostProfile = 3,
    FingerprintMismatch = 4,
    LocalProfileBlocksMultiplayer = 5,
    Error = 6,
    MissingPeerProfile = 7,
    AuthenticationTimeout = 8,
    InvalidPeerProfile = 9,
}

public sealed record NetworkCompatibilityResult(
    NetworkCompatibilityStatus Status,
    bool Allowed,
    string Message,
    string? HostFingerprint = null)
{
    public IReadOnlyList<NetworkModDifference> ModDifferences { get; init; } = [];
    public IReadOnlyList<NetworkModIdentity> RemoteRequiredMods { get; init; } = [];
}

public enum NetworkModDifferenceKind
{
    MissingLocal = 0,
    VersionMismatch = 1,
    UnexpectedLocal = 2,
}

public sealed record NetworkModDifference(
    NetworkModDifferenceKind Kind,
    string Id,
    string? LocalVersion,
    string? RemoteVersion);

/// <summary>Canonical, bounded Steam metadata for the required-mod set.</summary>
public static class NetworkProfileMetadata
{
    public const int MaximumEncodedCharacters = 4096;
    public const int MaximumModCount = 128;

    public static string EncodeRequiredMods(IEnumerable<NetworkModIdentity> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);
        var canonical = ValidateAndSort(mods, nameof(mods));
        var invalidClassification = canonical.FirstOrDefault(mod =>
            mod.Multiplayer is not ("required" or "unknown"));
        if (invalidClassification is not null)
        {
            throw new ArgumentException(
                $"Required-mod metadata cannot contain '{invalidClassification.Id}' " +
                $"classified as '{invalidClassification.Multiplayer}'.",
                nameof(mods));
        }
        var encoded = string.Join(",", canonical.Select(mod => $"{mod.Id}@{mod.Version}"));
        if (encoded.Length > MaximumEncodedCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mods),
                $"Required-mod metadata exceeds {MaximumEncodedCharacters} characters.");
        }
        return encoded;
    }

    public static bool TryDecodeRequiredMods(
        string? value,
        out IReadOnlyList<NetworkModIdentity> mods,
        out string error)
    {
        mods = [];
        error = string.Empty;
        if (value is null)
        {
            error = "Required-mod metadata is missing.";
            return false;
        }
        if (value.Length > MaximumEncodedCharacters)
        {
            error = $"Required-mod metadata exceeds {MaximumEncodedCharacters} characters.";
            return false;
        }
        if (value.Length == 0)
        {
            return true;
        }

        var parsed = new List<NetworkModIdentity>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in value.Split(','))
        {
            var separator = segment.IndexOf('@');
            if (separator <= 0 || separator != segment.LastIndexOf('@') ||
                separator == segment.Length - 1)
            {
                error = $"Invalid required-mod metadata segment '{segment}'.";
                return false;
            }
            var id = segment[..separator];
            var version = segment[(separator + 1)..];
            if (!ModManifestValidator.IsValidId(id) ||
                !ModVersion.TryParse(version, out _) ||
                !seen.Add(id))
            {
                error = $"Invalid or duplicate required mod '{id}@{version}'.";
                return false;
            }
            parsed.Add(new NetworkModIdentity(id, version, "required"));
            if (parsed.Count > MaximumModCount)
            {
                error = $"Required-mod metadata exceeds {MaximumModCount} entries.";
                return false;
            }
        }

        mods = parsed
            .OrderBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Version, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    public static IReadOnlyList<NetworkModDifference> CompareRequiredMods(
        IEnumerable<NetworkModIdentity> local,
        IEnumerable<NetworkModIdentity> remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);
        var localById = ValidateAndSort(local, nameof(local))
            .ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var remoteById = ValidateAndSort(remote, nameof(remote))
            .ToDictionary(mod => mod.Id, StringComparer.OrdinalIgnoreCase);
        var ids = localById.Keys.Concat(remoteById.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase);
        var differences = new List<NetworkModDifference>();
        foreach (var id in ids)
        {
            var hasLocal = localById.TryGetValue(id, out var localMod);
            var hasRemote = remoteById.TryGetValue(id, out var remoteMod);
            if (!hasLocal)
            {
                differences.Add(new NetworkModDifference(
                    NetworkModDifferenceKind.MissingLocal,
                    remoteMod!.Id,
                    null,
                    remoteMod.Version));
            }
            else if (!hasRemote)
            {
                differences.Add(new NetworkModDifference(
                    NetworkModDifferenceKind.UnexpectedLocal,
                    localMod!.Id,
                    localMod.Version,
                    null));
            }
            else if (!string.Equals(localMod!.Version, remoteMod!.Version, StringComparison.Ordinal))
            {
                differences.Add(new NetworkModDifference(
                    NetworkModDifferenceKind.VersionMismatch,
                    remoteMod.Id,
                    localMod.Version,
                    remoteMod.Version));
            }
        }
        return differences;
    }

    private static IReadOnlyList<NetworkModIdentity> ValidateAndSort(
        IEnumerable<NetworkModIdentity> mods,
        string parameterName)
    {
        var result = new List<NetworkModIdentity>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
        {
            if (mod is null ||
                !ModManifestValidator.IsValidId(mod.Id) ||
                !ModVersion.TryParse(mod.Version, out _) ||
                !seen.Add(mod.Id))
            {
                throw new ArgumentException(
                    $"Invalid or duplicate network mod identity '{mod?.Id ?? "<null>"}'.",
                    parameterName);
            }
            result.Add(mod);
            if (result.Count > MaximumModCount)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Network profile exceeds {MaximumModCount} mods.");
            }
        }
        return result
            .OrderBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Version, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed record NetworkAuthenticationCredentials(
    string Mode,
    string Username,
    string Password);

public static class NetworkAuthenticationProfiles
{
    public const string MirrorBasicMode = "mirror-basic-v1";

    public static NetworkAuthenticationCredentials Create(
        NetworkCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new NetworkAuthenticationCredentials(
            MirrorBasicMode,
            $"ofs/{profile.ProtocolVersion}/{profile.GameBuild}/{profile.FrameworkVersion}",
            profile.Fingerprint);
    }
}

public static class NetworkCompatibilityProfiles
{
    public const int CurrentProtocolVersion = 2;

    public static NetworkCompatibilityProfile Create(
        string gameBuild,
        string frameworkVersion,
        IEnumerable<NetworkModIdentity> loadedMods)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameBuild);
        if (!ModVersion.TryParse(frameworkVersion, out _))
        {
            throw new ArgumentException("Framework version must be stable semantic version.", nameof(frameworkVersion));
        }
        ArgumentNullException.ThrowIfNull(loadedMods);

        var required = new List<NetworkModIdentity>();
        var incompatible = new List<NetworkModIdentity>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in loadedMods)
        {
            if (!ModManifestValidator.IsValidId(mod.Id) ||
                !ModVersion.TryParse(mod.Version, out _) ||
                !seen.Add(mod.Id))
            {
                throw new ArgumentException($"Invalid or duplicate network mod identity '{mod.Id}'.");
            }
            if (seen.Count > NetworkProfileMetadata.MaximumModCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(loadedMods),
                    $"Network profile exceeds {NetworkProfileMetadata.MaximumModCount} mods.");
            }
            switch (mod.Multiplayer)
            {
                case "client":
                case "server":
                    break;
                case "incompatible":
                    incompatible.Add(mod);
                    break;
                case "required":
                case "unknown":
                    required.Add(mod);
                    break;
                default:
                    throw new ArgumentException(
                        $"Mod '{mod.Id}' has invalid multiplayer classification '{mod.Multiplayer}'.");
            }
        }

        required = required
            .OrderBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Version, StringComparer.Ordinal)
            .ToList();
        incompatible = incompatible
            .OrderBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(mod => mod.Version, StringComparer.Ordinal)
            .ToList();
        _ = NetworkProfileMetadata.EncodeRequiredMods(required);

        var canonical = new StringBuilder()
            .Append("ofs-network-profile/v2\n")
            .Append("game:").Append(gameBuild.ToLowerInvariant()).Append('\n')
            .Append("framework:").Append(frameworkVersion).Append('\n');
        foreach (var mod in required)
        {
            canonical.Append("required:")
                .Append(mod.Id.ToLowerInvariant())
                .Append('@')
                .Append(mod.Version)
                .Append('\n');
        }
        foreach (var mod in incompatible)
        {
            canonical.Append("incompatible:")
                .Append(mod.Id.ToLowerInvariant())
                .Append('@')
                .Append(mod.Version)
                .Append('\n');
        }
        var fingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
        return new NetworkCompatibilityProfile(
            CurrentProtocolVersion,
            gameBuild,
            frameworkVersion,
            required.ToArray(),
            incompatible.ToArray(),
            fingerprint);
    }

    public static NetworkCompatibilityResult CompareHost(
        NetworkCompatibilityProfile local,
        string? hostProtocol,
        string? hostFingerprint)
    {
        var result = CompareRemote(local, hostProtocol, hostFingerprint, "Host");
        return result.Status == NetworkCompatibilityStatus.MissingPeerProfile
            ? result with { Status = NetworkCompatibilityStatus.MissingHostProfile }
            : result;
    }

    public static NetworkCompatibilityResult CompareHost(
        NetworkCompatibilityProfile local,
        string? hostProtocol,
        string? hostFingerprint,
        string? hostRequiredMods)
    {
        var result = CompareRemote(
            local,
            hostProtocol,
            hostFingerprint,
            hostRequiredMods,
            "Host");
        return result.Status == NetworkCompatibilityStatus.MissingPeerProfile
            ? result with { Status = NetworkCompatibilityStatus.MissingHostProfile }
            : result;
    }

    public static NetworkCompatibilityResult ComparePeer(
        NetworkCompatibilityProfile local,
        string? peerProtocol,
        string? peerFingerprint) =>
        CompareRemote(local, peerProtocol, peerFingerprint, "Peer");

    public static NetworkCompatibilityResult ComparePeer(
        NetworkCompatibilityProfile local,
        string? peerProtocol,
        string? peerFingerprint,
        string? peerRequiredMods) =>
        CompareRemote(local, peerProtocol, peerFingerprint, peerRequiredMods, "Peer");

    private static NetworkCompatibilityResult CompareRemote(
        NetworkCompatibilityProfile local,
        string? remoteProtocol,
        string? remoteFingerprint,
        string remoteName)
        => CompareRemote(local, remoteProtocol, remoteFingerprint, null, remoteName);

    private static NetworkCompatibilityResult CompareRemote(
        NetworkCompatibilityProfile local,
        string? remoteProtocol,
        string? remoteFingerprint,
        string? remoteRequiredMods,
        string remoteName)
    {
        ArgumentNullException.ThrowIfNull(local);
        if (local.BlocksMultiplayer)
        {
            return new NetworkCompatibilityResult(
                NetworkCompatibilityStatus.LocalProfileBlocksMultiplayer,
                false,
                $"Active mods marked multiplayer=incompatible: " +
                string.Join(", ", local.IncompatibleMods.Select(mod => mod.Id)));
        }
        if (string.IsNullOrWhiteSpace(remoteProtocol) || string.IsNullOrWhiteSpace(remoteFingerprint))
        {
            return local.RequiresExactMatch
                ? new NetworkCompatibilityResult(
                    NetworkCompatibilityStatus.MissingPeerProfile,
                    false,
                    $"{remoteName} does not publish an OFS mod profile, but local required mods are active.")
                : new NetworkCompatibilityResult(
                    NetworkCompatibilityStatus.CompatibleLegacyHost,
                    true,
                    $"{remoteName} has no OFS profile and no local required mods need matching.");
        }
        if (!int.TryParse(remoteProtocol, out var protocol) || protocol != local.ProtocolVersion)
        {
            return new NetworkCompatibilityResult(
                NetworkCompatibilityStatus.FingerprintMismatch,
                false,
                $"{remoteName} uses unsupported OFS network profile protocol '{remoteProtocol}'.",
                remoteFingerprint);
        }
        if (string.Equals(local.Fingerprint, remoteFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new NetworkCompatibilityResult(
                NetworkCompatibilityStatus.Compatible,
                true,
                $"{remoteName} and local OFS mod profiles match.",
                remoteFingerprint);
        }

        if (remoteRequiredMods is null)
        {
            return new NetworkCompatibilityResult(
                NetworkCompatibilityStatus.FingerprintMismatch,
                false,
                $"{remoteName} and local OFS mod profile fingerprints differ.",
                remoteFingerprint);
        }
        if (!NetworkProfileMetadata.TryDecodeRequiredMods(
                remoteRequiredMods,
                out var decoded,
                out var metadataError))
        {
            return new NetworkCompatibilityResult(
                NetworkCompatibilityStatus.InvalidPeerProfile,
                false,
                $"{remoteName} publishes invalid OFS mod metadata: {metadataError}",
                remoteFingerprint);
        }
        var differences = NetworkProfileMetadata.CompareRequiredMods(
            local.RequiredMods,
            decoded);
        var summary = differences.Count == 0
            ? "Required mods match; game/framework or incompatible-mod metadata differs."
            : string.Join("; ", differences.Take(3).Select(FormatDifference)) +
              (differences.Count > 3 ? $"; +{differences.Count - 3} more" : string.Empty);
        return new NetworkCompatibilityResult(
            NetworkCompatibilityStatus.FingerprintMismatch,
            false,
            $"{remoteName} and local OFS profiles differ. {summary} " +
            (differences.Count == 0
                ? string.Empty
                : "Open MODS > SETTINGS to review a restart fix."),
            remoteFingerprint)
        {
            ModDifferences = differences,
            RemoteRequiredMods = decoded,
        };
    }

    private static string FormatDifference(NetworkModDifference difference) =>
        difference.Kind switch
        {
            NetworkModDifferenceKind.MissingLocal =>
                $"missing {difference.Id}@{difference.RemoteVersion}",
            NetworkModDifferenceKind.UnexpectedLocal =>
                $"disable {difference.Id}@{difference.LocalVersion}",
            _ =>
                $"change {difference.Id} {difference.LocalVersion}->{difference.RemoteVersion}",
        };
}
