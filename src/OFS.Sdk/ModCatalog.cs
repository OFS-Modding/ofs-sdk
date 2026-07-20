using System.Text.Json.Serialization;

namespace OFS.Sdk;

public sealed record ModCatalog
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("generatedAtUtc")]
    public DateTimeOffset GeneratedAtUtc { get; init; }

    [JsonPropertyName("gameBuild")]
    public required string GameBuild { get; init; }

    [JsonPropertyName("frameworkVersion")]
    public required string FrameworkVersion { get; init; }

    [JsonPropertyName("mods")]
    public IReadOnlyList<ModCatalogEntry> Mods { get; init; } = [];
}

public sealed record ModCatalogEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; init; } = string.Empty;

    [JsonPropertyName("sdkVersion")]
    public required string SdkVersion { get; init; }

    [JsonPropertyName("gameBuilds")]
    public IReadOnlyList<string> GameBuilds { get; init; } = [];

    [JsonPropertyName("dependencies")]
    public IReadOnlyList<ModDependency> Dependencies { get; init; } = [];

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    [JsonPropertyName("multiplayer")]
    public string Multiplayer { get; init; } = "unknown";

    [JsonPropertyName("thumbnail")]
    public ModCatalogThumbnail? Thumbnail { get; init; }

    [JsonPropertyName("package")]
    public required ModCatalogPackage Package { get; init; }
}

/// <summary>
/// Integrity metadata for a remotely hosted PNG or JPEG catalog thumbnail.
/// The enclosing signed catalog authenticates all three values.
/// </summary>
public sealed record ModCatalogThumbnail
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}

public sealed record ModCatalogPackage
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}

public static class ModCatalogValidator
{
    public const int CurrentSchemaVersion = 1;
    private const long MaximumPackageBytes = 2L * 1024 * 1024 * 1024;
    public const long MaximumThumbnailBytes = 2L * 1024 * 1024;

    public static IReadOnlyList<string> Validate(ModCatalog? catalog)
    {
        var errors = new List<string>();
        if (catalog is null)
        {
            errors.Add("Catalog deserialized to null.");
            return errors;
        }

        if (catalog.SchemaVersion != CurrentSchemaVersion)
        {
            errors.Add($"Unsupported catalog schemaVersion {catalog.SchemaVersion}; expected 1.");
        }
        if (string.IsNullOrWhiteSpace(catalog.GameBuild))
        {
            errors.Add("gameBuild is required.");
        }
        if (!ModVersion.TryParse(catalog.FrameworkVersion, out _))
        {
            errors.Add("frameworkVersion must be a stable three-component semantic version.");
        }
        if (catalog.Mods is null)
        {
            errors.Add("mods must be an array.");
            return errors;
        }
        if (catalog.Mods.Count > 100_000)
        {
            errors.Add("Catalog contains more than 100000 version entries.");
        }

        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in catalog.Mods)
        {
            var prefix = string.IsNullOrWhiteSpace(entry.Id) ? "<missing>" : entry.Id;
            if (!ModManifestValidator.IsValidId(entry.Id))
            {
                errors.Add($"catalog entry '{prefix}' has an invalid id.");
            }
            if (!ModVersion.TryParse(entry.Version, out _))
            {
                errors.Add($"catalog entry '{prefix}' has invalid version '{entry.Version}'.");
            }
            if (!versions.Add($"{entry.Id}\0{entry.Version}"))
            {
                errors.Add($"catalog contains duplicate '{entry.Id}' version '{entry.Version}'.");
            }
            if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 100)
            {
                errors.Add($"catalog entry '{prefix}' requires a name of at most 100 characters.");
            }
            if ((entry.Summary?.Length ?? 0) > 1000 || (entry.Author?.Length ?? 0) > 100)
            {
                errors.Add($"catalog entry '{prefix}' has oversized descriptive metadata.");
            }
            if (!ModVersion.TryParse(entry.SdkVersion, out _))
            {
                errors.Add($"catalog entry '{prefix}' has invalid sdkVersion '{entry.SdkVersion}'.");
            }
            if (entry.GameBuilds is null ||
                entry.GameBuilds.Count == 0 ||
                entry.GameBuilds.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add($"catalog entry '{prefix}' must declare at least one gameBuilds value.");
            }

            ValidateDependencies(errors, entry);
            ValidateCapabilities(errors, entry);
            ValidateThumbnail(errors, entry);
            ValidatePackage(errors, entry);
        }

        return errors;
    }

    public static bool IsCompatible(
        ModCatalogEntry entry,
        string gameBuild,
        ModVersion frameworkVersion)
    {
        if (!ModVersion.TryParse(entry.SdkVersion, out var requiredSdk) ||
            requiredSdk.Major != frameworkVersion.Major ||
            requiredSdk.Minor > frameworkVersion.Minor ||
            entry.GameBuilds is null)
        {
            return false;
        }

        return entry.GameBuilds.Any(build =>
            build == "*" || string.Equals(build, gameBuild, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateDependencies(ICollection<string> errors, ModCatalogEntry entry)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in entry.Dependencies ?? [])
        {
            if (!ModManifestValidator.IsValidId(dependency.Id) ||
                !ModVersionRange.TryParse(dependency.Version, out _))
            {
                errors.Add($"catalog entry '{entry.Id}' has invalid dependency '{dependency.Id}'.");
            }
            if (!seen.Add(dependency.Id))
            {
                errors.Add($"catalog entry '{entry.Id}' repeats dependency '{dependency.Id}'.");
            }
            if (string.Equals(entry.Id, dependency.Id, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"catalog entry '{entry.Id}' depends on itself.");
            }
        }
    }

    private static void ValidateCapabilities(ICollection<string> errors, ModCatalogEntry entry)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var capability in entry.Capabilities ?? [])
        {
            if (!ModManifestValidator.IsValidId(capability) || !seen.Add(capability))
            {
                errors.Add($"catalog entry '{entry.Id}' has invalid or duplicate capability '{capability}'.");
            }
        }
        if (entry.Multiplayer is not ("unknown" or "client" or "server" or "required" or "incompatible"))
        {
            errors.Add($"catalog entry '{entry.Id}' has invalid multiplayer classification.");
        }
    }

    private static void ValidatePackage(ICollection<string> errors, ModCatalogEntry entry)
    {
        if (entry.Package is null)
        {
            errors.Add($"catalog entry '{entry.Id}' requires package metadata.");
            return;
        }
        if (!Uri.TryCreate(entry.Package.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"catalog entry '{entry.Id}' package URL must be absolute HTTPS.");
        }
        if (entry.Package.Bytes is <= 0 or > MaximumPackageBytes)
        {
            errors.Add($"catalog entry '{entry.Id}' has invalid package size {entry.Package.Bytes}.");
        }
        if (entry.Package.Sha256 is null ||
            entry.Package.Sha256.Length != 64 ||
            entry.Package.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            errors.Add($"catalog entry '{entry.Id}' package sha256 must contain 64 hexadecimal characters.");
        }
    }

    private static void ValidateThumbnail(ICollection<string> errors, ModCatalogEntry entry)
    {
        if (entry.Thumbnail is null)
        {
            return;
        }
        if (!Uri.TryCreate(entry.Thumbnail.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add($"catalog entry '{entry.Id}' thumbnail URL must be absolute HTTPS.");
        }
        if (entry.Thumbnail.Bytes is <= 0 or > MaximumThumbnailBytes)
        {
            errors.Add(
                $"catalog entry '{entry.Id}' has invalid thumbnail size {entry.Thumbnail.Bytes}.");
        }
        if (entry.Thumbnail.Sha256 is null ||
            entry.Thumbnail.Sha256.Length != 64 ||
            entry.Thumbnail.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            errors.Add(
                $"catalog entry '{entry.Id}' thumbnail sha256 must contain 64 hexadecimal characters.");
        }
    }
}

public sealed record ModCatalogResolution(
    bool Success,
    IReadOnlyList<ModCatalogEntry> InstallOrder,
    IReadOnlyList<string> Errors);

public static class ModCatalogResolver
{
    public static ModCatalogResolution Resolve(
        ModCatalog catalog,
        IEnumerable<ModDependency> requested,
        string? gameBuild = null,
        string? frameworkVersion = null)
    {
        var validation = ModCatalogValidator.Validate(catalog);
        if (validation.Count != 0)
        {
            return new ModCatalogResolution(false, [], validation);
        }
        if (!ModVersion.TryParse(frameworkVersion ?? catalog.FrameworkVersion, out var framework))
        {
            return new ModCatalogResolution(false, [], ["Invalid framework version."]);
        }

        var constraints = new Dictionary<string, List<ModVersionRange>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in requested)
        {
            if (!ModManifestValidator.IsValidId(dependency.Id) ||
                !ModVersionRange.TryParse(dependency.Version, out var range) || range is null)
            {
                return new ModCatalogResolution(
                    false,
                    [],
                    [$"Invalid requested mod or range: '{dependency.Id}' '{dependency.Version}'."]);
            }
            AddConstraint(constraints, dependency.Id, range);
        }
        if (constraints.Count == 0)
        {
            return new ModCatalogResolution(false, [], ["At least one requested mod is required."]);
        }

        var entriesById = catalog.Mods
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Where(entry => ModCatalogValidator.IsCompatible(
                        entry,
                        gameBuild ?? catalog.GameBuild,
                        framework))
                    .OrderByDescending(entry => ParseVersion(entry.Version))
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var selected = Solve(entriesById, constraints, new(StringComparer.OrdinalIgnoreCase));
        if (selected is null)
        {
            var requestedText = string.Join(", ", constraints.Select(pair =>
                $"{pair.Key} {string.Join(" & ", pair.Value)}"));
            return new ModCatalogResolution(
                false,
                [],
                [$"No compatible dependency solution exists for: {requestedText}."]);
        }

        var order = new List<ModCatalogEntry>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in selected.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            if (!Visit(id, selected, visiting, visited, order))
            {
                return new ModCatalogResolution(false, [], ["The selected dependency graph contains a cycle."]);
            }
        }
        return new ModCatalogResolution(true, order, []);
    }

    private static Dictionary<string, ModCatalogEntry>? Solve(
        IReadOnlyDictionary<string, ModCatalogEntry[]> entriesById,
        Dictionary<string, List<ModVersionRange>> constraints,
        Dictionary<string, ModCatalogEntry> selected)
    {
        foreach (var pair in selected)
        {
            var version = ParseVersion(pair.Value.Version);
            if (constraints.TryGetValue(pair.Key, out var ranges) && ranges.Any(range => !range.Contains(version)))
            {
                return null;
            }
        }

        var unresolved = constraints.Keys.Where(id => !selected.ContainsKey(id)).ToArray();
        if (unresolved.Length == 0)
        {
            return selected;
        }

        var next = unresolved
            .Select(id => new
            {
                Id = id,
                Candidates = entriesById.GetValueOrDefault(id, [])
                    .Where(entry => constraints[id].All(range => range.Contains(ParseVersion(entry.Version))))
                    .ToArray(),
            })
            .OrderBy(item => item.Candidates.Length)
            .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .First();

        foreach (var candidate in next.Candidates)
        {
            var candidateSelected = new Dictionary<string, ModCatalogEntry>(selected, StringComparer.OrdinalIgnoreCase)
            {
                [next.Id] = candidate,
            };
            var candidateConstraints = constraints.ToDictionary(
                pair => pair.Key,
                pair => new List<ModVersionRange>(pair.Value),
                StringComparer.OrdinalIgnoreCase);
            var invalid = false;
            foreach (var dependency in candidate.Dependencies.Where(dependency => !dependency.Optional))
            {
                _ = ModVersionRange.TryParse(dependency.Version, out var range);
                AddConstraint(candidateConstraints, dependency.Id, range!);
                if (candidateSelected.TryGetValue(dependency.Id, out var existing) &&
                    !range!.Contains(ParseVersion(existing.Version)))
                {
                    invalid = true;
                    break;
                }
            }
            if (invalid) continue;

            var solution = Solve(entriesById, candidateConstraints, candidateSelected);
            if (solution is not null)
            {
                return solution;
            }
        }
        return null;
    }

    private static bool Visit(
        string id,
        IReadOnlyDictionary<string, ModCatalogEntry> selected,
        ISet<string> visiting,
        ISet<string> visited,
        ICollection<ModCatalogEntry> order)
    {
        if (visited.Contains(id)) return true;
        if (!visiting.Add(id)) return false;
        var entry = selected[id];
        foreach (var dependency in entry.Dependencies.Where(dependency => !dependency.Optional))
        {
            if (selected.ContainsKey(dependency.Id) &&
                !Visit(dependency.Id, selected, visiting, visited, order))
            {
                return false;
            }
        }
        visiting.Remove(id);
        visited.Add(id);
        order.Add(entry);
        return true;
    }

    private static void AddConstraint(
        IDictionary<string, List<ModVersionRange>> constraints,
        string id,
        ModVersionRange range)
    {
        if (!constraints.TryGetValue(id, out var ranges))
        {
            ranges = [];
            constraints[id] = ranges;
        }
        ranges.Add(range);
    }

    private static ModVersion ParseVersion(string value)
    {
        _ = ModVersion.TryParse(value, out var version);
        return version;
    }
}
