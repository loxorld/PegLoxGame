using UnityEngine;

public class MapManager : MonoBehaviour
{
    [SerializeField] private MapStage currentMapStage;
    [SerializeField] private GameFlowManager gameFlowManager;
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
            flow.ResetNodesVisited();

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

        OpenNode(nextNode);
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

                // futuro: tienda, evento, boss
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

}