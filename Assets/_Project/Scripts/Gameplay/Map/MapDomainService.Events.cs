using System.Collections.Generic;
using UnityEngine;

public partial class MapDomainService
{
    public EventScenarioOutcome BuildEventOutcome(
        MapNodeData currentNode,
        RunBalanceConfig balance,
        int stageIndex,
        int eventCoinsRewardMin,
        int eventCoinsRewardMax,
        int eventCoinsPenaltyMin,
        int eventCoinsPenaltyMax,
        int eventHealMin,
        int eventHealMax,
        int eventDamageMin,
        int eventDamageMax)
    {
        EventScenarioOutcome? definitionOutcome = BuildEventOutcomeFromDefinition(currentNode, stageIndex);
        if (definitionOutcome.HasValue)
            return definitionOutcome.Value;

        int rewardMin = balance != null ? balance.GetEventCoinsRewardMin(stageIndex, eventCoinsRewardMin) : eventCoinsRewardMin;
        int rewardMax = balance != null ? balance.GetEventCoinsRewardMax(stageIndex, eventCoinsRewardMax) : eventCoinsRewardMax;
        int penaltyMin = balance != null ? balance.GetEventCoinsPenaltyMin(stageIndex, eventCoinsPenaltyMin) : eventCoinsPenaltyMin;
        int penaltyMax = balance != null ? balance.GetEventCoinsPenaltyMax(stageIndex, eventCoinsPenaltyMax) : eventCoinsPenaltyMax;
        int healMin = balance != null ? balance.GetEventHealMin(stageIndex, eventHealMin) : eventHealMin;
        int healMax = balance != null ? balance.GetEventHealMax(stageIndex, eventHealMax) : eventHealMax;
        int damageMin = balance != null ? balance.GetEventDamageMin(stageIndex, eventDamageMin) : eventDamageMin;
        int damageMax = balance != null ? balance.GetEventDamageMax(stageIndex, eventDamageMax) : eventDamageMax;

        int safeRewardCoins = Random.Range(Mathf.Min(rewardMin, rewardMax), Mathf.Max(rewardMin, rewardMax) + 1);
        int riskyRewardCoins = Random.Range(Mathf.Min(rewardMin, rewardMax), Mathf.Max(rewardMin, rewardMax) + 1);
        int riskyPenaltyHp = -Random.Range(Mathf.Min(damageMin, damageMax), Mathf.Max(damageMin, damageMax) + 1);
        int bargainPenaltyCoins = -Random.Range(Mathf.Min(penaltyMin, penaltyMax), Mathf.Max(penaltyMin, penaltyMax) + 1);
        int bargainHeal = Random.Range(Mathf.Min(healMin, healMax), Mathf.Max(healMin, healMax) + 1);
        int restCoinPenalty = -Random.Range(Mathf.Min(penaltyMin, penaltyMax), Mathf.Max(penaltyMin, penaltyMax) + 1);
        int restHeal = Random.Range(Mathf.Min(healMin, healMax), Mathf.Max(healMin, healMax) + 1);

        var options = new List<EventOptionOutcome>
        {
            new EventOptionOutcome(
                "Investigar con cuidado",
                safeRewardCoins,
                0,
                $"Encuentras suministros utiles. +{safeRewardCoins} monedas."),
            new EventOptionOutcome(
                "Tomar un atajo arriesgado",
                0.6f,
                new EventResolutionOutcome(
                    riskyRewardCoins,
                    0,
                    $"El atajo funciona. +{riskyRewardCoins} monedas."),
                new EventResolutionOutcome(
                    0,
                    riskyPenaltyHp,
                    $"El atajo sale mal y terminas herido. {riskyPenaltyHp} HP.")),
            new EventOptionOutcome(
                "Negociar con comerciantes",
                bargainPenaltyCoins,
                bargainHeal,
                $"El trato drena tus bolsillos, pero recuperas energia. {bargainPenaltyCoins} monedas, +{bargainHeal} HP.")
        };

        if (Random.value > 0.4f)
        {
            options.Add(new EventOptionOutcome(
                "Descansar antes de seguir",
                restCoinPenalty,
                restHeal,
                $"Descansar cuesta recursos, pero te repones. {restCoinPenalty} monedas, +{restHeal} HP."));
        }

        return new EventScenarioOutcome(
            currentNode != null ? currentNode.title : "Evento",
            currentNode?.description ?? string.Empty,
            options);
    }

    public EventResolutionOutcome ResolveEventOptionOutcome(EventOptionOutcome option, float roll)
    {
        if (!option.Probability.HasValue)
            return option.SuccessOutcome;

        return roll <= option.Probability.Value ? option.SuccessOutcome : option.FailureOutcome;
    }

    public EventOptionAvailability EvaluateEventOptionAvailability(EventOptionOutcome option, EventOptionContext context)
    {
        EventOptionRequirement requirement = option.Requirement;
        if (!requirement.HasRequirements)
            return new EventOptionAvailability(true, string.Empty);

        var missingRequirements = new List<string>();
        if (requirement.MinCoins > 0 && context.Coins < requirement.MinCoins)
            missingRequirements.Add($"{requirement.MinCoins} monedas");

        if (requirement.MinHp > 0 && context.CurrentHp < requirement.MinHp)
            missingRequirements.Add($"{requirement.MinHp} HP");

        if (!string.IsNullOrWhiteSpace(requirement.RequiredRelicId) && !HasRelic(context.RelicIds, requirement.RequiredRelicId))
            missingRequirements.Add($"Reliquia '{requirement.RequiredRelicId}'");

        if (missingRequirements.Count == 0)
            return new EventOptionAvailability(true, string.Empty);

        string feedback = "Requiere: " + string.Join(", ", missingRequirements);
        return new EventOptionAvailability(false, feedback);
    }

    private EventScenarioOutcome? BuildEventOutcomeFromDefinition(MapNodeData node, int stageIndex)
    {
        EventDefinition definition = ResolveEventDefinition(node, stageIndex);
        if (definition == null || definition.options == null || definition.options.Length == 0)
            return null;

        var options = new List<EventOptionOutcome>(definition.options.Length);
        for (int i = 0; i < definition.options.Length; i++)
        {
            EventDefinition.EventOptionDefinition option = definition.options[i];
            if (string.IsNullOrWhiteSpace(option.optionLabel))
                continue;

            EventResolutionOutcome successOutcome = new EventResolutionOutcome(
                option.successOutcome.coinDelta,
                option.successOutcome.hpDelta,
                option.successOutcome.resultDescription ?? string.Empty);

            EventResolutionOutcome failureOutcome = new EventResolutionOutcome(
                option.failureOutcome.coinDelta,
                option.failureOutcome.hpDelta,
                option.failureOutcome.resultDescription ?? string.Empty);

            if (option.useSuccessProbability)
            {
                options.Add(new EventOptionOutcome(
                    option.optionLabel,
                    Mathf.Clamp01(option.successProbability),
                    successOutcome,
                    failureOutcome,
                    BuildRequirement(option)));
                continue;
            }

            options.Add(new EventOptionOutcome(
                option.optionLabel,
                successOutcome.CoinDelta,
                successOutcome.HpDelta,
                successOutcome.ResultDescription,
                null,
                BuildRequirement(option)));
        }

        if (options.Count == 0)
            return null;

        string title = string.IsNullOrWhiteSpace(definition.title)
            ? node != null ? node.title : "Evento"
            : definition.title;
        string description = string.IsNullOrWhiteSpace(definition.description)
            ? node?.description ?? string.Empty
            : definition.description;

        return new EventScenarioOutcome(title, description, options);
    }

    private static EventDefinition ResolveEventDefinition(MapNodeData node, int stageIndex)
    {
        if (node == null)
            return null;

        if (IsValidForStage(node.eventDefinition, stageIndex))
            return node.eventDefinition;

        if (node.eventDefinitionPool == null || node.eventDefinitionPool.Length == 0)
            return null;

        var validPool = new List<EventDefinition>();
        for (int i = 0; i < node.eventDefinitionPool.Length; i++)
        {
            EventDefinition poolDefinition = node.eventDefinitionPool[i];
            if (IsValidForStage(poolDefinition, stageIndex))
                validPool.Add(poolDefinition);
        }

        if (validPool.Count == 0)
            return null;

        int randomIndex = Random.Range(0, validPool.Count);
        return validPool[randomIndex];
    }

    private static bool IsValidForStage(EventDefinition definition, int stageIndex)
    {
        return definition != null && definition.conditions.Matches(stageIndex);
    }

    private static EventOptionRequirement? BuildRequirement(EventDefinition.EventOptionDefinition option)
    {
        if (!option.useRequirements)
            return null;

        return new EventOptionRequirement(option.minCoins, option.minHp, option.requiredRelicId);
    }

    private static bool HasRelic(IReadOnlyCollection<string> relicIds, string requiredRelicId)
    {
        if (relicIds == null || string.IsNullOrWhiteSpace(requiredRelicId))
            return false;

        foreach (string relicId in relicIds)
        {
            if (string.Equals(relicId, requiredRelicId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
