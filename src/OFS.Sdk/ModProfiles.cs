namespace OFS.Sdk;

public sealed record ModProfileResolution(
    bool Success,
    IReadOnlyList<string> EnabledIds,
    IReadOnlyList<string> AffectedIds,
    IReadOnlyList<string> Errors);

/// <summary>Pure dependency-aware enable/disable resolution for tools and runtimes.</summary>
public static class ModProfileResolver
{
    public static ModProfileResolution Enable(
        IEnumerable<ModManifest> installedMods,
        IEnumerable<string> currentlyEnabled,
        string modId) => Resolve(installedMods, currentlyEnabled, modId, enable: true);

    public static ModProfileResolution Disable(
        IEnumerable<ModManifest> installedMods,
        IEnumerable<string> currentlyEnabled,
        string modId) => Resolve(installedMods, currentlyEnabled, modId, enable: false);

    private static ModProfileResolution Resolve(
        IEnumerable<ModManifest> installedMods,
        IEnumerable<string> currentlyEnabled,
        string modId,
        bool enable)
    {
        var errors = new List<string>();
        var installed = new Dictionary<string, ModManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in installedMods)
        {
            var validation = ModManifestValidator.Validate(manifest);
            if (validation.Count != 0)
            {
                errors.Add($"Installed manifest '{manifest.Id}' is invalid: {string.Join(" ", validation)}");
            }
            else if (!installed.TryAdd(manifest.Id, manifest))
            {
                errors.Add($"Installed mod id '{manifest.Id}' is duplicated.");
            }
        }
        if (!installed.ContainsKey(modId))
        {
            errors.Add($"Installed mod '{modId}' was not found.");
        }
        if (errors.Count != 0)
        {
            return new ModProfileResolution(false, [], [], errors);
        }

        var enabled = currentlyEnabled.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (enable)
            {
                EnableRecursive(
                    modId,
                    installed,
                    enabled,
                    affected,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                DisableRecursive(modId, installed, enabled, affected);
            }
        }
        catch (InvalidOperationException exception)
        {
            return new ModProfileResolution(false, [], [], [exception.Message]);
        }

        return new ModProfileResolution(
            true,
            enabled.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            affected.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray(),
            []);
    }

    private static void EnableRecursive(
        string id,
        IReadOnlyDictionary<string, ModManifest> installed,
        ISet<string> enabled,
        ISet<string> affected,
        ISet<string> visiting)
    {
        if (!visiting.Add(id))
        {
            throw new InvalidOperationException($"Dependency cycle encountered while enabling '{id}'.");
        }
        var mod = installed[id];
        foreach (var dependency in mod.Dependencies.Where(value => !value.Optional))
        {
            if (!installed.TryGetValue(dependency.Id, out var dependencyMod) ||
                !ModVersion.TryParse(dependencyMod.Version, out var installedVersion) ||
                !ModVersionRange.TryParse(dependency.Version, out var range) || range is null ||
                !range.Contains(installedVersion))
            {
                throw new InvalidOperationException(
                    $"Dependency '{dependency.Id}' range '{dependency.Version}' is missing or incompatible.");
            }
            EnableRecursive(dependency.Id, installed, enabled, affected, visiting);
        }
        visiting.Remove(id);
        if (enabled.Add(id)) affected.Add(id);
    }

    private static void DisableRecursive(
        string id,
        IReadOnlyDictionary<string, ModManifest> installed,
        ISet<string> enabled,
        ISet<string> affected)
    {
        if (!enabled.Remove(id)) return;
        affected.Add(id);
        foreach (var dependent in installed.Values.Where(candidate =>
                     candidate.Dependencies.Any(dependency =>
                         !dependency.Optional &&
                         string.Equals(dependency.Id, id, StringComparison.OrdinalIgnoreCase))))
        {
            DisableRecursive(dependent.Id, installed, enabled, affected);
        }
    }
}
