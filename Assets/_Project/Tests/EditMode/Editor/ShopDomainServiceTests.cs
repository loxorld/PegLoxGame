using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ShopDomainServiceTests
{
    private const string ShopId = "test_shop";
    private const string BasicOrbAssetPath = "Assets/_Project/Data/Gameplay/Orbs/Orb_Basic.asset";
    private readonly List<Object> createdScriptableObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < createdScriptableObjects.Count; i++)
        {
            if (createdScriptableObjects[i] != null)
                Object.DestroyImmediate(createdScriptableObjects[i]);
        }

        createdScriptableObjects.Clear();

        if (GameFlowManager.Instance != null)
            Object.DestroyImmediate(GameFlowManager.Instance.gameObject);

        if (OrbManager.Instance != null)
            Object.DestroyImmediate(OrbManager.Instance.gameObject);

        if (RelicManager.Instance != null)
            Object.DestroyImmediate(RelicManager.Instance.gameObject);
    }

    [Test]
    public void TryPurchaseAtomic_FailsWhenStockIsExhausted()
    {
        ShopDomainService service = new ShopDomainService();
        GameFlowManager flow = CreateFlowWithCoins(20);
        ShopService.ShopOfferData offer = BuildOffer(stock: 0, cost: 5);
        flow.SaveShopCatalog(ShopId, new List<ShopService.ShopOfferData> { offer });

        ShopDomainService.PurchaseResult result = service.TryPurchaseAtomic(flow, null, ShopId, offer);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Sin stock para esta oferta.", result.Message);
    }

    [Test]
    public void TryPurchaseAtomic_FailsWhenFundsAreInsufficient_AndRestoresStock()
    {
        ShopDomainService service = new ShopDomainService();
        GameFlowManager flow = CreateFlowWithCoins(1);
        ShopService.ShopOfferData offer = BuildOffer(stock: 1, cost: 5);
        flow.SaveShopCatalog(ShopId, new List<ShopService.ShopOfferData> { offer });

        ShopDomainService.PurchaseResult result = service.TryPurchaseAtomic(flow, null, ShopId, offer);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(1, GetRemainingStock(flow, offer.OfferId));
        Assert.AreEqual(1, flow.Coins);
    }

    [Test]
    public void TryPurchaseAtomic_Succeeds_WhenOfferCanBePurchased()
    {
        ShopDomainService service = new ShopDomainService();
        GameFlowManager flow = CreateFlowWithCoins(10);
        ShopService.ShopOfferData offer = BuildOffer(stock: 1, cost: 4, primaryValue: 2);
        flow.SaveShopCatalog(ShopId, new List<ShopService.ShopOfferData> { offer });

        ShopDomainService.PurchaseResult result = service.TryPurchaseAtomic(flow, null, ShopId, offer);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(6, flow.Coins);
        Assert.AreEqual(0, GetRemainingStock(flow, offer.OfferId));
        Assert.Greater(flow.PlayerMaxHP, 0);
    }

    [Test]
    public void TryPurchaseAtomic_RollsBackFlowAndStock_OnIntermediateFailure()
    {
        ShopDomainService service = new ShopDomainService();
        typeof(ShopDomainService)
            .GetField("SimulateIntermediateFailureForTests", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(service, new System.Func<ShopService.ShopOfferData, bool>(_ => true));

        GameFlowManager flow = CreateFlowWithCoins(10);
        int initialCoins = flow.Coins;
        int initialMaxHp = flow.PlayerMaxHP;
        ShopService.ShopOfferData offer = BuildOffer(stock: 1, cost: 4, primaryValue: 3);
        flow.SaveShopCatalog(ShopId, new List<ShopService.ShopOfferData> { offer });

        ShopDomainService.PurchaseResult result = service.TryPurchaseAtomic(flow, null, ShopId, offer);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(initialCoins, flow.Coins);
        Assert.AreEqual(initialMaxHp, flow.PlayerMaxHP);
        Assert.AreEqual(1, GetRemainingStock(flow, offer.OfferId));
    }

    [Test]
    public void TryPurchaseAtomic_RollsBackVitalityBoost_WhenSaveRunThrows()
    {
        ShopDomainService service = new ShopDomainService();
        GameFlowManager flow = CreateFlowWithCoins(10);
        flow.InjectRunPersistenceService(new RunPersistenceService(new ThrowingGateway()));

        int initialCoins = flow.Coins;
        int initialMaxHp = flow.PlayerMaxHP;
        ShopService.ShopOfferData offer = BuildOffer(stock: 1, cost: 4, primaryValue: 3);
        flow.SaveShopCatalog(ShopId, new List<ShopService.ShopOfferData> { offer });

        ShopDomainService.PurchaseResult result = service.TryPurchaseAtomic(flow, null, ShopId, offer);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Ocurrió un error al procesar la compra.", result.Message);
        Assert.AreEqual(initialCoins, flow.Coins);
        Assert.AreEqual(initialMaxHp, flow.PlayerMaxHP);
        Assert.IsFalse(flow.HasSavedPlayerHP);
        Assert.AreEqual(0, flow.SavedPlayerHP);
        Assert.AreEqual(1, GetRemainingStock(flow, offer.OfferId));
    }

    [Test]
    public void TryPurchaseAtomic_RollsBackOrbUpgrade_WhenSaveRunThrows()
    {
        OrbData basicOrb = AssetDatabase.LoadAssetAtPath<OrbData>(BasicOrbAssetPath);
        Assert.NotNull(basicOrb);

        ShopDomainService service = new ShopDomainService();
        GameFlowManager flow = CreateFlowWithCoins(10, out OrbManager orbManager);
        flow.InjectRunPersistenceService(new RunPersistenceService(new ThrowingGateway()));

        Assert.NotNull(orbManager);
        orbManager.SetCurrentOrb(basicOrb);

        OrbInstance currentOrb = orbManager.CurrentOrb;
        Assert.NotNull(currentOrb);
        int initialLevel = currentOrb.Level;

        ShopService.ShopOfferData offer = BuildOffer(
            stock: 1,
            cost: 4,
            primaryValue: 1,
            type: ShopService.ShopOfferType.OrbUpgrade);
        flow.SaveShopCatalog(ShopId, new List<ShopService.ShopOfferData> { offer });

        ShopDomainService.PurchaseResult result = service.TryPurchaseAtomic(flow, orbManager, ShopId, offer);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Ocurrió un error al procesar la compra.", result.Message);
        Assert.AreEqual(initialLevel, orbManager.CurrentOrb.Level);
        Assert.AreEqual(10, flow.Coins);
        Assert.AreEqual(1, GetRemainingStock(flow, offer.OfferId));
    }

    [Test]
    public void GenerateOffers_ReturnsUniqueOfferFamilies_WhenDuplicatesAreDisabled()
    {
        ShopDomainService service = new ShopDomainService();
        ShopConfig config = CreateShopConfig(
            minOffers: 2,
            maxOffers: 2,
            offers: new[]
            {
                CreateOfferTemplate("heal", ShopService.ShopOfferType.Heal, ShopService.ShopOfferRarity.Common, baseCost: 10, baseStock: 2, primaryValue: 4, requiresMissingHp: true),
                CreateOfferTemplate("upgrade", ShopService.ShopOfferType.OrbUpgrade, ShopService.ShopOfferRarity.Common, baseCost: 15, baseStock: 1, primaryValue: 1, requiresUpgradableOrb: true, requiresAnyOrb: true)
            });

        List<ShopService.ShopOfferData> generated = service.GenerateOffers(config, null, 0, 10, 4, 15);

        Assert.AreEqual(2, generated.Count);
        CollectionAssert.AreEquivalent(new[] { "heal", "upgrade" }, ExtractBaseOfferIds(generated));
        Assert.AreEqual(3, SumStocks(generated));
    }

    [Test]
    public void GenerateOffers_SplitsTotalStockAcrossDuplicateListings_WhenDuplicatesAreAllowed()
    {
        ShopDomainService service = new ShopDomainService();
        ShopConfig config = CreateShopConfig(
            minOffers: 2,
            maxOffers: 2,
            offers: new[]
            {
                CreateOfferTemplate("shared", ShopService.ShopOfferType.Heal, ShopService.ShopOfferRarity.Common, baseCost: 10, baseStock: 4, primaryValue: 4, requiresMissingHp: true)
            });

        List<ShopService.ShopOfferData> generated = service.GenerateOffers(config, null, 0, 10, 4, 15, allowDuplicateOffersWhenStockAvailable: true);

        Assert.AreEqual(2, generated.Count);
        CollectionAssert.AreEquivalent(new[] { "shared", "shared" }, ExtractBaseOfferIds(generated));
        CollectionAssert.AreEquivalent(new[] { 2, 2 }, generated.ConvertAll(item => item.Stock));
        Assert.AreEqual(4, SumStocks(generated));
    }

    [Test]
    public void GenerateOffers_UsesOneStockPerListing_WhenDuplicateCountMatchesTemplateStock()
    {
        ShopDomainService service = new ShopDomainService();
        ShopConfig config = CreateShopConfig(
            minOffers: 3,
            maxOffers: 3,
            offers: new[]
            {
                CreateOfferTemplate("shared", ShopService.ShopOfferType.Heal, ShopService.ShopOfferRarity.Common, baseCost: 10, baseStock: 3, primaryValue: 4, requiresMissingHp: true)
            });

        List<ShopService.ShopOfferData> generated = service.GenerateOffers(config, null, 0, 10, 4, 15, allowDuplicateOffersWhenStockAvailable: true);

        Assert.AreEqual(3, generated.Count);
        CollectionAssert.AreEquivalent(new[] { "shared", "shared", "shared" }, ExtractBaseOfferIds(generated));
        CollectionAssert.AreEquivalent(new[] { 1, 1, 1 }, generated.ConvertAll(item => item.Stock));
        Assert.AreEqual(3, SumStocks(generated));
    }

    [Test]
    public void GenerateOffers_AppliesRarityPriceMultiplier()
    {
        ShopDomainService service = new ShopDomainService();
        ShopConfig config = CreateShopConfig(
            minOffers: 1,
            maxOffers: 1,
            offers: new[]
            {
                CreateOfferTemplate("rareHeal", ShopService.ShopOfferType.Heal, ShopService.ShopOfferRarity.Rare, baseCost: 10, baseStock: 1, primaryValue: 4, requiresMissingHp: true)
            },
            rarityWeights: new[]
            {
                CreateRarityWeight(ShopService.ShopOfferRarity.Rare, 1f)
            });

        List<ShopService.ShopOfferData> generated = service.GenerateOffers(config, null, 0, 10, 4, 15);

        Assert.AreEqual(1, generated.Count);
        Assert.AreEqual(12, generated[0].Cost);
        Assert.AreEqual(ShopService.ShopOfferRarity.Rare, generated[0].Rarity);
    }

    private static GameFlowManager CreateFlowWithCoins(int coins)
    {
        return CreateFlowWithCoins(coins, out _);
    }

    private static GameFlowManager CreateFlowWithCoins(int coins, out OrbManager orbManager)
    {
        GameObject go = new GameObject("FlowTest");
        GameFlowManager flow = go.AddComponent<GameFlowManager>();
        orbManager = new GameObject("OrbManagerTest").AddComponent<OrbManager>();
        RelicManager relicManager = new GameObject("RelicManagerTest").AddComponent<RelicManager>();
        flow.InjectDependencies(null, orbManager, relicManager);

        if (coins > 0)
            flow.AddCoins(coins);

        return flow;
    }

    private static ShopService.ShopOfferData BuildOffer(
        int stock,
        int cost,
        int primaryValue = 1,
        ShopService.ShopOfferType type = ShopService.ShopOfferType.VitalityBoost)
    {
        return new ShopService.ShopOfferData
        {
            OfferId = "offer_1",
            Type = type,
            Cost = cost,
            Stock = stock,
            PrimaryValue = primaryValue,
            Rarity = ShopService.ShopOfferRarity.Common
        };
    }

    private static int GetRemainingStock(GameFlowManager flow, string offerId)
    {
        List<GameFlowManager.ShopOfferRunData> catalog = flow.GetShopCatalog(ShopId);
        Assert.NotNull(catalog);

        GameFlowManager.ShopOfferRunData found = catalog.Find(item => item.OfferId == offerId);
        Assert.NotNull(found);
        return found.RemainingStock;
    }

    private ShopConfig CreateShopConfig(
        int minOffers,
        int maxOffers,
        ShopConfig.OfferTemplate[] offers,
        ShopConfig.RarityWeight[] rarityWeights = null)
    {
        ShopConfig config = ScriptableObject.CreateInstance<ShopConfig>();
        createdScriptableObjects.Add(config);

        SetPrivateField(config, "offerTable", new List<ShopConfig.OfferTemplate>(offers));
        SetPrivateField(
            config,
            "rarityWeights",
            rarityWeights != null
                ? new List<ShopConfig.RarityWeight>(rarityWeights)
                : new List<ShopConfig.RarityWeight> { CreateRarityWeight(ShopService.ShopOfferRarity.Common, 1f) });
        SetPrivateField(config, "minOffersPerVisit", minOffers);
        SetPrivateField(config, "maxOffersPerVisit", maxOffers);
        return config;
    }

    private static ShopConfig.OfferTemplate CreateOfferTemplate(
        string offerId,
        ShopService.ShopOfferType type,
        ShopService.ShopOfferRarity rarity,
        int baseCost,
        int baseStock,
        int primaryValue,
        bool requiresMissingHp = false,
        bool requiresUpgradableOrb = false,
        bool requiresAnyOrb = false)
    {
        var template = new ShopConfig.OfferTemplate();
        SetPrivateField(template, "offerId", offerId);
        SetPrivateField(template, "type", type);
        SetPrivateField(template, "rarity", rarity);
        SetPrivateField(template, "baseCost", baseCost);
        SetPrivateField(template, "baseStock", baseStock);
        SetPrivateField(template, "primaryValue", primaryValue);
        SetPrivateField(template, "requiresMissingHp", requiresMissingHp);
        SetPrivateField(template, "requiresUpgradableOrb", requiresUpgradableOrb);
        SetPrivateField(template, "requiresAnyOrb", requiresAnyOrb);
        return template;
    }

    private static ShopConfig.RarityWeight CreateRarityWeight(ShopService.ShopOfferRarity rarity, float weight)
    {
        var rarityWeight = new ShopConfig.RarityWeight();
        SetPrivateField(rarityWeight, "rarity", rarity);
        SetPrivateField(rarityWeight, "weight", weight);
        return rarityWeight;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static List<string> ExtractBaseOfferIds(List<ShopService.ShopOfferData> offers)
    {
        var ids = new List<string>(offers.Count);
        for (int i = 0; i < offers.Count; i++)
            ids.Add(ExtractBaseOfferId(offers[i].OfferId));

        return ids;
    }

    private static string ExtractBaseOfferId(string generatedOfferId)
    {
        int suffixIndex = generatedOfferId.LastIndexOf('_');
        return suffixIndex > 0 ? generatedOfferId.Substring(0, suffixIndex) : generatedOfferId;
    }

    private static int SumStocks(List<ShopService.ShopOfferData> offers)
    {
        int total = 0;
        for (int i = 0; i < offers.Count; i++)
            total += offers[i].Stock;

        return total;
    }

    private sealed class ThrowingGateway : IRunSaveGateway
    {
        public void Save(RunSaveData data)
        {
            throw new System.InvalidOperationException("forced save failure");
        }

        public bool TryLoad(out RunSaveData data)
        {
            data = null;
            return false;
        }
    }
}
