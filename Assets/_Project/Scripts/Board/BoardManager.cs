using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform boardRoot;

    [Header("Prefab / Definitions")]
    [SerializeField] private GameObject pegPrefab;
    [SerializeField] private PegDefinition normalPegDef;
    [SerializeField] private PegDefinition criticalPegDef;

    [Header("Config")]
    [SerializeField] private BoardConfig config;

    [Header("Anti-Overlap")]
    [SerializeField] private LayerMask pegOverlapMask;          // capa donde están los Pegs
    [SerializeField, Min(1)] private int maxTriesPerCell = 10;
    [SerializeField, Min(0f)] private float extraSeparation = 0.02f;

    [SerializeField] private BattleManager battle;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (boardRoot == null) boardRoot = transform;
    }

    private void Start()
    {
        if (battle != null)
            battle.EncounterStarted += OnEncounterStarted;

        // Primer encounter ya se dispara desde BattleManager.StartNewEncounter()
        // pero por seguridad, si no hay battle asignado generamos una vez.
        if (battle == null)
            GenerateBoard();
    }

    private void OnDestroy()
    {
        if (battle != null)
            battle.EncounterStarted -= OnEncounterStarted;
    }

    private void OnEncounterStarted()
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

        int seed = config.randomizeSeedEachRun
            ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
            : config.seed;

        var rng = new System.Random(seed);

        BoardLayout layout = PickRandomLayout(rng);
        int rows = config.rows;
        int cols = config.cols;

        if (layout != null && config.useLayoutDimensions)
        {
            rows = layout.rows;
            cols = layout.cols;
        }

        // Área jugable en world (por viewport) + margen
        Vector2 minW = cam.ViewportToWorldPoint(new Vector3(config.viewportMinX, config.viewportMinY, 0f));
        Vector2 maxW = cam.ViewportToWorldPoint(new Vector3(config.viewportMaxX, config.viewportMaxY, 0f));
        minW += Vector2.one * config.marginWorld;
        maxW -= Vector2.one * config.marginWorld;

        float gridW = (cols - 1) * config.spacingX;
        float gridH = (rows - 1) * config.spacingY;

        Vector2 center = (minW + maxW) * 0.5f;
        Vector2 start = new Vector2(center.x - gridW * 0.5f, center.y + gridH * 0.5f);

        // Clamp del origen para que la grilla entre completa
        start.x = Mathf.Clamp(start.x, minW.x, maxW.x - gridW);
        start.y = Mathf.Clamp(start.y, minW.y + gridH, maxW.y);

        float pegRadius = GetPegWorldRadius();
        float overlapRadius = pegRadius + extraSeparation;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool cellActive = layout == null || layout.IsActive(r, c);
                if (!cellActive) continue;

                Vector2 basePos = new Vector2(
                    start.x + c * config.spacingX,
                    start.y - r * config.spacingY
                );

                if (!TrySpawnPegInCell(basePos, rng, overlapRadius, out GameObject go))
                    continue;

                spawned.Add(go);

                Peg peg = go.GetComponent<Peg>();
                if (peg == null)
                {
                    Debug.LogError("[Board] Spawned peg prefab missing Peg component.");
                    continue;
                }

                bool isCrit = rng.NextDouble() < config.criticalChance;
                PegDefinition def = isCrit ? criticalPegDef : normalPegDef;

                peg.SetDefinition(def);
            }
        }

        // Al terminar, por las dudas de orden de ejecución, reseteamos estado/visual coherente
        PegManager.Instance?.ResetAllPegs();
    }

    private BoardLayout PickRandomLayout(System.Random rng)
    {
        if (config.layouts == null || config.layouts.Length == 0)
            return null;

        int idx = rng.Next(0, config.layouts.Length);
        return config.layouts[idx];
    }

    private bool TrySpawnPegInCell(Vector2 basePos, System.Random rng, float overlapRadius, out GameObject spawnedPeg)
    {
        spawnedPeg = null;

        for (int attempt = 0; attempt < maxTriesPerCell; attempt++)
        {
            Vector2 pos = ApplyJitter(basePos, rng);

            // Anti-overlap: si ya hay un peg cerca, reintenta
            if (Physics2D.OverlapCircle(pos, overlapRadius, pegOverlapMask) != null)
                continue;

            spawnedPeg = Instantiate(pegPrefab, pos, Quaternion.identity, boardRoot);
            return true;
        }

        return false;
    }

    private Vector2 ApplyJitter(Vector2 basePos, System.Random rng)
    {
        float jx = (float)(rng.NextDouble() * 2.0 - 1.0) * config.jitter * config.spacingX;
        float jy = (float)(rng.NextDouble() * 2.0 - 1.0) * config.jitter * config.spacingY;
        return new Vector2(basePos.x + jx, basePos.y + jy);
    }

    private float GetPegWorldRadius()
    {
        var col = pegPrefab.GetComponent<CircleCollider2D>();
        if (col == null) return 0.15f;

        float scale = pegPrefab.transform.localScale.x; // asumimos escala uniforme
        return col.radius * scale;
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
