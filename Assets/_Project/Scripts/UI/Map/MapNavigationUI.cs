using System.Collections.Generic;
using UnityEngine;

public class MapNavigationUI : MonoBehaviour
{
    public static MapNavigationUI Instance;

    [SerializeField] private MapManager mapManager;

    [SerializeField] private Transform nodeContainer;
    [SerializeField] private GameObject nodePrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MapNavigationUI] Ya existe una instancia activa. Se destruye el duplicado.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Debug.Log("[MapNavigationUI] Awake ejecutado. Instancia seteada.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowNode(MapNodeData node)
    {
        ClearNodes();
        if (node == null)
        {
            Debug.LogWarning("[MapNavigationUI] ShowNode llamado con MapNodeData nulo.");
            return;
        }

        if (!HasNodeRenderingSetup())
            return;

        MapManager mapManagerRef = ResolveMapManager();
        if (mapManagerRef != null && mapManagerRef.ShouldForceBossNode(out MapNodeData bossNode) && bossNode != null)
        {
            TryCreateNode(bossNode);
            return;
        }

        List<MapNodeData> nextNodes = BuildAvailableNextNodes(node);
        if (nextNodes.Count == 0)
        {
            Debug.LogWarning("[MapNavigationUI] El nodo no tiene conexiones disponibles.");
            return;
        }

        for (int i = nextNodes.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (nextNodes[i], nextNodes[swapIndex]) = (nextNodes[swapIndex], nextNodes[i]);
        }

        int nodesToShow = Mathf.Min(2, nextNodes.Count);
        for (int i = 0; i < nodesToShow; i++)
            TryCreateNode(nextNodes[i]);
    }

    void OnNodeSelected(MapNodeData next)
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


    private MapManager ResolveMapManager()
    {
        if (mapManager != null)
            return mapManager;

        mapManager = ServiceRegistry.ResolveWithFallback(nameof(MapNavigationUI), nameof(mapManager), () => ServiceRegistry.LegacyFind<MapManager>());
        return mapManager;
    }

    private bool HasNodeRenderingSetup()
    {
        if (nodeContainer == null)
        {
            Debug.LogWarning("[MapNavigationUI] Falta nodeContainer para mostrar nodos.");
            return false;
        }

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

    private List<MapNodeData> BuildAvailableNextNodes(MapNodeData node)
    {
        var validNodes = new List<MapNodeData>();
        if (node?.nextNodes == null || node.nextNodes.Length == 0)
            return validNodes;

        var seenTargets = new HashSet<MapNodeData>();
        for (int i = 0; i < node.nextNodes.Length; i++)
        {
            MapNodeConnection connection = node.nextNodes[i];
            if (connection?.targetNode == null)
                continue;

            if (seenTargets.Add(connection.targetNode))
                validNodes.Add(connection.targetNode);
        }

        return validNodes;
    }

    private bool TryCreateNode(MapNodeData node)
    {
        if (node == null)
            return false;

        GameObject nodeObject = Instantiate(nodePrefab, nodeContainer);
        if (nodeObject == null)
            return false;

        MapNodeUI nodeUI = nodeObject.GetComponent<MapNodeUI>();
        if (nodeUI == null)
        {
            Debug.LogWarning("[MapNavigationUI] Se instancio un nodePrefab sin MapNodeUI.");
            Destroy(nodeObject);
            return false;
        }

        nodeUI.Setup(node, OnNodeSelected);
        return true;
    }

    void ClearNodes()
    {
        if (nodeContainer == null)
            return;

        foreach (Transform child in nodeContainer)
            Destroy(child.gameObject);
    }
}
