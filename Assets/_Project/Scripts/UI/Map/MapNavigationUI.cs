using UnityEngine;

public class MapNavigationUI : MonoBehaviour
{
    public static MapNavigationUI Instance;

    [SerializeField] private Transform nodeContainer;
    [SerializeField] private GameObject nodePrefab;

    private void Awake()
    {
        Instance = this;
        Debug.Log("[MapNavigationUI] Awake ejecutado. Instancia seteada.");
    }


    public void ShowNode(MapNodeData node)
    {
        ClearNodes();

        foreach (var connection in node.nextNodes)
        {
            var obj = Instantiate(nodePrefab, nodeContainer);
            var nodeUI = obj.GetComponent<MapNodeUI>();
            nodeUI.Setup(connection.targetNode, OnNodeSelected);
        }
    }

    void OnNodeSelected(MapNodeData next)
    {
        MapManager mapManager = FindObjectOfType<MapManager>();
        mapManager.SelectPath(next);

        
        mapManager.ConfirmNode();
    }


    void ClearNodes()
    {
        foreach (Transform child in nodeContainer)
            Destroy(child.gameObject);
    }
}
