using System.Collections.Generic;
using UnityEngine;

public class MapDomainService
{
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
        public EventOptionOutcome(string optionLabel, int coinDelta, int hpDelta, string resultDescription, float? probability = null)
        {
            OptionLabel = optionLabel;
            Probability = probability;
            SuccessOutcome = new EventResolutionOutcome(coinDelta, hpDelta, resultDescription);
            FailureOutcome = SuccessOutcome;
        }

        public EventOptionOutcome(string optionLabel, float probability, EventResolutionOutcome successOutcome, EventResolutionOutcome failureOutcome)
        {
            OptionLabel = optionLabel;
            Probability = Mathf.Clamp01(probability);
            SuccessOutcome = successOutcome;
            FailureOutcome = failureOutcome;
        }

        public string OptionLabel { get; }
        public float? Probability { get; }
        public EventResolutionOutcome SuccessOutcome { get; }
        public EventResolutionOutcome FailureOutcome { get; }
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

    public EventResolutionOutcome ResolveEventOptionOutcome(EventOptionOutcome option, float roll)
    {
        if (!option.Probability.HasValue)
            return option.SuccessOutcome;

        return roll <= option.Probability.Value ? option.SuccessOutcome : option.FailureOutcome;
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