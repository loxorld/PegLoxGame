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

    internal Func<ShopService.ShopOfferData, bool> SimulateIntermediateFailureForTests;

    public List<ShopService.ShopOfferData> GenerateOffers(
        ShopConfig config,
        RunBalanceConfig balance,
        int stageIndex,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost,
        bool allowDuplicateOffersWhenStockAvailable = false)
    {
        if (config == null || config.OfferTable == null || config.OfferTable.Count == 0)
            return GenerateLegacyOffers(balance, stageIndex, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost);

        int amount = UnityEngine.Random.Range(config.MinOffersPerVisit, config.MaxOffersPerVisit + 1);
        var generated = new List<ShopService.ShopOfferData>(amount);
        var usedOfferIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxAttemptsPerOffer = Mathf.Max(4, config.OfferTable.Count * 3);

        for (int i = 0; i < amount; i++)
        {
            ShopConfig.OfferTemplate template = null;
            for (int attempt = 0; attempt < maxAttemptsPerOffer; attempt++)
            {
                ShopConfig.OfferTemplate candidate = PickTemplateByRarity(config);
                if (candidate == null)
                    break;

                if (CanUseTemplate(candidate, usedOfferIds, allowDuplicateOffersWhenStockAvailable))
                {
                    template = candidate;
                    break;
                }
            }

            if (template == null)
                break;

            usedOfferIds.Add(NormalizeOfferId(template.OfferId));

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

        if (generated.Count < amount)
        {
            int distinctTemplateCount = CountDistinctOfferIds(config.OfferTable);
            Debug.LogWarning($"[ShopDomainService] Configuración insuficiente para generar {amount} ofertas únicas. " +
                             $"Se generaron {generated.Count}. Plantillas distintas disponibles: {distinctTemplateCount}. " +
                             "Revisa ShopConfig o habilita duplicados de forma explícita cuando corresponda.");
        }

        return generated;
    }

    public bool IsOfferEnabled(ShopService.PlayerShopState state, ShopService.ShopOfferData offer, out string reason)
    {
        return IsOfferEnabled(state, offer, includeStockValidation: true, out reason);
    }

    public PurchaseResult TryPurchaseAtomic(GameFlowManager flow, OrbManager orbManager, string shopId, ShopService.ShopOfferData offer)
    {
        if (flow == null)
            return new PurchaseResult { Success = false, Message = "No se encontró GameFlowManager en la escena." };

        if (offer == null)
            return new PurchaseResult { Success = false, Message = "Oferta inválida." };

        if (!flow.TryConsumeShopOffer(shopId, offer.OfferId))
            return new PurchaseResult { Success = false, Message = "Sin stock para esta oferta." };

        PlayerPurchaseSnapshot snapshot = PlayerPurchaseSnapshot.Capture(flow);
        PurchaseResult result = ExecutePurchase(flow, orbManager, offer);
        if (result.Success)
        {
            flow.SaveRun();
            return result;
        }

        snapshot.Rollback(flow);
        flow.TryRestoreShopOffer(shopId, offer.OfferId);
        return result;
    }

    private bool IsOfferEnabled(ShopService.PlayerShopState state, ShopService.ShopOfferData offer, bool includeStockValidation, out string reason)
    {
        reason = null;
        if (state == null || offer == null)
        {
            reason = "Oferta inválida.";
            return false;
        }

        if (includeStockValidation && offer.Stock <= 0)
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

    private PurchaseResult ExecutePurchase(GameFlowManager flow, OrbManager orbManager, ShopService.ShopOfferData offer)
    {
        ShopService.PlayerShopState state = BuildPlayerState(flow, orbManager);
        if (!IsOfferEnabled(state, offer, includeStockValidation: false, out string reason))
            return new PurchaseResult { Success = false, Message = reason };

        switch (offer.Type)
        {
            case ShopService.ShopOfferType.Heal:
                return RunResult(TryHeal(flow, offer.Cost, offer.PrimaryValue, offer, out string heal), heal);
            case ShopService.ShopOfferType.OrbUpgrade:
            case ShopService.ShopOfferType.OrbUpgradeDiscount:
                return RunResult(TryUpgradeOrb(flow, orbManager, offer.Cost, offer, out string upgrade), upgrade);
            case ShopService.ShopOfferType.RecoveryPack:
                return RunResult(TryRecoveryPack(flow, offer.Cost, offer.PrimaryValue, offer, out string pack), pack);
            case ShopService.ShopOfferType.VitalityBoost:
                return RunResult(TryVitalityBoost(flow, offer.Cost, offer.PrimaryValue, offer, out string vitality), vitality);
            default:
                return new PurchaseResult { Success = false, Message = "Oferta no soportada." };
        }
    }

    private ShopService.PlayerShopState BuildPlayerState(GameFlowManager flow, OrbManager orbManager)
    {
        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        int orbCount = ownedOrbs != null ? ownedOrbs.Count : 0;
        int upgradable = GetUpgradableOrbs(ownedOrbs).Count;
        bool missingHp = flow != null && flow.HasSavedPlayerHP && flow.SavedPlayerHP < flow.PlayerMaxHP;

        return new ShopService.PlayerShopState
        {
            Coins = flow != null ? flow.Coins : 0,
            OrbCount = orbCount,
            UpgradableOrbCount = upgradable,
            HasMissingHp = missingHp
        };
    }

    private bool ShouldFailIntermediateForTests(ShopService.ShopOfferData offer)
    {
        return SimulateIntermediateFailureForTests != null && SimulateIntermediateFailureForTests(offer);
    }

    private static PurchaseResult RunResult(bool success, string message)
    {
        return new PurchaseResult { Success = success, Message = message };
    }

    private static string BuildOfferId(string baseId, int index)
    {
        return $"{NormalizeOfferId(baseId)}_{index}";
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

    private static bool CanUseTemplate(ShopConfig.OfferTemplate template, HashSet<string> usedOfferIds, bool allowDuplicateOffersWhenStockAvailable)
    {
        string normalizedOfferId = NormalizeOfferId(template.OfferId);
        if (!usedOfferIds.Contains(normalizedOfferId))
            return true;

        return allowDuplicateOffersWhenStockAvailable && template.BaseStock > 1;
    }

    private static int CountDistinctOfferIds(IReadOnlyList<ShopConfig.OfferTemplate> offers)
    {
        var distinctIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < offers.Count; i++)
            distinctIds.Add(NormalizeOfferId(offers[i].OfferId));

        return distinctIds.Count;
    }

    private static string NormalizeOfferId(string offerId)
    {
        return string.IsNullOrWhiteSpace(offerId) ? "offer" : offerId.Trim();
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

    private bool TryHeal(GameFlowManager flow, int healCost, int healAmount, ShopService.ShopOfferData offer, out string message)
    {
        message = null;
        if (!flow.SpendCoins(healCost))
        {
            message = "No alcanzan las monedas para curar.";
            return false;
        }

        if (ShouldFailIntermediateForTests(offer))
        {
            message = "Fallo intermedio en la compra.";
            return false;
        }

        flow.ModifySavedHP(healAmount);
        message = $"Te curaste +{healAmount} HP.";
        return true;
    }

    private bool TryUpgradeOrb(GameFlowManager flow, OrbManager orbManager, int upgradeCost, ShopService.ShopOfferData offer, out string message)
    {
        message = null;
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

        if (ShouldFailIntermediateForTests(offer))
        {
            message = "Fallo intermedio en la compra.";
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

    private bool TryRecoveryPack(GameFlowManager flow, int cost, int healAmount, ShopService.ShopOfferData offer, out string message)
    {
        message = null;
        if (!flow.SpendCoins(cost))
        {
            message = "No alcanzan las monedas para esta oferta.";
            return false;
        }

        if (ShouldFailIntermediateForTests(offer))
        {
            message = "Fallo intermedio en la compra.";
            return false;
        }

        int safeHeal = Mathf.Max(1, healAmount);
        flow.ModifySavedHP(safeHeal);
        flow.SavePlayerMaxHP(flow.PlayerMaxHP + 1);
        flow.ModifySavedHP(1);
        message = $"Recuperaste +{safeHeal} HP y +1 de vida máxima.";
        return true;
    }

    private bool TryVitalityBoost(GameFlowManager flow, int cost, int hpBonus, ShopService.ShopOfferData offer, out string message)
    {
        message = null;
        if (!flow.SpendCoins(cost))
        {
            message = "No alcanzan las monedas para potenciarte.";
            return false;
        }

        if (ShouldFailIntermediateForTests(offer))
        {
            message = "Fallo intermedio en la compra.";
            return false;
        }

        int nextMaxHp = flow.PlayerMaxHP + Mathf.Max(1, hpBonus);
        flow.SavePlayerMaxHP(nextMaxHp);
        flow.ModifySavedHP(Mathf.Max(1, hpBonus));
        message = $"Tu vida máxima sube +{hpBonus}.";
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

    private sealed class PlayerPurchaseSnapshot
    {
        private readonly int coins;
        private readonly int savedHp;
        private readonly bool hasSavedHp;
        private readonly int maxHp;

        private PlayerPurchaseSnapshot(GameFlowManager flow)
        {
            coins = flow.Coins;
            savedHp = flow.SavedPlayerHP;
            hasSavedHp = flow.HasSavedPlayerHP;
            maxHp = flow.PlayerMaxHP;
        }

        public static PlayerPurchaseSnapshot Capture(GameFlowManager flow)
        {
            return new PlayerPurchaseSnapshot(flow);
        }

        public void Rollback(GameFlowManager flow)
        {
            int deltaCoins = coins - flow.Coins;
            if (deltaCoins > 0)
                flow.AddCoins(deltaCoins);
            else if (deltaCoins < 0)
                flow.SpendCoins(-deltaCoins);

            flow.SavePlayerMaxHP(maxHp);
            if (hasSavedHp)
            {
                int hpDelta = savedHp - flow.SavedPlayerHP;
                if (hpDelta != 0)
                    flow.ModifySavedHP(hpDelta);
            }
        }
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
