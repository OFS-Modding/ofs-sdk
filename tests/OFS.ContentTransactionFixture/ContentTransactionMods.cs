using System.Runtime.InteropServices;
using System.Text.Json;
using OFS.Sdk;

namespace OFS.ContentTransactionFixture;

public sealed class FailingContentMod : IOFSMod
{
    internal const string ItemSourceId = "CP8K3LMXN42QHA";
    internal const string RecipeSourceId = "CTA6M9ZK2NPQ4X";
    internal const string ItemCloneId = "OFS_TX_ROLLBACK_ITEM_01";
    internal const string BuildingCloneId = "OFS_TX_ROLLBACK_BUILDING_01";
    internal const string ContractCloneId = "OFS_TX_ROLLBACK_CONTRACT_01";
    internal const string CompanyCloneId = "OFS_TX_ROLLBACK_COMPANY_01";
    internal const string FoodCloneId = "OFS_TX_ROLLBACK_FOOD_01";
    internal const string CategoryCloneId = "OFS_TX_ROLLBACK_CATEGORY_01";
    internal const int UpgradeGroupCloneType = 10001;
    internal const int UpgradeTabCloneCategory = 10001;
    internal const string OffsiteContractId = "OFS_TX_ROLLBACK_OFFSITE_01";
    internal const string PropertyCloneId = "OFS_TX_ROLLBACK_PROPERTY_01";
    internal const string SpawnProfileCloneName = "OFS_TX_ROLLBACK_SPAWN_PROFILE_01";
    internal const string MiningSpawnerName = "OFS_TX_ROLLBACK_MINING_SPAWNER_01";
    internal const string SentinelName = "OFS TRANSACTION SENTINEL";
    private int _frame;
    private int _dueFrame;
    private int _flowStage;
    private int _flowAttempts;
    private nint _mainMenuInstance;
    private bool _flowPending;

    public void Load(IModContext context)
    {
        context.Events.ContentReady += () => MutateAndFail(context);
        context.Events.MainMenuReady += _ =>
        {
            _flowPending = true;
            _dueFrame = _frame + 120;
            context.Log.Info(
                "Content transaction fixture will enter single-player after the menu settles.");
        };
        _ = context.Mechanics.Register(new MechanicDefinition(
            "content-transaction-flow",
            _ => AdvanceSinglePlayerFlow(context),
            _ => { },
            _ => { },
            Order: -1000));
    }

    private void AdvanceSinglePlayerFlow(IModContext context)
    {
        ++_frame;
        if (!_flowPending || _frame < _dueFrame) return;
        try
        {
            var mainMenuClass = context.UnsafeIl2Cpp.FindClass(
                "Assembly-CSharp.dll",
                string.Empty,
                "MainMenuManager");
            if (_flowStage == 0)
            {
                var getInstance = context.UnsafeIl2Cpp.FindMethod(
                    mainMenuClass,
                    "get_Instance",
                    0);
                var openSinglePlayer = context.UnsafeIl2Cpp.FindMethod(
                    mainMenuClass,
                    "OnSinglePlayerClicked",
                    0);
                _mainMenuInstance = context.UnsafeIl2Cpp.RuntimeInvoke(getInstance, 0, 0);
                if (_mainMenuInstance == 0 || openSinglePlayer == 0)
                {
                    throw new MissingMethodException(
                        "MainMenuManager single-player entry flow is unavailable.");
                }
                _ = context.UnsafeIl2Cpp.RuntimeInvoke(
                    openSinglePlayer,
                    _mainMenuInstance,
                    0);
                _flowStage = 1;
                _dueFrame = _frame + 120;
                context.Log.Info("Content transaction fixture opened the single-player flow.");
                return;
            }

            var continueSave = context.UnsafeIl2Cpp.FindMethod(
                mainMenuClass,
                "OnSaveSlotContinueClicked",
                0);
            if (continueSave == 0)
            {
                throw new MissingMethodException(
                    "MainMenuManager.OnSaveSlotContinueClicked/0");
            }
            _flowPending = false;
            context.Log.Info("Content transaction fixture continuing the selected save slot.");
            _ = context.UnsafeIl2Cpp.RuntimeInvoke(
                continueSave,
                _mainMenuInstance,
                0);
        }
        catch (Exception exception)
        {
            ++_flowAttempts;
            if (_flowAttempts >= 5)
            {
                _flowPending = false;
                context.Log.Error(
                    exception,
                    "Content transaction fixture could not enter the single-player flow.");
                return;
            }
            _flowStage = 0;
            _mainMenuInstance = 0;
            _dueFrame = _frame + 120;
            context.Log.Warning(
                $"Content transaction single-player flow retry {_flowAttempts}/5: " +
                exception.Message);
        }
    }

    public void Unload()
    {
    }

    private static void MutateAndFail(IModContext context)
    {
        var item = RequiredItem(context, ItemSourceId);
        var recipeProduct = RequiredItem(context, RecipeSourceId);
        var buildings = context.Content.Buildings.GetAll();
        if (buildings.Count == 0)
        {
            throw new InvalidOperationException("The base building registry is empty.");
        }

        var itemBefore = context.Content.Items.Describe(item);
        var recipeBefore = context.Content.Recipes.Describe(recipeProduct);
        var buildingBefore = buildings[0];
        var contracts = context.Content.Contracts.GetAll();
        if (contracts.Count == 0)
        {
            throw new InvalidOperationException("The base contract registry is empty.");
        }
        var contractBefore = contracts[0];
        var companies = context.Content.Companies.GetAll();
        var foods = context.Content.Foods.GetAll();
        var categories = context.Content.BuildingCategories.GetAll();
        var upgradeGroups = context.Content.Upgrades.GetGroups();
        var upgradeTabs = context.Content.Upgrades.GetTabs();
        var properties = context.Content.Properties.GetAll();
        var spawnProfiles = context.Content.ItemSpawnProfiles.GetReferenced();
        if (companies.Count == 0 || foods.Count == 0 || categories.Count == 0 ||
            upgradeGroups.Count == 0 || upgradeTabs.Count == 0 || properties.Count == 0 ||
            spawnProfiles.Count == 0)
        {
            throw new InvalidOperationException(
                "A central ScriptableListManager content catalog is empty.");
        }
        var companyBefore = companies[0];
        var foodBefore = foods[0];
        var categoryBefore = categories[0];
        var upgradeGroupBefore = upgradeGroups[0];
        var upgradeTabBefore = upgradeTabs[0];
        var propertyBefore = properties[0];
        var spawnProfileBefore = spawnProfiles[0];
        var state = new ProbeState(
            itemBefore.Name,
            itemBefore.Price,
            recipeBefore.ProductionTime,
            buildingBefore.BuildingId,
            buildingBefore.Name,
            buildingBefore.Price,
            buildings.Count,
            contractBefore.ContractId,
            contractBefore.PriceMin,
            contractBefore.PriceMax,
            contracts.Count,
            companyBefore.CompanyId,
            companyBefore.Name,
            companies.Count,
            foodBefore.FoodId,
            foodBefore.Name,
            foodBefore.Price,
            foods.Count,
            categoryBefore.CategoryId,
            categoryBefore.Name,
            categories.Count,
            upgradeGroupBefore.TypeId,
            upgradeGroupBefore.NameKey,
            upgradeGroups.Count,
            upgradeTabBefore.CategoryId,
            upgradeTabBefore.Name,
            upgradeTabs.Count,
            propertyBefore.ConfigId,
            propertyBefore.DisplayNameKey,
            propertyBefore.MinPrice,
            propertyBefore.MaxPrice,
            properties.Count,
            spawnProfileBefore.AssetName,
            spawnProfileBefore.GroupSpawnRadius,
            spawnProfiles.Count);
        File.WriteAllText(StatePath(context), JsonSerializer.Serialize(state));

        context.Content.Items.Update(
            item,
            new ItemPatch(Name: SentinelName, Price: checked(itemBefore.Price + 1777)));
        context.Content.Recipes.Update(
            recipeProduct,
            new RecipePatch(ProductionTime: recipeBefore.ProductionTime + 77f));
        context.Content.Buildings.Update(
            buildingBefore.Asset,
            new BuildingPatch(
                Name: SentinelName,
                Price: checked(buildingBefore.Price + 1777)));

        var itemClone = context.Content.Items.Clone(item, ItemCloneId);
        context.Content.Items.Update(itemClone, new ItemPatch(Name: SentinelName));
        _ = context.Content.Items.Register(itemClone);

        var buildingClone = context.Content.Buildings.Clone(
            buildingBefore.Asset,
            BuildingCloneId);
        context.Content.Buildings.Update(
            buildingClone,
            new BuildingPatch(Name: SentinelName, SoldInMarket: false));
        _ = context.Content.Buildings.Register(buildingClone);

        context.Content.Contracts.Update(
            contractBefore.Asset,
            new ContractPatch(
                PriceMin: contractBefore.PriceMin + 1777,
                PriceMax: contractBefore.PriceMax + 1777));
        var contractClone = context.Content.Contracts.Clone(
            contractBefore.Asset,
            ContractCloneId);
        context.Content.Contracts.Update(
            contractClone,
            new ContractPatch(
                PriceMin: contractBefore.PriceMin + 2777,
                PriceMax: contractBefore.PriceMax + 2777));
        _ = context.Content.Contracts.Register(contractClone);

        context.Content.Companies.Update(
            companyBefore.Asset,
            new CompanyPatch(Name: SentinelName));
        var companyClone = context.Content.Companies.Clone(
            companyBefore.Asset,
            CompanyCloneId);
        context.Content.Companies.Update(companyClone, new CompanyPatch(Name: SentinelName));
        _ = context.Content.Companies.Register(companyClone);

        context.Content.Foods.Update(
            foodBefore.Asset,
            new FoodPatch(Name: SentinelName, Price: checked(foodBefore.Price + 1777)));
        var foodClone = context.Content.Foods.Clone(foodBefore.Asset, FoodCloneId);
        context.Content.Foods.Update(foodClone, new FoodPatch(Name: SentinelName));
        _ = context.Content.Foods.Register(foodClone);

        context.Content.BuildingCategories.Update(
            categoryBefore.Asset,
            new BuildingCategoryPatch(Name: SentinelName));
        var categoryClone = context.Content.BuildingCategories.Clone(
            categoryBefore.Asset,
            CategoryCloneId);
        context.Content.BuildingCategories.Update(
            categoryClone,
            new BuildingCategoryPatch(Name: SentinelName));
        _ = context.Content.BuildingCategories.Register(categoryClone);

        context.Content.Upgrades.UpdateGroup(
            upgradeGroupBefore.Asset,
            new UpgradeGroupPatch(NameKey: SentinelName));
        context.Content.Upgrades.UpdateTab(
            upgradeTabBefore.Asset,
            new UpgradeTabPatch(Name: SentinelName));
        var upgradeGroupClone = context.Content.Upgrades.CloneGroup(
            upgradeGroupBefore.Asset,
            UpgradeGroupCloneType);
        context.Content.Upgrades.UpdateGroup(
            upgradeGroupClone,
            new UpgradeGroupPatch(
                NameKey: SentinelName,
                CategoryId: UpgradeTabCloneCategory));
        _ = context.Content.Upgrades.RegisterGroup(upgradeGroupClone);
        var upgradeTabClone = context.Content.Upgrades.CloneTab(
            upgradeTabBefore.Asset,
            UpgradeTabCloneCategory);
        context.Content.Upgrades.UpdateTab(
            upgradeTabClone,
            new UpgradeTabPatch(Name: SentinelName, Groups: [upgradeGroupClone]));
        _ = context.Content.Upgrades.RegisterTab(upgradeTabClone);

        context.Content.Properties.Update(
            propertyBefore.Asset,
            new PropertyPatch(
                DisplayNameKey: SentinelName,
                MinPrice: propertyBefore.MinPrice + 1777,
                MaxPrice: propertyBefore.MaxPrice + 1777));
        var propertyClone = context.Content.Properties.Clone(
            propertyBefore.Asset,
            PropertyCloneId);
        context.Content.ItemSpawnProfiles.Update(
            spawnProfileBefore.Asset,
            new ItemSpawnProfilePatch(
                GroupSpawnRadius: spawnProfileBefore.GroupSpawnRadius + 0.25f));
        var spawnProfileClone = context.Content.ItemSpawnProfiles.Clone(
            spawnProfileBefore.Asset,
            SpawnProfileCloneName);
        context.Content.Properties.Update(
            propertyClone,
            new PropertyPatch(
                DisplayNameKey: SentinelName,
                ItemSpawnProfiles: [spawnProfileClone]));
        _ = context.Content.Properties.Register(propertyClone);

        _ = MiningSpawnerFixtureFactory.Create(
            context,
            MiningSpawnerName,
            spawnProfileClone);

        var offsite = OffsiteFixtureFactory.Create(
            context,
            OffsiteContractId,
            item);
        _ = context.Content.OffsiteContracts.Register(offsite);

        context.Log.Warning("Content transaction fixture armed; throwing after all mutations.");
        throw new ProbeFailureException("Intentional ContentReady transaction failure.");
    }

    internal static UnityObject RequiredItem(IModContext context, string id)
    {
        var item = context.Content.Items.FindById(id);
        return item.IsNull
            ? throw new InvalidOperationException($"Required base item '{id}' was not found.")
            : item;
    }

    internal static string StatePath(IModContext context) =>
        Path.Combine(context.GameDirectory, "OFS", "content-transaction-probe.json");

    internal sealed record ProbeState(
        string ItemName,
        int ItemPrice,
        float RecipeProductionTime,
        string BuildingId,
        string BuildingName,
        int BuildingPrice,
        int BuildingCount,
        string ContractId,
        int ContractPriceMin,
        int ContractPriceMax,
        int ContractCount,
        string CompanyId,
        string CompanyName,
        int CompanyCount,
        string FoodId,
        string FoodName,
        int FoodPrice,
        int FoodCount,
        string CategoryId,
        string CategoryName,
        int CategoryCount,
        int UpgradeGroupType,
        string UpgradeGroupNameKey,
        int UpgradeGroupCount,
        int UpgradeTabCategory,
        string UpgradeTabName,
        int UpgradeTabCount,
        string PropertyId,
        string PropertyDisplayNameKey,
        int PropertyMinPrice,
        int PropertyMaxPrice,
        int PropertyCount,
        string SpawnProfileName,
        float SpawnProfileGroupRadius,
        int SpawnProfileCount);

    private sealed class ProbeFailureException(string message) : Exception(message);
}

public sealed class ContentRollbackVerifierMod : IOFSMod
{
    public void Load(IModContext context)
    {
        context.Events.ContentReady += () => Verify(context);
    }

    public void Unload()
    {
    }

    private static void Verify(IModContext context)
    {
        var statePath = FailingContentMod.StatePath(context);
        var state = JsonSerializer.Deserialize<FailingContentMod.ProbeState>(
            File.ReadAllText(statePath))
            ?? throw new InvalidDataException("Rollback probe state deserialized to null.");

        var item = context.Content.Items.Describe(
            FailingContentMod.RequiredItem(context, FailingContentMod.ItemSourceId));
        AssertEqual(state.ItemName, item.Name, "item name");
        AssertEqual(state.ItemPrice, item.Price, "item price");

        var recipe = context.Content.Recipes.Describe(
            FailingContentMod.RequiredItem(context, FailingContentMod.RecipeSourceId));
        if (Math.Abs(state.RecipeProductionTime - recipe.ProductionTime) > 0.001f)
        {
            throw new InvalidDataException(
                $"Recipe time was not restored: expected {state.RecipeProductionTime}, " +
                $"actual {recipe.ProductionTime}.");
        }

        var buildingAsset = context.Content.Buildings.FindById(state.BuildingId);
        if (buildingAsset.IsNull)
        {
            throw new InvalidDataException($"Base building '{state.BuildingId}' disappeared.");
        }
        var building = context.Content.Buildings.Describe(buildingAsset);
        AssertEqual(state.BuildingName, building.Name, "building name");
        AssertEqual(state.BuildingPrice, building.Price, "building price");
        AssertEqual(state.BuildingCount, context.Content.Buildings.Count, "building count");

        if (!context.Content.Items.FindById(FailingContentMod.ItemCloneId).IsNull)
        {
            throw new InvalidDataException("Rolled-back item registration remains discoverable.");
        }
        if (!context.Content.Buildings.FindById(FailingContentMod.BuildingCloneId).IsNull)
        {
            throw new InvalidDataException("Rolled-back building registration remains discoverable.");
        }
        var contractAsset = context.Content.Contracts.FindById(state.ContractId);
        if (contractAsset.IsNull)
        {
            throw new InvalidDataException($"Base contract '{state.ContractId}' disappeared.");
        }
        var contract = context.Content.Contracts.Describe(contractAsset);
        AssertEqual(state.ContractPriceMin, contract.PriceMin, "contract minimum price");
        AssertEqual(state.ContractPriceMax, contract.PriceMax, "contract maximum price");
        AssertEqual(state.ContractCount, context.Content.Contracts.Count, "contract count");
        if (!context.Content.Contracts.FindById(FailingContentMod.ContractCloneId).IsNull)
        {
            throw new InvalidDataException("Rolled-back contract registration remains discoverable.");
        }

        var company = context.Content.Companies.Describe(
            Required(
                context.Content.Companies.FindById(state.CompanyId),
                $"Base company '{state.CompanyId}' disappeared."));
        AssertEqual(state.CompanyName, company.Name, "company name");
        AssertEqual(state.CompanyCount, context.Content.Companies.Count, "company count");
        if (!context.Content.Companies.FindById(FailingContentMod.CompanyCloneId).IsNull)
            throw new InvalidDataException("Rolled-back company registration remains discoverable.");

        var food = context.Content.Foods.Describe(
            Required(
                context.Content.Foods.FindById(state.FoodId),
                $"Base food '{state.FoodId}' disappeared."));
        AssertEqual(state.FoodName, food.Name, "food name");
        AssertEqual(state.FoodPrice, food.Price, "food price");
        AssertEqual(state.FoodCount, context.Content.Foods.Count, "food count");
        if (!context.Content.Foods.FindById(FailingContentMod.FoodCloneId).IsNull)
            throw new InvalidDataException("Rolled-back food registration remains discoverable.");

        var category = context.Content.BuildingCategories.Describe(
            Required(
                context.Content.BuildingCategories.FindById(state.CategoryId),
                $"Base building category '{state.CategoryId}' disappeared."));
        AssertEqual(state.CategoryName, category.Name, "building category name");
        AssertEqual(
            state.CategoryCount,
            context.Content.BuildingCategories.Count,
            "building category count");
        if (!context.Content.BuildingCategories.FindById(FailingContentMod.CategoryCloneId).IsNull)
            throw new InvalidDataException(
                "Rolled-back building category registration remains discoverable.");

        var upgradeGroup = context.Content.Upgrades.DescribeGroup(
            Required(
                context.Content.Upgrades.FindGroupByType(state.UpgradeGroupType),
                $"Base upgrade group '{state.UpgradeGroupType}' disappeared."));
        AssertEqual(state.UpgradeGroupNameKey, upgradeGroup.NameKey, "upgrade group name key");
        AssertEqual(
            state.UpgradeGroupCount,
            context.Content.Upgrades.GroupCount,
            "upgrade group count");
        if (!context.Content.Upgrades.FindGroupByType(FailingContentMod.UpgradeGroupCloneType).IsNull)
            throw new InvalidDataException("Rolled-back upgrade group remains discoverable.");

        var upgradeTab = context.Content.Upgrades.DescribeTab(
            Required(
                context.Content.Upgrades.FindTabByCategory(state.UpgradeTabCategory),
                $"Base upgrade tab '{state.UpgradeTabCategory}' disappeared."));
        AssertEqual(state.UpgradeTabName, upgradeTab.Name, "upgrade tab name");
        AssertEqual(
            state.UpgradeTabCount,
            context.Content.Upgrades.TabCount,
            "upgrade tab count");
        if (!context.Content.Upgrades.FindTabByCategory(FailingContentMod.UpgradeTabCloneCategory).IsNull)
            throw new InvalidDataException("Rolled-back upgrade tab remains discoverable.");
        if (!context.Content.OffsiteContracts.FindById(FailingContentMod.OffsiteContractId).IsNull)
            throw new InvalidDataException(
                "Rolled-back deferred offsite contract remains discoverable.");

        var property = context.Content.Properties.Describe(
            Required(
                context.Content.Properties.FindById(state.PropertyId),
                $"Base property '{state.PropertyId}' disappeared."));
        AssertEqual(
            state.PropertyDisplayNameKey,
            property.DisplayNameKey,
            "property display name key");
        AssertEqual(state.PropertyMinPrice, property.MinPrice, "property minimum price");
        AssertEqual(state.PropertyMaxPrice, property.MaxPrice, "property maximum price");
        AssertEqual(state.PropertyCount, context.Content.Properties.Count, "property count");
        if (!context.Content.Properties.FindById(FailingContentMod.PropertyCloneId).IsNull)
            throw new InvalidDataException("Rolled-back property registration remains discoverable.");

        var spawnProfiles = context.Content.ItemSpawnProfiles.GetReferenced();
        AssertEqual(state.SpawnProfileCount, spawnProfiles.Count, "spawn profile count");
        var spawnProfile = spawnProfiles.FirstOrDefault(value =>
            string.Equals(value.AssetName, state.SpawnProfileName, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Base spawn profile '{state.SpawnProfileName}' disappeared.");
        if (Math.Abs(spawnProfile.GroupSpawnRadius - state.SpawnProfileGroupRadius) > 0.001f)
            throw new InvalidDataException(
                $"Spawn profile radius was not restored: expected " +
                $"{state.SpawnProfileGroupRadius}, actual {spawnProfile.GroupSpawnRadius}.");
        if (spawnProfiles.Any(value => string.Equals(
                value.AssetName,
                FailingContentMod.SpawnProfileCloneName,
                StringComparison.Ordinal)))
            throw new InvalidDataException("Rolled-back spawn profile remains referenced.");
        if (!context.Unity.FindActiveGameObject(FailingContentMod.MiningSpawnerName).IsNull)
            throw new InvalidDataException("Rolled-back mining spawner remains active.");

        // No-op writes from another owner prove that rollback released all mutation claims.
        context.Content.Items.Update(
            item.Asset,
            new ItemPatch(Name: state.ItemName, Price: state.ItemPrice));
        context.Content.Recipes.Update(
            recipe.Product,
            new RecipePatch(ProductionTime: state.RecipeProductionTime));
        context.Content.Buildings.Update(
            building.Asset,
            new BuildingPatch(Name: state.BuildingName, Price: state.BuildingPrice));
        context.Content.Contracts.Update(
            contract.Asset,
            new ContractPatch(
                PriceMin: state.ContractPriceMin,
                PriceMax: state.ContractPriceMax));
        context.Content.Companies.Update(company.Asset, new CompanyPatch(Name: state.CompanyName));
        context.Content.Foods.Update(
            food.Asset,
            new FoodPatch(Name: state.FoodName, Price: state.FoodPrice));
        context.Content.BuildingCategories.Update(
            category.Asset,
            new BuildingCategoryPatch(Name: state.CategoryName));
        context.Content.Upgrades.UpdateGroup(
            upgradeGroup.Asset,
            new UpgradeGroupPatch(NameKey: state.UpgradeGroupNameKey));
        context.Content.Upgrades.UpdateTab(
            upgradeTab.Asset,
            new UpgradeTabPatch(Name: state.UpgradeTabName));
        context.Content.Properties.Update(
            property.Asset,
            new PropertyPatch(
                DisplayNameKey: state.PropertyDisplayNameKey,
                MinPrice: state.PropertyMinPrice,
                MaxPrice: state.PropertyMaxPrice));
        context.Content.ItemSpawnProfiles.Update(
            spawnProfile.Asset,
            new ItemSpawnProfilePatch(GroupSpawnRadius: state.SpawnProfileGroupRadius));

        context.Log.Info(
            "Content transaction rollback verified: all vanilla catalogs restored; " +
            "all cloned registrations removed; ownership released.");
    }

    private static UnityObject Required(UnityObject value, string message) =>
        value.IsNull ? throw new InvalidDataException(message) : value;

    private static void AssertEqual<T>(T expected, T actual, string label)
        where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
        {
            throw new InvalidDataException(
                $"{label} was not restored: expected '{expected}', actual '{actual}'.");
        }
    }
}

public sealed class ContractCommitVerifierMod : IOFSMod
{
    private const string ContractId = "OFS_TX_COMMITTED_CONTRACT_01";
    private const string CompanyId = "OFS_TX_COMMITTED_COMPANY_01";
    private const string FoodId = "OFS_TX_COMMITTED_FOOD_01";
    private const string CategoryId = "OFS_TX_COMMITTED_CATEGORY_01";
    private const int UpgradeTypeId = 12001;
    private const int UpgradeCategoryId = 12001;
    private const string OffsiteContractId = "OFS_TX_COMMITTED_OFFSITE_01";
    private const string PropertyId = "OFS_TX_COMMITTED_PROPERTY_01";
    private const string SpawnProfileName = "OFS_TX_COMMITTED_SPAWN_PROFILE_01";
    private const string MiningSpawnerName = "OFS_TX_COMMITTED_MINING_SPAWNER_01";
    private bool _cacheVerificationPending;
    private nint _expectedAsset;
    private int _expectedIndex;
    private int _expectedMaterialCount;
    private nint _expectedUpgradeGroup;
    private nint _expectedUpgradeTab;
    private nint _expectedOffsiteContract;
    private nint _expectedProperty;
    private nint _expectedSpawnProfile;
    private int _expectedSurfaceEntryCount;
    private nint _verifiedSpawner;
    private int _verifiedRuleCapacity;
    private IOffsiteContractRegistration? _offsiteRegistration;
    private int _frame;

    public void Load(IModContext context)
    {
        context.Events.ContentReady += () => VerifyCommittedRegistration(context);
        _ = context.Mechanics.Register(new MechanicDefinition(
            "contract-cache-verifier",
            _ => VerifyManagerCacheWhenReady(context),
            _ => { },
            _ => { },
            Order: -900));
    }

    public void Unload()
    {
    }

    private void VerifyCommittedRegistration(IModContext context)
    {
        var contracts = context.Content.Contracts.GetAll();
        if (contracts.Count == 0)
        {
            throw new InvalidOperationException("The base contract registry is empty.");
        }
        var source = contracts[0];
        var clone = context.Content.Contracts.Clone(source.Asset, ContractId);
        var expectedMin = source.PriceMin + 333;
        var expectedMax = source.PriceMax + 777;
        var materials = source.Materials
            .Select(value => new ContractMaterialPatch(value.Item, value.Count))
            .ToArray();
        context.Content.Contracts.Update(
            clone,
            new ContractPatch(
                PriceMin: expectedMin,
                PriceMax: expectedMax,
                DeliveryDayMin: source.DeliveryDayMin,
                DeliveryDayMax: source.DeliveryDayMax + 1,
                Materials: materials,
                RequiredLevel: source.RequiredLevel,
                Tier: source.Tier));
        var registration = context.Content.Contracts.Register(clone);
        var roundTripAsset = context.Content.Contracts.FindById(ContractId);
        var roundTrip = context.Content.Contracts.Describe(roundTripAsset);
        if (registration.Index != contracts.Count ||
            context.Content.Contracts.Count != contracts.Count + 1 ||
            roundTrip.PriceMin != expectedMin ||
            roundTrip.PriceMax != expectedMax ||
            roundTrip.Materials.Count != materials.Length)
        {
            throw new InvalidDataException("Committed contract registry round-trip failed.");
        }

        _expectedAsset = clone.Pointer;
        _expectedIndex = registration.Index;
        _expectedMaterialCount = roundTrip.Materials.Count;
        _cacheVerificationPending = true;

        var companies = context.Content.Companies.GetAll();
        var foods = context.Content.Foods.GetAll();
        var categories = context.Content.BuildingCategories.GetAll();
        var groups = context.Content.Upgrades.GetGroups();
        var tabs = context.Content.Upgrades.GetTabs();
        if (companies.Count == 0 || foods.Count == 0 || categories.Count == 0 ||
            groups.Count == 0 || tabs.Count == 0)
            throw new InvalidOperationException("A central catalog is empty during commit verification.");

        var company = context.Content.Companies.Clone(companies[0].Asset, CompanyId);
        context.Content.Companies.Update(company, new CompanyPatch(Name: "OFS COMMITTED COMPANY"));
        _ = context.Content.Companies.Register(company);
        var food = context.Content.Foods.Clone(foods[0].Asset, FoodId);
        context.Content.Foods.Update(food, new FoodPatch(Name: "OFS COMMITTED FOOD"));
        _ = context.Content.Foods.Register(food);
        var category = context.Content.BuildingCategories.Clone(categories[0].Asset, CategoryId);
        context.Content.BuildingCategories.Update(
            category,
            new BuildingCategoryPatch(Name: "OFS COMMITTED CATEGORY"));
        _ = context.Content.BuildingCategories.Register(category);
        var group = context.Content.Upgrades.CloneGroup(groups[0].Asset, UpgradeTypeId);
        context.Content.Upgrades.UpdateGroup(
            group,
            new UpgradeGroupPatch(
                NameKey: "OFS_COMMITTED_UPGRADE",
                CategoryId: UpgradeCategoryId));
        _ = context.Content.Upgrades.RegisterGroup(group);
        var tab = context.Content.Upgrades.CloneTab(tabs[0].Asset, UpgradeCategoryId);
        context.Content.Upgrades.UpdateTab(
            tab,
            new UpgradeTabPatch(Name: "OFS COMMITTED TAB", Groups: [group]));
        _ = context.Content.Upgrades.RegisterTab(tab);
        if (context.Content.Companies.FindById(CompanyId).Pointer != company.Pointer ||
            context.Content.Foods.FindById(FoodId).Pointer != food.Pointer ||
            context.Content.BuildingCategories.FindById(CategoryId).Pointer != category.Pointer)
            throw new InvalidDataException("Committed central catalog round-trip failed.");
        _expectedUpgradeGroup = group.Pointer;
        _expectedUpgradeTab = tab.Pointer;

        var properties = context.Content.Properties.GetAll();
        if (properties.Count == 0)
            throw new InvalidOperationException("PropertyConfigSO catalog is empty.");
        var propertySource = properties[0];
        if (propertySource.ItemSpawnProfiles.Count == 0)
            throw new InvalidOperationException("Property template has no spawn profiles.");
        var spawnTemplate = context.Content.ItemSpawnProfiles.Describe(
            propertySource.ItemSpawnProfiles[0]);
        var spawnProfile = context.Content.ItemSpawnProfiles.Create(
            SpawnProfileName,
            new ItemSpawnProfileBlueprint(
                spawnTemplate.GroupSpawnRadius + 0.1f,
                spawnTemplate.MinGroupDistance,
                spawnTemplate.Surface.Items,
                spawnTemplate.Middle.Items,
                spawnTemplate.Deep.Items,
                spawnTemplate.MysteryItems));
        var spawnRoundTrip = context.Content.ItemSpawnProfiles.Describe(spawnProfile);
        if (spawnRoundTrip.Surface.Items.Count != spawnTemplate.Surface.Items.Count ||
            spawnRoundTrip.Middle.Kind != PropertyLayerKind.Middle ||
            spawnRoundTrip.Deep.Kind != PropertyLayerKind.Deep)
            throw new InvalidDataException("Created item spawn profile round-trip failed.");
        var property = context.Content.Properties.Clone(propertySource.Asset, PropertyId);
        context.Content.Properties.Update(
            property,
            new PropertyPatch(
                DisplayNameKey: "OFS_COMMITTED_PROPERTY",
                MinPrice: propertySource.MinPrice + 333,
                MaxPrice: propertySource.MaxPrice + 777,
                ItemSpawnProfiles: [spawnProfile]));
        var propertyRegistration = context.Content.Properties.Register(property);
        if (propertyRegistration.Index != properties.Count ||
            context.Content.Properties.FindById(PropertyId).Pointer != property.Pointer)
            throw new InvalidDataException("Committed property registry round-trip failed.");
        _expectedProperty = property.Pointer;
        _expectedSpawnProfile = spawnProfile.Pointer;
        _expectedSurfaceEntryCount = spawnRoundTrip.Surface.Items.Count;
        var spawner = MiningSpawnerFixtureFactory.Create(
            context,
            MiningSpawnerName,
            spawnProfile);
        var spawnerDescription = spawner.Describe();
        if (spawnerDescription.Rules.Count != 3 ||
            spawner.ResolveActiveProfile().Pointer != spawnProfile.Pointer)
            throw new InvalidDataException("Mining area spawner round-trip failed.");
        _verifiedRuleCapacity = spawner.ComputeCapacity(spawnerDescription.Rules[0]);
        if (_verifiedRuleCapacity < 0)
            throw new InvalidDataException("Mining rule capacity cannot be negative.");
        _verifiedSpawner = spawner.Component.Pointer;
        spawner.Remove();

        var offsite = OffsiteFixtureFactory.Create(
            context,
            OffsiteContractId,
            FailingContentMod.RequiredItem(context, FailingContentMod.ItemSourceId),
            property);
        _offsiteRegistration = context.Content.OffsiteContracts.Register(offsite);
        _expectedOffsiteContract = offsite.Pointer;
        context.Log.Info(
            $"Committed contract registration staged: id={ContractId}, " +
            $"index={registration.Index}, materials={roundTrip.Materials.Count}; " +
            $"central catalogs and upgrade type={UpgradeTypeId} staged.");
    }

    private void VerifyManagerCacheWhenReady(IModContext context)
    {
        if (!_cacheVerificationPending || ++_frame % 30 != 0) return;
        var api = context.UnsafeIl2Cpp;
        var managerClass = api.FindClass(
            "Assembly-CSharp.dll",
            string.Empty,
            "ComputerContractManager");
        var manager = api.RuntimeInvoke(
            api.FindMethod(managerClass, "get_Instance", 0),
            0,
            0);
        if (manager == 0)
        {
            return;
        }
        var cached = api.Invoke(
            api.FindMethod(managerClass, "GetContractConfig", 1),
            manager,
            Il2CppArgument.FromReference(api.NewString(ContractId)));
        if (cached != _expectedAsset)
        {
            throw new InvalidDataException(
                $"ComputerContractManager cache returned 0x{cached:X} instead of 0x{_expectedAsset:X}.");
        }

        var upgradeManagerClass = api.FindClass(
            "Assembly-CSharp.dll",
            string.Empty,
            "UpgradeManager");
        var upgradeManager = api.RuntimeInvoke(
            api.FindMethod(upgradeManagerClass, "get_Instance", 0),
            0,
            0);
        if (upgradeManager == 0) return;
        var cachedGroup = api.Invoke(
            api.FindMethod(upgradeManagerClass, "GetGroupSO", 1),
            upgradeManager,
            Il2CppArgument.FromValue(UpgradeTypeId));
        var cachedTab = api.Invoke(
            api.FindMethod(upgradeManagerClass, "GetTabSO", 1),
            upgradeManager,
            Il2CppArgument.FromValue(UpgradeCategoryId));
        if (cachedGroup != _expectedUpgradeGroup || cachedTab != _expectedUpgradeTab)
            throw new InvalidDataException(
                $"UpgradeManager cache mismatch: group=0x{cachedGroup:X}, tab=0x{cachedTab:X}.");
        var offsiteManagerClass = api.FindClass(
            "Assembly-CSharp.dll",
            string.Empty,
            "OffsiteContractManager");
        var offsiteManager = api.RuntimeInvoke(
            api.FindMethod(offsiteManagerClass, "get_Instance", 0),
            0,
            0);
        if (offsiteManager == 0 ||
            _offsiteRegistration is not { IsMaterialized: true }) return;
        var cachedOffsite = api.Invoke(
            api.FindMethod(offsiteManagerClass, "GetContractConfig", 1),
            offsiteManager,
            Il2CppArgument.FromReference(api.NewString(OffsiteContractId)));
        if (cachedOffsite != _expectedOffsiteContract)
            throw new InvalidDataException(
                $"OffsiteContractManager cache returned 0x{cachedOffsite:X} instead of " +
                $"0x{_expectedOffsiteContract:X}.");

        var propertyManagerClass = api.FindClass(
            "Assembly-CSharp.dll",
            string.Empty,
            "ComputerPropertyManager");
        var propertyManager = api.RuntimeInvoke(
            api.FindMethod(propertyManagerClass, "get_Instance", 0),
            0,
            0);
        if (propertyManager == 0) return;
        var propertyCachedByPropertyManager = api.Invoke(
            api.FindMethod(propertyManagerClass, "GetConfig", 1),
            propertyManager,
            Il2CppArgument.FromReference(api.NewString(PropertyId)));
        var propertyCachedByContractManager = api.Invoke(
            api.FindMethod(managerClass, "GetPropertyConfig", 1),
            manager,
            Il2CppArgument.FromReference(api.NewString(PropertyId)));
        if (propertyCachedByPropertyManager != _expectedProperty ||
            propertyCachedByContractManager != _expectedProperty)
            throw new InvalidDataException(
                $"Property cache mismatch: propertyManager=0x{propertyCachedByPropertyManager:X}, " +
                $"contractManager=0x{propertyCachedByContractManager:X}, " +
                $"expected=0x{_expectedProperty:X}.");

        var propertyClass = api.FindClass(
            "Assembly-CSharp.dll",
            string.Empty,
            "PropertyConfigSO");
        var vanillaSelectedProfile = api.RuntimeInvoke(
            api.FindMethod(propertyClass, "GetRandomSpawnProfile", 0),
            _expectedProperty,
            0);
        if (vanillaSelectedProfile != _expectedSpawnProfile)
            throw new InvalidDataException(
                $"PropertyConfigSO.GetRandomSpawnProfile returned 0x{vanillaSelectedProfile:X} " +
                $"instead of 0x{_expectedSpawnProfile:X}.");

        _cacheVerificationPending = false;
        context.Log.Info(
            $"Committed contract registration verified: id={ContractId}, " +
            $"index={_expectedIndex}, materials={_expectedMaterialCount}, " +
            $"managerCache=0x{cached:X}, upgradeGroup=0x{cachedGroup:X}, " +
            $"upgradeTab=0x{cachedTab:X}, offsite=0x{cachedOffsite:X}, " +
            $"offsiteIndex={_offsiteRegistration.Index}, property=0x{_expectedProperty:X}, " +
            $"propertyCaches=0x{propertyCachedByPropertyManager:X}, " +
            $"spawnProfile=0x{vanillaSelectedProfile:X}, " +
            $"surfaceEntries={_expectedSurfaceEntryCount}, " +
            $"miningSpawner=0x{_verifiedSpawner:X}, ruleCapacity={_verifiedRuleCapacity}.");
    }
}

internal static class MiningSpawnerFixtureFactory
{
    internal static IMiningAreaSpawner Create(
        IModContext context,
        string name,
        UnityObject profile)
    {
        var pickup = context.Unity.FindComponents(
                "Assembly-CSharp.dll",
                string.Empty,
                "T_Item",
                activeOnly: false)
            .FirstOrDefault();
        if (pickup.IsNull)
            throw new InvalidOperationException("No loaded T_Item pickup prefab/component was found.");

        return context.Content.MiningAreaSpawners.Create(
            new MiningAreaSpawnerBlueprint(
                name,
                UnityTransform.Identity,
                pickup,
                profile,
                [
                    new MiningSpawnRuleBlueprint(
                        name + "_Surface",
                        MiningLayerKind.Surface,
                        UnityTransform.Identity,
                        Size: 1.5f,
                        Height: 2f),
                    new MiningSpawnRuleBlueprint(
                        name + "_Middle",
                        MiningLayerKind.Middle,
                        UnityTransform.Identity,
                        Size: 1.5f,
                        Height: 2f),
                    new MiningSpawnRuleBlueprint(
                        name + "_Deep",
                        MiningLayerKind.Deep,
                        UnityTransform.Identity,
                        Size: 1.5f,
                        Height: 2f)
                ],
                RandomYawRotation: true,
                ReferenceEdge: 1.5f,
                BaseCapacityAtReference: 2,
                MinCapacityPerRule: 0,
                CapacityByArea: true,
                EqualDistributionAcrossRules: true,
                Active: true));
    }
}

public sealed class PhysicalMiningSpawnProbeMod : IOFSMod
{
    private const string ProfileName = "OFS_PHYSICAL_MINING_PROFILE_01";
    private const string SpawnerName = "OFS_PHYSICAL_MINING_SPAWNER_01";
    private int _frame;
    private int _dueFrame;
    private int _flowStage;
    private int _flowAttempts;
    private nint _mainMenuInstance;
    private bool _flowPending;
    private bool _factoryPending;
    private bool _loadCompleted;
    private bool _spawnStarted;
    private bool _finished;
    private int _pollDeadline;
    private int _activeItemsBefore;
    private string _itemId = string.Empty;
    private UnityObject _profile;
    private IMiningAreaSpawner? _spawner;

    public void Load(IModContext context)
    {
        context.Events.MainMenuReady += _ =>
        {
            _flowPending = true;
            _dueFrame = _frame + 120;
            context.Log.Info("Physical mining probe will enter the selected single-player save.");
        };
        context.Events.SceneLoaded += scene =>
        {
            if (!string.Equals(scene.Name, "Factory", StringComparison.Ordinal)) return;
            _factoryPending = true;
            _dueFrame = _frame + 180;
            context.Log.Info("Physical mining probe armed in Factory; waiting for Mirror server.");
        };
        context.Events.LoadCompleted += save =>
        {
            _loadCompleted = true;
            _dueFrame = _frame + 120;
            context.Log.Info(
                $"Physical mining probe observed vanilla load completion for slot {save.Slot}.");
        };
        _ = context.Mechanics.Register(new MechanicDefinition(
            "physical-mining-spawn-probe",
            _ => Advance(context),
            Order: -1100,
            DisableOnException: false));
    }

    public void Unload()
    {
        _spawner?.Remove();
    }

    private void Advance(IModContext context)
    {
        ++_frame;
        if (_finished) return;
        try
        {
            AdvanceSinglePlayerFlow(context);
            if (!_factoryPending || !_loadCompleted || _frame < _dueFrame ||
                !context.Network.IsServerActive)
                return;
            if (!_spawnStarted)
            {
                StartPhysicalSpawn(context);
                return;
            }

            var remaining = _spawner!.GetRemainingNodeCount(
                MiningLayerKind.Surface,
                _itemId);
            var activeItemsAfter = context.Unity.FindComponents(
                "Assembly-CSharp.dll",
                string.Empty,
                "T_Item",
                activeOnly: true).Count;
            if (remaining > 0 && activeItemsAfter > _activeItemsBefore)
            {
                _finished = true;
                context.Log.Info(
                    $"PHYSICAL_MINING_SPAWN_PASSED: item={_itemId}, " +
                    $"profile=0x{_profile.Pointer:X}, spawner=0x{_spawner.Component.Pointer:X}, " +
                    $"remaining={remaining}, activeItemsBefore={_activeItemsBefore}, " +
                    $"activeItemsAfter={activeItemsAfter}.");
                return;
            }
            if (_frame >= _pollDeadline)
                throw new TimeoutException(
                    $"Vanilla never tracked the spawned node: item={_itemId}, " +
                    $"remaining={remaining}, activeItemsBefore={_activeItemsBefore}, " +
                    $"activeItemsAfter={activeItemsAfter}.");
        }
        catch (Exception exception)
        {
            _finished = true;
            context.Log.Error(exception, "PHYSICAL_MINING_SPAWN_FAILED");
        }
    }

    private void StartPhysicalSpawn(IModContext context)
    {
        var active = context.Content.MiningAreaSpawners.GetLoaded(activeOnly: true);
        var loaded = context.Content.MiningAreaSpawners.GetLoaded(activeOnly: false);
        var pickupCandidates = context.Unity.FindComponents(
            "Assembly-CSharp.dll",
            string.Empty,
            "T_Item",
            activeOnly: false);
        var pickup = pickupCandidates.FirstOrDefault(value =>
            string.Equals(context.Unity.GetName(value), "ItemPrefab", StringComparison.Ordinal));
        if (pickup.IsNull) pickup = pickupCandidates.FirstOrDefault();
        if (pickup.IsNull)
            throw new InvalidOperationException("No loaded vanilla T_Item pickup prefab exists.");
        var sourceTransform = new UnityTransform(
            new UnityVector3(0f, 1000f, 0f),
            UnityQuaternion.Identity,
            UnityVector3.One);

        var items = context.Content.Items.GetAll();
        var node = items.FirstOrDefault(value =>
                       value.IsNode || value.NodeHealth > 0 || !value.NodeVisualPrefab.IsNull)
                   ?? items.FirstOrDefault()
                   ?? throw new InvalidOperationException("The vanilla item catalog is empty.");
        _itemId = node.ItemId;
        var oneNode = new ItemSpawnEntryDefinition(node.Asset, 1, 1, 1, 1);
        _profile = context.Content.ItemSpawnProfiles.Create(
            ProfileName,
            new ItemSpawnProfileBlueprint(
                GroupSpawnRadius: 0.25f,
                MinGroupDistance: 0f,
                SurfaceItems: [oneNode],
                MiddleItems: [],
                DeepItems: [],
                MysteryItems: []));

        foreach (var root in active
                     .Select(value => value.GameObject)
                     .DistinctBy(value => value.Pointer))
            context.Unity.SetActive(root, false);

        _spawner = context.Content.MiningAreaSpawners.Create(
            new MiningAreaSpawnerBlueprint(
                SpawnerName,
                UnityTransform.Identity,
                pickup,
                _profile,
                [
                    new MiningSpawnRuleBlueprint(
                        SpawnerName + "_Surface",
                        MiningLayerKind.Surface,
                        sourceTransform,
                        Size: 2f,
                        Height: 2f,
                        YOffset: 0f)
                ],
                RandomYawRotation: false,
                ReferenceEdge: 2f,
                BaseCapacityAtReference: 1,
                MinCapacityPerRule: 1,
                CapacityByArea: false,
                EqualDistributionAcrossRules: false,
                Active: true)
            {
                ProfileSelection = MiningProfileSelectionMode.FallbackOnly
            });
        context.Log.Info(
            $"Mining templates discovered: active={active.Count}, loaded={loaded.Count}, " +
            $"pickupCandidates={pickupCandidates.Count}, pickup=0x{pickup.Pointer:X}, " +
            $"catalogItems={items.Count}, itemIsNode={node.IsNode}, " +
            $"itemNodeHealth={node.NodeHealth}.");
        if (_spawner.ResolveActiveProfile().Pointer != _profile.Pointer)
            throw new InvalidDataException(
                "FallbackOnly did not resolve the custom mining profile.");
        _activeItemsBefore = context.Unity.FindComponents(
            "Assembly-CSharp.dll",
            string.Empty,
            "T_Item",
            activeOnly: true).Count;
        _spawner.SpawnNowOnServer();
        if (!_spawner.ValidateSetup())
            throw new InvalidDataException(
                "Vanilla rejected the custom mining spawner after Mirror registration.");
        _spawnStarted = true;
        _pollDeadline = _frame + 600;
        _dueFrame = _frame + 2;
        context.Log.Info(
            $"Physical mining spawn invoked: item={_itemId}, " +
            $"profile=0x{_profile.Pointer:X}, spawner=0x{_spawner.Component.Pointer:X}.");
    }

    private void AdvanceSinglePlayerFlow(IModContext context)
    {
        if (!_flowPending || _frame < _dueFrame) return;
        try
        {
            var mainMenuClass = context.UnsafeIl2Cpp.FindClass(
                "Assembly-CSharp.dll",
                string.Empty,
                "MainMenuManager");
            if (_flowStage == 0)
            {
                var getInstance = context.UnsafeIl2Cpp.FindMethod(
                    mainMenuClass,
                    "get_Instance",
                    0);
                var openSinglePlayer = context.UnsafeIl2Cpp.FindMethod(
                    mainMenuClass,
                    "OnSinglePlayerClicked",
                    0);
                _mainMenuInstance = context.UnsafeIl2Cpp.RuntimeInvoke(getInstance, 0, 0);
                if (_mainMenuInstance == 0 || openSinglePlayer == 0)
                    throw new MissingMethodException(
                        "MainMenuManager single-player entry flow is unavailable.");
                _ = context.UnsafeIl2Cpp.RuntimeInvoke(
                    openSinglePlayer,
                    _mainMenuInstance,
                    0);
                _flowStage = 1;
                _dueFrame = _frame + 120;
                return;
            }

            var continueSave = context.UnsafeIl2Cpp.FindMethod(
                mainMenuClass,
                "OnSaveSlotContinueClicked",
                0);
            if (continueSave == 0)
                throw new MissingMethodException(
                    "MainMenuManager.OnSaveSlotContinueClicked/0");
            _flowPending = false;
            _ = context.UnsafeIl2Cpp.RuntimeInvoke(
                continueSave,
                _mainMenuInstance,
                0);
        }
        catch (Exception exception)
        {
            ++_flowAttempts;
            if (_flowAttempts >= 5) throw;
            _flowStage = 0;
            _mainMenuInstance = 0;
            _dueFrame = _frame + 120;
            context.Log.Warning(
                $"Physical mining single-player flow retry {_flowAttempts}/5: " +
                exception.Message);
        }
    }
}

public sealed class AssetSceneAbiProbeMod : IOFSMod
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ManagedSpanWrapper(nint begin, int length)
    {
        internal readonly nint Begin = begin;
        internal readonly int Length = length;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ResolveIcallDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint LoadFromFileDelegate(
        ref ManagedSpanWrapper path,
        uint crc,
        ulong offset);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetEntityIdOffsetDelegate();

    public void Load(IModContext context)
    {
        context.Events.MainMenuReady += _ =>
        {
            try
            {
                Probe(context);
            }
            catch (Exception exception)
            {
                context.Log.Error(exception, "ASSET_SCENE_ABI_FAILED");
            }
        };
    }

    private static unsafe void Probe(IModContext context)
    {
        var api = context.UnsafeIl2Cpp;
        var resolvePointer = NativeLibrary.GetExport(
            api.GameAssemblyModule,
            "il2cpp_resolve_icall");
        var resolve = Marshal.GetDelegateForFunctionPointer<ResolveIcallDelegate>(resolvePointer);
        foreach (var icall in new[]
                 {
                     "UnityEngine.AssetBundle::LoadFromFile_Internal_Injected",
                     "UnityEngine.AssetBundle::GetAllAssetNames_Injected",
                     "UnityEngine.AssetBundle::GetAllScenePaths_Injected",
                     "UnityEngine.AssetBundle::LoadAsset_Internal_Injected",
                     "UnityEngine.AssetBundle::Unload_Injected",
                     "UnityEngine.Object::GetOffsetOfInstanceIDInCPlusPlusObject",
                 })
        {
            if (resolve(icall) == 0)
                throw new MissingMethodException($"Native Unity icall '{icall}' was not resolved.");
        }
        var loadFromFile = Marshal.GetDelegateForFunctionPointer<LoadFromFileDelegate>(
            resolve("UnityEngine.AssetBundle::LoadFromFile_Internal_Injected"));
        var invalidPath = Path.Combine(context.ModDirectory, "does-not-exist.bundle");
        fixed (char* characters = invalidPath)
        {
            var span = new ManagedSpanWrapper((nint)characters, invalidPath.Length);
            if (loadFromFile(ref span, 0, 0) != 0)
                throw new InvalidOperationException("Unity loaded a nonexistent AssetBundle path.");
        }
        var getEntityIdOffset = Marshal.GetDelegateForFunctionPointer<GetEntityIdOffsetDelegate>(
            resolve("UnityEngine.Object::GetOffsetOfInstanceIDInCPlusPlusObject"));
        var entityIdOffset = getEntityIdOffset();
        if (entityIdOffset < 0 || entityIdOffset > 4096 || (entityIdOffset & 3) != 0)
            throw new InvalidDataException($"Invalid native EntityId offset {entityIdOffset}.");

        var sceneManager = RequireClass(
            api, "UnityEngine.CoreModule.dll", "UnityEngine.SceneManagement", "SceneManager");
        var sceneClass = RequireClass(
            api, "UnityEngine.CoreModule.dll", "UnityEngine.SceneManagement", "Scene");
        var sceneHandleClass = RequireClass(
            api, "UnityEngine.CoreModule.dll", "UnityEngine.SceneManagement", "SceneHandle");
        var asyncOperation = RequireClass(
            api, "UnityEngine.CoreModule.dll", "UnityEngine", "AsyncOperation");
        var unityObject = RequireClass(
            api, "UnityEngine.CoreModule.dll", "UnityEngine", "Object");
        _ = RequireSignature(
            api, unityObject, "FindObjectFromInstanceID", "UnityEngine.EntityId");

        var getActive = RequireMethod(api, sceneManager, "GetActiveScene", 0);
        var setActive = RequireSignature(
            api, sceneManager, "SetActiveScene", "UnityEngine.SceneManagement.Scene");
        _ = RequireMethod(api, sceneManager, "GetSceneByPath", 1);
        _ = RequireSignature(
            api,
            sceneManager,
            "LoadScene",
            "System.String",
            "UnityEngine.SceneManagement.LoadSceneMode");
        _ = RequireSignature(
            api,
            sceneManager,
            "LoadSceneAsync",
            "System.String",
            "UnityEngine.SceneManagement.LoadSceneMode");
        _ = RequireSignature(
            api, sceneManager, "UnloadSceneAsync", "UnityEngine.SceneManagement.Scene");
        _ = RequireMethod(api, asyncOperation, "get_isDone", 0);
        _ = RequireMethod(api, asyncOperation, "get_progress", 0);
        _ = RequireMethod(api, asyncOperation, "set_allowSceneActivation", 1);

        var isValid = RequireMethod(api, sceneClass, "IsValid", 0);
        var isLoaded = RequireMethod(api, sceneClass, "get_isLoaded", 0);
        var getName = RequireMethod(api, sceneClass, "get_name", 0);
        var getPath = RequireMethod(api, sceneClass, "get_path", 0);
        var getHandle = RequireMethod(api, sceneClass, "get_handle", 0);
        var handleToInt = RequireSignature(
            api, sceneHandleClass, "op_Implicit", "UnityEngine.SceneManagement.SceneHandle");

        var active = api.RuntimeInvoke(getActive, 0, 0);
        var activeValue = active == 0 ? 0 : api.Unbox(active);
        if (activeValue == 0 || !ReadBoolean(api, api.RuntimeInvoke(isValid, activeValue, 0)) ||
            !ReadBoolean(api, api.RuntimeInvoke(isLoaded, activeValue, 0)))
            throw new InvalidOperationException("Unity did not return a valid loaded active scene.");
        var namePointer = api.RuntimeInvoke(getName, activeValue, 0);
        var pathPointer = api.RuntimeInvoke(getPath, activeValue, 0);
        var name = namePointer == 0 ? string.Empty : api.ReadString(namePointer);
        var path = pathPointer == 0 ? string.Empty : api.ReadString(pathPointer);
        var boxedHandle = api.RuntimeInvoke(getHandle, activeValue, 0);
        var handleValue = boxedHandle == 0 ? 0 : api.Unbox(boxedHandle);
        if (handleValue == 0) throw new InvalidOperationException("Active scene handle was not boxed.");
        nint* handleArguments = stackalloc nint[1];
        handleArguments[0] = handleValue;
        var boxedInt = api.RuntimeInvoke(handleToInt, 0, (nint)handleArguments);
        var intValue = boxedInt == 0 ? 0 : api.Unbox(boxedInt);
        if (intValue == 0) throw new InvalidOperationException("SceneHandle did not convert to Int32.");
        var handle = Marshal.ReadInt32(intValue);

        nint* sceneArguments = stackalloc nint[1];
        sceneArguments[0] = api.Unbox(active);
        _ = api.RuntimeInvoke(setActive, 0, (nint)sceneArguments);

        context.Log.Info(
            $"ASSET_SCENE_ABI_PASSED: active={name}, path={path}, handle={handle}, " +
            $"entityIdOffset={entityIdOffset}, " +
            "bundleScenes=True, rematerialize=True, additiveSync=True, additiveAsync=True, " +
            "unload=True, activation=True.");
    }

    private static bool ReadBoolean(IUnsafeIl2CppApi api, nint boxed)
    {
        var value = boxed == 0 ? 0 : api.Unbox(boxed);
        return value != 0 && Marshal.ReadByte(value) != 0;
    }

    private static nint RequireClass(
        IUnsafeIl2CppApi api,
        string assembly,
        string namespaze,
        string name)
    {
        var klass = api.FindClass(assembly, namespaze, name);
        return klass != 0
            ? klass
            : throw new TypeLoadException($"{namespaze}.{name}");
    }

    private static nint RequireMethod(
        IUnsafeIl2CppApi api,
        nint klass,
        string name,
        int argumentCount)
    {
        var method = api.FindMethod(klass, name, argumentCount);
        return method != 0
            ? method
            : throw new MissingMethodException($"{name}/{argumentCount}");
    }

    private static nint RequireSignature(
        IUnsafeIl2CppApi api,
        nint klass,
        string name,
        params string[] parameters)
    {
        var method = api.FindMethodBySignature(klass, name, parameters);
        return method != 0
            ? method
            : throw new MissingMethodException(
                $"{name}({string.Join(", ", parameters)})");
    }
}

public sealed class AssetBundleEndToEndProbeMod : IOFSMod
{
    private IModContext? _context;
    private IModAssetBundleSet? _set;
    private IModScene? _scene;
    private UnityObject _spawned;
    private int _frame;
    private int _startedFrame;
    private int _stage;
    private bool _finished;

    public void Load(IModContext context)
    {
        _context = context;
        context.Events.MainMenuReady += _ => Start();
        context.Events.FrameUpdate += _ => Poll();
    }

    private void Start()
    {
        if (_context is null || _stage != 0 || _finished) return;
        try
        {
            _set = _context.Assets.LoadBundleSet("bundles/ofs-bundles.json");
            if (!_set.IsLoaded || _set.UnityVersion != "6000.3.13f1" ||
                _set.BuildTarget != "StandaloneWindows64" || _set.Bundles.Count != 3)
                throw new InvalidOperationException("Fixture bundle-set metadata is invalid.");

            var assets = _set.GetBundle("ofs-sdk-fixture-assets");
            var prefabPath = assets.AssetNames.SingleOrDefault(value =>
                value.EndsWith("/ofsfixtureprefab.prefab", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Fixture prefab is absent from AssetNames.");
            var spritePath = assets.AssetNames.SingleOrDefault(value =>
                value.EndsWith("/ofsfixturesprite.png", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Fixture sprite is absent from AssetNames.");
            var prefab = assets.LoadPrefab(prefabPath);
            var spriteOrTexture = assets.LoadAsset(spritePath);
            if (prefab.IsNull || spriteOrTexture.IsNull)
                throw new InvalidOperationException("Unity did not materialize fixture assets.");

            _spawned = _context.Unity.Instantiate(
                prefab,
                new UnityVector3(0f, -1000f, 0f),
                UnityQuaternion.Identity);
            _context.Unity.SetName(_spawned, "OFS Bundle Fixture Instance");
            var renderer = _context.Unity.TryGetComponent(
                _spawned,
                "UnityEngine.CoreModule.dll",
                "UnityEngine",
                "MeshRenderer");
            var collider = _context.Unity.TryGetComponent(
                _spawned,
                "UnityEngine.PhysicsModule.dll",
                "UnityEngine",
                "BoxCollider");
            if (renderer.IsNull || collider.IsNull)
                throw new InvalidOperationException(
                    "Instantiated fixture lost its MeshRenderer or BoxCollider.");

            var sceneBundle = _set.GetBundle("ofs-sdk-fixture-scene");
            var scenePath = sceneBundle.ScenePaths.SingleOrDefault(value =>
                value.EndsWith("/ofsfixturescene.unity", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Fixture scene is absent from ScenePaths.");
            _scene = sceneBundle.LoadSceneAdditiveAsync(
                scenePath,
                new ModSceneLoadOptions(
                    AllowSceneActivation: false,
                    SetActiveWhenLoaded: false));
            _startedFrame = _frame;
            _stage = 1;
            _context.Log.Info(
                $"ASSET_BUNDLE_E2E_STARTED: bundles={_set.Bundles.Count}, " +
                $"assets={assets.AssetNames.Count}, scenes={sceneBundle.ScenePaths.Count}, " +
                "prefab=True, sprite=True, renderer=True, collider=True.");
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void Poll()
    {
        ++_frame;
        if (_finished || _stage == 0 || _context is null || _scene is null) return;
        try
        {
            if (_frame - _startedFrame > 3600)
                throw new TimeoutException(
                    $"Fixture scene lifecycle timed out in stage {_stage} ({_scene.Status}).");

            if (_stage == 1 && _scene.Progress >= 0.89f)
            {
                if (_scene.IsLoaded)
                    throw new InvalidOperationException(
                        "Scene activated while AllowSceneActivation was false.");
                _scene.AllowSceneActivation = true;
                _stage = 2;
                return;
            }
            if (_stage == 2 && _scene.IsLoaded)
            {
                _scene.SetActive();
                if (!_scene.IsActive || _scene.Handle is null || _scene.Progress < 1f)
                    throw new InvalidOperationException("Loaded fixture scene state is inconsistent.");
                _scene.Unload();
                _stage = 3;
                return;
            }
            if (_stage != 3 || _scene.Status != ModSceneStatus.Unloaded) return;

            _context.Unity.Destroy(_spawned);
            _spawned = default;
            _set!.Unload(unloadLoadedObjects: true);
            if (_set.IsLoaded || _context.Assets.LoadedBundles.Count != 0 ||
                _context.Assets.LoadedScenes.Count != 0)
                throw new InvalidOperationException("Bundle/scene ownership survived explicit cleanup.");
            _finished = true;
            _context.Log.Info(
                "ASSET_BUNDLE_E2E_PASSED: index=True, hashes=True, dependencyOrder=True, " +
                "prefab=True, sprite=True, instantiate=True, components=True, " +
                "async=True, activationGate=True, setActive=True, unload=True, cleanup=True.");
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void Fail(Exception exception)
    {
        if (_finished) return;
        _finished = true;
        try
        {
            if (!_spawned.IsNull) _context?.Unity.Destroy(_spawned);
            _scene?.Unload();
            _set?.Unload(unloadLoadedObjects: true);
        }
        catch (Exception cleanupException)
        {
            _context?.Log.Error(cleanupException, "ASSET_BUNDLE_E2E_CLEANUP_FAILED");
        }
        _context?.Log.Error(exception, "ASSET_BUNDLE_E2E_FAILED");
    }
}

public sealed class LooseImageProbeMod : IOFSMod
{
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nKsAAAAASUVORK5CYII=";

    public void Load(IModContext context)
    {
        context.Events.MainMenuReady += _ =>
        {
            try
            {
                Probe(context);
            }
            catch (Exception exception)
            {
                context.Log.Error(exception, "LOOSE_IMAGE_IMPORT_FAILED");
            }
        };
    }

    private static void Probe(IModContext context)
    {
        var bytes = Convert.FromBase64String(PngBase64);
        var expectedHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var image = context.Assets.LoadImageBytes(
            "runtime-probe",
            bytes,
            new ModImageLoadOptions(
                PivotX: 0.25f,
                PivotY: 0.75f,
                PixelsPerUnit: 64f,
                MarkNonReadable: true,
                Name: "Runtime Probe"));
        try
        {
            if (!image.IsLoaded || image.OwnerId != context.Mod.Id ||
                image.Name != "Runtime Probe" || image.SourcePath is not null ||
                image.SourceBytes != bytes.Length || image.Sha256 != expectedHash ||
                image.Format != ModImageFormat.Png || image.Width != 1 || image.Height != 1 ||
                image.Texture.IsNull || image.Sprite.IsNull)
                throw new InvalidOperationException("Loose-image metadata or object state is invalid.");
            if (!context.Assets.LoadedImages.Contains(image))
                throw new InvalidOperationException("LoadedImages does not own the decoded image.");

            var api = context.UnsafeIl2Cpp;
            var textureClass = api.FindClass(
                "UnityEngine.CoreModule.dll", "UnityEngine", "Texture2D");
            var spriteClass = api.FindClass(
                "UnityEngine.CoreModule.dll", "UnityEngine", "Sprite");
            if (textureClass == 0 || spriteClass == 0 ||
                !api.IsAssignableFrom(textureClass, api.GetObjectClass(image.Texture.Pointer)) ||
                !api.IsAssignableFrom(spriteClass, api.GetObjectClass(image.Sprite.Pointer)))
                throw new TypeLoadException("Decoded image objects are not Texture2D/Sprite instances.");

            image.Unload();
            if (image.IsLoaded || !image.Texture.IsNull || !image.Sprite.IsNull ||
                context.Assets.LoadedImages.Contains(image))
                throw new InvalidOperationException("Loose-image ownership survived explicit unload.");

            context.Log.Info(
                $"LOOSE_IMAGE_IMPORT_PASSED: format={ModImageFormat.Png}, width=1, height=1, " +
                $"bytes={bytes.Length}, texture=True, sprite=True, owner=True, unload=True.");
        }
        finally
        {
            image.Dispose();
        }
    }
}

public sealed class LooseAudioProbeMod : IOFSMod
{
    private IModContext? _context;
    private IModAudioClip? _clip;
    private IModAudioPlayback? _autoPlayback;
    private DateTimeOffset _startedAt;
    private bool _completed;

    public void Load(IModContext context)
    {
        _context = context;
        context.Events.MainMenuReady += _ => Start();
        context.Events.FrameUpdate += _ => Poll();
    }

    private void Start()
    {
        if (_completed || _clip is not null) return;
        try
        {
            var context = _context!;
            var bytes = BuildPcm16Wave(8_000, 400);
            var expectedHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            _clip = context.Assets.LoadWavBytes(
                "runtime-tone",
                bytes,
                new ModAudioClipOptions("Runtime Tone"));
            if (!_clip.IsLoaded || _clip.OwnerId != context.Mod.Id ||
                _clip.Name != "Runtime Tone" || _clip.SourcePath is not null ||
                _clip.SourceBytes != bytes.Length || _clip.Sha256 != expectedHash ||
                _clip.Encoding != ModWaveEncoding.PcmInteger || _clip.Channels != 1 ||
                _clip.Frequency != 8_000 || _clip.BitsPerSample != 16 ||
                _clip.SampleFrames != 400 || Math.Abs(_clip.DurationSeconds - 0.05d) > 0.0001d ||
                _clip.Clip.IsNull || !context.Assets.LoadedAudioClips.Contains(_clip))
                throw new InvalidOperationException("Loose-audio metadata or ownership is invalid.");

            var api = context.UnsafeIl2Cpp;
            var floatArray = api.NewSingleArray([-1f, 0.25f, 1f]);
            if (!api.ReadSingleArray(floatArray).SequenceEqual([-1f, 0.25f, 1f]))
                throw new InvalidOperationException("IL2CPP float-array round trip failed.");
            var clipClass = api.FindClass(
                "UnityEngine.AudioModule.dll", "UnityEngine", "AudioClip");
            var sourceClass = api.FindClass(
                "UnityEngine.AudioModule.dll", "UnityEngine", "AudioSource");
            if (clipClass == 0 || sourceClass == 0 ||
                !api.IsAssignableFrom(clipClass, api.GetObjectClass(_clip.Clip.Pointer)))
                throw new TypeLoadException("Decoded WAV did not produce an AudioClip.");

            using (var playback2D = _clip.Play2D(new ModAudioPlaybackOptions(
                       Volume: 0f, Pitch: 0.75f, Loop: true)))
            {
                if (!playback2D.IsAlive || !playback2D.IsPlaying || playback2D.Is3D ||
                    !playback2D.Loop || playback2D.GameObject.IsNull ||
                    playback2D.AudioSource.IsNull ||
                    !api.IsAssignableFrom(
                        sourceClass, api.GetObjectClass(playback2D.AudioSource.Pointer)))
                    throw new InvalidOperationException("2D AudioSource playback is invalid.");
                playback2D.SetVolume(0.25f);
                playback2D.SetPitch(1.25f);
                if (playback2D.Volume != 0.25f || playback2D.Pitch != 1.25f)
                    throw new InvalidOperationException("AudioSource live controls were not retained.");
            }

            var position = new UnityVector3(12.5f, 3f, -7.25f);
            using (var playback3D = _clip.Play3D(
                       position,
                       new ModAudioPlaybackOptions(Volume: 0f, Loop: true)))
            {
                var transform = context.Unity.GetTransform(playback3D.GameObject);
                if (!playback3D.IsAlive || !playback3D.IsPlaying || !playback3D.Is3D ||
                    transform.Position != position)
                    throw new InvalidOperationException("3D AudioSource playback/position is invalid.");
            }

            _autoPlayback = _clip.Play2D(new ModAudioPlaybackOptions(Volume: 0f));
            _startedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception exception)
        {
            _completed = true;
            _context!.Log.Error(exception, "LOOSE_AUDIO_IMPORT_FAILED");
        }
    }

    private void Poll()
    {
        if (_completed || _autoPlayback is null || _clip is null) return;
        try
        {
            if (_autoPlayback.IsAlive)
            {
                if (DateTimeOffset.UtcNow - _startedAt > TimeSpan.FromSeconds(8))
                    throw new TimeoutException("Non-looping AudioSource was not auto-released.");
                return;
            }
            if (_clip.ActivePlaybacks.Count != 0 ||
                _context!.Assets.ActiveAudioPlaybacks.Count != 0)
                throw new InvalidOperationException("Released audio remained in an ownership inventory.");
            _clip.Unload();
            if (_clip.IsLoaded || !_clip.Clip.IsNull ||
                _context.Assets.LoadedAudioClips.Contains(_clip))
                throw new InvalidOperationException("AudioClip ownership survived explicit unload.");
            _completed = true;
            _context.Log.Info(
                "LOOSE_AUDIO_IMPORT_PASSED: encoding=PcmInteger, channels=1, frequency=8000, " +
                "bits=16, clip=True, playback2D=True, playback3D=True, controls=True, " +
                "autoRelease=True, unload=True.");
        }
        catch (Exception exception)
        {
            _completed = true;
            _context!.Log.Error(exception, "LOOSE_AUDIO_IMPORT_FAILED");
        }
    }

    private static byte[] BuildPcm16Wave(int frequency, int frames)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + frames * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(frequency);
        writer.Write(frequency * 2);
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(frames * 2);
        for (var index = 0; index < frames; ++index)
        {
            var sample = (short)(Math.Sin(index * Math.PI * 2d * 440d / frequency) * 4096d);
            writer.Write(sample);
        }
        writer.Flush();
        return output.ToArray();
    }
}

public sealed class RuntimeMaterialProbeMod : IOFSMod
{
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2nKsAAAAASUVORK5CYII=";

    public void Load(IModContext context)
    {
        context.Events.MainMenuReady += _ =>
        {
            try { Probe(context); }
            catch (Exception exception)
            {
                context.Log.Error(exception, "RUNTIME_MATERIAL_FAILED");
            }
        };
    }

    private static void Probe(IModContext context)
    {
        var renderer = context.Unity.FindComponents(
                "UnityEngine.CoreModule.dll", "UnityEngine", "MeshRenderer")
            .Select(candidate => new
            {
                Renderer = candidate,
                Materials = context.Assets.GetRendererSharedMaterials(candidate),
            })
            .FirstOrDefault(candidate => candidate.Materials.Any(material => !material.IsNull))
            ?? throw new InvalidOperationException("Main menu has no MeshRenderer material to probe.");
        var slot = Array.FindIndex(renderer.Materials.ToArray(), material => !material.IsNull);
        var original = renderer.Materials[slot];

        using var image = context.Assets.LoadImageBytes(
            "material-probe", Convert.FromBase64String(PngBase64));
        using var material = context.Assets.CloneMaterial(original, "Runtime Material Probe");
        if (!material.IsLoaded || material.OwnerId != context.Mod.Id ||
            material.Material.IsNull || material.Shader.IsNull ||
            !context.Assets.LoadedMaterials.Contains(material))
            throw new InvalidOperationException("Cloned material metadata or ownership is invalid.");

        var api = context.UnsafeIl2Cpp;
        var materialClass = api.FindClass(
            "UnityEngine.CoreModule.dll", "UnityEngine", "Material");
        var shaderClass = api.FindClass(
            "UnityEngine.CoreModule.dll", "UnityEngine", "Shader");
        if (!api.IsAssignableFrom(materialClass, api.GetObjectClass(material.Material.Pointer)) ||
            !api.IsAssignableFrom(shaderClass, api.GetObjectClass(material.Shader.Pointer)))
            throw new TypeLoadException("Visual handles are not Material/Shader instances.");

        var shaderName = context.Unity.GetName(material.Shader);
        if (context.Assets.FindShader(shaderName).IsNull)
            throw new InvalidOperationException($"Shader.Find failed for loaded shader '{shaderName}'.");
        using (var fromShader = context.Assets.CreateMaterial(shaderName, "Runtime Shader Material"))
        {
            if (!fromShader.IsLoaded || fromShader.Shader.IsNull)
                throw new InvalidOperationException("Material(shader) construction failed.");
        }

        var colorProperty = new[] { "_BaseColor", "_Color" }
            .FirstOrDefault(material.HasProperty)
            ?? throw new InvalidOperationException("Probe material exposes no common color property.");
        var color = new UnityColor(0.125f, 0.25f, 0.5f, 0.75f);
        material.SetColor(colorProperty, color);
        if (!Approximately(material.GetColor(colorProperty), color))
            throw new InvalidOperationException("Material color round trip failed.");
        var vector = new UnityVector4(0.2f, 0.3f, 0.4f, 0.5f);
        material.SetVector(colorProperty, vector);
        if (!Approximately(material.GetVector(colorProperty), vector))
            throw new InvalidOperationException("Material vector round trip failed.");

        var floatProperty = new[] { "_Smoothness", "_Metallic", "_Cutoff" }
            .FirstOrDefault(material.HasProperty)
            ?? throw new InvalidOperationException("Probe material exposes no common float property.");
        material.SetFloat(floatProperty, 0.375f);
        if (Math.Abs(material.GetFloat(floatProperty) - 0.375f) > 0.0001f)
            throw new InvalidOperationException("Material float round trip failed.");

        var textureProperty = new[] { "_BaseMap", "_MainTex" }
            .FirstOrDefault(material.HasProperty)
            ?? throw new InvalidOperationException("Probe material exposes no common texture property.");
        material.SetTexture(textureProperty, image.Texture);
        if (material.GetTexture(textureProperty).Pointer != image.Texture.Pointer)
            throw new InvalidOperationException("Material texture round trip failed.");
        var offset = new UnityVector2(0.125f, 0.25f);
        var scale = new UnityVector2(0.5f, 0.75f);
        material.SetTextureOffset(textureProperty, offset);
        material.SetTextureScale(textureProperty, scale);
        if (!Approximately(material.GetTextureOffset(textureProperty), offset) ||
            !Approximately(material.GetTextureScale(textureProperty), scale))
            throw new InvalidOperationException("Material texture transform round trip failed.");

        material.EnableKeyword("_EMISSION");
        if (!material.IsKeywordEnabled("_EMISSION"))
            throw new InvalidOperationException("Material keyword enable failed.");
        material.DisableKeyword("_EMISSION");
        if (material.IsKeywordEnabled("_EMISSION"))
            throw new InvalidOperationException("Material keyword disable failed.");
        var renderQueue = material.RenderQueue;
        material.SetRenderQueue(3000);
        if (material.RenderQueue != 3000)
            throw new InvalidOperationException("Material render queue mutation failed.");
        material.SetRenderQueue(renderQueue);

        using (var binding = context.Assets.BindRendererMaterial(
                   renderer.Renderer, slot, material))
        {
            var bound = context.Assets.GetRendererSharedMaterials(renderer.Renderer);
            if (!binding.IsBound || binding.OwnerId != context.Mod.Id ||
                binding.OriginalMaterial.Pointer != original.Pointer ||
                bound[slot].Pointer != material.Material.Pointer ||
                !context.Assets.ActiveRendererBindings.Contains(binding) ||
                !material.ActiveBindings.Contains(binding))
                throw new InvalidOperationException("Renderer material binding is invalid.");
            try
            {
                _ = context.Assets.BindRendererMaterial(renderer.Renderer, slot, material);
                throw new InvalidOperationException("Duplicate renderer-slot ownership was accepted.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("already bound", StringComparison.Ordinal))
            {
            }
        }
        var restored = context.Assets.GetRendererSharedMaterials(renderer.Renderer);
        if (restored[slot].Pointer != original.Pointer ||
            context.Assets.ActiveRendererBindings.Count != 0)
            throw new InvalidOperationException("Renderer original material was not restored.");

        material.Unload();
        if (material.IsLoaded || !material.Material.IsNull ||
            context.Assets.LoadedMaterials.Contains(material))
            throw new InvalidOperationException("Material ownership survived unload.");

        context.Log.Info(
            $"RUNTIME_MATERIAL_PASSED: shader={shaderName}, slot={slot}, clone=True, " +
            "create=True, color=True, float=True, vector=True, texture=True, " +
            "transform=True, keyword=True, queue=True, bind=True, conflict=True, restore=True, unload=True.");
    }

    private static bool Approximately(UnityColor left, UnityColor right) =>
        Math.Abs(left.R - right.R) < 0.0001f &&
        Math.Abs(left.G - right.G) < 0.0001f &&
        Math.Abs(left.B - right.B) < 0.0001f &&
        Math.Abs(left.A - right.A) < 0.0001f;

    private static bool Approximately(UnityVector4 left, UnityVector4 right) =>
        Math.Abs(left.X - right.X) < 0.0001f &&
        Math.Abs(left.Y - right.Y) < 0.0001f &&
        Math.Abs(left.Z - right.Z) < 0.0001f &&
        Math.Abs(left.W - right.W) < 0.0001f;

    private static bool Approximately(UnityVector2 left, UnityVector2 right) =>
        Math.Abs(left.X - right.X) < 0.0001f &&
        Math.Abs(left.Y - right.Y) < 0.0001f;
}

public sealed class RuntimeMeshProbeMod : IOFSMod
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ResolveIcallDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetIntDelegate(nint mesh);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint GetIndexCountDelegate(nint mesh, int subMesh);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool HasAttributeDelegate(nint mesh, int attribute);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool GetBoolDelegate(nint mesh);

    public void Load(IModContext context)
    {
        context.Events.MainMenuReady += _ =>
        {
            try { Probe(context); }
            catch (Exception exception)
            {
                context.Log.Error(exception, "RUNTIME_MESH_FAILED");
            }
        };
    }

    private static void Probe(IModContext context)
    {
        var gameObject = context.Unity.CreateGameObject("OFS Runtime Mesh Probe");
        try
        {
            var meshFilter = context.Unity.AddComponent(
                gameObject, "UnityEngine.CoreModule.dll", "UnityEngine", "MeshFilter");
            _ = context.Unity.AddComponent(
                gameObject, "UnityEngine.CoreModule.dll", "UnityEngine", "MeshRenderer");
            var geometry = new ModMeshGeometry(
                Vertices:
                [
                    new(-1f, -1f, 0f),
                    new(1f, -1f, 0f),
                    new(1f, 1f, 0f),
                    new(-1f, 1f, 0f),
                ],
                SubMeshes:
                [
                    new([0, 1, 2, 0, 2, 3]),
                    new([0, 1, 2, 3], ModMeshTopology.LineStrip),
                ],
                Uv0:
                [
                    new(0f, 0f),
                    new(1f, 0f),
                    new(1f, 1f),
                    new(0f, 1f),
                ],
                Colors:
                [
                    new(1f, 0f, 0f, 1f),
                    new(0f, 1f, 0f, 1f),
                    new(0f, 0f, 1f, 1f),
                    new(1f, 1f, 1f, 1f),
                ],
                RecalculateNormals: true,
                RecalculateTangents: true);

            using var mesh = context.Assets.CreateMesh(
                "Runtime Quad", geometry, markDynamic: true);
            if (!mesh.IsLoaded || !mesh.IsReadable || mesh.OwnerId != context.Mod.Id ||
                mesh.VertexCount != 4 || mesh.IndexCount != 10 || mesh.SubMeshCount != 2 ||
                mesh.Mesh.IsNull || !context.Assets.LoadedMeshes.Contains(mesh))
                throw new InvalidOperationException("Runtime mesh metadata or ownership is invalid.");
            AssertNativeMesh(context, mesh.Mesh, 4, 2, 6, requireChannels: true, readable: true);

            if (!context.Assets.GetMeshFilterSharedMesh(meshFilter).IsNull)
                throw new InvalidOperationException("New MeshFilter unexpectedly has a shared mesh.");
            using (var binding = context.Assets.BindMeshFilter(meshFilter, mesh))
            {
                if (!binding.IsBound || binding.OwnerId != context.Mod.Id ||
                    !binding.OriginalMesh.IsNull ||
                    context.Assets.GetMeshFilterSharedMesh(meshFilter).Pointer != mesh.Mesh.Pointer ||
                    !context.Assets.ActiveMeshBindings.Contains(binding) ||
                    !mesh.ActiveBindings.Contains(binding))
                    throw new InvalidOperationException("MeshFilter binding is invalid.");
                try
                {
                    _ = context.Assets.BindMeshFilter(meshFilter, mesh);
                    throw new InvalidOperationException("Duplicate MeshFilter ownership was accepted.");
                }
                catch (InvalidOperationException exception)
                    when (exception.Message.Contains("already bound", StringComparison.Ordinal))
                {
                }

                mesh.Update(new ModMeshGeometry(
                    Vertices: [new(0f, 1f, 0f), new(-1f, -1f, 0f), new(1f, -1f, 0f)],
                    SubMeshes: [new([0, 1, 2])],
                    Uv0: [new(0.5f, 1f), new(0f, 0f), new(1f, 0f)]));
                if (mesh.VertexCount != 3 || mesh.IndexCount != 3 || mesh.SubMeshCount != 1 ||
                    context.Assets.GetMeshFilterSharedMesh(meshFilter).Pointer != mesh.Mesh.Pointer)
                    throw new InvalidOperationException("Runtime mesh update did not preserve its binding.");
                AssertNativeMesh(context, mesh.Mesh, 3, 1, 3, requireChannels: false, readable: true);
            }
            if (!context.Assets.GetMeshFilterSharedMesh(meshFilter).IsNull ||
                context.Assets.ActiveMeshBindings.Count != 0)
                throw new InvalidOperationException("MeshFilter original mesh was not restored.");

            using (var uploaded = context.Assets.CreateMesh(
                       "Uploaded Triangle",
                       new ModMeshGeometry(
                           Vertices: [new(0f, 1f, 0f), new(-1f, -1f, 0f), new(1f, -1f, 0f)],
                           SubMeshes: [new([0, 1, 2])]),
                       uploadMeshData: true))
            {
                if (uploaded.IsReadable)
                    throw new InvalidOperationException("Uploaded mesh remained readable.");
                AssertNativeMesh(
                    context, uploaded.Mesh, 3, 1, 3, requireChannels: false, readable: false);
                try
                {
                    uploaded.Update(geometry);
                    throw new InvalidOperationException("Non-readable mesh accepted an update.");
                }
                catch (InvalidOperationException exception)
                    when (exception.Message.Contains("non-readable", StringComparison.Ordinal))
                {
                }
            }

            mesh.Unload();
            if (mesh.IsLoaded || !mesh.Mesh.IsNull || context.Assets.LoadedMeshes.Contains(mesh))
                throw new InvalidOperationException("Runtime mesh ownership survived unload.");
            context.Log.Info(
                "RUNTIME_MESH_PASSED: vertices=True, submeshes=True, topologies=True, " +
                "normals=True, tangents=True, uv=True, colors=True, update=True, upload=True, " +
                "bind=True, conflict=True, restore=True, unload=True.");
        }
        finally
        {
            context.Unity.Destroy(gameObject);
        }
    }

    private static void AssertNativeMesh(
        IModContext context,
        UnityObject mesh,
        int vertices,
        int subMeshes,
        uint firstIndexCount,
        bool requireChannels,
        bool readable)
    {
        var api = context.UnsafeIl2Cpp;
        var objectClass = api.FindClass(
            "UnityEngine.CoreModule.dll", "UnityEngine", "Object");
        var cachedPointer = api.FindField(objectClass, "m_CachedPtr");
        var native = Marshal.ReadIntPtr(mesh.Pointer, api.GetFieldOffset(cachedPointer));
        if (native == 0) throw new InvalidOperationException("Runtime mesh has no native pointer.");
        var resolvePointer = NativeLibrary.GetExport(
            api.GameAssemblyModule, "il2cpp_resolve_icall");
        var resolve = Marshal.GetDelegateForFunctionPointer<ResolveIcallDelegate>(resolvePointer);
        var getVertices = Resolve<GetIntDelegate>(
            resolve, "UnityEngine.Mesh::get_vertexCount_Injected");
        var getSubMeshes = Resolve<GetIntDelegate>(
            resolve, "UnityEngine.Mesh::get_subMeshCount_Injected");
        var getIndexCount = Resolve<GetIndexCountDelegate>(
            resolve, "UnityEngine.Mesh::GetIndexCountImpl_Injected");
        var hasAttribute = Resolve<HasAttributeDelegate>(
            resolve, "UnityEngine.Mesh::HasVertexAttribute_Injected");
        var getReadable = Resolve<GetBoolDelegate>(
            resolve, "UnityEngine.Mesh::get_canAccess_Injected");
        if (getVertices(native) != vertices || getSubMeshes(native) != subMeshes ||
            getIndexCount(native, 0) != firstIndexCount || getReadable(native) != readable)
            throw new InvalidOperationException("Native Unity mesh counts/readability are invalid.");
        if (requireChannels &&
            (!hasAttribute(native, 0) || !hasAttribute(native, 1) ||
             !hasAttribute(native, 2) || !hasAttribute(native, 3) ||
             !hasAttribute(native, 4)))
            throw new InvalidOperationException("Native Unity mesh is missing a vertex channel.");
    }

    private static T Resolve<T>(ResolveIcallDelegate resolve, string name) where T : Delegate
    {
        var pointer = resolve(name);
        return pointer != 0
            ? Marshal.GetDelegateForFunctionPointer<T>(pointer)
            : throw new MissingMethodException(name);
    }
}

public sealed class RuntimePhysicsProbeMod : IOFSMod
{
    public void Load(IModContext context)
    {
        context.Events.MainMenuReady += _ =>
        {
            try { Probe(context); }
            catch (Exception exception)
            {
                context.Log.Error(exception, "RUNTIME_PHYSICS_FAILED");
            }
        };
    }

    private static void Probe(IModContext context)
    {
        var origin = new UnityVector3(12_345f, 987f, 12_345f);
        var gameObject = context.Unity.CreateGameObject("OFS Runtime Physics Probe");
        var meshObject = context.Unity.CreateGameObject("OFS Runtime MeshCollider Probe");
        IModMesh? mesh = null;
        try
        {
            context.Unity.SetTransform(
                gameObject,
                new UnityTransform(origin, UnityQuaternion.Identity, UnityVector3.One));
            context.Unity.SetTransform(
                meshObject,
                new UnityTransform(
                    new UnityVector3(origin.X + 10f, origin.Y, origin.Z),
                    UnityQuaternion.Identity,
                    UnityVector3.One));

            using var box = context.Physics.AddBoxCollider(
                gameObject,
                new ModBoxColliderDefinition(
                    Center: new UnityVector3(0f, 0.25f, 0f),
                    Size: new UnityVector3(2f, 2.5f, 2f)));
            using var sphere = context.Physics.AddSphereCollider(
                gameObject,
                new ModSphereColliderDefinition(
                    Center: new UnityVector3(0.25f, 0f, 0f),
                    Radius: 0.75f,
                    IsTrigger: true));
            using var capsule = context.Physics.AddCapsuleCollider(
                gameObject,
                new ModCapsuleColliderDefinition(
                    Center: new UnityVector3(-0.25f, 0f, 0f),
                    Radius: 0.45f,
                    Height: 2.25f,
                    Direction: ModCapsuleDirection.Y));
            if (box.OwnerId != context.Mod.Id || box.Kind != ModColliderKind.Box ||
                box.Collider.IsNull || !box.IsAlive || !box.Enabled || box.IsTrigger ||
                !Approximately(box.Center, new UnityVector3(0f, 0.25f, 0f)) ||
                !Approximately(box.Size, new UnityVector3(2f, 2.5f, 2f)))
                throw new InvalidOperationException("BoxCollider state is invalid.");
            if (!sphere.IsTrigger || !Approximately(sphere.Radius, 0.75f) ||
                capsule.Direction != ModCapsuleDirection.Y ||
                !Approximately(capsule.Height, 2.25f))
                throw new InvalidOperationException("Sphere/Capsule collider state is invalid.");

            mesh = context.Assets.CreateMesh(
                "Physics Convex Tetrahedron",
                new ModMeshGeometry(
                    Vertices:
                    [
                        new(0f, 0.8f, 0f),
                        new(-0.7f, -0.5f, -0.5f),
                        new(0.7f, -0.5f, -0.5f),
                        new(0f, -0.5f, 0.7f),
                    ],
                    SubMeshes:
                    [
                        new([0, 1, 2, 0, 2, 3, 0, 3, 1, 1, 3, 2]),
                    ]));
            using var meshCollider = context.Physics.AddMeshCollider(
                meshObject,
                new ModMeshColliderDefinition(mesh.Mesh, Convex: true, IsTrigger: true));
            if (!meshCollider.Convex || !meshCollider.IsTrigger ||
                meshCollider.SharedMesh.Pointer != mesh.Mesh.Pointer)
                throw new InvalidOperationException("MeshCollider state is invalid.");
            try
            {
                meshCollider.Convex = false;
                throw new InvalidOperationException("A live trigger MeshCollider became concave.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("convex", StringComparison.OrdinalIgnoreCase))
            {
            }

            using var body = context.Physics.AddRigidbody(
                gameObject,
                new ModRigidbodyDefinition(
                    Mass: 3.5f,
                    UseGravity: false,
                    IsKinematic: false,
                    LinearDamping: 0.15f,
                    AngularDamping: 0.2f,
                    Constraints: ModRigidbodyConstraints.FreezeAll,
                    CollisionDetection: ModCollisionDetectionMode.Discrete,
                    Interpolation: ModRigidbodyInterpolation.Interpolate));
            if (body.OwnerId != context.Mod.Id || !body.IsAlive || body.Rigidbody.IsNull ||
                !Approximately(body.Mass, 3.5f) || body.UseGravity || body.IsKinematic ||
                !Approximately(body.LinearDamping, 0.15f) ||
                !Approximately(body.AngularDamping, 0.2f) || !body.DetectCollisions ||
                body.Constraints != ModRigidbodyConstraints.FreezeAll ||
                body.CollisionDetection != ModCollisionDetectionMode.Discrete ||
                body.Interpolation != ModRigidbodyInterpolation.Interpolate)
                throw new InvalidOperationException("Rigidbody state is invalid.");
            try
            {
                _ = context.Physics.AddRigidbody(gameObject);
                throw new InvalidOperationException("Duplicate Rigidbody ownership was accepted.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
            }
            body.LinearVelocity = UnityVector3.Zero;
            body.AngularVelocity = UnityVector3.Zero;
            body.AddForce(new UnityVector3(1f, 0f, 0f), ModForceMode.Impulse);
            body.AddTorque(new UnityVector3(0f, 1f, 0f));
            body.AddForceAtPosition(
                new UnityVector3(0f, 0f, 1f), origin, ModForceMode.VelocityChange);
            body.Sleep();
            body.WakeUp();

            context.Physics.SyncTransforms();
            var sphereQuery = context.Physics.CheckSphere(origin, 0.5f);
            var boxQuery = context.Physics.CheckBox(
                origin, new UnityVector3(0.5f, 0.5f, 0.5f), UnityQuaternion.Identity);
            var capsuleQuery = context.Physics.CheckCapsule(
                new UnityVector3(origin.X, origin.Y - 0.5f, origin.Z),
                new UnityVector3(origin.X, origin.Y + 0.5f, origin.Z),
                0.25f);
            var rayQuery = context.Physics.Raycast(
                new UnityVector3(origin.X, origin.Y, origin.Z - 5f),
                new UnityVector3(0f, 0f, 1f),
                out var rayHit,
                maxDistance: 10f);
            if (!sphereQuery || !boxQuery || !capsuleQuery || !rayQuery ||
                rayHit.Collider.IsNull || rayHit.GameObject.Pointer != gameObject.Pointer ||
                rayHit.Distance <= 0f || rayHit.Distance > 10f)
                throw new InvalidOperationException("Physics scene queries did not hit owned colliders.");

            if (context.Physics.Colliders.Count != 4 || context.Physics.Rigidbodies.Count != 1)
                throw new InvalidOperationException("Owner physics registries are incomplete.");
            sphere.Remove();
            if (sphere.IsAlive || context.Physics.Colliders.Count != 3)
                throw new InvalidOperationException("Collider removal did not update ownership.");
            body.Remove();
            if (body.IsAlive || context.Physics.Rigidbodies.Count != 0)
                throw new InvalidOperationException("Rigidbody removal did not update ownership.");

            box.Remove();
            capsule.Remove();
            meshCollider.Remove();
            mesh.Unload();
            if (context.Physics.Colliders.Count != 0 || mesh.IsLoaded)
                throw new InvalidOperationException("Physics or mesh ownership survived cleanup.");
            context.Log.Info(
                "RUNTIME_PHYSICS_PASSED: box=True, sphere=True, capsule=True, mesh=True, " +
                "rigidbody=True, forces=True, queries=True, raycast=True, ownership=True, " +
                "conflict=True, cleanup=True.");
        }
        finally
        {
            try { mesh?.Unload(); } catch { }
            context.Unity.Destroy(meshObject);
            context.Unity.Destroy(gameObject);
        }
    }

    private static bool Approximately(float left, float right) =>
        Math.Abs(left - right) < 0.0001f;

    private static bool Approximately(UnityVector3 left, UnityVector3 right) =>
        Approximately(left.X, right.X) &&
        Approximately(left.Y, right.Y) &&
        Approximately(left.Z, right.Z);
}

public sealed class LocalBusProviderProbeMod : IOFSMod
{
    public void Load(IModContext context)
    {
        context.Messages.Subscribe(
            "ofs.probe.consumer.ack",
            message =>
            {
                if (message.SenderModId != "ofs.sdk.local-bus-consumer" ||
                    message.TargetModId != context.Mod.Id ||
                    !message.Payload.Span.SequenceEqual(new byte[] { 9, 8, 7 }))
                    throw new InvalidOperationException("Targeted local-bus acknowledgement is invalid.");
                context.Log.Info(
                    "LOCAL_BUS_PROVIDER_ACK: sender=True, target=True, payload=True.");
            },
            new ModMessageSubscriptionOptions(
                SenderModId: "ofs.sdk.local-bus-consumer"));
        context.Messages.Publish(
            "ofs.probe.provider.ready",
            new byte[] { 1, 2, 3, 4 },
            new ModMessagePublishOptions(Retain: true));
        context.Events.MainMenuReady += _ =>
        {
            context.Messages.Publish(
                "ofs.probe.provider.isolation",
                new byte[] { 5 });
            context.Messages.Publish(
                "ofs.probe.provider.after",
                new byte[] { 6 });
        };
    }
}

public sealed class DiagnosticsLoadedProbeMod : IOFSMod
{
    public void Load(IModContext context) =>
        context.Log.Info("Diagnostics loaded probe entered successfully.");
}

public sealed class DiagnosticsFailingProbeMod : IOFSMod
{
    public void Load(IModContext context) =>
        throw new InvalidOperationException("expected structured diagnostics load failure");
}

public sealed class LocalBusConsumerProbeMod : IOFSMod
{
    public void Load(IModContext context)
    {
        if (context.Runtime.FrameworkVersion != new Version(0, 1, 0) ||
            context.Runtime.GameVersion != "1.0.3" ||
            context.Runtime.UnityVersion != "6000.3.13f1" ||
            context.Runtime.Il2CppMetadataVersion != 39 ||
            context.Runtime.ProcessArchitecture != "X64" ||
            context.Runtime.PointerSize != 8 ||
            context.Runtime.GameBuildFingerprint !=
                "8370257f4d60c7b8def58be8804d8724d76b95639baf4f199a7d54ef75d6e782" ||
            !context.Runtime.IsVerifiedGameBuild ||
            context.Runtime.IsMainThread)
        {
            throw new InvalidOperationException("Runtime environment load state is invalid.");
        }
        var provider = context.Mods.Get("ofs.sdk.local-bus-provider");
        if (provider is null ||
            provider.Mod.Version != new Version(0, 1, 0) ||
            provider.SdkVersion != new Version(0, 1, 0) ||
            !provider.HasCapability("MESSAGES.LOCAL") ||
            context.Mods.IsLoaded(context.Mod.Id))
        {
            throw new InvalidOperationException("Loaded-mod registry startup state is invalid.");
        }
        var replayed = false;
        var failingHandlerEntered = false;
        context.Messages.Subscribe(
            "ofs.probe.provider.ready",
            message =>
            {
                replayed = message.Retained &&
                    message.SenderModId == "ofs.sdk.local-bus-provider" &&
                    message.Payload.Span.SequenceEqual(new byte[] { 1, 2, 3, 4 });
                if (!replayed)
                    throw new InvalidOperationException("Retained provider message is invalid.");
                context.Log.Info(
                    "LOCAL_BUS_CONSUMER_REPLAY: sender=True, retained=True, payload=True.");
            },
            new ModMessageSubscriptionOptions(
                SenderModId: "ofs.sdk.local-bus-provider",
                ReplayRetained: true));
        context.Messages.Subscribe(
            "ofs.probe.provider.isolation",
            _ =>
            {
                failingHandlerEntered = true;
                throw new InvalidOperationException("expected local-bus handler isolation probe");
            },
            new ModMessageSubscriptionOptions(
                SenderModId: "ofs.sdk.local-bus-provider"));
        context.Messages.Subscribe(
            "ofs.probe.provider.after",
            message =>
            {
                if (!replayed || !failingHandlerEntered ||
                    message.SenderModId != "ofs.sdk.local-bus-provider" ||
                    !message.Payload.Span.SequenceEqual(new byte[] { 6 }))
                    throw new InvalidOperationException("Local-bus isolation sequence is invalid.");
                var self = context.Mods.Get(context.Mod.Id);
                if (self is null ||
                    self.Dependencies.Count != 1 ||
                    !string.Equals(
                        self.Dependencies[0].Id,
                        "ofs.sdk.local-bus-provider",
                        StringComparison.OrdinalIgnoreCase) ||
                    context.Mods.FindByCapability("messages.local").Count != 2 ||
                    !context.Runtime.IsMainThread)
                {
                    throw new InvalidOperationException("Loaded-mod registry final state is invalid.");
                }
                context.Log.Info(
                    "RUNTIME_LOCAL_BUS_PASSED: dependency=True, replay=True, target=True, " +
                    "filter=True, isolation=True, ordering=True, registry=True, environment=True.");
            },
            new ModMessageSubscriptionOptions(
                SenderModId: "ofs.sdk.local-bus-provider"));
        context.Messages.Publish(
            "ofs.probe.consumer.ack",
            new byte[] { 9, 8, 7 },
            new ModMessagePublishOptions(
                TargetModId: "ofs.sdk.local-bus-provider"));
    }
}

internal static class OffsiteFixtureFactory
{
    internal static UnityObject Create(
        IModContext context,
        string contractId,
        UnityObject rewardItem,
        UnityObject? property = null)
    {
        var selectedProperty = property ?? GetFirstProperty(context.UnsafeIl2Cpp);
        var asset = context.Content.OffsiteContracts.Create(contractId);
        var objectClass = context.UnsafeIl2Cpp.FindClass(
            "UnityEngine.CoreModule.dll",
            "UnityEngine",
            "Object");
        var implicitMethod = context.UnsafeIl2Cpp.FindMethodBySignature(
            objectClass,
            "op_Implicit",
            ["UnityEngine.Object"]);
        var alive = context.UnsafeIl2Cpp.Invoke(
            implicitMethod,
            0,
            Il2CppArgument.FromReference(asset.Pointer));
        if (alive == 0 || Marshal.ReadByte(context.UnsafeIl2Cpp.Unbox(alive)) == 0)
            throw new InvalidOperationException(
                "ScriptableObject.CreateInstance produced a destroyed Unity object.");
        context.Content.OffsiteContracts.Update(
            asset,
            new OffsiteContractPatch(
                Property: selectedProperty,
                RequiredLevel: 1,
                DurationHoursMin: 2,
                DurationHoursMax: 4,
                ItemPool: [rewardItem],
                AmountPerHourMin: 1,
                AmountPerHourMax: 3,
                RewardItemCount: 1,
                MatchingProfiles: [EmployeeStatKind.Technique],
                RequiredMinerCount: 1));
        return asset;
    }

    private static UnityObject GetFirstProperty(IUnsafeIl2CppApi api)
    {
        var managerClass = api.FindClass(
            "Assembly-CSharp.dll",
            string.Empty,
            "ScriptableListManager");
        var manager = api.RuntimeInvoke(
            api.FindMethod(managerClass, "get_Instance", 0),
            0,
            0);
        if (manager == 0)
            throw new InvalidOperationException("ScriptableListManager.Instance is unavailable.");
        var properties = api.RuntimeInvoke(
            api.FindMethod(managerClass, "get_AllPropertyConfigs", 0),
            manager,
            0);
        if (properties == 0)
            throw new InvalidOperationException("PropertyConfigSO catalog is unavailable.");
        var listClass = api.GetObjectClass(properties);
        var property = api.Invoke(
            api.FindMethod(listClass, "get_Item", 1),
            properties,
            Il2CppArgument.FromInt32(0));
        return property != 0
            ? new UnityObject(property)
            : throw new InvalidOperationException("PropertyConfigSO catalog is empty.");
    }
}
