using UnityEngine;

public class MapManager : MonoBehaviour
{
    [SerializeField] private MapStage currentMapStage;
    private MapNodeData currentNode;

    public void StartStage(MapStage stage)
    {
        currentMapStage = stage;
        currentNode = stage.startingNode;
        OpenNode(currentNode);
    }

    public void OpenNode(MapNodeData node)
    {
        currentNode = node;

        // Notificar al GameFlowManager
        GameFlowManager.Instance.SetState(GameState.MapNavigation);

        // Mostrar UI
        MapNavigationUI.Instance.ShowNode(node);
    }

    public void SelectPath(MapNodeData nextNode)
    {
        OpenNode(nextNode);
    }

    public void ConfirmNode()
    {
        switch (currentNode.nodeType)
        {
            case NodeType.Combat:
                GameFlowManager.Instance.SetState(GameState.Combat);
                FindObjectOfType<BattleManager>().StartEncounterFromMap();
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

    public MapStage CurrentMapStage => currentMapStage; // NUEVO: getter público

}
