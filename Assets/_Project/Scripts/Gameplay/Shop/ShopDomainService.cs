using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShopDomainService
{
    private const string UnexpectedPurchaseErrorMessage = "Ocurrió un error al procesar la compra.";

    public sealed class PurchaseResult
    {
        public bool Success;
        public string Message;
    }

#pragma warning disable CS0649
    internal Func<ShopService.ShopOfferData, bool> SimulateIntermediateFailureForTests;
#pragma warning restore CS0649

    public List<ShopService.ShopOfferData> GenerateOffers(
        ShopConfig config,
        RunBalanceConfig balance,
        int stageIndex,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost,
        ShopService.PlayerShopState playerState = null,
        bool allowDuplicateOffersWhenStockAvailable = false)
    {
        if (config == null || config.OfferTable == null || config.OfferTable.Count == 0)
            return GenerateLegacyOffers(balance, stageIndex, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost);

        int amount = UnityEngine.Random.Range(config.MinOffersPerVisit, config.MaxOffersPerVisit + 1);
        List<ShopConfig.OfferTemplate> selectedTemplates = SelectOfferTemplates(
            config,
            amount,
            playerState,
            stageIndex,
            allowDuplicateOffersWhenStockAvailable);
        List<ShopService.ShopOfferData> generated = BuildGeneratedOffers(
            selectedTemplates,
            config,
            balance,
            stageIndex,
            fallbackHealCost,
            fallbackHealAmount,
            fallbackUpgradeCost);

        if (generated.Count < amount)
        {
            int generationCapacity = CountOfferGenerationCapacity(config.OfferTable, allowDuplicateOffersWhenStockAvailable);
            string capacityLabel = allowDuplicateOffersWhenStockAvailable ? "cupos generables" : "ofertas únicas";
            Debug.LogWarning($"[ShopDomainService] Configuración insuficiente para generar {amount} ofertas. " +
                             $"Se generaron {generated.Count}. Capacidad disponible ({capacityLabel}): {generationCapacity}. " +
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

        PlayerPurchaseSnapshot snapshot = PlayerPurchaseSnapshot.Capture(flow, orbManager);

        try
        {
            PurchaseResult result = ExecutePurchase(flow, orbManager, offer);
            if (result.Success)
            {
                flow.SaveRun();
                return result;
            }

            RollbackFailedPurchase(flow, orbManager, shopId, offer.OfferId, snapshot);
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ShopDomainService] Compra revertida por excepción: {ex.Message}");
            RollbackFailedPurchase(flow, orbManager, shopId, offer.OfferId, snapshot);
            return new PurchaseResult { Success = false, Message = UnexpectedPurchaseErrorMessage };
        }
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

        if (offer.Type == ShopService.ShopOfferType.FocusedUpgrade && !state.CurrentOrbCanUpgrade)
        {
            reason = state.HasCurrentOrb
                ? "El orbe actual ya esta al maximo."
                : "No tienes un orbe equipado para mejorar.";
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
            case ShopService.ShopOfferType.FocusedUpgrade:
                return RunResult(TryFocusedUpgrade(flow, orbManager, offer.Cost, offer, out string focusedUpgrade), focusedUpgrade);
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
        OrbInstance currentOrb = orbManager != null ? orbManager.CurrentOrb : null;
        int maxHp = flow != null ? Mathf.Max(1, flow.PlayerMaxHP) : 0;
        int currentHp = flow != null
            ? (flow.HasSavedPlayerHP ? Mathf.Clamp(flow.SavedPlayerHP, 0, maxHp) : maxHp)
            : 0;

        return new ShopService.PlayerShopState
        {
            Coins = flow != null ? flow.Coins : 0,
            OrbCount = orbCount,
            UpgradableOrbCount = upgradable,
            HasMissingHp = missingHp,
            CurrentHp = currentHp,
            MaxHp = maxHp,
            HasCurrentOrb = currentOrb != null,
            CurrentOrbCanUpgrade = currentOrb != null && currentOrb.CanLevelUp,
            CurrentOrbName = currentOrb != null ? currentOrb.OrbName : string.Empty
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

    private static void RollbackFailedPurchase(
        GameFlowManager flow,
        OrbManager orbManager,
        string shopId,
        string offerId,
        PlayerPurchaseSnapshot snapshot)
    {
        snapshot?.Rollback(flow, orbManager);
        flow?.TryRestoreShopOffer(shopId, offerId);
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
            case ShopService.ShopOfferType.FocusedUpgrade:
            case ShopService.ShopOfferType.OrbUpgradeDiscount:
                return balance != null ? balance.GetShopOrbUpgradeCost(stageIndex, template.BaseCost) : template.BaseCost;
            default:
                if (template.BaseCost > 0)
                    return template.BaseCost;

                return Mathf.Max(fallbackHealCost, fallbackUpgradeCost);
        }
    }

    private enum ShopSelectionIntent
    {
        Wildcard,
        Sustain,
        FocusCurrentOrb,
        Growth
    }

    private static List<ShopConfig.OfferTemplate> SelectOfferTemplates(
        ShopConfig config,
        int amount,
        ShopService.PlayerShopState playerState,
        int stageIndex,
        bool allowDuplicateOffersWhenStockAvailable)
    {
        var selectedTemplates = new List<ShopConfig.OfferTemplate>(Mathf.Max(0, amount));
        if (config == null || config.OfferTable == null || config.OfferTable.Count == 0 || amount <= 0)
            return selectedTemplates;

        Dictionary<string, int> listingCapacityByOfferId = BuildListingCapacityByOfferId(config.OfferTable, allowDuplicateOffersWhenStockAvailable);
        var selectedCountsByOfferId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        List<ShopSelectionIntent> selectionPlan = BuildSelectionPlan(playerState, amount);

        for (int i = 0; i < amount; i++)
        {
            List<ShopConfig.OfferTemplate> availableTemplates = BuildAvailableTemplates(
                config.OfferTable,
                selectedCountsByOfferId,
                listingCapacityByOfferId);
            if (availableTemplates.Count == 0)
                break;

            ShopSelectionIntent intent = i < selectionPlan.Count ? selectionPlan[i] : ShopSelectionIntent.Wildcard;
            List<ShopConfig.OfferTemplate> preferredTemplates = BuildPreferredTemplates(availableTemplates, playerState, intent);
            IReadOnlyList<ShopConfig.OfferTemplate> pool = preferredTemplates.Count > 0 ? preferredTemplates : availableTemplates;
            ShopConfig.OfferTemplate pickedTemplate = PickTemplateByRarity(pool, config.RarityWeights, playerState, stageIndex);
            if (pickedTemplate == null)
                break;

            selectedTemplates.Add(pickedTemplate);
            IncrementOfferCount(selectedCountsByOfferId, NormalizeOfferId(pickedTemplate.OfferId));
        }

        return selectedTemplates;
    }

    private static List<ShopSelectionIntent> BuildSelectionPlan(ShopService.PlayerShopState playerState, int amount)
    {
        var plan = new List<ShopSelectionIntent>(Mathf.Max(0, amount));
        if (amount <= 0)
            return plan;

        if (playerState != null && playerState.HasMissingHp)
            plan.Add(ShopSelectionIntent.Sustain);

        if (playerState != null && playerState.CurrentOrbCanUpgrade)
            plan.Add(ShopSelectionIntent.FocusCurrentOrb);
        else if (playerState != null && playerState.UpgradableOrbCount > 0)
            plan.Add(ShopSelectionIntent.Growth);

        while (plan.Count < amount)
            plan.Add(ShopSelectionIntent.Wildcard);

        return plan;
    }

    private static List<ShopConfig.OfferTemplate> BuildPreferredTemplates(
        IReadOnlyList<ShopConfig.OfferTemplate> availableTemplates,
        ShopService.PlayerShopState playerState,
        ShopSelectionIntent intent)
    {
        var preferred = new List<ShopConfig.OfferTemplate>();
        if (availableTemplates == null)
            return preferred;

        for (int i = 0; i < availableTemplates.Count; i++)
        {
            ShopConfig.OfferTemplate template = availableTemplates[i];
            if (template == null)
                continue;

            if (!IsTemplateUsableForState(template, playerState))
                continue;

            if (MatchesIntent(template, intent))
                preferred.Add(template);
        }

        if (preferred.Count > 0)
            return preferred;

        for (int i = 0; i < availableTemplates.Count; i++)
        {
            ShopConfig.OfferTemplate template = availableTemplates[i];
            if (template != null && IsTemplateUsableForState(template, playerState))
                preferred.Add(template);
        }

        return preferred;
    }

    private static bool MatchesIntent(ShopConfig.OfferTemplate template, ShopSelectionIntent intent)
    {
        if (template == null)
            return false;

        return intent switch
        {
            ShopSelectionIntent.Sustain => template.Type == ShopService.ShopOfferType.Heal || template.Type == ShopService.ShopOfferType.RecoveryPack,
            ShopSelectionIntent.FocusCurrentOrb => template.Type == ShopService.ShopOfferType.FocusedUpgrade,
            ShopSelectionIntent.Growth => template.Type == ShopService.ShopOfferType.OrbUpgrade
                || template.Type == ShopService.ShopOfferType.OrbUpgradeDiscount
                || template.Type == ShopService.ShopOfferType.FocusedUpgrade
                || template.Type == ShopService.ShopOfferType.VitalityBoost,
            _ => true
        };
    }

    private static bool IsTemplateUsableForState(ShopConfig.OfferTemplate template, ShopService.PlayerShopState playerState)
    {
        if (template == null || playerState == null)
            return true;

        if (template.RequiresMissingHp && !playerState.HasMissingHp)
            return false;

        if (template.RequiresAnyOrb && playerState.OrbCount <= 0)
            return false;

        if (template.RequiresUpgradableOrb && playerState.UpgradableOrbCount <= 0)
            return false;

        if (template.Type == ShopService.ShopOfferType.FocusedUpgrade && !playerState.CurrentOrbCanUpgrade)
            return false;

        return true;
    }

    private static List<ShopService.ShopOfferData> BuildGeneratedOffers(
        IReadOnlyList<ShopConfig.OfferTemplate> selectedTemplates,
        ShopConfig config,
        RunBalanceConfig balance,
        int stageIndex,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost)
    {
        var generated = new List<ShopService.ShopOfferData>(selectedTemplates != null ? selectedTemplates.Count : 0);
        if (selectedTemplates == null || selectedTemplates.Count == 0)
            return generated;

        Dictionary<string, int> selectedCountsByOfferId = CountSelectedOffersById(selectedTemplates);
        Dictionary<string, int> totalStockByOfferId = ResolveTotalStockByOfferId(selectedTemplates);
        var emittedCountsByOfferId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < selectedTemplates.Count; i++)
        {
            ShopConfig.OfferTemplate template = selectedTemplates[i];
            string normalizedOfferId = NormalizeOfferId(template.OfferId);
            int emittedCount = GetCounterValue(emittedCountsByOfferId, normalizedOfferId);
            int listingCount = GetCounterValue(selectedCountsByOfferId, normalizedOfferId);
            int totalStock = GetCounterValue(totalStockByOfferId, normalizedOfferId);

            int baseCost = ResolveTemplateCost(template, balance, stageIndex, fallbackHealCost, fallbackUpgradeCost);
            int adjustedCost = Mathf.Max(0, Mathf.RoundToInt(baseCost * config.GetPriceMultiplier(template.Rarity)));
            generated.Add(new ShopService.ShopOfferData
            {
                OfferId = BuildOfferId(template.OfferId, i),
                Type = template.Type,
                Cost = adjustedCost,
                Stock = ResolveGeneratedStock(totalStock, listingCount, emittedCount),
                Rarity = template.Rarity,
                PrimaryValue = ResolvePrimaryValue(template, fallbackHealAmount),
                RequiresMissingHp = template.RequiresMissingHp,
                RequiresUpgradableOrb = template.RequiresUpgradableOrb,
                RequiresAnyOrb = template.RequiresAnyOrb
            });

            emittedCountsByOfferId[normalizedOfferId] = emittedCount + 1;
        }

        return generated;
    }

    private static Dictionary<string, int> BuildListingCapacityByOfferId(
        IReadOnlyList<ShopConfig.OfferTemplate> offers,
        bool allowDuplicateOffersWhenStockAvailable)
    {
        var capacityByOfferId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (offers == null)
            return capacityByOfferId;

        for (int i = 0; i < offers.Count; i++)
        {
            ShopConfig.OfferTemplate template = offers[i];
            if (template == null)
                continue;

            string normalizedOfferId = NormalizeOfferId(template.OfferId);
            int capacity = allowDuplicateOffersWhenStockAvailable ? Mathf.Max(1, template.BaseStock) : 1;
            if (!capacityByOfferId.TryGetValue(normalizedOfferId, out int currentCapacity) || capacity > currentCapacity)
                capacityByOfferId[normalizedOfferId] = capacity;
        }

        return capacityByOfferId;
    }

    private static List<ShopConfig.OfferTemplate> BuildAvailableTemplates(
        IReadOnlyList<ShopConfig.OfferTemplate> offers,
        Dictionary<string, int> selectedCountsByOfferId,
        Dictionary<string, int> listingCapacityByOfferId)
    {
        var availableTemplates = new List<ShopConfig.OfferTemplate>();
        if (offers == null)
            return availableTemplates;

        for (int i = 0; i < offers.Count; i++)
        {
            ShopConfig.OfferTemplate template = offers[i];
            if (template == null)
                continue;

            string normalizedOfferId = NormalizeOfferId(template.OfferId);
            int selectedCount = GetCounterValue(selectedCountsByOfferId, normalizedOfferId);
            int listingCapacity = GetCounterValue(listingCapacityByOfferId, normalizedOfferId);
            if (selectedCount < listingCapacity)
                availableTemplates.Add(template);
        }

        return availableTemplates;
    }

    private static Dictionary<string, int> CountSelectedOffersById(IReadOnlyList<ShopConfig.OfferTemplate> selectedTemplates)
    {
        var countsByOfferId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (selectedTemplates == null)
            return countsByOfferId;

        for (int i = 0; i < selectedTemplates.Count; i++)
        {
            ShopConfig.OfferTemplate template = selectedTemplates[i];
            if (template == null)
                continue;

            IncrementOfferCount(countsByOfferId, NormalizeOfferId(template.OfferId));
        }

        return countsByOfferId;
    }

    private static Dictionary<string, int> ResolveTotalStockByOfferId(IReadOnlyList<ShopConfig.OfferTemplate> selectedTemplates)
    {
        var totalStockByOfferId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (selectedTemplates == null)
            return totalStockByOfferId;

        for (int i = 0; i < selectedTemplates.Count; i++)
        {
            ShopConfig.OfferTemplate template = selectedTemplates[i];
            if (template == null)
                continue;

            string normalizedOfferId = NormalizeOfferId(template.OfferId);
            int totalStock = Mathf.Max(1, template.BaseStock);
            if (!totalStockByOfferId.TryGetValue(normalizedOfferId, out int currentTotalStock) || totalStock > currentTotalStock)
                totalStockByOfferId[normalizedOfferId] = totalStock;
        }

        return totalStockByOfferId;
    }

    private static int ResolveGeneratedStock(int totalStock, int listingCount, int emittedCount)
    {
        int safeTotalStock = Mathf.Max(1, totalStock);
        int safeListingCount = Mathf.Max(1, listingCount);
        int safeEmittedCount = Mathf.Clamp(emittedCount, 0, safeListingCount - 1);

        int stockPerListing = safeTotalStock / safeListingCount;
        int remainder = safeTotalStock % safeListingCount;
        return stockPerListing + (safeEmittedCount < remainder ? 1 : 0);
    }

    private static void IncrementOfferCount(Dictionary<string, int> countsByOfferId, string normalizedOfferId)
    {
        countsByOfferId[normalizedOfferId] = GetCounterValue(countsByOfferId, normalizedOfferId) + 1;
    }

    private static int GetCounterValue(Dictionary<string, int> countsByOfferId, string normalizedOfferId)
    {
        return countsByOfferId != null && countsByOfferId.TryGetValue(normalizedOfferId, out int count)
            ? count
            : 0;
    }

    private static int CountOfferGenerationCapacity(IReadOnlyList<ShopConfig.OfferTemplate> offers, bool allowDuplicateOffersWhenStockAvailable)
    {
        Dictionary<string, int> capacityByOfferId = BuildListingCapacityByOfferId(offers, allowDuplicateOffersWhenStockAvailable);
        int totalCapacity = 0;
        foreach (KeyValuePair<string, int> entry in capacityByOfferId)
            totalCapacity += Mathf.Max(0, entry.Value);

        return totalCapacity;
    }

    private static string NormalizeOfferId(string offerId)
    {
        return string.IsNullOrWhiteSpace(offerId) ? "offer" : offerId.Trim();
    }

    private static ShopConfig.OfferTemplate PickTemplateByRarity(
        IReadOnlyList<ShopConfig.OfferTemplate> availableTemplates,
        IReadOnlyList<ShopConfig.RarityWeight> rarityWeights,
        ShopService.PlayerShopState playerState,
        int stageIndex)
    {
        if (availableTemplates == null || availableTemplates.Count == 0)
            return null;

        float total = 0f;
        var weightedTemplates = new List<(ShopConfig.OfferTemplate Template, float Weight)>(availableTemplates.Count);
        for (int i = 0; i < availableTemplates.Count; i++)
        {
            ShopConfig.OfferTemplate template = availableTemplates[i];
            if (template == null)
                continue;

            float rarityWeight = GetRarityWeight(rarityWeights, template.Rarity);
            float contextWeight = GetTemplateContextWeight(template, playerState, stageIndex);
            float finalWeight = Mathf.Max(0.01f, rarityWeight * contextWeight);
            weightedTemplates.Add((template, finalWeight));
            total += finalWeight;
        }

        if (total <= 0f)
            return availableTemplates[UnityEngine.Random.Range(0, availableTemplates.Count)];

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < weightedTemplates.Count; i++)
        {
            cumulative += weightedTemplates[i].Weight;
            if (roll <= cumulative)
                return weightedTemplates[i].Template;
        }

        return weightedTemplates[weightedTemplates.Count - 1].Template;
    }

    private static float GetRarityWeight(IReadOnlyList<ShopConfig.RarityWeight> rarityWeights, ShopService.ShopOfferRarity rarity)
    {
        if (rarityWeights == null)
            return 1f;

        for (int i = 0; i < rarityWeights.Count; i++)
        {
            ShopConfig.RarityWeight weight = rarityWeights[i];
            if (weight != null && weight.Rarity == rarity)
                return Mathf.Max(0.01f, weight.Weight);
        }

        return 1f;
    }

    private static float GetTemplateContextWeight(
        ShopConfig.OfferTemplate template,
        ShopService.PlayerShopState playerState,
        int stageIndex)
    {
        if (template == null)
            return 0.01f;

        float weight = 1f;
        if (playerState == null)
            return weight;

        if (template.Type == ShopService.ShopOfferType.Heal)
            weight *= playerState.HasMissingHp ? 2.4f : 0.18f;
        else if (template.Type == ShopService.ShopOfferType.RecoveryPack)
            weight *= playerState.HasMissingHp ? 1.8f : 0.95f;
        else if (template.Type == ShopService.ShopOfferType.OrbUpgrade || template.Type == ShopService.ShopOfferType.OrbUpgradeDiscount)
            weight *= playerState.UpgradableOrbCount > 0 ? 2.1f : 0.15f;
        else if (template.Type == ShopService.ShopOfferType.FocusedUpgrade)
            weight *= playerState.CurrentOrbCanUpgrade ? 2.6f : 0.08f;
        else if (template.Type == ShopService.ShopOfferType.VitalityBoost)
            weight *= playerState.HasMissingHp ? 1.35f : 1.1f;

        if (playerState.Coins < template.BaseCost)
            weight *= 0.82f;
        else
            weight *= 1.08f;

        if (stageIndex >= 2 && template.Type == ShopService.ShopOfferType.VitalityBoost)
            weight *= 1.25f;
        if (stageIndex == 0 && template.Type == ShopService.ShopOfferType.Heal)
            weight *= 1.1f;

        return Mathf.Max(0.01f, weight);
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

    private bool TryFocusedUpgrade(GameFlowManager flow, OrbManager orbManager, int upgradeCost, ShopService.ShopOfferData offer, out string message)
    {
        message = null;
        OrbInstance currentOrb = orbManager != null ? orbManager.CurrentOrb : null;
        if (currentOrb == null)
        {
            message = "No tienes un orbe equipado.";
            return false;
        }

        if (!currentOrb.CanLevelUp)
        {
            message = $"{currentOrb.OrbName} ya esta al maximo.";
            return false;
        }

        if (!flow.SpendCoins(upgradeCost))
        {
            message = "No alcanzan las monedas para esta mejora.";
            return false;
        }

        if (ShouldFailIntermediateForTests(offer))
        {
            message = "Fallo intermedio en la compra.";
            return false;
        }

        int previousLevel = currentOrb.Level;
        currentOrb.LevelUp();
        message = currentOrb.Level > previousLevel
            ? $"{currentOrb.OrbName} sube a nivel {currentOrb.Level}."
            : $"{currentOrb.OrbName} ya esta al maximo.";
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
        private readonly List<RunSaveData.OrbSaveData> serializedOrbs;
        private readonly string currentOrbId;
        private readonly bool hasOrbSnapshot;

        private PlayerPurchaseSnapshot(GameFlowManager flow, OrbManager orbManager)
        {
            coins = flow.Coins;
            savedHp = flow.SavedPlayerHP;
            hasSavedHp = flow.HasSavedPlayerHP;
            maxHp = flow.PlayerMaxHP;

            if (orbManager != null)
            {
                serializedOrbs = CloneOrbSaves(orbManager.SerializeOrbs());
                currentOrbId = orbManager.GetCurrentOrbId();
                hasOrbSnapshot = true;
            }
        }

        public static PlayerPurchaseSnapshot Capture(GameFlowManager flow, OrbManager orbManager)
        {
            return new PlayerPurchaseSnapshot(flow, orbManager);
        }

        public void Rollback(GameFlowManager flow, OrbManager orbManager)
        {
            int deltaCoins = coins - flow.Coins;
            if (deltaCoins > 0)
                flow.AddCoins(deltaCoins);
            else if (deltaCoins < 0)
                flow.SpendCoins(-deltaCoins);

            flow.SavePlayerMaxHP(maxHp);
            RestoreSavedHp(flow);

            if (hasOrbSnapshot && orbManager != null)
                orbManager.DeserializeOrbs(CloneOrbSaves(serializedOrbs), currentOrbId);
        }

        private void RestoreSavedHp(GameFlowManager flow)
        {
            if (hasSavedHp)
                flow.SavePlayerHP(savedHp);
            else
                flow.ClearSavedPlayerHP();
        }

        private static List<RunSaveData.OrbSaveData> CloneOrbSaves(List<RunSaveData.OrbSaveData> source)
        {
            if (source == null)
                return null;

            var clone = new List<RunSaveData.OrbSaveData>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                RunSaveData.OrbSaveData orb = source[i];
                if (orb == null)
                    continue;

                clone.Add(new RunSaveData.OrbSaveData
                {
                    OrbId = orb.OrbId,
                    Level = orb.Level
                });
            }

            return clone;
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
            new ShopService.ShopOfferData { OfferId = "upgrade_focus", Type = ShopService.ShopOfferType.FocusedUpgrade, Cost = Mathf.Max(0, upgradeCost + 2), Stock = 1, Rarity = ShopService.ShopOfferRarity.Rare, PrimaryValue = 1, RequiresAnyOrb = true },
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
