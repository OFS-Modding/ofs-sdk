namespace OFS.Sdk;

/// <summary>
/// Immutable process/build facts plus the live Unity main-thread state exposed
/// to every loaded mod. The game fingerprint is the authoritative compatibility
/// identity; display versions alone are not sufficient for native hooks.
/// </summary>
public interface IModRuntimeInfo
{
    Version FrameworkVersion { get; }
    string GameVersion { get; }
    string GameBuildFingerprint { get; }
    string UnityVersion { get; }
    int Il2CppMetadataVersion { get; }
    string ProcessArchitecture { get; }
    int PointerSize { get; }
    bool IsVerifiedGameBuild { get; }
    bool IsMainThread { get; }
}
