using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Board/Board Config", fileName = "BoardConfig_")]
public class BoardConfig : ScriptableObject
{
    [Header("Layout")]
    public int rows = 8;
    public int cols = 7;

    [Tooltip("Separación en unidades del mundo (no píxeles)")]
    public float spacingX = 1.2f;
    public float spacingY = 1.0f;

    [Tooltip("Aleatoriedad en posición para que no quede grilla perfecta")]
    [Range(0f, 0.45f)] public float jitter = 0.15f;

    [Header("Spawn Area (viewport-based)")]
    [Range(0f, 1f)] public float viewportMinY = 0.25f; // debajo del HUD
    [Range(0f, 1f)] public float viewportMaxY = 0.90f; // cerca del techo visible
    [Range(0f, 1f)] public float viewportMinX = 0.10f;
    [Range(0f, 1f)] public float viewportMaxX = 0.90f;

    [Header("Margins")]
    public float marginWorld = 0.4f; // evita paredes/techo

    [Header("Random")]
    public int seed = 12345;
    public bool randomizeSeedEachRun = true;

    [Header("Pegs")]
    [Range(0f, 1f)] public float criticalChance = 0.15f;
}
