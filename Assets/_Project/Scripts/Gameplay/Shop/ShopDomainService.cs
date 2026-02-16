using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShopDomainService
{
    public sealed class PurchaseResult
    {
        public bool Success;
        public string Message;
    }

    public List<ShopService.ShopOfferData> GenerateOffers(
        ShopConfig config,
        RunBalanceConfig balance,
        int stageIndex,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost)
    {
        if (config == null || config.OfferTable == null || config.OfferTable.Count == 0)
            return GenerateLegacyOffers(balance, stageIndex, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost);

        int amount = UnityEngine.Random.Range(config.MinOffersPerVisit, config.MaxOffersPerVisit + 1);
        var generated = new List<ShopService.ShopOfferData>(amount);
        for (int i = 0; i < amount; i++)
        {
            ShopConfig.OfferTemplate template = PickTemplateByRarity(config);
            if (template == null)
                continue;

            int baseCost = ResolveTemplateCost(template, balance, stageIndex, fallbackHealCost, fallbackUpgradeCost);
            int adjustedCost = Mathf.Max(0, Mathf.RoundToInt(baseCost * config.GetPriceMultiplier(template.Rarity)));
            generated.Add(new ShopService.ShopOfferData
            {
                OfferId = BuildOfferId(template.OfferId, i),
                Type = template.Type,
                Cost = adjustedCost,
                Stock = Mathf.Max(1, template.BaseStock),
                Rarity = template.Rarity,
                PrimaryValue = ResolvePrimaryValue(template, fallbackHealAmount),
                RequiresMissingHp = template.RequiresMissingHp,
                RequiresUpgradableOrb = template.RequiresUpgradableOrb,
                RequiresAnyOrb = template.RequiresAnyOrb
            });
        }

        return generated;
    }

    public bool IsOfferEnabled(ShopService.PlayerShopState state, ShopService.ShopOfferData offer, out string reason)
    {
        reason = null;
        if (state == null || offer == null)
        {
            reason = "Oferta inválida.";
            return false;
        }

        if (offer.Stock <= 0)
        {
            reason = "Sin stock.";
            return false;
        }

        if (state.Coins < offer.Cost)
        {
            reason = $"Faltan {offer.Cost - state.Coins} monedas.";
            return false;
        }

        if (offer.RequiresMissingHp && !state.HasMissingHp)
        {
            reason = "No necesitas curarte.";
            return false;
        }

        if (offer.RequiresAnyOrb && state.OrbCount <= 0)
        {
            reason = "No tienes orbes.";
            return false;
        }

        if (offer.RequiresUpgradableOrb && state.UpgradableOrbCount <= 0)
        {
            reason = "No hay orbes para mejorar.";
            return false;
        }

        return true;
    }

    public PurchaseResult ExecutePurchase(GameFlowManager flow, OrbManager orbManager, ShopService.ShopOfferData offer)
    {
        if (offer == null)
            return new PurchaseResult { Success = false, Message = "Oferta inválida." };

        switch (offer.Type)
        {
            case ShopService.ShopOfferType.Heal:
                return RunResult(ShopService.TryHeal(flow, offer.Cost, offer.PrimaryValue, out string heal), heal);
            case ShopService.ShopOfferType.OrbUpgrade:
            case ShopService.ShopOfferType.OrbUpgradeDiscount:
                return RunResult(ShopService.TryUpgradeOrb(flow, orbManager, offer.Cost, out string upgrade), upgrade);
            case ShopService.ShopOfferType.RecoveryPack:
                return RunResult(TryRecoveryPack(flow, offer.Cost, offer.PrimaryValue, out string pack), pack);
            case ShopService.ShopOfferType.VitalityBoost:
                return RunResult(TryVitalityBoost(flow, offer.Cost, offer.PrimaryValue, out string vitality), vitality);
            default:
                return new PurchaseResult { Success = false, Message = "Oferta no soportada." };
        }
    }

    private static PurchaseResult RunResult(bool success, string message)
    {
        return new PurchaseResult { Success = success, Message = message };
    }

    private static string BuildOfferId(string baseId, int index)
    {
        string safeId = string.IsNullOrWhiteSpace(baseId) ? "offer" : baseId.Trim();
        return $"{safeId}_{index}";
    }

    private static int ResolvePrimaryValue(ShopConfig.OfferTemplate template, int fallbackHealAmount)
    {
        if (template.Type == ShopService.ShopOfferType.Heal)
            return Mathf.Max(1, template.PrimaryValue > 0 ? template.PrimaryValue : fallbackHealAmount);

        return Mathf.Max(1, template.PrimaryValue);
    }

    private static int ResolveTemplateCost(ShopConfig.OfferTemplate template, RunBalanceConfig balance, int stageIndex, int fallbackHealCost, int fallbackUpgradeCost)
    {
        switch (template.Type)
        {
            case ShopService.ShopOfferType.Heal:
                return balance != null ? balance.GetShopHealCost(stageIndex, template.BaseCost) : template.BaseCost;
            case ShopService.ShopOfferType.OrbUpgrade:
            case ShopService.ShopOfferType.OrbUpgradeDiscount:
                return balance != null ? balance.GetShopOrbUpgradeCost(stageIndex, template.BaseCost) : template.BaseCost;
            default:
                if (template.BaseCost > 0)
                    return template.BaseCost;

                return Mathf.Max(fallbackHealCost, fallbackUpgradeCost);
        }
    }

    private static ShopConfig.OfferTemplate PickTemplateByRarity(ShopConfig config)
    {
        List<ShopConfig.OfferTemplate> offers = new List<ShopConfig.OfferTemplate>(config.OfferTable);
        if (offers.Count == 0)
            return null;

        float total = 0f;
        for (int i = 0; i < config.RarityWeights.Count; i++)
            total += config.RarityWeights[i].Weight;

        if (total <= 0f)
            return offers[UnityEngine.Random.Range(0, offers.Count)];

        float roll = UnityEngine.Random.Range(0f, total);
        ShopService.ShopOfferRarity picked = ShopService.ShopOfferRarity.Common;
        float cumulative = 0f;
        for (int i = 0; i < config.RarityWeights.Count; i++)
        {
            ShopConfig.RarityWeight weight = config.RarityWeights[i];
            cumulative += weight.Weight;
            if (roll <= cumulative)
            {
                picked = weight.Rarity;
                break;
            }
        }

        var filtered = new List<ShopConfig.OfferTemplate>();
        for (int i = 0; i < offers.Count; i++)
        {
            if (offers[i].Rarity == picked)
                filtered.Add(offers[i]);
        }

        if (filtered.Count == 0)
            filtered = offers;

        return filtered[UnityEngine.Random.Range(0, filtered.Count)];
    }

    private static bool TryRecoveryPack(GameFlowManager flow, int cost, int healAmount, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontró GameFlowManager en la escena.";
            return false;
        }

        if (!flow.SpendCoins(cost))
        {
            message = "No alcanzan las monedas para esta oferta.";
            return false;
        }

        int safeHeal = Mathf.Max(1, healAmount);
        flow.ModifySavedHP(safeHeal);
        flow.SavePlayerMaxHP(flow.PlayerMaxHP + 1);
        flow.ModifySavedHP(1);
        message = $"Recuperaste +{safeHeal} HP y +1 de vida máxima.";
        return true;
    }

    private static bool TryVitalityBoost(GameFlowManager flow, int cost, int hpBonus, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontró GameFlowManager en la escena.";
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
        message = $"Tu vida máxima sube +{hpBonus}.";
        return true;
    }

    private static List<ShopService.ShopOfferData> GenerateLegacyOffers(
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

        var templates = new List<ShopService.ShopOfferData>
        {
            new ShopService.ShopOfferData { OfferId = "heal_common", Type = ShopService.ShopOfferType.Heal, Cost = healCost, Stock = 2, Rarity = ShopService.ShopOfferRarity.Common, PrimaryValue = healAmount, RequiresMissingHp = true },
            new ShopService.ShopOfferData { OfferId = "heal_rare", Type = ShopService.ShopOfferType.Heal, Cost = Mathf.Max(0, healCost + 4), Stock = 1, Rarity = ShopService.ShopOfferRarity.Rare, PrimaryValue = healAmount + 4, RequiresMissingHp = true },
            new ShopService.ShopOfferData { OfferId = "upgrade_standard", Type = ShopService.ShopOfferType.OrbUpgrade, Cost = upgradeCost, Stock = 1, Rarity = ShopService.ShopOfferRarity.Common, PrimaryValue = 1, RequiresUpgradableOrb = true, RequiresAnyOrb = true },
            new ShopService.ShopOfferData { OfferId = "upgrade_discount", Type = ShopService.ShopOfferType.OrbUpgradeDiscount, Cost = Mathf.Max(0, upgradeCost - 4), Stock = 1, Rarity = ShopService.ShopOfferRarity.Epic, PrimaryValue = 1, RequiresUpgradableOrb = true, RequiresAnyOrb = true },
            new ShopService.ShopOfferData { OfferId = "recovery_pack", Type = ShopService.ShopOfferType.RecoveryPack, Cost = Mathf.Max(0, healCost + 1), Stock = 1, Rarity = ShopService.ShopOfferRarity.Rare, PrimaryValue = Mathf.Max(6, healAmount - 1) },
            new ShopService.ShopOfferData { OfferId = "vitality_boost", Type = ShopService.ShopOfferType.VitalityBoost, Cost = Mathf.Max(0, upgradeCost - 3), Stock = 1, Rarity = ShopService.ShopOfferRarity.Legendary, PrimaryValue = 3 + stageIndex }
        };

        Shuffle(templates);
        var generated = new List<ShopService.ShopOfferData>();
        for (int i = 0; i < templates.Count && generated.Count < offerCount; i++)
            generated.Add(templates[i]);

        return generated;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
