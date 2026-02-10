using UnityEngine;

[CreateAssetMenu(fileName = "MapStage", menuName = "Map/Stage")]
public class MapStage : ScriptableObject
{
    public string stageName;
    [TextArea] public string stageDescription;
    public MapNodeData startingNode;
    public MapNodeData bossNode;
    public int bossAfterNodes = 10;
}