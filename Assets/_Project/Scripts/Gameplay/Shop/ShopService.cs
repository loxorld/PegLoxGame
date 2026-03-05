using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopService
{
    public enum ShopOfferType
    {
        Heal,
        OrbUpgrade,
        OrbUpgradeDiscount,
        RecoveryPack,
        VitalityBoost
    }

    public enum ShopOfferRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public sealed class ShopOfferData
    {
        public string OfferId;
        public ShopOfferType Type;
        public int Cost;
        public int Stock;
        public ShopOfferRarity Rarity;
        public int PrimaryValue;
        public bool RequiresMissingHp;
        public bool RequiresUpgradableOrb;
        public bool RequiresAnyOrb;
    }


    public sealed class ShopOptionData
    {
        public ShopOptionData(string label, bool isEnabled, Action onSelect, ShopOfferRarity? rarity = null, bool isExitOption = false)
        {
            Label = label;
            IsEnabled = isEnabled;
            OnSelect = onSelect;
            Rarity = rarity;
            IsExitOption = isExitOption;
        }

        public string Label { get; }
        public bool IsEnabled { get; }
        public Action OnSelect { get; }
        public ShopOfferRarity? Rarity { get; }
        public bool IsExitOption { get; }
    }

    public sealed class PlayerShopState
    {
        public int Coins;
        public int OrbCount;
        public int UpgradableOrbCount;
        public bool HasMissingHp;
    }

    private readonly ShopDomainService domainService = new ShopDomainService();

    public List<ShopOfferData> BuildOrLoadCatalog(
        GameFlowManager flow,
        ShopConfig config,
        RunBalanceConfig balance,
        int stageIndex,
        string shopId,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost,
        bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            List<GameFlowManager.ShopOfferRunData> persisted = flow != null ? flow.GetShopCatalog(shopId) : null;
            if (persisted != null && persisted.Count > 0)
            {
                var restored = new List<ShopOfferData>(persisted.Count);
                for (int i = 0; i < persisted.Count; i++)
                {
                    GameFlowManager.ShopOfferRunData data = persisted[i];
                    restored.Add(new ShopOfferData
                    {
                        OfferId = data.OfferId,
                        Type = data.OfferType,
                        Cost = data.Cost,
                        Stock = data.RemainingStock,
                        Rarity = data.Rarity,
                        PrimaryValue = data.PrimaryValue > 0
                            ? data.PrimaryValue
                            : ResolveDefaultPrimaryValue(data.OfferType, fallbackHealAmount),
                        RequiresMissingHp = data.RequiresMissingHp,
                        RequiresUpgradableOrb = data.RequiresUpgradableOrb,
                        RequiresAnyOrb = data.RequiresAnyOrb
                    });
                }

                return restored;
            }
        }

        List<ShopOfferData> generated = domainService.GenerateOffers(config, balance, stageIndex, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost);
        if (generated == null || generated.Count == 0)
            return new List<ShopOfferData>();

        flow?.SaveShopCatalog(shopId, generated);
        return generated;
    }

    public PlayerShopState BuildPlayerState(GameFlowManager flow, OrbManager orbManager)
    {
        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        int orbCount = ownedOrbs != null ? ownedOrbs.Count : 0;
        int upgradable = GetUpgradableOrbs(ownedOrbs).Count;
        bool missingHp = flow != null && flow.HasSavedPlayerHP && flow.SavedPlayerHP < flow.PlayerMaxHP;

        return new PlayerShopState
        {
            Coins = flow != null ? flow.Coins : 0,
            OrbCount = orbCount,
            UpgradableOrbCount = upgradable,
            HasMissingHp = missingHp
        };
    }

    public bool IsOfferEnabled(PlayerShopState state, ShopOfferData offer, out string reason)
    {
        return domainService.IsOfferEnabled(state, offer, out reason);
    }

    public bool TryPurchaseOffer(GameFlowManager flow, OrbManager orbManager, string shopId, ShopOfferData offer, out string message)
    {
        ShopDomainService.PurchaseResult result = domainService.TryPurchaseAtomic(flow, orbManager, shopId, offer);
        message = result.Message;
        return result.Success;
    }

    private static int ResolveDefaultPrimaryValue(ShopOfferType type, int fallbackHealAmount)
    {
        switch (type)
        {
            case ShopOfferType.Heal:
                return Mathf.Max(1, fallbackHealAmount);
            case ShopOfferType.RecoveryPack:
                return 8;
            case ShopOfferType.VitalityBoost:
                return 4;
            default:
                return 1;
        }
    }

    private static List<OrbInstance> GetUpgradableOrbs(IReadOnlyList<OrbInstance> ownedOrbs)
    {
        var upgradableOrbs = new List<OrbInstance>();
        if (ownedOrbs == null)
            return upgradableOrbs;

        for (int i = 0; i < ownedOrbs.Count; i++)
        {
            OrbInstance orb = ownedOrbs[i];
            if (orb != null && orb.CanLevelUp)
                upgradableOrbs.Add(orb);
        }

        return upgradableOrbs;
    }
}