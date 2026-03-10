using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapNavigationUI : MonoBehaviour
{
    public static MapNavigationUI Instance;

    private static readonly Vector2 BottomCenterAnchor = new Vector2(0.5f, 0f);

    [SerializeField] private MapManager mapManager;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Transform nodeContainer;
    [SerializeField] private GameObject nodePrefab;

    [Header("Graph Layout")]
    [SerializeField] private Vector2 graphNodeSize = new Vector2(228f, 108f);
    [SerializeField, Min(1f)] private float connectionThickness = 12f;
    [SerializeField, Range(2f, 4f)] private float visibleStepWindow = 3f;
    [SerializeField, Range(0.08f, 0.34f)] private float currentNodeViewportAnchor = 0.18f;

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
    private RectTransform graphViewport;
    private RectTransform nodesLayer;
    private ScrollRect graphScrollRect;
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
        if (graphHost == null)
            return;

        ClearNodes();
        Canvas.ForceUpdateCanvases();

        Rect viewportRect = graphViewport != null ? graphViewport.rect : graphHost.rect;
        if (viewportRect.width <= 1f || viewportRect.height <= 1f)
            viewportRect = new Rect(0f, 0f, 1200f, 700f);

        GameFlowManager flow = GameFlowManager.Instance;
        int stageIndex = flow != null ? Mathf.Max(0, flow.CurrentStageIndex) : 0;
        int nodesVisited = flow != null ? flow.NodesVisited : 0;
        int bossAfterNodes = mapManagerRef.GetBossAfterNodes();
        activeGraphNodeSize = ResolveGraphNodeSize(viewportRect);
        float contentHeight = ResolveGraphContentHeight(
            viewportRect.height,
            activeGraphNodeSize,
            bossAfterNodes,
            mapManagerRef.CurrentMapStage.bossNode != null);
        ApplyGraphContentSizing(contentHeight);
        Canvas.ForceUpdateCanvases();

        Rect graphRect = new Rect(0f, 0f, Mathf.Max(320f, viewportRect.width), Mathf.Max(viewportRect.height, contentHeight));
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
        Canvas.ForceUpdateCanvases();
        FocusGraphOnCurrent(layout);
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
        EnsureScrollableViewport();
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
        RectTransform host = ResolveOrCreateGraphHost();

        if (host == null)
            return null;

        host.anchorMin = new Vector2(0f, 0f);
        host.anchorMax = new Vector2(1f, 0f);
        host.pivot = BottomCenterAnchor;
        host.anchoredPosition = Vector2.zero;
        host.localScale = Vector3.one;
        graphHost = host;
        return graphHost;
    }

    private RectTransform ResolveOrCreateGraphHost()
    {
        RectTransform host = nodeContainer as RectTransform;
        if (host == null)
        {
            Transform existing = (graphViewport != null ? graphViewport : transform).Find("GraphContent");
            host = existing as RectTransform;
            if (host == null)
            {
                var hostObject = new GameObject("GraphContent", typeof(RectTransform));
                host = hostObject.GetComponent<RectTransform>();
                host.SetParent(graphViewport != null ? graphViewport : transform, false);
            }

            nodeContainer = host;
        }

        if (graphViewport != null && host.parent != graphViewport)
            host.SetParent(graphViewport, false);

        return host;
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
            var layerObject = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer));
            layer = layerObject.GetComponent<RectTransform>();
            layer.SetParent(graphHost, false);
        }

        if (layer == null)
            return null;

        EnsureCanvasRenderer(layer.gameObject);

        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.pivot = BottomCenterAnchor;
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
        {
            rectTransform.anchorMin = BottomCenterAnchor;
            rectTransform.anchorMax = BottomCenterAnchor;
            rectTransform.anchoredPosition = nodeLayout.Position;
            rectTransform.localScale = Vector3.one;
        }

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
                scale = 1.04f;
                size = baseNodeSize;
                showLabel = true;
                break;
            case MapGraphLayoutService.NodeVisualState.Available:
                baseColor = availableNodeColor;
                labelColor = new Color(0.15f, 0.19f, 0.11f, 1f);
                scale = 1f;
                size = baseNodeSize * 0.94f;
                showLabel = true;
                break;
            case MapGraphLayoutService.NodeVisualState.BossAvailable:
                baseColor = bossAvailableColor;
                labelColor = new Color(0.18f, 0.11f, 0.05f, 1f);
                scale = 1.08f;
                size = baseNodeSize * 1.04f;
                showLabel = true;
                break;
            case MapGraphLayoutService.NodeVisualState.BossLocked:
                baseColor = bossLockedColor;
                labelColor = new Color(0.34f, 0.17f, 0.13f, 1f);
                scale = 0.98f;
                size = baseNodeSize * 0.92f;
                showLabel = true;
                break;
            default:
                baseColor = upcomingNodeColor;
                labelColor = new Color(0.23f, 0.25f, 0.29f, 1f);
                scale = 0.96f;
                size = baseNodeSize * 0.78f;
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

    private Vector2 ResolveGraphNodeSize(Rect graphRect)
    {
        float safeHeight = Mathf.Max(360f, graphRect.height);
        float safeWidth = Mathf.Max(320f, graphRect.width);
        float laneHeight = safeHeight / Mathf.Max(2.35f, visibleStepWindow);
        float targetHeight = Mathf.Clamp(
            laneHeight * 0.42f,
            64f,
            graphNodeSize.y);
        float targetWidth = Mathf.Clamp(
            targetHeight * 2.08f,
            140f,
            Mathf.Min(graphNodeSize.x, safeWidth * 0.46f));

        return new Vector2(targetWidth, targetHeight);
    }

    private float ResolveGraphContentHeight(float viewportHeight, Vector2 nodeSize, int bossAfterNodes, bool hasBossNode)
    {
        float safeViewportHeight = Mathf.Max(360f, viewportHeight);
        int regularStepCount = Mathf.Max(1, bossAfterNodes + 1);
        int maxStepIndex = Mathf.Max(0, regularStepCount - 1);
        float bottomPadding = Mathf.Max(nodeSize.y * 1.6f, 92f);
        float topPadding = Mathf.Max(nodeSize.y * 2.8f, 168f);
        float bossSpacing = hasBossNode ? Mathf.Max(nodeSize.y * 1.35f, 70f) : 0f;
        float desiredStepSpacing = Mathf.Max(
            nodeSize.y * 2.15f,
            safeViewportHeight / Mathf.Max(2f, visibleStepWindow));

        return Mathf.Max(
            safeViewportHeight,
            bottomPadding + topPadding + bossSpacing + (desiredStepSpacing * maxStepIndex));
    }

    private void ApplyGraphContentSizing(float contentHeight)
    {
        if (graphHost == null)
            return;

        graphHost.anchorMin = new Vector2(0f, 0f);
        graphHost.anchorMax = new Vector2(1f, 0f);
        graphHost.pivot = BottomCenterAnchor;
        graphHost.anchoredPosition = Vector2.zero;
        graphHost.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(contentHeight, graphViewport != null ? graphViewport.rect.height : contentHeight));
    }

    private float ResolveConnectionThickness(Vector2 nodeSize)
    {
        float fallbackSize = Mathf.Max(6f, connectionThickness);
        float derivedThickness = Mathf.Clamp(nodeSize.y * 0.11f, 5f, fallbackSize);
        return derivedThickness;
    }

    private void EnsureScrollableViewport()
    {
        RectTransform rootRect = transform as RectTransform;
        if (rootRect == null)
            return;

        graphScrollRect = GetComponent<ScrollRect>();
        if (graphScrollRect == null)
            graphScrollRect = gameObject.AddComponent<ScrollRect>();

        graphScrollRect.horizontal = false;
        graphScrollRect.vertical = true;
        graphScrollRect.movementType = ScrollRect.MovementType.Clamped;
        graphScrollRect.scrollSensitivity = 36f;
        graphScrollRect.inertia = true;
        graphScrollRect.decelerationRate = 0.1f;

        Transform existingViewport = transform.Find("GraphViewport");
        graphViewport = existingViewport as RectTransform;
        if (graphViewport == null)
        {
            var viewportObject = new GameObject("GraphViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            graphViewport = viewportObject.GetComponent<RectTransform>();
            graphViewport.SetParent(transform, false);
        }

        EnsureCanvasRenderer(graphViewport.gameObject);
        graphViewport.anchorMin = Vector2.zero;
        graphViewport.anchorMax = Vector2.one;
        graphViewport.pivot = BottomCenterAnchor;
        graphViewport.anchoredPosition = Vector2.zero;
        graphViewport.sizeDelta = Vector2.zero;
        graphViewport.localScale = Vector3.one;
        graphViewport.SetAsFirstSibling();

        Image viewportImage = graphViewport.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = graphViewport.gameObject.AddComponent<Image>();

        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        RectMask2D mask = graphViewport.GetComponent<RectMask2D>();
        if (mask == null)
            mask = graphViewport.gameObject.AddComponent<RectMask2D>();

        mask.padding = Vector4.zero;
        graphScrollRect.viewport = graphViewport;
        graphScrollRect.content = ResolveOrCreateGraphHost();
    }

    private static void EnsureCanvasRenderer(GameObject target)
    {
        if (target == null || target.GetComponent<CanvasRenderer>() != null)
            return;

        target.AddComponent<CanvasRenderer>();
    }

    private void FocusGraphOnCurrent(MapGraphLayoutService.LayoutResult layout)
    {
        if (graphHost == null || graphViewport == null || graphScrollRect == null)
            return;

        float viewportHeight = Mathf.Max(0f, graphViewport.rect.height);
        float contentHeight = Mathf.Max(viewportHeight, graphHost.rect.height);
        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
        if (maxOffset <= 0.5f)
        {
            graphHost.anchoredPosition = Vector2.zero;
            graphScrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        Vector2 focusPosition = ResolveFocusPosition(layout);
        float viewportAnchorOffset = Mathf.Clamp(
            viewportHeight * currentNodeViewportAnchor,
            32f,
            viewportHeight * 0.42f);
        float targetOffset = Mathf.Clamp(focusPosition.y - viewportAnchorOffset, 0f, maxOffset);

        graphScrollRect.StopMovement();
        graphHost.anchoredPosition = new Vector2(0f, targetOffset);
        graphScrollRect.verticalNormalizedPosition = targetOffset / maxOffset;
    }

    private static Vector2 ResolveFocusPosition(MapGraphLayoutService.LayoutResult layout)
    {
        for (int i = 0; i < layout.Nodes.Count; i++)
        {
            if (layout.Nodes[i].VisualState == MapGraphLayoutService.NodeVisualState.Current)
                return layout.Nodes[i].Position;
        }

        for (int i = 0; i < layout.Nodes.Count; i++)
        {
            if (layout.Nodes[i].VisualState == MapGraphLayoutService.NodeVisualState.Available)
                return layout.Nodes[i].Position;
        }

        return layout.Nodes.Count > 0 ? layout.Nodes[0].Position : Vector2.zero;
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
