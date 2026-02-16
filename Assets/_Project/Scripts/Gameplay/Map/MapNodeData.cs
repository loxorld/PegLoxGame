using UnityEngine;

[CreateAssetMenu(fileName = "MapNode", menuName = "Map/Node")]
public class MapNodeData : ScriptableObject
{
    public NodeType nodeType;
    public string title;
    [TextArea] public string description;

    [Header("Event Settings")]
    public EventDefinition eventDefinition;
    public EventDefinition[] eventDefinitionPool;

    public MapNodeConnection[] nextNodes;

    [Header("Boss Settings")]
    public EnemyData bossEnemy;
    [Min(1f)] public float bossHpMultiplier = 2f;
    [Min(1f)] public float bossDamageMultiplier = 1.5f;
    [Min(0)] public int bossHpBonus = 0;
    [Min(0)] public int bossDamageBonus = 0;
}