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

    public readonly struct EventOutcome
    {
        public EventOutcome(string title, string description, int coinDelta, int hpDelta)
        {
            Title = title;
            Description = description;
            CoinDelta = coinDelta;
            HpDelta = hpDelta;
        }

        public string Title { get; }
        public string Description { get; }
        public int CoinDelta { get; }
        public int HpDelta { get; }
    }

    public readonly struct ShopOutcome
    {
        public ShopOutcome(string title, string description, int healCost, int healAmount, int orbUpgradeCost)
        {
            Title = title;
            Description = description;
            HealCost = healCost;
            HealAmount = healAmount;
            OrbUpgradeCost = orbUpgradeCost;
        }

        public string Title { get; }
        public string Description { get; }
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

    public EventOutcome BuildEventOutcome(
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
        float goodEventChance = balance != null
            ? balance.GetEventPositiveChance(stageIndex, 0.5f)
            : 0.5f;
        bool isGoodEvent = Random.value <= goodEventChance;

        int rewardMin = balance != null ? balance.GetEventCoinsRewardMin(stageIndex, eventCoinsRewardMin) : eventCoinsRewardMin;
        int rewardMax = balance != null ? balance.GetEventCoinsRewardMax(stageIndex, eventCoinsRewardMax) : eventCoinsRewardMax;
        int penaltyMin = balance != null ? balance.GetEventCoinsPenaltyMin(stageIndex, eventCoinsPenaltyMin) : eventCoinsPenaltyMin;
        int penaltyMax = balance != null ? balance.GetEventCoinsPenaltyMax(stageIndex, eventCoinsPenaltyMax) : eventCoinsPenaltyMax;
        int healMin = balance != null ? balance.GetEventHealMin(stageIndex, eventHealMin) : eventHealMin;
        int healMax = balance != null ? balance.GetEventHealMax(stageIndex, eventHealMax) : eventHealMax;
        int damageMin = balance != null ? balance.GetEventDamageMin(stageIndex, eventDamageMin) : eventDamageMin;
        int damageMax = balance != null ? balance.GetEventDamageMax(stageIndex, eventDamageMax) : eventDamageMax;

        int coinDelta = isGoodEvent
            ? Random.Range(Mathf.Min(rewardMin, rewardMax), Mathf.Max(rewardMin, rewardMax) + 1)
            : -Random.Range(Mathf.Min(penaltyMin, penaltyMax), Mathf.Max(penaltyMin, penaltyMax) + 1);

        int hpDelta = isGoodEvent
            ? Random.Range(Mathf.Min(healMin, healMax), Mathf.Max(healMin, healMax) + 1)
            : -Random.Range(Mathf.Min(damageMin, damageMax), Mathf.Max(damageMin, damageMax) + 1);

        string outcome = isGoodEvent
            ? $"Encontraste algo útil. +{coinDelta} monedas, +{hpDelta} HP."
            : $"La expedición salió mal. {coinDelta} monedas, {hpDelta} HP.";

        return new EventOutcome(
            currentNode != null ? currentNode.title : "Evento",
            $"{currentNode?.description}\n\n{outcome}",
            coinDelta,
            hpDelta);
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

        string description = $"{currentNode?.description}\n\nMonedas: {coins}";
        if (!string.IsNullOrWhiteSpace(extraMessage))
            description += $"\n{extraMessage}";

        return new ShopOutcome(
            currentNode != null ? currentNode.title : "Tienda",
            description,
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