using System.Collections.Generic;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    [SerializeField] private MapStage currentMapStage;
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
    [SerializeField] private MonoBehaviour mapNodeModalView;

    [Header("Shop Settings")]
    [SerializeField, Min(0)] private int shopHealCost = 10;
    [SerializeField, Min(1)] private int shopHealAmount = 8;
    [SerializeField, Min(0)] private int shopOrbUpgradeCost = 15;
    [SerializeField] private ShopService shopService;


    private MapNodeData currentNode;

    public void StartStage(MapStage stage)
    {
        if (stage == null)
        {
            Debug.LogWarning("[MapManager] StartStage llamado con MapStage nulo.");
            return;
        }

        currentMapStage = stage;

        GameFlowManager flow = ResolveGameFlowManager();
        if (flow != null && (flow.SavedMapNode == null || !HasConnections(flow.SavedMapNode)))
            flow.ResetRunState();

        if (flow != null && flow.SavedMapNode != null && HasConnections(flow.SavedMapNode))
            currentNode = flow.SavedMapNode;
        else
            currentNode = stage.startingNode;

        if (currentNode == null)
        {
            Debug.LogWarning("[MapManager] El MapStage no tiene startingNode asignado.");
            return;
        }

        OpenNode(currentNode);
    }

    public void OpenNode(MapNodeData node)
    {
        if (node == null)
        {
            Debug.LogWarning("[MapManager] OpenNode llamado con MapNodeData nulo.");
            return;
        }

        currentNode = node;

        // Notificar al GameFlowManager
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontr GameFlowManager en la escena.");
            return;
        }

        flow.SetState(GameState.MapNavigation);

        // Mostrar UI
        if (MapNavigationUI.Instance == null)
        {
            Debug.LogWarning("[MapManager] MapNavigationUI.Instance es nulo. Reintentando...");
            StartCoroutine(WaitForMapUIAndShow(node));
            return;
        }

        MapNavigationUI.Instance.ShowNode(node);
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
            Debug.LogWarning("[MapManager] No se encontr GameFlowManager al guardar MapNode.");

        currentNode = nextNode;
        ConfirmNode();
    }

    public void ConfirmNode()
    {

        Debug.Log("[MapManager] ConfirmNode llamado");
        switch (currentNode.nodeType)
        {
            case NodeType.Combat:
                // Guardar datos de nodo actual, si es necesario

                GameFlowManager flow = ResolveGameFlowManager();
                if (flow == null)
                {
                    Debug.LogWarning("[MapManager] No se encontr GameFlowManager en la escena.");
                    return;
                }

                flow.SetState(GameState.Combat);

                // Cargar la escena de combate
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneCatalog.Load().CombatScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
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


    private void Start()
    {
        if (currentMapStage != null)
            StartStage(currentMapStage);
        else
            Debug.LogWarning("[MapManager] No hay MapStage asignado.");
    }

    private GameFlowManager ResolveGameFlowManager()
    {
        if (gameFlowManager != null)
            return gameFlowManager;

        gameFlowManager = GameFlowManager.Instance;
        if (gameFlowManager != null)
            return gameFlowManager;

        gameFlowManager = FindObjectOfType<GameFlowManager>();
        return gameFlowManager;
    }

    public MapStage CurrentMapStage => currentMapStage; // NUEVO: getter pblico

    public bool ShouldForceBossNode(out MapNodeData bossNode)
    {
        bossNode = null;
        if (currentMapStage == null || currentMapStage.bossNode == null)
            return false;

        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
            return false;

        int bossAfterNodes = GetBossAfterNodes();
        if (bossAfterNodes <= 0)
        {
            bossNode = currentMapStage.bossNode;
            return true;
        }

        if (flow.NodesVisited >= bossAfterNodes)
        {
            bossNode = currentMapStage.bossNode;
            return true;
        }

        return false;
    }

    private static bool HasConnections(MapNodeData node)
    {
        return node != null && node.nextNodes != null && node.nextNodes.Length > 0;
    }

    private System.Collections.IEnumerator WaitForMapUIAndShow(MapNodeData node)
    {
        while (MapNavigationUI.Instance == null)
            yield return null;

        MapNavigationUI.Instance.ShowNode(node);
    }

    private void HandleEventNode()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontr GameFlowManager en la escena.");
            return;
        }
        RunBalanceConfig balance = ResolveBalanceConfig();
        int stageIndex = GetStageIndexForBalance(flow);
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

        if (coinDelta != 0)
            flow.AddCoins(coinDelta);

        if (hpDelta != 0)
            flow.ModifySavedHP(hpDelta);

        flow.SaveRun();

        IMapNodeModalView modalView = ResolveMapNodeModalView();
        if (modalView == null)
        {
            Debug.LogWarning("[MapManager] No se encontr IMapNodeModalView en la escena.");
            return;
        }

        var options = new List<MapNodeModalOption>
        {
            new MapNodeModalOption("Continuar", () => OpenNode(currentNode), true)
        };

        modalView.ShowEvent(
            currentNode != null ? currentNode.title : "Evento",
            $"{currentNode?.description}\n\n{outcome}",
            options
        );
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
        if (shopService == null)
            shopService = new ShopService();

        OrbManager orbManager = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);
        RunBalanceConfig balance = ResolveBalanceConfig();
        int stageIndex = GetStageIndexForBalance(flow);
        int healCost = balance != null ? balance.GetShopHealCost(stageIndex, shopHealCost) : shopHealCost;
        int healAmount = balance != null ? balance.GetShopHealAmount(stageIndex, shopHealAmount) : shopHealAmount;
        int orbUpgradeCost = balance != null ? balance.GetShopOrbUpgradeCost(stageIndex, shopOrbUpgradeCost) : shopOrbUpgradeCost;

        string description = $"{currentNode?.description}\n\nMonedas: {flow.Coins}";
        if (!string.IsNullOrWhiteSpace(extraMessage))
            description += $"\n{extraMessage}";

        List<ShopService.ShopOptionData> shopOptions = shopService.GetShopOptions(
            flow,
            orbManager,
            healCost,
            healAmount,
            orbUpgradeCost,
            ShowShopModal,
            () => OpenNode(currentNode));

        var options = new List<MapNodeModalOption>();
        for (int i = 0; i < shopOptions.Count; i++)
        {
            ShopService.ShopOptionData option = shopOptions[i];
            options.Add(new MapNodeModalOption(option.Label, option.OnSelect, option.IsEnabled));
        }

        IMapNodeModalView modalView = ResolveMapNodeModalView();
        if (modalView == null)
        {
            Debug.LogWarning("[MapManager] No se encontr IMapNodeModalView en la escena.");
            return;
        }

        modalView.ShowShop(
            currentNode != null ? currentNode.title : "Tienda",
            description,
            options
        );
    }

    private void HandleBossNode()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontr GameFlowManager en la escena.");
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

        flow.SetState(GameState.Combat);
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneCatalog.Load().CombatScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private IMapNodeModalView ResolveMapNodeModalView()
    {
        if (mapNodeModalView != null && mapNodeModalView is IMapNodeModalView view)
            return view;

        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMapNodeModalView candidate)
            {
                mapNodeModalView = behaviours[i];
                return candidate;
            }
        }
        MapNodeModalUI modalUI = MapNodeModalUI.GetOrCreate();
        if (modalUI != null)
        {
            mapNodeModalView = modalUI;
            return modalUI;
        }
        return null;
    }

    private int GetStageIndexForBalance(GameFlowManager flow)
    {
        if (flow != null)
            return Mathf.Max(0, flow.EncounterIndex);

        return 0;
    }

    private RunBalanceConfig ResolveBalanceConfig()
    {
        if (balanceConfig == null)
            balanceConfig = RunBalanceConfig.LoadDefault();

        return balanceConfig;
    }

    public int GetBossAfterNodes()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        int defaultValue = currentMapStage != null ? currentMapStage.bossAfterNodes : 0;
        RunBalanceConfig balance = ResolveBalanceConfig();
        int stageIndex = flow != null ? Mathf.Max(0, flow.EncounterIndex) : 0;
        return balance != null ? balance.GetBossAfterNodes(stageIndex, defaultValue) : defaultValue;
    }
}