using UnityEngine;

[CreateAssetMenu(fileName = "MapNode", menuName = "Map/Node")]
public class MapNodeData : ScriptableObject
{
    public NodeType nodeType;
    public string title;
    [TextArea] public string description;

    public MapNodeConnection[] nextNodes;
}
