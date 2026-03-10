using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapNavigationUI : MonoBehaviour
{
    public static MapNavigationUI Instance;

    [SerializeField] private MapManager mapManager;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Transform nodeContainer;
    [SerializeField] private GameObject nodePrefab;

    [Header("Graph Layout")]
    [SerializeField] private Vector2 graphNodeSize = new Vector2(172f, 76f);
    [SerializeField, Min(1f)] private float connectionThickness = 8f;

    [Header("Graph Colors")]
    [SerializeField] private Color currentNodeColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color availableNodeColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color upcomingNodeColor = new Color(1f, 1f, 1f, 0.38f);
    [SerializeField] private Color bossLockedColor = new Color(1f, 1f, 1f, 0.48f);
    [SerializeField] private Color bossAvailableColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color defaultConnectionColor = new Color(0.35f, 0.4f, 0.47f, 0.28f);
    [SerializeField] private Color availableConnectionColor = new Color(0.32f, 0.68f, 0.39f, 0.72f);
    [SerializeField] private Color bossPreviewConnectionColor = new Color(0.6f, 0.25f, 0.25f, 0.22f);
    [SerializeField] private Color bossAvailableConnectionColor = new Color(0.85f, 0.2f, 0.2f, 0.85f);

    private readonly MapGraphLayoutService layoutService = new MapGraphLayoutService();
    private RectTransform graphHost;
    private RectTransform nodesLayer;
    private MapGraphConnectionsGraphic connectionsGraphic;
    private MapNodeData lastShownNode;
    private MapNodeData lastForcedBossNode;
    private Vector2 activeGraphNodeSize;
    private float activeConnectionThickness;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MapNavigationUI] Ya existe una instancia activa. Se destruye el duplicado.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ApplyVisualDefaultsIfNeeded();
        Debug.Log("[MapNavigationUI] Awake ejecutado. Instancia seteada.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowNode(MapNodeData node)
    {
        if (node == null)
        {
            Debug.LogWarning("[MapNavigationUI] ShowNode llamado con MapNodeData nulo.");
            return;
        }

        if (!HasNodeRenderingSetup())
            return;

        MapManager mapManagerRef = ResolveMapManager();
        if (mapManagerRef == null)
            return;

        MapNodeData forcedBossNode = null;
        if (mapManagerRef.ShouldForceBossNode(out MapNodeData resolvedBossNode) && resolvedBossNode != null)
            forcedBossNode = resolvedBossNode;

        lastShownNode = node;
        lastForcedBossNode = forcedBossNode;
        RenderGraph(node, forcedBossNode);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled || lastShownNode == null)
            return;

        RenderGraph(lastShownNode, lastForcedBossNode);
    }

    private void OnNodeSelected(MapNodeData next)
    {
        if (next == null)
        {
            Debug.LogWarning("[MapNavigationUI] OnNodeSelected recibio un destino nulo.");
            return;
        }

        MapManager mapManagerRef = ResolveMapManager();
        if (mapManagerRef == null)
            return;

        mapManagerRef.SelectPath(next);
    }

    private void RenderGraph(MapNodeData currentNode, MapNodeData forcedBossNode)
    {
        MapManager mapManagerRef = ResolveMapManager();
        if (mapManagerRef == null || mapManagerRef.CurrentMapStage == null)
            return;

        PrepareGraphLayers();
        ClearNodes();
        Canvas.ForceUpdateCanvases();

        Rect graphRect = graphHost != null ? graphHost.rect : new Rect(0f, 0f, 1200f, 700f);
        if (graphRect.width <= 1f || graphRect.height <= 1f)
            graphRect = new Rect(0f, 0f, 1200f, 700f);

        GameFlowManager flow = GameFlowManager.Instance;
        int stageIndex = flow != null ? Mathf.Max(0, flow.CurrentStageIndex) : 0;
        int nodesVisited = flow != null ? flow.NodesVisited : 0;
        int bossAfterNodes = mapManagerRef.GetBossAfterNodes();
        activeGraphNodeSize = ResolveGraphNodeSize(graphRect, bossAfterNodes);
        activeConnectionThickness = ResolveConnectionThickness(activeGraphNodeSize);

        MapGraphLayoutService.LayoutResult layout = layoutService.Build(
            mapManagerRef.CurrentMapStage,
            currentNode,
            forcedBossNode,
            stageIndex,
            nodesVisited,
            bossAfterNodes,
            graphRect,
            activeGraphNodeSize);

        RenderConnections(layout);
        RenderNodes(layout);
        UpdateTitle(layout);
    }

    private MapManager ResolveMapManager()
    {
        if (mapManager != null)
            return mapManager;

        mapManager = ServiceRegistry.ResolveWithFallback(nameof(MapNavigationUI), nameof(mapManager), () => ServiceRegistry.LegacyFind<MapManager>());
        return mapManager;
    }

    private bool HasNodeRenderingSetup()
    {
        if (nodePrefab == null)
        {
            Debug.LogWarning("[MapNavigationUI] Falta nodePrefab para mostrar nodos.");
            return false;
        }

        if (nodePrefab.GetComponent<MapNodeUI>() == null)
        {
            Debug.LogWarning("[MapNavigationUI] nodePrefab no tiene MapNodeUI.");
            return false;
        }

        return true;
    }

    private void PrepareGraphLayers()
    {
        graphHost = ResolveGraphHost();
        if (graphHost == null)
            return;

        DisableLegacyLayout(graphHost.gameObject);

        nodesLayer = EnsureLayer("GraphNodes", 1);
        RectTransform connectionsLayer = EnsureLayer("GraphConnections", 0);
        connectionsGraphic = connectionsLayer != null ? connectionsLayer.GetComponent<MapGraphConnectionsGraphic>() : null;
        if (connectionsGraphic == null && connectionsLayer != null)
            connectionsGraphic = connectionsLayer.gameObject.AddComponent<MapGraphConnectionsGraphic>();

        if (connectionsGraphic != null)
            connectionsGraphic.raycastTarget = false;
    }

    private RectTransform ResolveGraphHost()
    {
        if (graphHost != null)
            return graphHost;

        RectTransform host = nodeContainer as RectTransform;
        if (host == null)
            host = transform as RectTransform;

        if (host == null)
            return null;

        host.anchorMin = Vector2.zero;
        host.anchorMax = Vector2.one;
        host.anchoredPosition = Vector2.zero;
        host.sizeDelta = Vector2.zero;
        host.localScale = Vector3.one;
        graphHost = host;
        return graphHost;
    }

    private static void DisableLegacyLayout(GameObject target)
    {
        if (target == null)
            return;

        ContentSizeFitter contentSizeFitter = target.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter != null)
            contentSizeFitter.enabled = false;

        VerticalLayoutGroup verticalLayoutGroup = target.GetComponent<VerticalLayoutGroup>();
        if (verticalLayoutGroup != null)
            verticalLayoutGroup.enabled = false;
    }

    private RectTransform EnsureLayer(string layerName, int siblingIndex)
    {
        Transform existing = graphHost != null ? graphHost.Find(layerName) : null;
        RectTransform layer = existing as RectTransform;
        if (layer == null && graphHost != null)
        {
            var layerObject = new GameObject(layerName, typeof(RectTransform));
            layer = layerObject.GetComponent<RectTransform>();
            layer.SetParent(graphHost, false);
        }

        if (layer == null)
            return null;

        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.anchoredPosition = Vector2.zero;
        layer.sizeDelta = Vector2.zero;
        layer.localScale = Vector3.one;
        layer.SetSiblingIndex(siblingIndex);
        return layer;
    }

    private void RenderConnections(MapGraphLayoutService.LayoutResult layout)
    {
        if (connectionsGraphic == null)
            return;

        var segments = new System.Collections.Generic.List<MapGraphConnectionsGraphic.Segment>(layout.Edges.Count);
        for (int i = 0; i < layout.Edges.Count; i++)
        {
            MapGraphLayoutService.EdgeLayout edge = layout.Edges[i];
            segments.Add(new MapGraphConnectionsGraphic.Segment(
                edge.Start,
                edge.End,
                ResolveEdgeColor(edge.VisualState),
                activeConnectionThickness > 0f ? activeConnectionThickness : connectionThickness));
        }

        connectionsGraphic.SetSegments(segments);
    }

    private void RenderNodes(MapGraphLayoutService.LayoutResult layout)
    {
        if (nodesLayer == null)
            return;

        for (int i = 0; i < layout.Nodes.Count; i++)
            TryCreateNode(layout.Nodes[i]);
    }

    private void UpdateTitle(MapGraphLayoutService.LayoutResult layout)
    {
        TMP_Text label = ResolveTitleLabel();
        if (label == null)
            return;

        if (string.IsNullOrWhiteSpace(layout.Subtitle))
            label.text = layout.Title;
        else
            label.text = $"<b>{layout.Title}</b>\n<size=58%>{layout.Subtitle}</size>";

        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = 34f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = new Color(0.22f, 0.16f, 0.1f, 1f);
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color(0.97f, 0.92f, 0.82f, 0.92f);
    }

    private TMP_Text ResolveTitleLabel()
    {
        if (titleLabel != null)
            return titleLabel;

        Transform parent = transform.parent;
        if (parent == null)
            return null;

        Transform titleTransform = parent.Find("MapTitle");
        titleLabel = titleTransform != null ? titleTransform.GetComponent<TMP_Text>() : null;
        return titleLabel;
    }

    private bool TryCreateNode(MapGraphLayoutService.NodeLayout nodeLayout)
    {
        if (nodesLayer == null || nodeLayout.Node == null)
            return false;

        GameObject nodeObject = Instantiate(nodePrefab, nodesLayer);
        if (nodeObject == null)
            return false;

        RectTransform rectTransform = nodeObject.transform as RectTransform;
        if (rectTransform != null)
            rectTransform.anchoredPosition = nodeLayout.Position;

        MapNodeUI nodeUI = nodeObject.GetComponent<MapNodeUI>();
        if (nodeUI == null)
        {
            Debug.LogWarning("[MapNavigationUI] Se instancio un nodePrefab sin MapNodeUI.");
            Destroy(nodeObject);
            return false;
        }

        nodeUI.Setup(nodeLayout.Node, nodeLayout.IsInteractable ? OnNodeSelected : null, ResolvePresentation(nodeLayout));
        return true;
    }

    private MapNodeUI.Presentation ResolvePresentation(MapGraphLayoutService.NodeLayout nodeLayout)
    {
        Vector2 baseNodeSize = activeGraphNodeSize == Vector2.zero ? graphNodeSize : activeGraphNodeSize;
        Color baseColor;
        Color labelColor;
        float scale;
        Vector2 size;
        bool showLabel;

        switch (nodeLayout.VisualState)
        {
            case MapGraphLayoutService.NodeVisualState.Current:
                baseColor = currentNodeColor;
                labelColor = new Color(0.17f, 0.12f, 0.07f, 1f);
                scale = 0.82f;
                size = baseNodeSize * 0.78f;
                showLabel = true;
                break;
            case MapGraphLayoutService.NodeVisualState.Available:
                baseColor = availableNodeColor;
                labelColor = new Color(0.15f, 0.19f, 0.11f, 1f);
                scale = 0.74f;
                size = baseNodeSize * 0.7f;
                showLabel = true;
                break;
            case MapGraphLayoutService.NodeVisualState.BossAvailable:
                baseColor = bossAvailableColor;
                labelColor = new Color(0.18f, 0.11f, 0.05f, 1f);
                scale = 0.92f;
                size = baseNodeSize * 0.88f;
                showLabel = true;
                break;
            case MapGraphLayoutService.NodeVisualState.BossLocked:
                baseColor = bossLockedColor;
                labelColor = new Color(0.34f, 0.17f, 0.13f, 1f);
                scale = 0.72f;
                size = baseNodeSize * 0.72f;
                showLabel = true;
                break;
            default:
                baseColor = upcomingNodeColor;
                labelColor = new Color(0.23f, 0.25f, 0.29f, 1f);
                scale = 0.42f;
                size = baseNodeSize * 0.42f;
                showLabel = false;
                break;
        }

        return new MapNodeUI.Presentation(
            size,
            baseColor,
            AdjustBrightness(baseColor, 1.08f),
            AdjustBrightness(baseColor, 0.86f),
            new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f, 0.5f),
            labelColor,
            showLabel,
            nodeLayout.IsInteractable,
            scale);
    }

    private Vector2 ResolveGraphNodeSize(Rect graphRect, int bossAfterNodes)
    {
        float safeHeight = Mathf.Max(360f, graphRect.height);
        float safeWidth = Mathf.Max(220f, graphRect.width);
        float targetHeight = Mathf.Clamp(
            safeHeight / Mathf.Max(9.5f, bossAfterNodes + 6.5f),
            38f,
            graphNodeSize.y);
        float targetWidth = Mathf.Clamp(
            targetHeight * 1.95f,
            76f,
            Mathf.Min(graphNodeSize.x, safeWidth * 0.26f));

        return new Vector2(targetWidth, targetHeight);
    }

    private float ResolveConnectionThickness(Vector2 nodeSize)
    {
        float fallbackSize = Mathf.Max(4f, connectionThickness);
        float derivedThickness = Mathf.Clamp(nodeSize.y * 0.075f, 3.5f, fallbackSize);
        return derivedThickness;
    }

    private void ApplyVisualDefaultsIfNeeded()
    {
        if (!LooksLikeLegacyPalette())
            return;

        currentNodeColor = new Color(0.95f, 0.87f, 0.64f, 1f);
        availableNodeColor = new Color(0.8f, 0.9f, 0.72f, 1f);
        upcomingNodeColor = new Color(0.43f, 0.32f, 0.22f, 0.26f);
        bossLockedColor = new Color(0.55f, 0.31f, 0.24f, 0.42f);
        bossAvailableColor = new Color(0.95f, 0.74f, 0.39f, 1f);
        defaultConnectionColor = new Color(0.39f, 0.28f, 0.18f, 0.2f);
        availableConnectionColor = new Color(0.52f, 0.67f, 0.39f, 0.7f);
        bossPreviewConnectionColor = new Color(0.58f, 0.31f, 0.2f, 0.26f);
        bossAvailableConnectionColor = new Color(0.89f, 0.56f, 0.21f, 0.86f);
    }

    private bool LooksLikeLegacyPalette()
    {
        return IsCloseColor(currentNodeColor, new Color(1f, 1f, 1f, 1f))
            && IsCloseColor(availableNodeColor, new Color(1f, 1f, 1f, 1f))
            && upcomingNodeColor.a <= 0.4f
            && bossLockedColor.a <= 0.5f;
    }

    private static bool IsCloseColor(Color left, Color right)
    {
        return Mathf.Abs(left.r - right.r) <= 0.03f
            && Mathf.Abs(left.g - right.g) <= 0.03f
            && Mathf.Abs(left.b - right.b) <= 0.03f
            && Mathf.Abs(left.a - right.a) <= 0.03f;
    }

    private Color ResolveEdgeColor(MapGraphLayoutService.EdgeVisualState state)
    {
        return state switch
        {
            MapGraphLayoutService.EdgeVisualState.Available => availableConnectionColor,
            MapGraphLayoutService.EdgeVisualState.BossPreview => bossPreviewConnectionColor,
            MapGraphLayoutService.EdgeVisualState.BossAvailable => bossAvailableConnectionColor,
            _ => defaultConnectionColor
        };
    }

    private static Color AdjustBrightness(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a);
    }

    private void ClearNodes()
    {
        if (nodesLayer != null)
        {
            for (int i = nodesLayer.childCount - 1; i >= 0; i--)
                Destroy(nodesLayer.GetChild(i).gameObject);
        }

        if (connectionsGraphic != null)
            connectionsGraphic.SetSegments(System.Array.Empty<MapGraphConnectionsGraphic.Segment>());
    }
}
