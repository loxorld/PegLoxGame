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
        currentNode = flow != null && flow.SavedMapNode != null ? flow.SavedMapNode : stage.startingNode;

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
            Debug.LogWarning("[MapManager] No se encontró GameFlowManager en la escena.");
            return;
        }

        flow.SetState(GameState.MapNavigation);

        // Mostrar UI
        if (MapNavigationUI.Instance == null)
        {
            Debug.LogWarning("[MapManager] MapNavigationUI.Instance es nulo.");
            return;
        }

        MapNavigationUI.Instance.ShowNode(node);
    }

    public void SelectPath(MapNodeData nextNode)
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow != null)
            flow.SaveMapNode(nextNode);
        else
            Debug.LogWarning("[MapManager] No se encontró GameFlowManager al guardar MapNode.");

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
                    Debug.LogWarning("[MapManager] No se encontró GameFlowManager en la escena.");
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

    public MapStage CurrentMapStage => currentMapStage; // NUEVO: getter público

}