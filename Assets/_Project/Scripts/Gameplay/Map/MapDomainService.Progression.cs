using System.Collections.Generic;
using UnityEngine;

public partial class MapDomainService
{
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
            description = string.IsNullOrWhiteSpace(description)
                ? extraMessage
                : $"{description}\n{extraMessage}";

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

        return IsNodeReachableFromStageStart(stage.startingNode, savedNode);
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
