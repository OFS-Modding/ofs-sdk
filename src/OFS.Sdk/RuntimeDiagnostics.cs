using System.Text.Json.Serialization;

namespace OFS.Sdk;

/// <summary>Startup state persisted by the OFS runtime diagnostic reporter.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeStartupState
{
    Loading = 0,
    Ready = 1,
}

/// <summary>Final startup disposition of one discovered manifest.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModStartupStatus
{
    Loaded = 0,
    Disabled = 1,
    Quarantined = 2,
    Rejected = 3,
    Blocked = 4,
    Failed = 5,
}

/// <summary>Serializable build/ABI facts captured before loading user mods.</summary>
public sealed record RuntimeEnvironmentSnapshot(
    string FrameworkVersion,
    string GameVersion,
    string GameBuildFingerprint,
    string UnityVersion,
    int Il2CppMetadataVersion,
    string ProcessArchitecture,
    int PointerSize,
    bool IsVerifiedGameBuild);

/// <summary>One manifest's final result in a runtime startup report.</summary>
public sealed record RuntimeModDiagnostic(
    string? Id,
    string? Name,
    string? Version,
    string ManifestPath,
    ModStartupStatus Status,
    string Phase,
    string Message,
    IReadOnlyList<string> RelatedModIds);

/// <summary>
/// Versioned report stored at OFS/diagnostics/last-session.json. Loading state
/// means startup did not reach its final commit, usually because the process ended.
/// </summary>
public sealed record RuntimeDiagnosticReport(
    int SchemaVersion,
    string SessionId,
    int ProcessId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? StartupCompletedAtUtc,
    RuntimeStartupState State,
    RuntimeEnvironmentSnapshot Environment,
    int DiscoveredManifestCount,
    IReadOnlyList<RuntimeModDiagnostic> Mods)
{
    public const int CurrentSchemaVersion = 1;

    [JsonIgnore]
    public int LoadedCount => Mods.Count(value => value.Status == ModStartupStatus.Loaded);

    [JsonIgnore]
    public int ProblemCount => Mods.Count(value => value.Status is
        ModStartupStatus.Quarantined or ModStartupStatus.Rejected or
        ModStartupStatus.Blocked or ModStartupStatus.Failed);

    [JsonIgnore]
    public bool HasProblems => State != RuntimeStartupState.Ready || ProblemCount != 0;
}
