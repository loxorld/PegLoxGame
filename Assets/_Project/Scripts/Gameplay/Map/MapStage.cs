using UnityEngine;

[CreateAssetMenu(fileName = "MapStage", menuName = "Map/Stage")]
public class MapStage : ScriptableObject
{
    public string stageName;
    public MapNodeData startingNode;
}
