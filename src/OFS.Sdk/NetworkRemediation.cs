namespace OFS.Sdk;

/// <summary>
/// Deterministic restart plan for aligning installed/enabled mods with a remote
/// required-mod profile. Download/install execution remains a separate trusted operation.
/// </summary>
public sealed record NetworkRemediationPlan(
    bool Success,
    bool RestartRequired,
    IReadOnlyList<NetworkModDifference> Differences,
    IReadOnlyList<ModCatalogEntry> InstallOrder,
    IReadOnlyList<string> EnableIds,
    IReadOnlyList<string> DisableIds,
    IReadOnlyList<string> Errors);

public static class NetworkRemediationPlanner
{
    public static NetworkRemediationPlan Create(
        NetworkCompatibilityProfile localProfile,
        IEnumerable<NetworkModIdentity> remoteRequiredMods,
        IEnumerable<ModManifest> installedMods,
        IEnumerable<string> enabledIds,
        ModCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(localProfile);
        ArgumentNullException.ThrowIfNull(remoteRequiredMods);
        ArgumentNullException.ThrowIfNull(installedMods);
        ArgumentNullException.ThrowIfNull(enabledIds);
        ArgumentNullException.ThrowIfNull(catalog);

        var errors = new List<string>();
        IReadOnlyList<NetworkModIdentity> remote;
        try
        {
            var encoded = NetworkProfileMetadata.EncodeRequiredMods(remoteRequiredMods);
            if (!NetworkProfileMetadata.TryDecodeRequiredMods(encoded, out remote, out var error))
            {
                return Failed([], [error]);
            }
        }
        catch (ArgumentException exception)
        {
            return Failed([], [exception.Message]);
        }

        IReadOnlyList<NetworkModDifference> differences;
        try
        {
            differences = NetworkProfileMetadata.CompareRequiredMods(
                localProfile.RequiredMods,
                remote);
        }
        catch (ArgumentException exception)
        {
            return Failed([], [exception.Message]);
        }

        var catalogErrors = ModCatalogValidator.Validate(catalog);
        if (catalogErrors.Count != 0)
        {
            return Failed(differences, catalogErrors);
        }

        var installed = new Dictionary<string, ModManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var manifest in installedMods)
        {
            var validation = ModManifestValidator.Validate(manifest);
            if (validation.Count != 0)
            {
                errors.Add(
                    $"Installed manifest '{manifest.Id}' is invalid: {string.Join(" ", validation)}");
            }
            else if (!installed.TryAdd(manifest.Id, manifest))
            {
                errors.Add($"Installed mod id '{manifest.Id}' is duplicated.");
            }
        }

        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in enabledIds)
        {
            if (!ModManifestValidator.IsValidId(id) || !enabled.Add(id))
            {
                errors.Add($"Enabled mod id '{id}' is invalid or duplicated.");
            }
            else if (!installed.ContainsKey(id))
            {
                errors.Add($"Enabled mod '{id}' is not installed.");
            }
        }
        if (errors.Count != 0)
        {
            return Failed(differences, errors);
        }

        var directDisable = differences
            .Where(difference => difference.Kind == NetworkModDifferenceKind.UnexpectedLocal)
            .Select(difference => difference.Id)
            .Concat(installed.Values
                .Where(manifest =>
                    enabled.Contains(manifest.Id) &&
                    string.Equals(
                        manifest.Multiplayer,
                        "incompatible",
                        StringComparison.Ordinal))
                .Select(manifest => manifest.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var desired = new HashSet<string>(enabled, StringComparer.OrdinalIgnoreCase);
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in directDisable)
        {
            var resolution = ModProfileResolver.Disable(
                installed.Values,
                desired,
                id);
            if (!resolution.Success)
            {
                errors.AddRange(resolution.Errors);
                continue;
            }
            desired = resolution.EnabledIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            disabled.UnionWith(resolution.AffectedIds);
        }
        if (errors.Count != 0)
        {
            return Failed(differences, errors);
        }

        var remoteById = remote.ToDictionary(
            mod => mod.Id,
            StringComparer.OrdinalIgnoreCase);
        var requestedInstalls = remote
            .Where(mod =>
                !installed.TryGetValue(mod.Id, out var manifest) ||
                !string.Equals(manifest.Version, mod.Version, StringComparison.Ordinal))
            .Select(mod => new ModDependency { Id = mod.Id, Version = mod.Version })
            .ToArray();

        IReadOnlyList<ModCatalogEntry> selected = [];
        if (requestedInstalls.Length != 0)
        {
            var resolution = ModCatalogResolver.Resolve(
                catalog,
                requestedInstalls,
                localProfile.GameBuild,
                localProfile.FrameworkVersion);
            if (!resolution.Success)
            {
                return Failed(differences, resolution.Errors);
            }
            selected = resolution.InstallOrder;

            foreach (var entry in selected)
            {
                if (entry.Multiplayer == "incompatible")
                {
                    errors.Add(
                        $"Catalog dependency '{entry.Id}' is multiplayer=incompatible.");
                    continue;
                }
                if (entry.Multiplayer is "required" or "unknown")
                {
                    if (!remoteById.TryGetValue(entry.Id, out var declared) ||
                        !string.Equals(declared.Version, entry.Version, StringComparison.Ordinal))
                    {
                        errors.Add(
                            $"Remote profile omits required catalog dependency " +
                            $"'{entry.Id}@{entry.Version}'.");
                    }
                }
            }
            foreach (var remoteMod in remote)
            {
                if (installed.TryGetValue(remoteMod.Id, out var installedMod) &&
                    string.Equals(installedMod.Version, remoteMod.Version, StringComparison.Ordinal))
                {
                    continue;
                }
                var entry = selected.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, remoteMod.Id, StringComparison.OrdinalIgnoreCase));
                if (entry is null || entry.Multiplayer is "client" or "server" or "incompatible")
                {
                    errors.Add(
                        $"Catalog metadata for remote required mod " +
                        $"'{remoteMod.Id}@{remoteMod.Version}' is inconsistent.");
                }
            }
        }
        if (errors.Count != 0)
        {
            return Failed(differences, errors);
        }

        var installOrder = selected
            .Where(entry =>
                !installed.TryGetValue(entry.Id, out var manifest) ||
                !string.Equals(manifest.Version, entry.Version, StringComparison.Ordinal))
            .ToArray();
        var enable = remote.Select(mod => mod.Id)
            .Concat(selected.Select(entry => entry.Id))
            .Where(id => !desired.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var disable = disabled
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var restartRequired = installOrder.Length != 0 ||
                              enable.Length != 0 ||
                              disable.Length != 0;
        return new NetworkRemediationPlan(
            true,
            restartRequired,
            differences,
            installOrder,
            enable,
            disable,
            []);
    }

    private static NetworkRemediationPlan Failed(
        IReadOnlyList<NetworkModDifference> differences,
        IEnumerable<string> errors) =>
        new(
            false,
            false,
            differences,
            [],
            [],
            [],
            errors.Distinct(StringComparer.Ordinal).ToArray());
}
