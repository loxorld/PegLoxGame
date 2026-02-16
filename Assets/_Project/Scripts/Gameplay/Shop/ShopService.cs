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
        CoinCache,
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
                        PrimaryValue = ResolveDefaultPrimaryValue(data.OfferType, fallbackHealAmount),
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
        PlayerShopState currentState = BuildPlayerState(flow, orbManager);
        if (!domainService.IsOfferEnabled(currentState, offer, out string reason))
        {
            message = reason;
            return false;
        }

        ShopDomainService.PurchaseResult result = domainService.ExecutePurchase(flow, orbManager, offer);
        if (!result.Success)
        {
            message = result.Message;
            return false;
        }

        if (!flow.TryConsumeShopOffer(shopId, offer.OfferId))
        {
            message = "Sin stock para esta oferta.";
            return false;
        }

        flow.SaveRun();
        message = result.Message;
        return true;
    }

    private static int ResolveDefaultPrimaryValue(ShopOfferType type, int fallbackHealAmount)
    {
        switch (type)
        {
            case ShopOfferType.Heal:
                return Mathf.Max(1, fallbackHealAmount);
            case ShopOfferType.CoinCache:
                return 10;
            case ShopOfferType.VitalityBoost:
                return 4;
            default:
                return 1;
        }
    }

    public static bool TryHeal(GameFlowManager flow, int healCost, int healAmount, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontró GameFlowManager en la escena.";
            return false;
        }

        if (!flow.SpendCoins(healCost))
        {
            message = "No alcanzan las monedas para curar.";
            return false;
        }

        flow.ModifySavedHP(healAmount);
        message = $"Te curaste +{healAmount} HP.";
        return true;
    }

    public static bool TryUpgradeOrb(GameFlowManager flow, OrbManager orbManager, int upgradeCost, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontró GameFlowManager en la escena.";
            return false;
        }

        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        if (ownedOrbs == null || ownedOrbs.Count == 0)
        {
            message = "No hay orbes para mejorar.";
            return false;
        }

        List<OrbInstance> upgradableOrbs = GetUpgradableOrbs(ownedOrbs);
        if (upgradableOrbs.Count == 0)
        {
            message = "Todos los orbes ya están al máximo.";
            return false;
        }

        if (!flow.SpendCoins(upgradeCost))
        {
            message = "No alcanzan las monedas para mejorar un orbe.";
            return false;
        }

        OrbInstance chosenOrb = upgradableOrbs[UnityEngine.Random.Range(0, upgradableOrbs.Count)];
        int prev = chosenOrb.Level;
        chosenOrb.LevelUp();
        message = chosenOrb.Level > prev
            ? $"Mejoraste {chosenOrb.OrbName} a nivel {chosenOrb.Level}."
            : $"{chosenOrb.OrbName} ya está al máximo.";
        return true;
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