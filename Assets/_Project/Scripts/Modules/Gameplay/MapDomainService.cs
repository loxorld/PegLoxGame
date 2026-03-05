using System.Collections.Generic;
using UnityEngine;

public class MapDomainService
{
    public readonly struct EventOptionRequirement
    {
        public EventOptionRequirement(int minCoins, int minHp, string requiredRelicId)
        {
            MinCoins = Mathf.Max(0, minCoins);
            MinHp = Mathf.Max(0, minHp);
            RequiredRelicId = requiredRelicId ?? string.Empty;
        }

        public int MinCoins { get; }
        public int MinHp { get; }
        public string RequiredRelicId { get; }
        public bool HasRequirements => MinCoins > 0 || MinHp > 0 || !string.IsNullOrWhiteSpace(RequiredRelicId);
    }

    public readonly struct EventOptionContext
    {
        public EventOptionContext(int coins, int currentHp, IReadOnlyCollection<string> relicIds)
        {
            Coins = Mathf.Max(0, coins);
            CurrentHp = Mathf.Max(0, currentHp);
            RelicIds = relicIds;
        }

        public int Coins { get; }
        public int CurrentHp { get; }
        public IReadOnlyCollection<string> RelicIds { get; }
    }

    public readonly struct EventOptionAvailability
    {
        public EventOptionAvailability(bool isAvailable, string missingRequirementText)
        {
            IsAvailable = isAvailable;
            MissingRequirementText = missingRequirementText ?? string.Empty;
        }

        public bool IsAvailable { get; }
        public string MissingRequirementText { get; }
    }
    public readonly struct NodeResolution
    {
        public NodeResolution(MapNodeData node, bool shouldClearSavedNode)
        {
            Node = node;
            ShouldClearSavedNode = shouldClearSavedNode;
        }

        public MapNodeData Node { get; }
        public bool ShouldClearSavedNode { get; }
    }

    public readonly struct EventOptionOutcome
    {
        public EventOptionOutcome(string optionLabel, int coinDelta, int hpDelta, string resultDescription, float? probability = null, EventOptionRequirement? requirement = null)
        {
            OptionLabel = optionLabel;
            Probability = probability;
            SuccessOutcome = new EventResolutionOutcome(coinDelta, hpDelta, resultDescription);
            FailureOutcome = SuccessOutcome;
            Requirement = requirement ?? default;
        }

        public EventOptionOutcome(string optionLabel, float probability, EventResolutionOutcome successOutcome, EventResolutionOutcome failureOutcome, EventOptionRequirement? requirement = null)
        {
            OptionLabel = optionLabel;
            Probability = Mathf.Clamp01(probability);
            SuccessOutcome = successOutcome;
            FailureOutcome = failureOutcome;
            Requirement = requirement ?? default;
        }

        public string OptionLabel { get; }
        public float? Probability { get; }
        public EventResolutionOutcome SuccessOutcome { get; }
        public EventResolutionOutcome FailureOutcome { get; }
        public EventOptionRequirement Requirement { get; }
    }

    public readonly struct EventResolutionOutcome
    {
        public EventResolutionOutcome(int coinDelta, int hpDelta, string resultDescription)
        {
            CoinDelta = coinDelta;
            HpDelta = hpDelta;
            ResultDescription = resultDescription;
        }

        public int CoinDelta { get; }
        public int HpDelta { get; }
        public string ResultDescription { get; }
    }

    public readonly struct EventScenarioOutcome
    {
        public EventScenarioOutcome(string title, string description, IReadOnlyList<EventOptionOutcome> options)
        {
            Title = title;
            Description = description;
            Options = options;
        }


        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<EventOptionOutcome> Options { get; }
    }

    public readonly struct ShopOutcome
    {
        public ShopOutcome(string title, string description, int currentCoins, int healCost, int healAmount, int orbUpgradeCost)
        {
            Title = title;
            Description = description;
            CurrentCoins = currentCoins;
            HealCost = healCost;
            HealAmount = healAmount;
            OrbUpgradeCost = orbUpgradeCost;
        }

        public string Title { get; }
        public string Description { get; }
        public int CurrentCoins { get; }
        public int HealCost { get; }
        public int HealAmount { get; }
        public int OrbUpgradeCost { get; }
    }

    public NodeResolution ResolveCurrentNode(MapStage stage, MapNodeData savedNode)
    {
        if (stage == null)
            return new NodeResolution(null, false);

        if (IsSavedNodeValidForStage(savedNode, stage))
            return new NodeResolution(savedNode, false);

        return new NodeResolution(stage.startingNode, savedNode != null);
    }

    public bool ShouldForceBossNode(MapStage stage, int nodesVisited, int bossAfterNodes, out MapNodeData bossNode)
    {
        bossNode = null;
        if (stage == null || stage.bossNode == null)
            return false;

        if (bossAfterNodes <= 0 || nodesVisited >= bossAfterNodes)
        {
            bossNode = stage.bossNode;
            return true;
        }

        return false;
    }

    public int GetBossAfterNodes(MapStage stage, RunBalanceConfig balance, int stageIndex)
    {
        int fallback = stage != null ? stage.bossAfterNodes : 0;
        return balance != null ? balance.GetBossAfterNodes(stageIndex, fallback) : fallback;
    }

    public bool HasStageConsistencyIssue(MapStage[] stageSequence, MapStage stage, int expectedStageIndex)
    {
        int resolvedIndex = GetStageIndex(stageSequence, stage);
        return resolvedIndex >= 0 && resolvedIndex != expectedStageIndex;
    }

    public int GetStageIndex(MapStage[] stageSequence, MapStage stage)
    {
        if (stage == null || stageSequence == null)
            return -1;

        for (int i = 0; i < stageSequence.Length; i++)
        {
            if (stageSequence[i] == stage)
                return i;
        }

        return -1;
    }

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
                $"Encuentras suministros útiles. +{safeRewardCoins} monedas."),
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
                $"El trato drena tus bolsillos, pero recuperas energía. {bargainPenaltyCoins} monedas, +{bargainHeal} HP.")
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

        string title = string.IsNullOrWhiteSpace(definition.title) ? node != null ? node.title : "Evento" : definition.title;
        string description = string.IsNullOrWhiteSpace(definition.description) ? node?.description ?? string.Empty : definition.description;

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


    public ShopOutcome BuildShopOutcome(
        MapNodeData currentNode,
        RunBalanceConfig balance,
        int stageIndex,
        int coins,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackOrbUpgradeCost,
        string extraMessage)
    {
        int healCost = balance != null ? balance.GetShopHealCost(stageIndex, fallbackHealCost) : fallbackHealCost;
        int healAmount = balance != null ? balance.GetShopHealAmount(stageIndex, fallbackHealAmount) : fallbackHealAmount;
        int orbUpgradeCost = balance != null ? balance.GetShopOrbUpgradeCost(stageIndex, fallbackOrbUpgradeCost) : fallbackOrbUpgradeCost;

        string description = currentNode?.description ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(extraMessage))
            description += $"\n{extraMessage}";

        return new ShopOutcome(
            currentNode != null ? currentNode.title : "Tienda",
            description,
            coins,
            healCost,
            healAmount,
            orbUpgradeCost);
    }

    private static bool IsSavedNodeValidForStage(MapNodeData savedNode, MapStage stage)
    {
        if (savedNode == null || stage == null)
            return false;

        if (!HasConnections(savedNode))
            return false;

        return IsNodeReachableFromStageStart(stage.startingNode, savedNode);
    }

    private static bool HasConnections(MapNodeData node)
    {
        return node != null && node.nextNodes != null && node.nextNodes.Length > 0;
    }

    private static bool IsNodeReachableFromStageStart(MapNodeData startNode, MapNodeData targetNode)
    {
        if (startNode == null || targetNode == null)
            return false;

        var visited = new HashSet<MapNodeData>();
        var pending = new Stack<MapNodeData>();
        pending.Push(startNode);

        while (pending.Count > 0)
        {
            MapNodeData current = pending.Pop();
            if (current == null || !visited.Add(current))
                continue;

            if (current == targetNode)
                return true;

            if (current.nextNodes == null)
                continue;

            for (int i = 0; i < current.nextNodes.Length; i++)
            {
                MapNodeConnection connection = current.nextNodes[i];
                if (connection.targetNode != null)
                    pending.Push(connection.targetNode);
            }
        }

        return false;
    }
}