using System.Collections.Generic;
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
        if (node == null)
        {
            Debug.LogWarning("[MapNavigationUI] ShowNode llamado con MapNodeData nulo.");
            return;
        }

        if (node.nextNodes == null || node.nextNodes.Length == 0)
        {
            Debug.LogWarning("[MapNavigationUI] El nodo no tiene conexiones disponibles.");
            return;
        }

        var connections = new List<MapNodeConnection>(node.nextNodes);
        for (int i = connections.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (connections[i], connections[swapIndex]) = (connections[swapIndex], connections[i]);
        }

        int nodesToShow = Mathf.Min(2, connections.Count);
        for (int i = 0; i < nodesToShow; i++)
        {
            var connection = connections[i];
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
