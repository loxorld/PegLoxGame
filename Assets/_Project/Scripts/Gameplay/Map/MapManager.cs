using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapManager : MonoBehaviour
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
    [SerializeField, Min(0)] private int shopHealCost = 10;
    [SerializeField, Min(1)] private int shopHealAmount = 8;
    [SerializeField, Min(0)] private int shopOrbUpgradeCost = 15;
    [SerializeField] private ShopService shopService;

    private readonly MapDomainService domainService = new MapDomainService();
    private MapNodeData currentNode;

    public MapStage CurrentMapStage => currentMapStage;

    private void Awake()
    {
        ServiceRegistry.Register(this);
    }

    private void Start()
    {
        StartStageForCurrentRun();
    }

    public void InjectDependencies(GameFlowManager injectedGameFlowManager, ShopService injectedShopService, IMapNodeModalView injectedMapNodeModalView)
    {
        if (injectedGameFlowManager != null)
            gameFlowManager = injectedGameFlowManager;

        if (injectedShopService != null)
            shopService = injectedShopService;

        if (injectedMapNodeModalView != null)
            mapNodeModalView = injectedMapNodeModalView as MonoBehaviour;

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

        // En una run fresca (an sin nodos elegidos) siempre queremos arrancar desde
        // el nodo inicial del stage, aunque exista un save viejo cargado en memoria.
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
            Debug.LogWarning("[MapManager] No se encontró GameFlowManager en la escena.");
            return;
        }

        flow.SetState(GameState.MapNavigation);
        EnsurePresentationController();
        presentationController?.OpenNode(node);
    }

    public void SelectPath(MapNodeData nextNode)
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow != null)
        {
            flow.SaveMapNode(nextNode);
            flow.IncrementNodesVisited();
            flow.SaveRun();
        }
        else
        {
            Debug.LogWarning("[MapManager] No se encontró GameFlowManager al guardar MapNode.");
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

    private void StartCombatEncounter()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontró GameFlowManager en la escena.");
            return;
        }

        flow.SetState(GameState.Combat);
        SceneManager.LoadScene(SceneCatalog.Load().CombatScene, LoadSceneMode.Single);
    }

    private void HandleEventNode()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontr GameFlowManager en la escena.");
            return;
        }

        int balanceStageIndex = GetStageIndexForBalance(flow);
        MapDomainService.EventScenarioOutcome eventOutcome = domainService.BuildEventOutcome(
            currentNode,
            ResolveBalanceConfig(),
            balanceStageIndex,
            eventCoinsRewardMin,
            eventCoinsRewardMax,
            eventCoinsPenaltyMin,
            eventCoinsPenaltyMax,
            eventHealMin,
            eventHealMax,
            eventDamageMin,
            eventDamageMax);

        EnsurePresentationController();
        presentationController?.ShowEvent(
            eventOutcome,
            option =>
            {
                float roll = option.Probability.HasValue ? UnityEngine.Random.value : 0f;
                MapDomainService.EventResolutionOutcome resolvedOutcome = domainService.ResolveEventOptionOutcome(option, roll);

                flow.AddCoins(resolvedOutcome.CoinDelta);
                flow.ModifySavedHP(resolvedOutcome.HpDelta);
                flow.SaveRun();

                presentationController?.ShowGenericResult(
                    eventOutcome.Title,
                    resolvedOutcome.ResultDescription,
                    () => OpenNode(currentNode));
            },
            () => OpenNode(currentNode));
    }


    private void HandleShopNode()
    {
        ShowShopModal(null);
    }

    private void ShowShopModal(string extraMessage)
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontr GameFlowManager en la escena.");
            return;
        }

        ShopService resolvedShopService = ResolveShopService();
        if (resolvedShopService == null)
            return;

        OrbManager orbManager = ResolveOrbManagerForShop();
        if (orbManager == null)
            return;

        int balanceStageIndex = GetStageIndexForBalance(flow);

        MapDomainService.ShopOutcome shopOutcome = domainService.BuildShopOutcome(
            currentNode,
            ResolveBalanceConfig(),
            balanceStageIndex,
            flow.Coins,
            shopHealCost,
            shopHealAmount,
            shopOrbUpgradeCost,
            extraMessage);

        string shopId = BuildShopId(currentNode, flow, balanceStageIndex);

        List<ShopService.ShopOptionData> shopOptions = resolvedShopService.GetShopOptionsForNode(
            flow,
            orbManager,
            ResolveBalanceConfig(),
            balanceStageIndex,
            shopId,
            shopOutcome.HealCost,
            shopOutcome.HealAmount,
            shopOutcome.OrbUpgradeCost,
            ShowShopModal,
            () => OpenNode(currentNode));

        EnsurePresentationController();
        presentationController?.ShowShopModal(shopOutcome, shopOptions);
    }

    private static string BuildShopId(MapNodeData node, GameFlowManager flow, int stageIndex)
    {
        string nodeId = node != null && !string.IsNullOrWhiteSpace(node.name) ? node.name : "unknown-node";
        int encounterIndex = flow != null ? flow.EncounterIndex : 0;
        return $"shop_{stageIndex}_{encounterIndex}_{nodeId}";
    }
    private void HandleBossNode()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontró GameFlowManager en la escena.");
            return;
        }

        if (currentNode != null)
        {
            flow.SetBossEncounter(
                currentNode.bossEnemy,
                currentNode.bossHpMultiplier,
                currentNode.bossDamageMultiplier,
                currentNode.bossHpBonus,
                currentNode.bossDamageBonus);
        }

        flow.SaveMapNode(null);
        flow.SetState(GameState.Combat);
        SceneManager.LoadScene(SceneCatalog.Load().CombatScene, LoadSceneMode.Single);
    }

    private void EnsurePresentationController()
    {
        if (presentationController == null)
            presentationController = GetComponent<MapPresentationController>();

        if (presentationController == null)
            presentationController = gameObject.AddComponent<MapPresentationController>();

        if (mapNodeModalView is IMapNodeModalView modalView)
            presentationController.InjectModalView(modalView);
    }

    private GameFlowManager ResolveGameFlowManager()
    {
        if (gameFlowManager != null)
            return gameFlowManager;
        if (ServiceRegistry.TryResolve(out gameFlowManager))
            return gameFlowManager;

        ServiceRegistry.LogFallback(nameof(MapManager), nameof(gameFlowManager), "missing-injected-reference");

        gameFlowManager = GameFlowManager.Instance;
        if (gameFlowManager != null)
        {
            ServiceRegistry.Register(gameFlowManager);
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(gameFlowManager), "gameflow-instance");
            return gameFlowManager;
        }

        if (IsMigratedMapSceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(gameFlowManager), "strict-missing-reference");
            Debug.LogError("[MapManager] DI estricto: falta GameFlowManager en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        gameFlowManager = ServiceRegistry.ResolveWithFallback(nameof(MapManager), nameof(gameFlowManager), () => ServiceRegistry.LegacyFind<GameFlowManager>());
        if (gameFlowManager != null)
        {
            ServiceRegistry.Register(gameFlowManager);
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(gameFlowManager), "findobjectoftype");
        }

        return gameFlowManager;
    }


    private ShopService ResolveShopService()
    {
        if (shopService != null)
            return shopService;

        if (ServiceRegistry.TryResolve(out shopService))
            return shopService;

        ServiceRegistry.LogFallback(nameof(MapManager), nameof(shopService), "missing-injected-reference");

        if (IsMigratedMapSceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(shopService), "strict-missing-reference");
            Debug.LogError("[MapManager] DI estricto: falta ShopService en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        shopService = new ShopService();
        ServiceRegistry.Register(shopService);
        ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(shopService), "in-process-default");
        return shopService;
    }

    private OrbManager ResolveOrbManagerForShop()
    {
        if (ServiceRegistry.TryResolve(out OrbManager registeredOrbManager))
            return registeredOrbManager;

        ServiceRegistry.LogFallback(nameof(MapManager), "OrbManagerForShop", "missing-injected-reference");

        if (IsMigratedMapSceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), "OrbManagerForShop", "strict-missing-reference");
            Debug.LogError("[MapManager] DI estricto: falta OrbManager en escena migrada para tienda. Revisa el cableado de dependencias.");
            return null;
        }

        OrbManager orbManager = ServiceRegistry.ResolveWithFallback(nameof(MapManager), "OrbManagerForShop", () => OrbManager.Instance ?? ServiceRegistry.LegacyFind<OrbManager>(true));
        if (orbManager != null)
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), "OrbManagerForShop", "legacy-resolver");

        return orbManager;
    }

    private static bool IsMigratedMapSceneActive()
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal);
    }

    private MapStage ResolveStageByIndex(int stageIndex)
    {
        if (stageSequence != null && stageSequence.Length > 0)
        {
            int clamped = Mathf.Clamp(stageIndex, 0, stageSequence.Length - 1);
            MapStage selected = stageSequence[clamped];
            if (selected != null)
                return selected;
        }

        return currentMapStage;
    }

    private int GetStageIndexForBalance(GameFlowManager flow)
    {
        return flow != null ? Mathf.Max(0, flow.CurrentStageIndex) : 0;
    }

    private void ValidateStageConsistency(int expectedStageIndex, MapStage stage, GameFlowManager flow)
    {
        if (flow == null || stage == null)
            return;

        if (domainService.HasStageConsistencyIssue(stageSequence, stage, expectedStageIndex))
        {
            int resolvedIndex = domainService.GetStageIndex(stageSequence, stage);
            Debug.LogWarning($"[MapManager] Stage mismatch detected. FlowStage={expectedStageIndex}, MapStageIndex={resolvedIndex}, MapStage='{stage.stageName}'.");
        }
    }

    private RunBalanceConfig ResolveBalanceConfig()
    {
        if (balanceConfig == null)
            balanceConfig = RunBalanceConfig.LoadDefault();

        return balanceConfig;
    }
}
