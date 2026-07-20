using System.Text.Json.Serialization;

namespace OFS.Sdk;

public sealed record ModManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; init; } = string.Empty;

    [JsonPropertyName("assembly")]
    public required string Assembly { get; init; }

    [JsonPropertyName("entryPoint")]
    public required string EntryPoint { get; init; }

    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; init; } =
        ModManifestValidator.CurrentSdkVersion.ToString(3);

    [JsonPropertyName("dependencies")]
    public IReadOnlyList<ModDependency> Dependencies { get; init; } = [];

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    [JsonPropertyName("multiplayer")]
    public string Multiplayer { get; init; } = "unknown";
}

public sealed record ModDependency
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "*";

    [JsonPropertyName("optional")]
    public bool Optional { get; init; }
}
