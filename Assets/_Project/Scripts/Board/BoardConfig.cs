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

    [Header("Base Pegs")]
    [Range(0f, 1f)] public float criticalChance = 0.15f;

    // ------------------ NUEVO ------------------

    [System.Serializable]
    public class SpecialPegSpawn
    {
        [Tooltip("Qué PegDefinition spawnea (ej: PegDef_Bomb, PegDef_Refresh, PegDef_Durable)")]
        public PegDefinition definition;

        [Tooltip("Peso relativo dentro de los especiales (no es % directo)")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("Máximo por board (0 = sin límite)")]
        [Min(0)] public int maxPerBoard = 0;
    }

    [Header("Special Pegs (data-driven)")]
    [Tooltip("Probabilidad global de que un peg sea 'especial' (en vez de normal/critical)")]
    [Range(0f, 1f)] public float specialChance = 0.10f;

    public SpecialPegSpawn[] specialPegs;

    // ------------------------------------------

    [Header("Layouts")]
    public BoardLayout[] layouts;
    public bool useLayoutDimensions = true;
}
