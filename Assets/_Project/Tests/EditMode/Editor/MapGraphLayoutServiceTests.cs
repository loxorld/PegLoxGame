using NUnit.Framework;
using UnityEngine;

public class MapGraphLayoutServiceTests
{
    private MapStage stage;
    private MapNodeData startNode;
    private MapNodeData combatNode;
    private MapNodeData eventNode;
    private MapNodeData bossNode;

    [TearDown]
    public void TearDown()
    {
        DestroyIfNeeded(stage);
        DestroyIfNeeded(startNode);
        DestroyIfNeeded(combatNode);
        DestroyIfNeeded(eventNode);
        DestroyIfNeeded(bossNode);
    }

    [Test]
    public void Build_UnrollsTheStageAcrossProgressRows()
    {
        CreateReusableBranchingStage();
        var service = new MapGraphLayoutService();

        MapGraphLayoutService.LayoutResult result = service.Build(
            stage,
            currentNode: startNode,
            forcedBossNode: null,
            stageIndex: 0,
            nodesVisited: 0,
            bossAfterNodes: 4,
            layoutRect: new Rect(0f, 0f, 420f, 760f),
            nodeSize: new Vector2(110f, 52f));

        Assert.AreEqual(10, result.Nodes.Count);
        Assert.AreEqual(1, CountNodes(result, MapGraphLayoutService.NodeVisualState.Current));
        Assert.AreEqual(2, CountNodes(result, MapGraphLayoutService.NodeVisualState.Available));
        Assert.AreEqual(2, CountEdges(result, MapGraphLayoutService.EdgeVisualState.BossPreview));

        Vector2 startPosition = FindNodePosition(result, MapGraphLayoutService.NodeVisualState.Current);
        Vector2 bossPosition = FindBossPosition(result, MapGraphLayoutService.NodeVisualState.BossLocked);
        Assert.That(bossPosition.y, Is.GreaterThan(startPosition.y));
    }

    [Test]
    public void Build_PlacesAvailableNodesAboveTheCurrentRow()
    {
        CreateReusableBranchingStage();
        var service = new MapGraphLayoutService();

        MapGraphLayoutService.LayoutResult result = service.Build(
            stage,
            currentNode: combatNode,
            forcedBossNode: null,
            stageIndex: 0,
            nodesVisited: 2,
            bossAfterNodes: 4,
            layoutRect: new Rect(0f, 0f, 420f, 760f),
            nodeSize: new Vector2(110f, 52f));

        Vector2 currentPosition = FindNodePosition(result, MapGraphLayoutService.NodeVisualState.Current);
        int availableCount = 0;
        for (int i = 0; i < result.Nodes.Count; i++)
        {
            MapGraphLayoutService.NodeLayout node = result.Nodes[i];
            if (node.VisualState != MapGraphLayoutService.NodeVisualState.Available)
                continue;

            availableCount++;
            Assert.That(node.Position.y, Is.GreaterThan(currentPosition.y));
        }

        Assert.AreEqual(2, availableCount);
        Assert.AreEqual(2, CountEdges(result, MapGraphLayoutService.EdgeVisualState.Available));
    }

    [Test]
    public void Build_MarksBossAsAvailable_WhenBossIsForced()
    {
        CreateReusableBranchingStage();
        var service = new MapGraphLayoutService();

        MapGraphLayoutService.LayoutResult result = service.Build(
            stage,
            currentNode: eventNode,
            forcedBossNode: bossNode,
            stageIndex: 0,
            nodesVisited: 4,
            bossAfterNodes: 4,
            layoutRect: new Rect(0f, 0f, 420f, 760f),
            nodeSize: new Vector2(110f, 52f));

        Assert.AreEqual(1, CountNodes(result, MapGraphLayoutService.NodeVisualState.BossAvailable));
        Assert.AreEqual(0, CountNodes(result, MapGraphLayoutService.NodeVisualState.Available));
        Assert.AreEqual(1, CountEdges(result, MapGraphLayoutService.EdgeVisualState.BossAvailable));
    }

    private void CreateReusableBranchingStage()
    {
        startNode = CreateNode("Inicio", NodeType.Combat);
        combatNode = CreateNode("Combate", NodeType.Combat);
        eventNode = CreateNode("Ruinas", NodeType.Event);
        bossNode = CreateNode("Jefe", NodeType.Boss);

        startNode.nextNodes = new[]
        {
            new MapNodeConnection { targetNode = combatNode },
            new MapNodeConnection { targetNode = eventNode }
        };

        combatNode.nextNodes = new[]
        {
            new MapNodeConnection { targetNode = combatNode },
            new MapNodeConnection { targetNode = eventNode }
        };

        eventNode.nextNodes = new[]
        {
            new MapNodeConnection { targetNode = combatNode },
            new MapNodeConnection { targetNode = eventNode }
        };

        stage = ScriptableObject.CreateInstance<MapStage>();
        stage.stageName = "Bosque";
        stage.startingNode = startNode;
        stage.bossNode = bossNode;
        stage.bossAfterNodes = 4;
    }

    private static MapNodeData CreateNode(string title, NodeType nodeType)
    {
        MapNodeData node = ScriptableObject.CreateInstance<MapNodeData>();
        node.title = title;
        node.nodeType = nodeType;
        return node;
    }

    private static Vector2 FindNodePosition(MapGraphLayoutService.LayoutResult result, MapGraphLayoutService.NodeVisualState state)
    {
        for (int i = 0; i < result.Nodes.Count; i++)
        {
            if (result.Nodes[i].VisualState == state)
                return result.Nodes[i].Position;
        }

        Assert.Fail($"No se encontro un nodo con estado {state}.");
        return Vector2.zero;
    }

    private static Vector2 FindBossPosition(MapGraphLayoutService.LayoutResult result, MapGraphLayoutService.NodeVisualState state)
    {
        for (int i = 0; i < result.Nodes.Count; i++)
        {
            MapGraphLayoutService.NodeLayout node = result.Nodes[i];
            if (node.VisualState == state && node.Node != null && node.Node.nodeType == NodeType.Boss)
                return node.Position;
        }

        Assert.Fail($"No se encontro el boss con estado {state}.");
        return Vector2.zero;
    }

    private static int CountNodes(MapGraphLayoutService.LayoutResult result, MapGraphLayoutService.NodeVisualState state)
    {
        int count = 0;
        for (int i = 0; i < result.Nodes.Count; i++)
        {
            if (result.Nodes[i].VisualState == state)
                count++;
        }

        return count;
    }

    private static int CountEdges(MapGraphLayoutService.LayoutResult result, MapGraphLayoutService.EdgeVisualState state)
    {
        int count = 0;
        for (int i = 0; i < result.Edges.Count; i++)
        {
            if (result.Edges[i].VisualState == state)
                count++;
        }

        return count;
    }

    private static void DestroyIfNeeded(Object obj)
    {
        if (obj != null)
            Object.DestroyImmediate(obj);
    }
}
