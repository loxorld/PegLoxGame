using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MapGraphLayoutService
{
    private readonly MapDomainService domainService = new MapDomainService();

    public enum NodeVisualState
    {
        Current,
        Available,
        Upcoming,
        BossLocked,
        BossAvailable
    }

    public enum EdgeVisualState
    {
        Default,
        Available,
        BossPreview,
        BossAvailable
    }

    public readonly struct NodeLayout
    {
        public NodeLayout(MapNodeData node, Vector2 position, NodeVisualState visualState, bool isInteractable)
        {
            Node = node;
            Position = position;
            VisualState = visualState;
            IsInteractable = isInteractable;
        }

        public MapNodeData Node { get; }
        public Vector2 Position { get; }
        public NodeVisualState VisualState { get; }
        public bool IsInteractable { get; }
    }

    public readonly struct EdgeLayout
    {
        public EdgeLayout(MapNodeData fromNode, MapNodeData toNode, Vector2 start, Vector2 end, EdgeVisualState visualState)
        {
            FromNode = fromNode;
            ToNode = toNode;
            Start = start;
            End = end;
            VisualState = visualState;
        }

        public MapNodeData FromNode { get; }
        public MapNodeData ToNode { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }
        public EdgeVisualState VisualState { get; }
    }

    public readonly struct LayoutResult
    {
        public LayoutResult(string title, string subtitle, IReadOnlyList<NodeLayout> nodes, IReadOnlyList<EdgeLayout> edges)
        {
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Nodes = nodes ?? Array.Empty<NodeLayout>();
            Edges = edges ?? Array.Empty<EdgeLayout>();
        }

        public string Title { get; }
        public string Subtitle { get; }
        public IReadOnlyList<NodeLayout> Nodes { get; }
        public IReadOnlyList<EdgeLayout> Edges { get; }
    }

    private readonly struct RankedNode
    {
        public RankedNode(MapNodeData node, float orderScore)
        {
            Node = node;
            OrderScore = orderScore;
        }

        public MapNodeData Node { get; }
        public float OrderScore { get; }
    }

    private readonly struct StepNodeKey : IEquatable<StepNodeKey>
    {
        public StepNodeKey(MapNodeData node, int stepIndex)
        {
            Node = node;
            StepIndex = stepIndex;
        }

        public MapNodeData Node { get; }
        public int StepIndex { get; }

        public bool Equals(StepNodeKey other)
        {
            return Node == other.Node && StepIndex == other.StepIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is StepNodeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Node != null ? Node.GetHashCode() : 0) * 397) ^ StepIndex;
            }
        }
    }

    public LayoutResult Build(
        MapStage stage,
        MapNodeData currentNode,
        MapNodeData forcedBossNode,
        int stageIndex,
        int nodesVisited,
        int bossAfterNodes,
        Rect layoutRect,
        Vector2 nodeSize)
    {
        if (stage == null || stage.startingNode == null)
            return new LayoutResult("Mapa", string.Empty, Array.Empty<NodeLayout>(), Array.Empty<EdgeLayout>());

        float layoutWidth = Mathf.Max(layoutRect.width, 320f);
        float layoutHeight = Mathf.Max(layoutRect.height, 480f);
        Vector2 safeNodeSize = new Vector2(Mathf.Max(56f, nodeSize.x), Mathf.Max(34f, nodeSize.y));

        int regularStepCount = Mathf.Max(1, bossAfterNodes + 1);
        int currentStepIndex = Mathf.Clamp(nodesVisited, 0, regularStepCount - 1);
        List<List<MapNodeData>> steps = BuildRegularSteps(stage, stageIndex, regularStepCount);
        Dictionary<StepNodeKey, Vector2> positionsByKey = BuildPositions(
            steps,
            layoutWidth,
            layoutHeight,
            safeNodeSize,
            stage.bossNode != null,
            out Vector2 bossPosition);

        IReadOnlyList<MapNodeData> selectableTargets = domainService.ResolveSelectableNextNodes(
            stage,
            currentNode,
            forcedBossNode,
            stageIndex,
            currentStepIndex,
            maxChoices: 2);
        var availableTargets = new HashSet<MapNodeData>(selectableTargets);

        var nodes = new List<NodeLayout>(EstimateNodeCount(steps) + (stage.bossNode != null ? 1 : 0));
        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            List<MapNodeData> step = steps[stepIndex];
            if (step == null)
                continue;

            for (int nodeIndex = 0; nodeIndex < step.Count; nodeIndex++)
            {
                MapNodeData node = step[nodeIndex];
                StepNodeKey key = new StepNodeKey(node, stepIndex);
                if (node == null || !positionsByKey.TryGetValue(key, out Vector2 position))
                    continue;

                NodeVisualState visualState = ResolveRegularNodeState(node, stepIndex, currentStepIndex, currentNode, availableTargets);
                nodes.Add(new NodeLayout(node, position, visualState, visualState == NodeVisualState.Available));
            }
        }

        if (stage.bossNode != null)
        {
            bool bossAvailable = forcedBossNode == stage.bossNode;
            nodes.Add(new NodeLayout(
                stage.bossNode,
                bossPosition,
                bossAvailable ? NodeVisualState.BossAvailable : NodeVisualState.BossLocked,
                bossAvailable));
        }

        List<EdgeLayout> edges = BuildEdges(
            stage,
            steps,
            positionsByKey,
            stageIndex,
            currentNode,
            currentStepIndex,
            forcedBossNode,
            bossPosition,
            availableTargets);

        string title = string.IsNullOrWhiteSpace(stage.stageName) ? "Mapa" : stage.stageName.Trim();
        string subtitle = BuildSubtitle(nodesVisited, bossAfterNodes, forcedBossNode != null);
        return new LayoutResult(title, subtitle, nodes, edges);
    }

    private List<List<MapNodeData>> BuildRegularSteps(MapStage stage, int stageIndex, int stepCount)
    {
        var steps = new List<List<MapNodeData>>(stepCount)
        {
            new List<MapNodeData> { stage.startingNode }
        };

        for (int stepIndex = 1; stepIndex < stepCount; stepIndex++)
        {
            List<MapNodeData> previousStep = steps[stepIndex - 1];
            steps.Add(BuildNextStep(previousStep, stage, stageIndex, stepIndex - 1));
        }

        return steps;
    }

    private List<MapNodeData> BuildNextStep(List<MapNodeData> previousStep, MapStage stage, int stageIndex, int sourceStepIndex)
    {
        var uniqueNodes = new HashSet<MapNodeData>();
        var candidates = new List<MapNodeData>();

        if (previousStep == null)
            return candidates;

        for (int i = 0; i < previousStep.Count; i++)
        {
            MapNodeData sourceNode = previousStep[i];
            IReadOnlyList<MapNodeData> selectedTargets = domainService.ResolveSelectableNextNodes(
                stage,
                sourceNode,
                forcedBossNode: null,
                stageIndex,
                sourceStepIndex,
                maxChoices: 2);
            if (selectedTargets == null || selectedTargets.Count == 0)
                continue;

            for (int connectionIndex = 0; connectionIndex < selectedTargets.Count; connectionIndex++)
            {
                MapNodeData targetNode = selectedTargets[connectionIndex];
                if (uniqueNodes.Add(targetNode))
                    candidates.Add(targetNode);
            }
        }

        return OrderStep(candidates, previousStep);
    }

    private static List<MapNodeData> OrderStep(List<MapNodeData> candidates, List<MapNodeData> previousStep)
    {
        if (candidates == null || candidates.Count <= 1)
            return candidates ?? new List<MapNodeData>();

        var parentIndexByNode = new Dictionary<MapNodeData, int>();
        if (previousStep != null)
        {
            for (int i = 0; i < previousStep.Count; i++)
            {
                MapNodeData node = previousStep[i];
                if (node != null && !parentIndexByNode.ContainsKey(node))
                    parentIndexByNode[node] = i;
            }
        }

        var rankedNodes = new List<RankedNode>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
        {
            MapNodeData node = candidates[i];
            rankedNodes.Add(new RankedNode(node, CalculateOrderScore(node, previousStep, parentIndexByNode)));
        }

        rankedNodes.Sort((left, right) =>
        {
            int orderComparison = left.OrderScore.CompareTo(right.OrderScore);
            if (orderComparison != 0)
                return orderComparison;

            int typeComparison = GetNodeTypeOrder(left.Node).CompareTo(GetNodeTypeOrder(right.Node));
            if (typeComparison != 0)
                return typeComparison;

            return string.Compare(left.Node != null ? left.Node.title : string.Empty, right.Node != null ? right.Node.title : string.Empty, StringComparison.OrdinalIgnoreCase);
        });

        candidates.Clear();
        for (int i = 0; i < rankedNodes.Count; i++)
            candidates.Add(rankedNodes[i].Node);

        return candidates;
    }

    private static float CalculateOrderScore(MapNodeData node, List<MapNodeData> previousStep, Dictionary<MapNodeData, int> parentIndexByNode)
    {
        if (node == null || previousStep == null || previousStep.Count == 0)
            return 0f;

        float sum = 0f;
        int count = 0;
        for (int i = 0; i < previousStep.Count; i++)
        {
            MapNodeData parent = previousStep[i];
            if (parent == null || !ConnectsTo(parent, node))
                continue;

            if (parentIndexByNode.TryGetValue(parent, out int index))
            {
                sum += index;
                count++;
            }
        }

        if (count == 0)
            return GetNodeTypeOrder(node);

        return (sum / count) + (GetNodeTypeOrder(node) * 0.05f);
    }

    private static bool ConnectsTo(MapNodeData sourceNode, MapNodeData targetNode)
    {
        if (sourceNode?.nextNodes == null || targetNode == null)
            return false;

        for (int i = 0; i < sourceNode.nextNodes.Length; i++)
        {
            MapNodeConnection connection = sourceNode.nextNodes[i];
            if (connection != null && connection.targetNode == targetNode)
                return true;
        }

        return false;
    }

    private static Dictionary<StepNodeKey, Vector2> BuildPositions(
        List<List<MapNodeData>> steps,
        float layoutWidth,
        float layoutHeight,
        Vector2 nodeSize,
        bool hasBossNode,
        out Vector2 bossPosition)
    {
        var positions = new Dictionary<StepNodeKey, Vector2>();

        float horizontalPadding = Mathf.Max(nodeSize.x * 0.9f, 54f);
        float bottomPadding = Mathf.Max(nodeSize.y * 1.6f, 92f);
        float topPadding = Mathf.Max(nodeSize.y * 2.8f, 168f);
        float bossSpacing = hasBossNode ? Mathf.Max(nodeSize.y * 1.35f, 70f) : 0f;

        float left = (-layoutWidth * 0.5f) + horizontalPadding;
        float right = (layoutWidth * 0.5f) - horizontalPadding;
        float bottom = bottomPadding;
        float top = layoutHeight - topPadding - bossSpacing;

        int maxStepIndex = Mathf.Max(0, steps.Count - 1);
        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            List<MapNodeData> step = steps[stepIndex];
            if (step == null || step.Count == 0)
                continue;

            float y = maxStepIndex == 0
                ? bottom
                : Mathf.Lerp(bottom, top, stepIndex / (float)maxStepIndex);

            for (int nodeIndex = 0; nodeIndex < step.Count; nodeIndex++)
            {
                MapNodeData node = step[nodeIndex];
                positions[new StepNodeKey(node, stepIndex)] = new Vector2(
                    CalculateRowX(nodeIndex, step.Count, left, right, nodeSize.x),
                    y);
            }
        }

        float bossY = layoutHeight - Mathf.Max(nodeSize.y * 1.25f, 84f);
        if (bossY <= top)
            bossY = top + Mathf.Max(nodeSize.y * 1.3f, 72f);

        bossPosition = new Vector2(0f, bossY);
        return positions;
    }

    private static float CalculateRowX(int index, int count, float left, float right, float nodeWidth)
    {
        if (count <= 1)
            return 0f;

        float availableSpan = Mathf.Max(0f, right - left);
        float desiredSpan = Mathf.Max(nodeWidth * 1.45f * (count - 1), nodeWidth * 1.35f);
        float totalSpan = Mathf.Min(availableSpan, desiredSpan);
        float start = -totalSpan * 0.5f;
        float end = totalSpan * 0.5f;
        return Mathf.Lerp(start, end, index / (float)(count - 1));
    }

    private List<EdgeLayout> BuildEdges(
        MapStage stage,
        List<List<MapNodeData>> steps,
        Dictionary<StepNodeKey, Vector2> positionsByKey,
        int stageIndex,
        MapNodeData currentNode,
        int currentStepIndex,
        MapNodeData forcedBossNode,
        Vector2 bossPosition,
        HashSet<MapNodeData> availableTargets)
    {
        var edges = new List<EdgeLayout>();
        var targetsBuffer = new HashSet<MapNodeData>();

        for (int stepIndex = 0; stepIndex < steps.Count - 1; stepIndex++)
        {
            List<MapNodeData> sourceStep = steps[stepIndex];
            List<MapNodeData> destinationStep = steps[stepIndex + 1];
            if (sourceStep == null || sourceStep.Count == 0 || destinationStep == null || destinationStep.Count == 0)
                continue;

            for (int sourceIndex = 0; sourceIndex < sourceStep.Count; sourceIndex++)
            {
                MapNodeData sourceNode = sourceStep[sourceIndex];
                StepNodeKey sourceKey = new StepNodeKey(sourceNode, stepIndex);
                if (sourceNode == null || !positionsByKey.TryGetValue(sourceKey, out Vector2 sourcePosition))
                    continue;

                targetsBuffer.Clear();
                IReadOnlyList<MapNodeData> selectedTargets = domainService.ResolveSelectableNextNodes(
                    stage,
                    sourceNode,
                    forcedBossNode: null,
                    stageIndex,
                    stepIndex,
                    maxChoices: 2);
                for (int targetIndex = 0; targetIndex < selectedTargets.Count; targetIndex++)
                {
                    MapNodeData targetNode = selectedTargets[targetIndex];
                    if (!targetsBuffer.Add(targetNode))
                        continue;

                    StepNodeKey targetKey = new StepNodeKey(targetNode, stepIndex + 1);
                    if (!positionsByKey.TryGetValue(targetKey, out Vector2 targetPosition))
                        continue;

                    EdgeVisualState visualState = stepIndex == currentStepIndex
                        && forcedBossNode == null
                        && currentNode == sourceNode
                        && availableTargets.Contains(targetNode)
                        ? EdgeVisualState.Available
                        : EdgeVisualState.Default;

                    edges.Add(new EdgeLayout(sourceNode, targetNode, sourcePosition, targetPosition, visualState));
                }
            }
        }

        if (stage?.bossNode == null)
            return edges;

        if (forcedBossNode == stage.bossNode)
        {
            StepNodeKey currentKey = new StepNodeKey(currentNode, currentStepIndex);
            if (currentNode != null && positionsByKey.TryGetValue(currentKey, out Vector2 currentPosition))
                edges.Add(new EdgeLayout(currentNode, stage.bossNode, currentPosition, bossPosition, EdgeVisualState.BossAvailable));

            return edges;
        }

        int previewStepIndex = FindLastNonEmptyStep(steps);
        if (previewStepIndex < 0)
            return edges;

        List<MapNodeData> previewStep = steps[previewStepIndex];
        for (int i = 0; i < previewStep.Count; i++)
        {
            MapNodeData sourceNode = previewStep[i];
            StepNodeKey sourceKey = new StepNodeKey(sourceNode, previewStepIndex);
            if (sourceNode == null || !positionsByKey.TryGetValue(sourceKey, out Vector2 sourcePosition))
                continue;

            edges.Add(new EdgeLayout(sourceNode, stage.bossNode, sourcePosition, bossPosition, EdgeVisualState.BossPreview));
        }

        return edges;
    }

    private static int FindLastNonEmptyStep(List<List<MapNodeData>> steps)
    {
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            List<MapNodeData> step = steps[i];
            if (step != null && step.Count > 0)
                return i;
        }

        return -1;
    }

    private static int EstimateNodeCount(List<List<MapNodeData>> steps)
    {
        int count = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null)
                count += steps[i].Count;
        }

        return count;
    }

    private static NodeVisualState ResolveRegularNodeState(
        MapNodeData node,
        int stepIndex,
        int currentStepIndex,
        MapNodeData currentNode,
        HashSet<MapNodeData> availableTargets)
    {
        if (stepIndex == currentStepIndex && node == currentNode)
            return NodeVisualState.Current;

        if (stepIndex == currentStepIndex + 1 && availableTargets.Contains(node))
            return NodeVisualState.Available;

        return NodeVisualState.Upcoming;
    }

    private static string BuildSubtitle(int nodesVisited, int bossAfterNodes, bool bossAvailable)
    {
        if (bossAvailable || bossAfterNodes <= 0)
            return "Jefe disponible";

        int safeTarget = Mathf.Max(1, bossAfterNodes);
        int clampedProgress = Mathf.Clamp(nodesVisited, 0, safeTarget);
        return $"{clampedProgress}/{safeTarget} nodos para el jefe";
    }

    private static int GetNodeTypeOrder(MapNodeData node)
    {
        if (node == null)
            return int.MaxValue;

        return node.nodeType switch
        {
            NodeType.Combat => 0,
            NodeType.Event => 1,
            NodeType.Shop => 2,
            NodeType.Boss => 3,
            _ => 4
        };
    }
}
