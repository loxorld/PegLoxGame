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

    public IReadOnlyList<MapNodeData> ResolveSelectableNextNodes(
        MapStage stage,
        MapNodeData currentNode,
        MapNodeData forcedBossNode,
        int stageIndex,
        int stepIndex,
        int maxChoices = 2)
    {
        var options = new List<MapNodeData>();
        if (maxChoices <= 0)
            return options;

        if (forcedBossNode != null)
        {
            options.Add(forcedBossNode);
            return options;
        }

        CollectOrderedUniqueTargets(currentNode, stage != null ? stage.bossNode : null, stage != null ? stage.startingNode : null, options);
        if (options.Count <= maxChoices)
            return options;

        int seed = BuildChoiceSeed(stage, currentNode, stageIndex, stepIndex);
        return SelectDeterministicSubset(options, maxChoices, seed);
    }

    public bool IsSelectableNextNode(
        MapStage stage,
        MapNodeData currentNode,
        MapNodeData candidateNode,
        MapNodeData forcedBossNode,
        int stageIndex,
        int stepIndex,
        int maxChoices = 2)
    {
        if (candidateNode == null)
            return false;

        IReadOnlyList<MapNodeData> options = ResolveSelectableNextNodes(stage, currentNode, forcedBossNode, stageIndex, stepIndex, maxChoices);
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == candidateNode)
                return true;
        }

        return false;
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

    private static void CollectOrderedUniqueTargets(
        MapNodeData currentNode,
        MapNodeData excludedNode,
        MapNodeData additionallyExcludedNode,
        List<MapNodeData> destination)
    {
        destination?.Clear();
        if (currentNode?.nextNodes == null || destination == null)
            return;

        var seen = new HashSet<MapNodeData>();
        for (int i = 0; i < currentNode.nextNodes.Length; i++)
        {
            MapNodeConnection connection = currentNode.nextNodes[i];
            MapNodeData targetNode = connection != null ? connection.targetNode : null;
            if (targetNode == null || targetNode == excludedNode || targetNode == additionallyExcludedNode || !seen.Add(targetNode))
                continue;

            destination.Add(targetNode);
        }
    }

    private static List<MapNodeData> SelectDeterministicSubset(List<MapNodeData> candidates, int maxChoices, int seed)
    {
        var indices = new List<int>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
            indices.Add(i);

        var rng = new System.Random(seed);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int swapIndex = rng.Next(i + 1);
            (indices[i], indices[swapIndex]) = (indices[swapIndex], indices[i]);
        }

        if (indices.Count > maxChoices)
            indices.RemoveRange(maxChoices, indices.Count - maxChoices);

        indices.Sort();

        var selected = new List<MapNodeData>(indices.Count);
        for (int i = 0; i < indices.Count; i++)
            selected.Add(candidates[indices[i]]);

        return selected;
    }

    private static int BuildChoiceSeed(MapStage stage, MapNodeData currentNode, int stageIndex, int stepIndex)
    {
        uint hash = 2166136261u;
        hash = AppendHash(hash, stageIndex);
        hash = AppendHash(hash, stepIndex);
        hash = AppendHash(hash, BuildStableStageKey(stage));
        hash = AppendHash(hash, BuildStableNodeKey(currentNode));
        return (int)(hash & 0x7fffffff);
    }

    private static uint AppendHash(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
            return hash;
        }
    }

    private static uint AppendHash(uint hash, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return AppendHash(hash, 0);

        unchecked
        {
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash;
        }
    }

    private static string BuildStableStageKey(MapStage stage)
    {
        if (stage == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(stage.name))
            return stage.name.Trim();

        return stage.stageName != null ? stage.stageName.Trim() : string.Empty;
    }

    private static string BuildStableNodeKey(MapNodeData node)
    {
        if (node == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(node.PersistentId))
            return node.PersistentId.Trim();

        if (!string.IsNullOrWhiteSpace(node.name))
            return node.name.Trim();

        return !string.IsNullOrWhiteSpace(node.title)
            ? node.title.Trim()
            : node.nodeType.ToString();
    }
}
