using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public partial class BoardManager : MonoBehaviour
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
    [SerializeField] private BoardGenerationProfile generationProfile;

    [Header("Anti-Overlap")]
    [SerializeField] private LayerMask pegOverlapMask;
    [SerializeField, Min(1)] private int maxTriesPerCell = 10;
    [SerializeField, Min(0f)] private float extraSeparation = 0.02f;

    [Header("Spawn Safe Zone")]
    [SerializeField, Min(0f)] private float spawnSafeHeight = 1.5f;

    [SerializeField] private BattleManager battle;
    [SerializeField] private BoardBoundsProvider boundsProvider;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<PlacedPegData> placedPegs = new List<PlacedPegData>();
    private readonly Dictionary<PegDefinition, float> effectiveRadiusByDefinition = new Dictionary<PegDefinition, float>();

    private Rect playableBounds;
    private bool hasPlayableBounds;

    private readonly Dictionary<PegDefinition, int> specialCounts = new Dictionary<PegDefinition, int>();
    private readonly Dictionary<PegDefinition, int> runtimeSpecialLimits = new Dictionary<PegDefinition, int>();
    private readonly Dictionary<int, bool> densityDecisionByGroup = new Dictionary<int, bool>();
    private readonly Dictionary<int, int> specialCountByVerticalBand = new Dictionary<int, int>();
    private readonly Dictionary<int, int> activeCellCountByVerticalBand = new Dictionary<int, int>();

    private int runtimeSpecialAssignmentRetries;
    private int runtimeMinSpecialManhattanDistance;
    private float runtimeMinSpecialEuclideanDistance;
    private float runtimeTopBandSpecialDensityCap;
    private float runtimeMiddleBandSpecialDensityCap;
    private float runtimeBottomBandSpecialDensityCap;

    private float runtimeJitter;
    private float runtimeCriticalChance;
    private float runtimeSpecialChance;
    private float runtimeTargetDensity;
    private BoardGenerationProfile.SymmetryRule runtimeSymmetry;
    private string runtimeProfileId;
    private BoardGenerationProfile.LayoutWeight[] runtimeAllowedLayouts;

    private struct CellPlan
    {
        public int row;
        public int col;
        public Vector2 basePosition;
        public PegDefinition definition;
    }

    private struct PlacedPegData
    {
        public Vector2 position;
        public float radius;
    }

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (boardRoot == null) boardRoot = transform;
        if (boundsProvider == null) boundsProvider = GetComponent<BoardBoundsProvider>();
    }

    private void Start()
    {
        if (battle != null)
            battle.EncounterStarted += OnEncounterStarted;

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
        Debug.Log("[BoardManager] OnEncounterStarted invocado, generando tablero...");
        GenerateBoard();
    }

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        ClearBoard();
        specialCounts.Clear();
        runtimeSpecialLimits.Clear();
        densityDecisionByGroup.Clear();
        specialCountByVerticalBand.Clear();
        activeCellCountByVerticalBand.Clear();
        placedPegs.Clear();
        effectiveRadiusByDefinition.Clear();

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

        int stageIndex = battle != null ? battle.CurrentStageIndex : -1;
        int encounterIndex = battle != null ? battle.EncounterIndex : -1;
        int encounterInStage = battle != null ? battle.EncounterInStage : -1;

        ResolveRuntimeGenerationSettings(stageIndex, encounterIndex, encounterInStage);

        BoardLayout layout = PickLayout(rng);
        int rows = config.rows;
        int cols = config.cols;

        if (layout != null && config.useLayoutDimensions)
        {
            rows = layout.rows;
            cols = layout.cols;
        }

        Debug.Log($"[BoardTelemetry] seed={seed} profile={(string.IsNullOrEmpty(runtimeProfileId) ? "BoardConfigFallback" : runtimeProfileId)} stage={stageIndex} encounter={encounterIndex} encounterInStage={encounterInStage} layout={(layout != null ? layout.name : "None")}");

        Rect bounds = CalculatePlayableBounds();
        float leftBound = bounds.xMin;
        float rightBound = bounds.xMax;
        float topBound = bounds.yMax;

        float bottomBound = bounds.yMin + spawnSafeHeight;
        if (bottomBound >= topBound)
        {
            Debug.LogWarning("[Board] spawnSafeHeight too large for current playable area. Reducing to fit.");
            bottomBound = bounds.yMin;
        }

        float spacingX = config.spacingX;
        float spacingY = config.spacingY;
        float gridW = (cols - 1) * spacingX;
        float gridH = (rows - 1) * spacingY;
        float availableW = rightBound - leftBound;
        float availableH = topBound - bottomBound;

        if ((gridW > availableW || gridH > availableH) && availableW > 0f && availableH > 0f)
        {
            float scaleX = gridW > 0f ? availableW / gridW : 1f;
            float scaleY = gridH > 0f ? availableH / gridH : 1f;
            float scale = Mathf.Min(scaleX, scaleY);
            spacingX *= scale;
            spacingY *= scale;
            gridW = (cols - 1) * spacingX;
            gridH = (rows - 1) * spacingY;
        }

        Vector2 permittedCenter = new Vector2(
            (leftBound + rightBound) * 0.5f,
            (bottomBound + topBound) * 0.5f
        );

        Vector2 start = new Vector2(
            permittedCenter.x - gridW * 0.5f,
            permittedCenter.y + gridH * 0.5f
        );

        start.x = Mathf.Clamp(start.x, leftBound, rightBound - gridW);
        start.y = Mathf.Clamp(start.y, bottomBound + gridH, topBound);

        List<CellPlan> cellPlans = BuildActiveCellPlans(rows, cols, layout, rng, start, spacingX, spacingY);
        AssignPegDefinitions(cellPlans, rows, rng);

        int totalSpawned = 0;
        int specialSpawned = 0;
        int assignmentFailures = 0;

        for (int i = 0; i < cellPlans.Count; i++)
        {
            CellPlan plan = cellPlans[i];
            if (!TrySpawnPegInCell(plan.basePosition, plan.definition, rng, spacingX, spacingY, out GameObject go))
            {
                assignmentFailures++;
                continue;
            }

            spawned.Add(go);
            totalSpawned++;

            Peg peg = go.GetComponent<Peg>();
            if (peg == null)
            {
                Debug.LogError("[Board] Spawned peg prefab missing Peg component.");
                continue;
            }

            peg.SetDefinition(plan.definition);
            if (IsSpecialDefinition(plan.definition))
                specialSpawned++;
        }

        LogDistributionMetrics(rows, cols, cellPlans, totalSpawned, specialSpawned, assignmentFailures);

        PegManager.Instance?.ResetAllPegsForNewEncounter();
    }

    private List<CellPlan> BuildActiveCellPlans(int rows, int cols, BoardLayout layout, System.Random rng, Vector2 start, float spacingX, float spacingY)
    {
        List<CellPlan> plans = new List<CellPlan>(rows * cols);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool baseCellActive = layout == null || layout.IsActive(r, c);
                bool cellActive = IsCellEnabled(r, c, rows, cols, baseCellActive, rng);
                if (!cellActive) continue;

                Vector2 basePos = new Vector2(
                    start.x + c * spacingX,
                    start.y - r * spacingY
                );

                CellPlan plan = new CellPlan
                {
                    row = r,
                    col = c,
                    basePosition = basePos,
                    definition = normalPegDef
                };

                plans.Add(plan);

                int band = GetVerticalBand(r, rows);
                activeCellCountByVerticalBand.TryGetValue(band, out int count);
                activeCellCountByVerticalBand[band] = count + 1;
            }
        }

        return plans;
    }

    public bool TryGetPlayableBounds(out Rect bounds)
    {
        bounds = playableBounds;
        return hasPlayableBounds;
    }

    public void ClearBoard()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i]);
        }
        spawned.Clear();
        placedPegs.Clear();
        effectiveRadiusByDefinition.Clear();
    }
}
