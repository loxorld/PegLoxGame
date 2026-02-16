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

    public List<ShopOptionData> GetShopOptionsForNode(
        GameFlowManager flow,
        OrbManager orbManager,
        RunBalanceConfig balance,
        int stageIndex,
        string shopId,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost,
        Action<string> refreshAction,
        Action exitAction)
    {
        try
        {
            List<ShopOfferData> catalog = BuildOrLoadCatalog(flow, balance, stageIndex, shopId, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost);
            if (catalog == null || catalog.Count == 0)
                return GetShopOptions(flow, orbManager, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost, refreshAction, exitAction);

            return BuildDynamicShopOptions(flow, orbManager, shopId, catalog, refreshAction, exitAction);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShopService] Falló catálogo dinámico, usando fallback. {ex.Message}");
            return GetShopOptions(flow, orbManager, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost, refreshAction, exitAction);
        }
    }

    public bool TryHeal(GameFlowManager flow, int healCost, int healAmount, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontr GameFlowManager en la escena.";
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

    public bool TryUpgradeOrb(GameFlowManager flow, OrbManager orbManager, int upgradeCost, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontr GameFlowManager en la escena.";
            return false;
        }

        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        if (ownedOrbs == null || ownedOrbs.Count == 0)
        {
            message = "No hay orbes para mejorar.";
            return false;
        }

        var upgradableOrbs = GetUpgradableOrbs(ownedOrbs);
        if (upgradableOrbs.Count == 0)
        {
            message = "Todos los orbes ya estn al mximo.";
            return false;
        }

        if (!flow.SpendCoins(upgradeCost))
        {
            message = "No alcanzan las monedas para mejorar un orbe.";
            return false;
        }

        int randomIndex = UnityEngine.Random.Range(0, upgradableOrbs.Count);
        OrbInstance chosenOrb = upgradableOrbs[randomIndex];
        int previousLevel = chosenOrb.Level;
        chosenOrb.LevelUp();

        message = chosenOrb.Level > previousLevel
            ? $"Mejoraste {chosenOrb.OrbName} a nivel {chosenOrb.Level}."
            : $"{chosenOrb.OrbName} ya est al mximo.";

        return true;
    }

    public List<ShopOptionData> GetShopOptions(
        GameFlowManager flow,
        OrbManager orbManager,
        int healCost,
        int healAmount,
        int upgradeCost,
        Action<string> refreshAction,
        Action exitAction)
    {
        var options = new List<ShopOptionData>();
        int missingHealCoins = Mathf.Max(0, healCost - (flow != null ? flow.Coins : 0));
        bool canAffordHeal = flow != null && flow.Coins >= healCost;
        if (canAffordHeal)
        {
            options.Add(new ShopOptionData(
                $"Curar +{healAmount} HP ({healCost} monedas)",
                true,
                () =>
                {
                    bool success = TryHeal(flow, healCost, healAmount, out string result);
                    if (success)
                        exitAction?.Invoke();
                    else
                        refreshAction?.Invoke(result);
                }));
        }
        else
        {
            options.Add(new ShopOptionData(
                $"Curar +{healAmount} HP (faltan {missingHealCoins} monedas)",
                false,
                () => refreshAction?.Invoke("No alcanzan las monedas para curar.")));
        }

        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        bool hasOrbs = ownedOrbs != null && ownedOrbs.Count > 0;
        List<OrbInstance> upgradableOrbs = hasOrbs ? GetUpgradableOrbs(ownedOrbs) : new List<OrbInstance>();

        if (!hasOrbs)
        {
            options.Add(new ShopOptionData(
                "Mejora de Orbe (sin orbes disponibles)",
                false,
                () => refreshAction?.Invoke("No hay orbes para mejorar.")));
        }
        else if (upgradableOrbs.Count == 0)
        {
            options.Add(new ShopOptionData(
                "Mejora de Orbe (orbes al mximo)",
                false,
                () => refreshAction?.Invoke("Todos los orbes ya estn al mximo.")));
        }
        else
        {
            bool canAffordUpgrade = flow != null && flow.Coins >= upgradeCost;
            int missingUpgradeCoins = Mathf.Max(0, upgradeCost - (flow != null ? flow.Coins : 0));
            if (canAffordUpgrade)
            {
                options.Add(new ShopOptionData(
                    $"Mejora de Orbe (+1 nivel, {upgradeCost} monedas)",
                    true,
                    () =>
                    {
                        bool success = TryUpgradeOrb(flow, orbManager, upgradeCost, out string result);
                        if (success)
                            exitAction?.Invoke();
                        else
                            refreshAction?.Invoke(result);
                    }));
            }
            else
            {
                options.Add(new ShopOptionData(
                    $"Mejora de Orbe (+1 nivel, faltan {missingUpgradeCoins} monedas)",
                    false,
                    () => refreshAction?.Invoke("No alcanzan las monedas para mejorar un orbe.")));
            }
        }

        options.Add(new ShopOptionData("Salir", true, exitAction));
        return options;
    }

    private List<ShopOptionData> BuildDynamicShopOptions(
        GameFlowManager flow,
        OrbManager orbManager,
        string shopId,
        IReadOnlyList<ShopOfferData> offers,
        Action<string> refreshAction,
        Action exitAction)
    {
        var options = new List<ShopOptionData>();
        for (int i = 0; i < offers.Count; i++)
        {
            ShopOfferData offer = offers[i];
            if (offer == null)
                continue;

            bool enabled = IsOfferEnabled(flow, orbManager, offer, out string disableReason);
            string label = BuildOfferLabel(offer, enabled, disableReason);
            options.Add(new ShopOptionData(label, enabled, () => ExecuteOffer(flow, orbManager, shopId, offer, refreshAction, exitAction), offer.Rarity));
        }

        options.Add(new ShopOptionData("Salir", true, exitAction, null, true));
        return options;
    }

    private void ExecuteOffer(
        GameFlowManager flow,
        OrbManager orbManager,
        string shopId,
        ShopOfferData offer,
        Action<string> refreshAction,
        Action exitAction)
    {
        if (!IsOfferEnabled(flow, orbManager, offer, out string reason))
        {
            refreshAction?.Invoke(reason);
            return;
        }

        bool success;
        string result;
        switch (offer.Type)
        {
            case ShopOfferType.Heal:
                success = TryHeal(flow, offer.Cost, offer.PrimaryValue, out result);
                break;
            case ShopOfferType.OrbUpgrade:
            case ShopOfferType.OrbUpgradeDiscount:
                success = TryUpgradeOrb(flow, orbManager, offer.Cost, out result);
                break;
            case ShopOfferType.CoinCache:
                success = TryCoinCache(flow, offer.Cost, offer.PrimaryValue, out result);
                break;
            case ShopOfferType.VitalityBoost:
                success = TryVitalityBoost(flow, offer.Cost, offer.PrimaryValue, out result);
                break;
            default:
                success = false;
                result = "Oferta no soportada.";
                break;
        }

        if (!success)
        {
            refreshAction?.Invoke(result);
            return;
        }

        if (!flow.TryConsumeShopOffer(shopId, offer.OfferId))
        {
            refreshAction?.Invoke("Sin stock para esta oferta.");
            return;
        }

        flow.SaveRun();
        exitAction?.Invoke();
    }

    private static bool TryCoinCache(GameFlowManager flow, int cost, int reward, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontr GameFlowManager en la escena.";
            return false;
        }

        if (!flow.SpendCoins(cost))
        {
            message = "No alcanzan las monedas para esta oferta.";
            return false;
        }

        flow.AddCoins(reward);
        message = $"Recibiste +{reward} monedas.";
        return true;
    }

    private static bool TryVitalityBoost(GameFlowManager flow, int cost, int hpBonus, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontr GameFlowManager en la escena.";
            return false;
        }

        if (!flow.SpendCoins(cost))
        {
            message = "No alcanzan las monedas para potenciarte.";
            return false;
        }

        int nextMaxHp = flow.PlayerMaxHP + Mathf.Max(1, hpBonus);
        flow.SavePlayerMaxHP(nextMaxHp);
        flow.ModifySavedHP(Mathf.Max(1, hpBonus));
        message = $"Tu vida mxima sube +{hpBonus}.";
        return true;
    }

    private List<ShopOfferData> BuildOrLoadCatalog(
        GameFlowManager flow,
        RunBalanceConfig balance,
        int stageIndex,
        string shopId,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost)
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

        List<ShopOfferData> generated = GenerateOffersByVisit(balance, stageIndex, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost);
        if (generated == null || generated.Count == 0)
            return null;

        flow?.SaveShopCatalog(shopId, generated);
        return generated;
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

    private static bool IsOfferEnabled(GameFlowManager flow, OrbManager orbManager, ShopOfferData offer, out string reason)
    {
        reason = null;
        if (flow == null || offer == null)
        {
            reason = "Oferta invlida.";
            return false;
        }

        if (offer.Stock <= 0)
        {
            reason = "Sin stock.";
            return false;
        }

        if (flow.Coins < offer.Cost)
        {
            reason = $"Faltan {offer.Cost - flow.Coins} monedas.";
            return false;
        }

        bool hasMissingHp = flow.HasSavedPlayerHP && flow.SavedPlayerHP < flow.PlayerMaxHP;
        if (offer.RequiresMissingHp && !hasMissingHp)
        {
            reason = "No necesitas curarte.";
            return false;
        }

        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        bool hasAnyOrb = ownedOrbs != null && ownedOrbs.Count > 0;
        if (offer.RequiresAnyOrb && !hasAnyOrb)
        {
            reason = "No tienes orbes.";
            return false;
        }

        if (offer.RequiresUpgradableOrb && GetUpgradableOrbs(ownedOrbs).Count == 0)
        {
            reason = "No hay orbes para mejorar.";
            return false;
        }

        return true;
    }

    private static string BuildOfferLabel(ShopOfferData offer, bool enabled, string disabledReason)
    {
        string text;
        switch (offer.Type)
        {
            case ShopOfferType.Heal:
                text = $"Curar +{offer.PrimaryValue} HP ({offer.Cost} monedas, stock {offer.Stock})";
                break;
            case ShopOfferType.OrbUpgrade:
                text = $"Mejorar Orbe aleatorio ({offer.Cost} monedas, stock {offer.Stock})";
                break;
            case ShopOfferType.OrbUpgradeDiscount:
                text = $"Mejorar Orbe con descuento ({offer.Cost} monedas, stock {offer.Stock})";
                break;
            case ShopOfferType.CoinCache:
                text = $"Cofre temporal (+{offer.PrimaryValue} monedas por {offer.Cost}, stock {offer.Stock})";
                break;
            case ShopOfferType.VitalityBoost:
                text = $"Tónico Vital (+{offer.PrimaryValue} HP máx, {offer.Cost} monedas, stock {offer.Stock})";
                break;
            default:
                text = $"Oferta desconocida ({offer.Cost} monedas)";
                break;
        }


        if (!enabled && !string.IsNullOrWhiteSpace(disabledReason))
            text += $" - {disabledReason}";

        return text;
    }

    private static List<ShopOfferData> GenerateOffersByVisit(
        RunBalanceConfig balance,
        int stageIndex,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost)
    {
        int offerCount = UnityEngine.Random.Range(3, 7);
        int healCost = balance != null ? balance.GetShopHealCost(stageIndex, fallbackHealCost) : fallbackHealCost;
        int healAmount = balance != null ? balance.GetShopHealAmount(stageIndex, fallbackHealAmount) : fallbackHealAmount;
        int upgradeCost = balance != null ? balance.GetShopOrbUpgradeCost(stageIndex, fallbackUpgradeCost) : fallbackUpgradeCost;

        var templates = new List<ShopOfferData>
        {
            new ShopOfferData { OfferId = "heal_common", Type = ShopOfferType.Heal, Cost = healCost, Stock = 2, Rarity = ShopOfferRarity.Common, PrimaryValue = healAmount, RequiresMissingHp = true },
            new ShopOfferData { OfferId = "heal_rare", Type = ShopOfferType.Heal, Cost = Mathf.Max(0, healCost + 4), Stock = 1, Rarity = ShopOfferRarity.Rare, PrimaryValue = healAmount + 4, RequiresMissingHp = true },
            new ShopOfferData { OfferId = "upgrade_standard", Type = ShopOfferType.OrbUpgrade, Cost = upgradeCost, Stock = 1, Rarity = ShopOfferRarity.Common, PrimaryValue = 1, RequiresUpgradableOrb = true, RequiresAnyOrb = true },
            new ShopOfferData { OfferId = "upgrade_discount", Type = ShopOfferType.OrbUpgradeDiscount, Cost = Mathf.Max(0, upgradeCost - 4), Stock = 1, Rarity = ShopOfferRarity.Epic, PrimaryValue = 1, RequiresUpgradableOrb = true, RequiresAnyOrb = true },
            new ShopOfferData { OfferId = "coin_cache", Type = ShopOfferType.CoinCache, Cost = Mathf.Max(0, healCost - 2), Stock = 1, Rarity = ShopOfferRarity.Rare, PrimaryValue = Mathf.Max(5, healCost + 5) },
            new ShopOfferData { OfferId = "vitality_boost", Type = ShopOfferType.VitalityBoost, Cost = Mathf.Max(0, upgradeCost - 3), Stock = 1, Rarity = ShopOfferRarity.Legendary, PrimaryValue = 3 + stageIndex }
        };

        Shuffle(templates);
        var generated = new List<ShopOfferData>();
        for (int i = 0; i < templates.Count && generated.Count < offerCount; i++)
            generated.Add(templates[i]);

        return generated;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        if (list == null)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[swapIndex];
            list[swapIndex] = tmp;
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