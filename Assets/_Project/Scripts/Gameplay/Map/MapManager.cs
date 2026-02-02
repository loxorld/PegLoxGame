using UnityEngine;

public class MapManager : MonoBehaviour
{
    [SerializeField] private MapStage currentMapStage;
    [SerializeField] private GameFlowManager gameFlowManager;
    [Header("Event Settings")]
    [SerializeField, Min(0)] private int eventCoinsRewardMin = 5;
    [SerializeField, Min(0)] private int eventCoinsRewardMax = 15;
    [SerializeField, Min(0)] private int eventCoinsPenaltyMin = 3;
    [SerializeField, Min(0)] private int eventCoinsPenaltyMax = 10;
    [SerializeField, Min(0)] private int eventHealMin = 3;
    [SerializeField, Min(0)] private int eventHealMax = 6;
    [SerializeField, Min(0)] private int eventDamageMin = 2;
    [SerializeField, Min(0)] private int eventDamageMax = 5;

    [Header("Shop Settings")]
    [SerializeField, Min(0)] private int shopHealCost = 10;
    [SerializeField, Min(1)] private int shopHealAmount = 8;

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
                UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
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

        if (currentMapStage.bossAfterNodes <= 0)
        {
            bossNode = currentMapStage.bossNode;
            return true;
        }

        if (flow.NodesVisited >= currentMapStage.bossAfterNodes)
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

        bool isGoodEvent = Random.value >= 0.5f;
        int coinDelta = isGoodEvent
            ? Random.Range(eventCoinsRewardMin, eventCoinsRewardMax + 1)
            : -Random.Range(eventCoinsPenaltyMin, eventCoinsPenaltyMax + 1);

        int hpDelta = isGoodEvent
            ? Random.Range(eventHealMin, eventHealMax + 1)
            : -Random.Range(eventDamageMin, eventDamageMax + 1);

        string outcome = isGoodEvent
            ? $"Encontraste algo útil. +{coinDelta} monedas, +{hpDelta} HP."
            : $"La expedición salió mal. {coinDelta} monedas, {hpDelta} HP.";

        if (coinDelta != 0)
            flow.AddCoins(coinDelta);

        if (hpDelta != 0)
            flow.ModifySavedHP(hpDelta);

        MapNodeModalUI.Show(
            currentNode != null ? currentNode.title : "Evento",
            $"{currentNode?.description}\n\n{outcome}",
            new MapNodeModalUI.Option("Continuar", () => OpenNode(currentNode))
        );
    }

    private void HandleShopNode()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontr GameFlowManager en la escena.");
            return;
        }

        string description = $"{currentNode?.description}\n\nMonedas: {flow.Coins}";

        var options = new System.Collections.Generic.List<MapNodeModalUI.Option>();
        if (flow.Coins >= shopHealCost)
        {
            options.Add(new MapNodeModalUI.Option(
                $"Curar +{shopHealAmount} HP ({shopHealCost} monedas)",
                () =>
                {
                    if (flow.SpendCoins(shopHealCost))
                        flow.ModifySavedHP(shopHealAmount);
                    OpenNode(currentNode);
                }));
        }

        options.Add(new MapNodeModalUI.Option("Salir", () => OpenNode(currentNode)));

        MapNodeModalUI.Show(
            currentNode != null ? currentNode.title : "Tienda",
            description,
            options.ToArray()
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
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

}