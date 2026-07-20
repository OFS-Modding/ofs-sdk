namespace OFS.Sdk;

/// <summary>
/// Scene-aware OffsiteContractSO catalog. Registrations made during ContentReady
/// are materialized when Factory creates OffsiteContractManager.
/// </summary>
public interface IOffsiteContractRegistry
{
    bool IsAvailable { get; }
    int Count { get; }
    UnityObject Create(string contractId);
    UnityObject Clone(UnityObject source, string newContractId);
    UnityObject FindById(string contractId);
    OffsiteContractDefinition Describe(UnityObject contractScriptableObject);
    IReadOnlyList<OffsiteContractDefinition> GetAll();
    void Update(UnityObject contractScriptableObject, OffsiteContractPatch patch);
    IOffsiteContractRegistration Register(UnityObject contractScriptableObject);
}

public sealed record OffsiteContractDefinition(
    UnityObject Asset,
    string ContractId,
    UnityObject Property,
    int RequiredLevel,
    int DurationHoursMin,
    int DurationHoursMax,
    IReadOnlyList<UnityObject> ItemPool,
    int AmountPerHourMin,
    int AmountPerHourMax,
    int RewardItemCount,
    IReadOnlyList<EmployeeStatKind> MatchingProfiles,
    int RequiredMinerCount);

public sealed record OffsiteContractPatch(
    UnityObject? Property = null,
    int? RequiredLevel = null,
    int? DurationHoursMin = null,
    int? DurationHoursMax = null,
    IReadOnlyList<UnityObject>? ItemPool = null,
    int? AmountPerHourMin = null,
    int? AmountPerHourMax = null,
    int? RewardItemCount = null,
    IReadOnlyList<EmployeeStatKind>? MatchingProfiles = null,
    int? RequiredMinerCount = null);

public enum EmployeeStatKind
{
    Agility = 0,
    Intelligence = 1,
    Technique = 2,
    Stamina = 3,
}

/// <summary>
/// Process-lifetime handle for a deferred offsite contract registration.
/// Index is -1 until Factory materializes it into the vanilla generation pool.
/// </summary>
public interface IOffsiteContractRegistration
{
    string ContractId { get; }
    UnityObject Asset { get; }
    int Index { get; }
    bool IsMaterialized { get; }
    string? MaterializationError { get; }
}
