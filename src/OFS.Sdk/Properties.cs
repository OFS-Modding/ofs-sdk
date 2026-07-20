namespace OFS.Sdk;

/// <summary>
/// Lookup, mutation and append-only registration for PropertyConfigSO assets.
/// </summary>
public interface IPropertyRegistry
{
    int Count { get; }
    UnityObject Clone(UnityObject source, string newConfigId);
    UnityObject FindById(string configId);
    PropertyDefinition Describe(UnityObject propertyConfigScriptableObject);
    IReadOnlyList<PropertyDefinition> GetAll();
    void Update(UnityObject propertyConfigScriptableObject, PropertyPatch patch);
    IPropertyRegistration Register(UnityObject propertyConfigScriptableObject);
}

public enum PropertyKind
{
    Residential = 0,
    Commercial = 1,
    Industrial = 2,
    Forestry = 3,
    Development = 4,
}

public sealed record PropertyDefinition(
    UnityObject Asset,
    string ConfigId,
    string DisplayNameKey,
    IReadOnlyList<string> PropertyNameKeys,
    PropertyKind Kind,
    int Level,
    int MinPrice,
    int MaxPrice,
    int PriceRoundingStep,
    IReadOnlyList<string> AddressKeys,
    IReadOnlyList<int> Sizes,
    IReadOnlyList<UnityObject> Visuals,
    IReadOnlyList<UnityObject> LoadingBackgrounds,
    string LinkedSceneName,
    IReadOnlyList<UnityObject> ItemSpawnProfiles,
    IReadOnlyList<UnityObject> Contracts);

public sealed record PropertyPatch(
    string? DisplayNameKey = null,
    IReadOnlyList<string>? PropertyNameKeys = null,
    PropertyKind? Kind = null,
    int? Level = null,
    int? MinPrice = null,
    int? MaxPrice = null,
    int? PriceRoundingStep = null,
    IReadOnlyList<string>? AddressKeys = null,
    IReadOnlyList<int>? Sizes = null,
    IReadOnlyList<UnityObject>? Visuals = null,
    IReadOnlyList<UnityObject>? LoadingBackgrounds = null,
    string? LinkedSceneName = null,
    IReadOnlyList<UnityObject>? ItemSpawnProfiles = null,
    IReadOnlyList<UnityObject>? Contracts = null);

public interface IPropertyRegistration
{
    string ConfigId { get; }
    UnityObject Asset { get; }
    int Index { get; }
}
