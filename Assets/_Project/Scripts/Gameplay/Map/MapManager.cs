using UnityEngine;

public partial class MapManager : MonoBehaviour
{
    [SerializeField] private MapStage currentMapStage;
    [SerializeField] private MapStage[] stageSequence;
    [SerializeField] private GameFlowManager gameFlowManager;
    [SerializeField] private RunBalanceConfig balanceConfig;

    [Header("Event Settings")]
    [SerializeField, Min(0)] private int eventCoinsRewardMin = 5;
    [SerializeField, Min(0)] private int eventCoinsRewardMax = 15;
    [SerializeField, Min(0)] private int eventCoinsPenaltyMin = 3;
    [SerializeField, Min(0)] private int eventCoinsPenaltyMax = 10;
    [SerializeField, Min(0)] private int eventHealMin = 3;
    [SerializeField, Min(0)] private int eventHealMax = 6;
    [SerializeField, Min(0)] private int eventDamageMin = 2;
    [SerializeField, Min(0)] private int eventDamageMax = 5;

    [Header("Modal UI")]
    [SerializeField] private MapPresentationController presentationController;
    [SerializeField] private MonoBehaviour mapNodeModalView;

    [Header("Shop Settings")]
    [SerializeField] private ShopConfig shopConfig;
    [SerializeField, Min(0)] private int shopHealCost = 10;
    [SerializeField, Min(1)] private int shopHealAmount = 8;
    [SerializeField, Min(0)] private int shopOrbUpgradeCost = 15;
    [SerializeField] private ShopService shopService;
    [SerializeField] private OrbManager orbManager;
    [SerializeField] private RelicManager relicManager;

    private readonly MapDomainService domainService = new MapDomainService();
    private IEventRngService eventRngService;
    private MapNodeData currentNode;

    public MapStage CurrentMapStage => currentMapStage;

    private void Awake()
    {
        if (shopConfig == null)
            shopConfig = Resources.Load<ShopConfig>("ShopConfig_Default");

        eventRngService ??= new UnityEventRngService();
        ServiceRegistry.Register(this);
    }

    private void Start()
    {
        StartStageForCurrentRun();
    }

    public void InjectEventRngService(IEventRngService injectedEventRngService)
    {
        if (injectedEventRngService != null)
            eventRngService = injectedEventRngService;
    }

    public void InjectDependencies(GameFlowManager injectedGameFlowManager, ShopService injectedShopService, IMapNodeModalView injectedMapNodeModalView, OrbManager injectedOrbManager, RelicManager injectedRelicManager)
    {
        if (injectedGameFlowManager != null)
            gameFlowManager = injectedGameFlowManager;

        if (injectedShopService != null)
            shopService = injectedShopService;

        if (injectedMapNodeModalView != null)
            mapNodeModalView = injectedMapNodeModalView as MonoBehaviour;

        if (injectedOrbManager != null)
            orbManager = injectedOrbManager;

        if (injectedRelicManager != null)
            relicManager = injectedRelicManager;

        EnsurePresentationController();
        presentationController?.InjectModalView(injectedMapNodeModalView);
    }

    public void StartStageForCurrentRun()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        int stageIndex = flow != null ? Mathf.Max(0, flow.CurrentStageIndex) : 0;
        MapStage stageToLoad = ResolveStageByIndex(stageIndex);
        ValidateStageConsistency(stageIndex, stageToLoad, flow);
        if (stageToLoad == null)
        {
            Debug.LogWarning("[MapManager] No hay MapStage asignado.");
            return;
        }

        StartStage(stageToLoad);
    }

    public void StartStage(MapStage stage)
    {
        if (stage == null)
        {
            Debug.LogWarning("[MapManager] StartStage llamado con MapStage nulo.");
            return;
        }

        currentMapStage = stage;

        GameFlowManager flow = ResolveGameFlowManager();
        if (flow != null)
        {
            int stageIndex = domainService.GetStageIndex(stageSequence, stage);
            if (stageIndex >= 0)
                flow.SetCurrentStageIndex(stageIndex);
        }

        MapNodeData savedNode = ShouldResumeFromSavedNode(flow) ? flow.SavedMapNode : null;
        MapDomainService.NodeResolution nodeResolution = domainService.ResolveCurrentNode(stage, savedNode);
        if (nodeResolution.ShouldClearSavedNode)
            flow?.SaveMapNode(null);

        currentNode = nodeResolution.Node;
        if (currentNode == null)
        {
            Debug.LogWarning("[MapManager] El MapStage no tiene startingNode asignado.");
            return;
        }

        OpenNode(currentNode);
    }

    private static bool ShouldResumeFromSavedNode(GameFlowManager flow)
    {
        if (flow == null || flow.SavedMapNode == null)
            return false;

        // In a fresh run we always restart from the stage entry node.
        return flow.NodesVisited > 0;
    }

    public void OpenNode(MapNodeData node)
    {
        if (node == null)
        {
            Debug.LogWarning("[MapManager] OpenNode llamado con MapNodeData nulo.");
            return;
        }

        currentNode = node;

        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontro GameFlowManager en la escena.");
            return;
        }

        flow.SetState(GameState.MapNavigation);
        EnsurePresentationController();
        presentationController?.OpenNode(node);
    }

    public void SelectPath(MapNodeData nextNode)
    {
        if (nextNode == null)
        {
            Debug.LogWarning("[MapManager] SelectPath recibido con destino nulo.");
            return;
        }

        GameFlowManager flow = ResolveGameFlowManager();
        int nodesVisited = flow != null ? flow.NodesVisited : 0;
        int stageIndex = GetStageIndexForBalance(flow);
        MapNodeData forcedBossNode = null;
        if (domainService.ShouldForceBossNode(currentMapStage, nodesVisited, GetBossAfterNodes(), out MapNodeData resolvedBossNode))
            forcedBossNode = resolvedBossNode;

        if (!domainService.IsSelectableNextNode(currentMapStage, currentNode, nextNode, forcedBossNode, stageIndex, nodesVisited, maxChoices: 2))
        {
            Debug.LogWarning($"[MapManager] Seleccion invalida. El nodo '{nextNode.title}' no forma parte de las opciones activas del mapa.");
            return;
        }

        if (flow != null)
        {
            flow.SaveMapNode(nextNode);
            flow.IncrementNodesVisited();
            flow.SaveRun();
        }
        else
        {
            Debug.LogWarning("[MapManager] No se encontro GameFlowManager al guardar MapNode.");
        }

        currentNode = nextNode;
        ConfirmNode();
    }

    public void ConfirmNode()
    {
        if (currentNode == null)
        {
            Debug.LogWarning("[MapManager] ConfirmNode llamado sin nodo actual.");
            return;
        }

        switch (currentNode.nodeType)
        {
            case NodeType.Combat:
                StartCombatEncounter();
                break;
            case NodeType.Event:
                HandleEventNode();
                break;
            case NodeType.Shop:
                HandleShopNode();
                break;
            case NodeType.Boss:
                HandleBossNode();
                break;
        }
    }

    public bool ShouldForceBossNode(out MapNodeData bossNode)
    {
        GameFlowManager flow = ResolveGameFlowManager();
        int nodesVisited = flow != null ? flow.NodesVisited : 0;
        int bossAfterNodes = GetBossAfterNodes();
        return domainService.ShouldForceBossNode(currentMapStage, nodesVisited, bossAfterNodes, out bossNode);
    }

    public int GetBossAfterNodes()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        int stageIndex = GetStageIndexForBalance(flow);
        return domainService.GetBossAfterNodes(currentMapStage, ResolveBalanceConfig(), stageIndex);
    }
}
