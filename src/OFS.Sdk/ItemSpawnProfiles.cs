namespace OFS.Sdk;

/// <summary>
/// Creates and edits T_ItemSpawnProfile assets referenced by PropertyConfigSO.
/// </summary>
public interface IItemSpawnProfileRegistry
{
    IReadOnlyList<ItemSpawnProfileDefinition> GetReferenced();
    UnityObject Create(string assetName, ItemSpawnProfileBlueprint blueprint);
    UnityObject Clone(UnityObject source, string assetName);
    ItemSpawnProfileDefinition Describe(UnityObject itemSpawnProfile);
    void Update(UnityObject itemSpawnProfile, ItemSpawnProfilePatch patch);
}

public enum PropertyLayerKind
{
    Surface = 0,
    Middle = 1,
    Deep = 2,
}

public sealed record ItemSpawnEntryDefinition(
    UnityObject Item,
    int MinCount,
    int MaxCount,
    int SpawnGroupMin,
    int SpawnGroupMax);

public sealed record ItemSpawnLayerDefinition(
    PropertyLayerKind Kind,
    IReadOnlyList<ItemSpawnEntryDefinition> Items);

public sealed record ItemSpawnProfileDefinition(
    UnityObject Asset,
    string AssetName,
    float GroupSpawnRadius,
    float MinGroupDistance,
    ItemSpawnLayerDefinition Surface,
    ItemSpawnLayerDefinition Middle,
    ItemSpawnLayerDefinition Deep,
    IReadOnlyList<ItemSpawnEntryDefinition> MysteryItems);

public sealed record ItemSpawnProfileBlueprint(
    float GroupSpawnRadius,
    float MinGroupDistance,
    IReadOnlyList<ItemSpawnEntryDefinition> SurfaceItems,
    IReadOnlyList<ItemSpawnEntryDefinition> MiddleItems,
    IReadOnlyList<ItemSpawnEntryDefinition> DeepItems,
    IReadOnlyList<ItemSpawnEntryDefinition> MysteryItems);

public sealed record ItemSpawnProfilePatch(
    float? GroupSpawnRadius = null,
    float? MinGroupDistance = null,
    IReadOnlyList<ItemSpawnEntryDefinition>? SurfaceItems = null,
    IReadOnlyList<ItemSpawnEntryDefinition>? MiddleItems = null,
    IReadOnlyList<ItemSpawnEntryDefinition>? DeepItems = null,
    IReadOnlyList<ItemSpawnEntryDefinition>? MysteryItems = null);
