using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform boardRoot;

    [Header("Prefabs / Definitions")]
    [SerializeField] private GameObject pegPrefab;           
    [SerializeField] private PegDefinition normalPegDef;
    [SerializeField] private PegDefinition criticalPegDef;
    [SerializeField] private PegDefinition definition;

    [Header("Config")]
    [SerializeField] private BoardConfig config;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (boardRoot == null) boardRoot = transform;
    }

    private void Start()
    {
        GenerateBoard();
    }

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        ClearBoard();

        if (cam == null || pegPrefab == null || config == null)
        {
            Debug.LogError("[Board] Missing references (cam/pegPrefab/config).");
            return;
        }

        if (normalPegDef == null || criticalPegDef == null)
        {
            Debug.LogError("[Board] Missing PegDefinitions (normal/critical).");
            return;
        }

        int seed = config.randomizeSeedEachRun ? Random.Range(int.MinValue, int.MaxValue) : config.seed;
        var rng = new System.Random(seed);

     
        Vector2 minW = cam.ViewportToWorldPoint(new Vector3(config.viewportMinX, config.viewportMinY, 0f));
        Vector2 maxW = cam.ViewportToWorldPoint(new Vector3(config.viewportMaxX, config.viewportMaxY, 0f));

        // Margen extra para no tocar paredes/techo
        minW += Vector2.one * config.marginWorld;
        maxW -= Vector2.one * config.marginWorld;

        // Tamaño total que ocuparía la grilla
        float gridW = (config.cols - 1) * config.spacingX;
        float gridH = (config.rows - 1) * config.spacingY;

        // Punto de inicio centrado dentro del rectángulo jugable
        Vector2 center = (minW + maxW) * 0.5f;
        Vector2 start = new Vector2(center.x - gridW * 0.5f, center.y + gridH * 0.5f);

        // Clamp por si la grilla es más grande que el área
       
        start.x = Mathf.Clamp(start.x, minW.x, maxW.x - gridW);
        start.y = Mathf.Clamp(start.y, minW.y + gridH, maxW.y);

        for (int r = 0; r < config.rows; r++)
        {
            for (int c = 0; c < config.cols; c++)
            {
                float x = start.x + c * config.spacingX;
                float y = start.y - r * config.spacingY;

                // Jitter
                float jx = (float)(rng.NextDouble() * 2.0 - 1.0) * config.jitter * config.spacingX;
                float jy = (float)(rng.NextDouble() * 2.0 - 1.0) * config.jitter * config.spacingY;

                Vector2 pos = new Vector2(x + jx, y + jy);

                // Asegurar que queda dentro del área
                pos.x = Mathf.Clamp(pos.x, minW.x, maxW.x);
                pos.y = Mathf.Clamp(pos.y, minW.y, maxW.y);

                var go = Instantiate(pegPrefab, pos, Quaternion.identity, boardRoot);
                spawned.Add(go);

                // Elegir tipo por probabilidad
                bool isCrit = rng.NextDouble() < config.criticalChance;

                // Setear definition en el Peg
                var peg = go.GetComponent<Peg>();
                if (peg == null)
                {
                    Debug.LogError("[Board] Spawned peg prefab missing Peg component.");
                    continue;
                }

                // Set Definition por reflexión controlada
                ApplyDefinition(peg, isCrit ? criticalPegDef : normalPegDef);
            }
        }

        Debug.Log($"[Board] Generated {spawned.Count} pegs (seed={seed}).");

        // Importante: al generar, reseteamos conteo/colores
        PegManager.Instance?.ResetAllPegs();
    }

    private void ApplyDefinition(Peg peg, PegDefinition def)
    {
        
        // Esto evita reflection y mantiene encapsulación.
        var setter = peg as IPegDefinitionReceiver;
        if (setter != null)
        {
            setter.SetDefinition(def);
            return;
        }

      
        var so = new SerializedObject(peg);
        var prop = so.FindProperty("definition");
        if (prop != null)
        {
            prop.objectReferenceValue = def;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public void ClearBoard()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
    }
}
