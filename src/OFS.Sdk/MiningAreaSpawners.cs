namespace OFS.Sdk;

public enum MiningLayerKind
{
    Surface = 0,
    Middle = 1,
    Deep = 2,
}

public enum MiningProfileSelectionMode
{
    PropertyOrFallback = 0,
    FallbackOnly = 1,
}

public sealed record MiningSpawnRuleBlueprint(
    string Name,
    MiningLayerKind Layer,
    UnityTransform Transform,
    float Size,
    float Height,
    float YOffset = 0f,
    UnityObject HostGameObject = default);

public sealed record MiningAreaSpawnerBlueprint(
    string Name,
    UnityTransform Transform,
    UnityObject PickupPrefab,
    UnityObject FallbackProfile,
    IReadOnlyList<MiningSpawnRuleBlueprint> Rules,
    bool RandomYawRotation = true,
    float ReferenceEdge = 10f,
    int BaseCapacityAtReference = 20,
    int MinCapacityPerRule = 1,
    bool CapacityByArea = true,
    bool EqualDistributionAcrossRules = false,
    bool Active = true)
{
    public MiningProfileSelectionMode ProfileSelection { get; init; } =
        MiningProfileSelectionMode.PropertyOrFallback;
}

public sealed record MiningSpawnRuleDefinition(
    UnityObject GameObject,
    UnityObject Component,
    int SpawnRuleId,
    MiningLayerKind Layer,
    float Size,
    float Height,
    float YOffset);

public sealed record MiningAreaSpawnerDefinition(
    UnityObject GameObject,
    UnityObject Component,
    UnityObject PickupPrefab,
    UnityObject FallbackProfile,
    bool RandomYawRotation,
    float ReferenceEdge,
    int BaseCapacityAtReference,
    int MinCapacityPerRule,
    bool CapacityByArea,
    bool EqualDistributionAcrossRules,
    IReadOnlyList<MiningSpawnRuleDefinition> Rules,
    bool IsRestoringFromSave = false,
    bool InitialCountsCalculated = false);

/// <summary>Creates or attaches the vanilla networked mining-area spawner.</summary>
public interface IMiningAreaSpawnerRegistry
{
    IMiningAreaSpawner Create(
        MiningAreaSpawnerBlueprint blueprint,
        UnityObject parent = default);
    IMiningAreaSpawner Attach(
        UnityObject gameObject,
        MiningAreaSpawnerBlueprint blueprint);
    IReadOnlyList<MiningAreaSpawnerDefinition> GetLoaded(bool activeOnly = true);
}

public interface IMiningAreaSpawner : IDisposable
{
    UnityObject GameObject { get; }
    UnityObject Component { get; }
    IReadOnlyList<MiningSpawnRuleDefinition> Rules { get; }
    bool IsAlive { get; }
    bool IsRestoringFromSave { get; }
    bool InitialCountsCalculated { get; }
    MiningAreaSpawnerDefinition Describe();
    UnityObject ResolveActiveProfile();
    int ComputeCapacity(MiningSpawnRuleDefinition rule);
    int GetRemainingNodeCount(MiningLayerKind layer, string itemId);
    bool ValidateSetup();
    void NotifyRestoreComplete();
    void SpawnOnServer();
    void SpawnNowOnServer();
    void ClearTrackingData();
    void Remove();
}
