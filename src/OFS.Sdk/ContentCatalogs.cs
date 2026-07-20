namespace OFS.Sdk;

/// <summary>Linear RGBA color stored by UnityEngine.Color.</summary>
public readonly record struct UnityColor(float R, float G, float B, float A)
{
    public static UnityColor White => new(1f, 1f, 1f, 1f);
}

/// <summary>Lookup, mutation and append-only registration for CompanySO assets.</summary>
public interface ICompanyRegistry
{
    int Count { get; }
    UnityObject Clone(UnityObject source, string newCompanyId);
    UnityObject FindById(string companyId);
    CompanyDefinition Describe(UnityObject companyScriptableObject);
    IReadOnlyList<CompanyDefinition> GetAll();
    void Update(UnityObject companyScriptableObject, CompanyPatch patch);
    ICompanyRegistration Register(UnityObject companyScriptableObject);
}

public sealed record CompanyDefinition(
    UnityObject Asset,
    string CompanyId,
    string Name,
    string DescriptionKey,
    UnityObject Logo,
    UnityObject Background,
    UnityColor LogoColor,
    IReadOnlyList<ItemFilter> InterestedCategories);

public sealed record CompanyPatch(
    string? Name = null,
    string? DescriptionKey = null,
    UnityObject? Logo = null,
    UnityObject? Background = null,
    UnityColor? LogoColor = null,
    IReadOnlyList<ItemFilter>? InterestedCategories = null);

public interface ICompanyRegistration
{
    string CompanyId { get; }
    UnityObject Asset { get; }
    int Index { get; }
}

/// <summary>Lookup, mutation and append-only registration for T_FoodSO assets.</summary>
public interface IFoodRegistry
{
    int Count { get; }
    UnityObject Clone(UnityObject source, string newFoodId);
    UnityObject FindById(string foodId);
    FoodDefinition Describe(UnityObject foodScriptableObject);
    IReadOnlyList<FoodDefinition> GetAll();
    void Update(UnityObject foodScriptableObject, FoodPatch patch);
    IFoodRegistration Register(UnityObject foodScriptableObject);
}

public sealed record FoodBuffDefinition(
    FoodBuffKind Kind,
    float Value,
    float DurationSeconds);

public sealed record FoodDefinition(
    UnityObject Asset,
    string FoodId,
    string Name,
    string Description,
    int Price,
    UnityObject Icon,
    UnityObject CategoryIcon,
    bool IsAlcohol,
    int AlcoholAmount,
    UnityObject EatClip,
    float EatClipVolume,
    FoodConsumptionKind ConsumptionKind,
    float ConsumptionTime,
    IReadOnlyList<FoodBuffDefinition> Buffs);

public sealed record FoodPatch(
    string? Name = null,
    string? Description = null,
    int? Price = null,
    UnityObject? Icon = null,
    UnityObject? CategoryIcon = null,
    bool? IsAlcohol = null,
    int? AlcoholAmount = null,
    UnityObject? EatClip = null,
    float? EatClipVolume = null,
    FoodConsumptionKind? ConsumptionKind = null,
    float? ConsumptionTime = null,
    IReadOnlyList<FoodBuffDefinition>? Buffs = null);

public enum FoodConsumptionKind
{
    Eating = 0,
    Drinking = 1,
}

public enum FoodBuffKind
{
    None = 0,
    MaxStamina = 1,
    MiningDamage = 2,
    MiningEfficiency = 3,
    DamageReduction = 4,
    InventoryCapacity = 5,
    VehicleSpeed = 6,
}

public interface IFoodRegistration
{
    string FoodId { get; }
    UnityObject Asset { get; }
    int Index { get; }
}

/// <summary>
/// Lookup, mutation and append-only registration for the radial building-menu categories.
/// </summary>
public interface IBuildingCategoryRegistry
{
    int Count { get; }
    UnityObject Clone(UnityObject source, string newCategoryId);
    UnityObject FindById(string categoryId);
    BuildingCategoryDefinition Describe(UnityObject categoryScriptableObject);
    IReadOnlyList<BuildingCategoryDefinition> GetAll();
    void Update(UnityObject categoryScriptableObject, BuildingCategoryPatch patch);
    IBuildingCategoryRegistration Register(UnityObject categoryScriptableObject);
}

public sealed record BuildingCategoryDefinition(
    UnityObject Asset,
    string CategoryId,
    string Name,
    string Description,
    UnityObject Icon,
    IReadOnlyList<UnityObject> Buildings,
    bool AllowScrollCycle,
    int DefaultSelectedIndex);

public sealed record BuildingCategoryPatch(
    string? Name = null,
    string? Description = null,
    UnityObject? Icon = null,
    IReadOnlyList<UnityObject>? Buildings = null,
    bool? AllowScrollCycle = null,
    int? DefaultSelectedIndex = null);

public interface IBuildingCategoryRegistration
{
    string CategoryId { get; }
    UnityObject Asset { get; }
    int Index { get; }
}

/// <summary>UpgradeGroupSO and UpgradeTabSO catalog plus the live UpgradeManager facade.</summary>
public interface IUpgradeRegistry
{
    int GroupCount { get; }
    int TabCount { get; }
    UnityObject CloneGroup(UnityObject source, int newTypeId);
    UnityObject CloneTab(UnityObject source, int newCategoryId);
    UnityObject FindGroupByType(int typeId);
    UnityObject FindTabByCategory(int categoryId);
    UpgradeGroupDefinition DescribeGroup(UnityObject groupScriptableObject);
    UpgradeTabDefinition DescribeTab(UnityObject tabScriptableObject);
    IReadOnlyList<UpgradeGroupDefinition> GetGroups();
    IReadOnlyList<UpgradeTabDefinition> GetTabs();
    void UpdateGroup(UnityObject groupScriptableObject, UpgradeGroupPatch patch);
    void UpdateTab(UnityObject tabScriptableObject, UpgradeTabPatch patch);
    IUpgradeGroupRegistration RegisterGroup(UnityObject groupScriptableObject);
    IUpgradeTabRegistration RegisterTab(UnityObject tabScriptableObject);
    bool IsManagerReady { get; }
    int GetLevel(int typeId);
    bool CanUpgrade(int typeId);
    void RequestUpgrade(int typeId);
}

public sealed record UpgradeChangeDefinition(
    string TextKey,
    string OldValue,
    string NewValue);

public sealed record UpgradeLevelDefinition(
    string TitleKey,
    string DescriptionKey,
    IReadOnlyList<UpgradeChangeDefinition> Changes,
    int RequiredFactoryLevel,
    int Cost,
    bool AvailableInDemo,
    UnityObject Icon);

public sealed record UpgradeGroupDefinition(
    UnityObject Asset,
    int TypeId,
    string NameKey,
    UnityObject Icon,
    int CategoryId,
    string LevelPrefixKey,
    IReadOnlyList<UpgradeLevelDefinition> Levels,
    int LinkedItemTypeId);

public sealed record UpgradeGroupPatch(
    string? NameKey = null,
    UnityObject? Icon = null,
    int? CategoryId = null,
    string? LevelPrefixKey = null,
    IReadOnlyList<UpgradeLevelDefinition>? Levels = null,
    int? LinkedItemTypeId = null);

public sealed record UpgradeTabDefinition(
    UnityObject Asset,
    int CategoryId,
    string Name,
    IReadOnlyList<UnityObject> Groups);

public sealed record UpgradeTabPatch(
    string? Name = null,
    IReadOnlyList<UnityObject>? Groups = null);

public interface IUpgradeGroupRegistration
{
    int TypeId { get; }
    UnityObject Asset { get; }
    int Index { get; }
}

public interface IUpgradeTabRegistration
{
    int CategoryId { get; }
    UnityObject Asset { get; }
    int Index { get; }
}
