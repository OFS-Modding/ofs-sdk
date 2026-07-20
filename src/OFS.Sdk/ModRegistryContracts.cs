namespace OFS.Sdk;

/// <summary>
/// Read-only discovery of mods that completed Load successfully in this process.
/// Snapshots grow only during startup; code-mod changes require a restart.
/// </summary>
public interface IModRegistry
{
    IReadOnlyList<LoadedModDescriptor> Loaded { get; }
    bool IsLoaded(string modId);
    LoadedModDescriptor? Get(string modId);
    IReadOnlyList<LoadedModDescriptor> FindByCapability(string capability);
}

/// <summary>
/// Public manifest metadata for one successfully loaded mod. Capabilities are
/// declarations used for discovery, not permissions or security boundaries.
/// </summary>
public sealed record LoadedModDescriptor(
    ModInfo Mod,
    Version SdkVersion,
    string Multiplayer,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<ModDependency> Dependencies)
{
    public bool HasCapability(string capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        return Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
    }
}
