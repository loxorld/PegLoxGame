using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PegLox.Gameplay.Shop;

public class ShopDomainServiceTests
{
    private const string ShopId = "test_shop";

    [TearDown]
    public void TearDown()
    {
        if (GameFlowManager.Instance != null)
            Object.DestroyImmediate(GameFlowManager.Instance.gameObject);

        if (OrbManager.Instance != null)
            Object.DestroyImmediate(OrbManager.Instance.gameObject);
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
        ShopDomainService service = new ShopDomainService
        {
            SimulateIntermediateFailureForTests = _ => true
        };

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

    private static GameFlowManager CreateFlowWithCoins(int coins)
    {
        GameObject go = new GameObject("FlowTest");
        GameFlowManager flow = go.AddComponent<GameFlowManager>();
        if (coins > 0)
            flow.AddCoins(coins);

        return flow;
    }

    private static ShopService.ShopOfferData BuildOffer(int stock, int cost, int primaryValue = 1)
    {
        return new ShopService.ShopOfferData
        {
            OfferId = "offer_1",
            Type = ShopService.ShopOfferType.VitalityBoost,
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
}
