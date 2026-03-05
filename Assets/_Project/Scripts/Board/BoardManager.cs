using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
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

        float pegRadius = GetPegWorldRadius();
        float overlapRadius = pegRadius + extraSeparation;

        int totalSpawned = 0;
        int specialSpawned = 0;
        int assignmentFailures = 0;

        for (int i = 0; i < cellPlans.Count; i++)
        {
            CellPlan plan = cellPlans[i];
            if (!TrySpawnPegInCell(plan.basePosition, rng, overlapRadius, spacingX, spacingY, out GameObject go))
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

    private void ResolveRuntimeGenerationSettings(int stageIndex, int encounterIndex, int encounterInStage)
    {
        runtimeJitter = config.jitter;
        runtimeCriticalChance = config.criticalChance;
        runtimeSpecialChance = config.specialChance;
        runtimeTargetDensity = 1f;
        runtimeSymmetry = BoardGenerationProfile.SymmetryRule.None;
        runtimeProfileId = null;
        runtimeAllowedLayouts = null;
        runtimeSpecialAssignmentRetries = Mathf.Max(1, config.specialTypeAssignmentRetries);
        runtimeMinSpecialManhattanDistance = Mathf.Max(0, config.minSpecialManhattanDistance);
        runtimeMinSpecialEuclideanDistance = Mathf.Max(0f, config.minSpecialEuclideanDistance);
        runtimeTopBandSpecialDensityCap = Mathf.Clamp01(config.topBandSpecialDensityCap);
        runtimeMiddleBandSpecialDensityCap = Mathf.Clamp01(config.middleBandSpecialDensityCap);
        runtimeBottomBandSpecialDensityCap = Mathf.Clamp01(config.bottomBandSpecialDensityCap);

        if (generationProfile == null)
            return;

        if (!generationProfile.TryGetProfile(stageIndex, encounterIndex, encounterInStage, out BoardGenerationProfile.EncounterProfile profile))
            return;

        runtimeProfileId = string.IsNullOrWhiteSpace(profile.profileId) ? "unnamed" : profile.profileId;
        runtimeJitter = profile.jitter;
        runtimeCriticalChance = profile.criticalChance;
        runtimeSpecialChance = profile.specialChance;
        runtimeTargetDensity = profile.targetDensity;
        runtimeSymmetry = profile.symmetryRule;
        runtimeAllowedLayouts = profile.allowedLayouts;
        runtimeSpecialAssignmentRetries = Mathf.Max(1, profile.specialTypeAssignmentRetries);
        runtimeMinSpecialManhattanDistance = Mathf.Max(0, profile.minSpecialManhattanDistance);
        runtimeMinSpecialEuclideanDistance = Mathf.Max(0f, profile.minSpecialEuclideanDistance);
        runtimeTopBandSpecialDensityCap = Mathf.Clamp01(profile.topBandSpecialDensityCap);
        runtimeMiddleBandSpecialDensityCap = Mathf.Clamp01(profile.middleBandSpecialDensityCap);
        runtimeBottomBandSpecialDensityCap = Mathf.Clamp01(profile.bottomBandSpecialDensityCap);

        if (profile.specialLimits == null)
            return;

        for (int i = 0; i < profile.specialLimits.Length; i++)
        {
            BoardGenerationProfile.SpecialLimit limit = profile.specialLimits[i];
            if (limit == null || limit.definition == null)
                continue;

            runtimeSpecialLimits[limit.definition] = Mathf.Max(0, limit.maxPerBoard);
        }
    }

    private bool IsCellEnabled(int row, int col, int rows, int cols, bool baseCellActive, System.Random rng)
    {
        if (!baseCellActive)
            return false;

        if (runtimeTargetDensity >= 0.999f)
            return true;

        int key = GetSymmetryGroupKey(row, col, rows, cols, runtimeSymmetry);
        if (!densityDecisionByGroup.TryGetValue(key, out bool enabled))
        {
            enabled = rng.NextDouble() <= runtimeTargetDensity;
            densityDecisionByGroup[key] = enabled;
        }

        return enabled;
    }

    private int GetSymmetryGroupKey(int row, int col, int rows, int cols, BoardGenerationProfile.SymmetryRule rule)
    {
        int mirroredRow = rows - 1 - row;
        int mirroredCol = cols - 1 - col;

        switch (rule)
        {
            case BoardGenerationProfile.SymmetryRule.MirrorHorizontal:
                return PackKey(Mathf.Min(row, mirroredRow), col);

            case BoardGenerationProfile.SymmetryRule.MirrorVertical:
                return PackKey(row, Mathf.Min(col, mirroredCol));

            case BoardGenerationProfile.SymmetryRule.MirrorBoth:
            case BoardGenerationProfile.SymmetryRule.Rotational180:
                return PackKey(Mathf.Min(row, mirroredRow), Mathf.Min(col, mirroredCol));

            default:
                return PackKey(row, col);
        }
    }

    private int PackKey(int a, int b)
    {
        unchecked
        {
            return (a * 397) ^ b;
        }
    }

    private void AssignPegDefinitions(List<CellPlan> cellPlans, int rows, System.Random rng)
    {
        for (int i = 0; i < cellPlans.Count; i++)
        {
            CellPlan plan = cellPlans[i];
            plan.definition = ChoosePegDefinitionForCell(plan, cellPlans, i, rows, rng);
            cellPlans[i] = plan;

            if (IsSpecialDefinition(plan.definition))
            {
                if (!specialCounts.ContainsKey(plan.definition))
                    specialCounts[plan.definition] = 0;

                specialCounts[plan.definition]++;

                int band = GetVerticalBand(plan.row, rows);
                specialCountByVerticalBand.TryGetValue(band, out int specialInBand);
                specialCountByVerticalBand[band] = specialInBand + 1;
            }
        }
    }

    private PegDefinition ChoosePegDefinitionForCell(CellPlan currentCell, List<CellPlan> cellPlans, int assignedUntilIndex, int rows, System.Random rng)
    {
        if (config.specialPegs != null && config.specialPegs.Length > 0)
        {
            double roll = rng.NextDouble();
            if (roll < runtimeSpecialChance)
            {
                int retries = Mathf.Max(1, runtimeSpecialAssignmentRetries);
                for (int attempt = 0; attempt < retries; attempt++)
                {
                    PegDefinition special = PickWeightedSpecialForCell(currentCell, cellPlans, assignedUntilIndex, rows, rng);
                    if (special != null)
                        return special;
                }
            }
        }

        bool isCrit = rng.NextDouble() < runtimeCriticalChance;
        return isCrit ? criticalPegDef : normalPegDef;
    }

    private PegDefinition PickWeightedSpecialForCell(CellPlan currentCell, List<CellPlan> cellPlans, int assignedUntilIndex, int rows, System.Random rng)
    {
        float totalWeight = 0f;
        int band = GetVerticalBand(currentCell.row, rows);

        for (int i = 0; i < config.specialPegs.Length; i++)
        {
            var entry = config.specialPegs[i];
            if (entry == null || entry.definition == null) continue;
            if (entry.weight <= 0f) continue;
            if (!CanPlaceSpecialInBand(band)) continue;
            if (!CanPlaceSpecialByDistance(currentCell, cellPlans, assignedUntilIndex)) continue;

            int maxPerBoard = GetMaxPerBoard(entry.definition, entry.maxPerBoard);
            if (maxPerBoard > 0)
            {
                int current = specialCounts.TryGetValue(entry.definition, out int v) ? v : 0;
                if (current >= maxPerBoard) continue;
            }

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float pick = (float)(rng.NextDouble() * totalWeight);
        float acc = 0f;

        for (int i = 0; i < config.specialPegs.Length; i++)
        {
            var entry = config.specialPegs[i];
            if (entry == null || entry.definition == null) continue;
            if (entry.weight <= 0f) continue;
            if (!CanPlaceSpecialInBand(band)) continue;
            if (!CanPlaceSpecialByDistance(currentCell, cellPlans, assignedUntilIndex)) continue;

            int maxPerBoard = GetMaxPerBoard(entry.definition, entry.maxPerBoard);
            if (maxPerBoard > 0)
            {
                int current = specialCounts.TryGetValue(entry.definition, out int v) ? v : 0;
                if (current >= maxPerBoard) continue;
            }

            acc += entry.weight;
            if (pick <= acc)
            {
                return entry.definition;
            }
        }

        return null;
    }

    private int GetMaxPerBoard(PegDefinition definition, int configDefault)
    {
        if (definition != null && runtimeSpecialLimits.TryGetValue(definition, out int runtimeLimit))
            return runtimeLimit;

        return configDefault;
    }

    private bool CanPlaceSpecialByDistance(CellPlan currentCell, List<CellPlan> cellPlans, int assignedUntilIndex)
    {
        if (runtimeMinSpecialManhattanDistance <= 0 && runtimeMinSpecialEuclideanDistance <= 0f)
            return true;

        float minEuclideanSq = runtimeMinSpecialEuclideanDistance * runtimeMinSpecialEuclideanDistance;
        for (int i = 0; i < assignedUntilIndex; i++)
        {
            CellPlan other = cellPlans[i];
            if (!IsSpecialDefinition(other.definition))
                continue;

            int dx = Mathf.Abs(currentCell.col - other.col);
            int dy = Mathf.Abs(currentCell.row - other.row);

            if (runtimeMinSpecialManhattanDistance > 0 && dx + dy < runtimeMinSpecialManhattanDistance)
                return false;

            if (runtimeMinSpecialEuclideanDistance > 0f)
            {
                int d2 = dx * dx + dy * dy;
                if (d2 < minEuclideanSq)
                    return false;
            }
        }

        return true;
    }

    private bool CanPlaceSpecialInBand(int band)
    {
        activeCellCountByVerticalBand.TryGetValue(band, out int activeCount);
        if (activeCount <= 0)
            return false;

        specialCountByVerticalBand.TryGetValue(band, out int currentSpecials);
        float cap = GetBandSpecialDensityCap(band);
        int limit = Mathf.CeilToInt(activeCount * cap);
        return currentSpecials < limit;
    }

    private float GetBandSpecialDensityCap(int band)
    {
        switch (band)
        {
            case 0: return runtimeTopBandSpecialDensityCap;
            case 1: return runtimeMiddleBandSpecialDensityCap;
            default: return runtimeBottomBandSpecialDensityCap;
        }
    }

    private int GetVerticalBand(int row, int rows)
    {
        if (rows <= 1)
            return 1;

        float normalized = row / (float)(rows - 1);
        if (normalized < 0.3334f) return 0;
        if (normalized < 0.6667f) return 1;
        return 2;
    }

    private bool IsSpecialDefinition(PegDefinition definition)
    {
        if (definition == null || definition == normalPegDef || definition == criticalPegDef)
            return false;

        return true;
    }

    private void LogDistributionMetrics(int rows, int cols, List<CellPlan> plans, int spawnedCount, int spawnedSpecialCount, int spawnFailures)
    {
        float[] quadrantTotals = new float[4];
        float[] quadrantSpecials = new float[4];

        for (int i = 0; i < plans.Count; i++)
        {
            CellPlan plan = plans[i];
            int quadrant = GetQuadrant(plan.row, plan.col, rows, cols);
            quadrantTotals[quadrant] += 1f;
            if (IsSpecialDefinition(plan.definition))
                quadrantSpecials[quadrant] += 1f;
        }

        float totalVariance = CalculateVariance(quadrantTotals);
        float specialVariance = CalculateVariance(quadrantSpecials);

        Debug.Log($"[BoardTelemetry] distribution plans={plans.Count} spawned={spawnedCount} spawnedSpecials={spawnedSpecialCount} spawnFailures={spawnFailures} totalVarQ={totalVariance:F3} specialVarQ={specialVariance:F3}");
    }

    private int GetQuadrant(int row, int col, int rows, int cols)
    {
        bool top = row < rows * 0.5f;
        bool left = col < cols * 0.5f;
        if (top && left) return 0;
        if (top) return 1;
        if (left) return 2;
        return 3;
    }

    private float CalculateVariance(float[] values)
    {
        if (values == null || values.Length == 0)
            return 0f;

        float mean = 0f;
        for (int i = 0; i < values.Length; i++)
            mean += values[i];
        mean /= values.Length;

        float sum = 0f;
        for (int i = 0; i < values.Length; i++)
        {
            float delta = values[i] - mean;
            sum += delta * delta;
        }

        return sum / values.Length;
    }

    private BoardLayout PickLayout(System.Random rng)
    {
        if (runtimeAllowedLayouts != null && runtimeAllowedLayouts.Length > 0)
        {
            float totalWeight = 0f;
            for (int i = 0; i < runtimeAllowedLayouts.Length; i++)
            {
                BoardGenerationProfile.LayoutWeight entry = runtimeAllowedLayouts[i];
                if (entry == null || entry.layout == null || entry.weight <= 0f)
                    continue;

                totalWeight += entry.weight;
            }

            if (totalWeight > 0f)
            {
                float pick = (float)(rng.NextDouble() * totalWeight);
                float acc = 0f;
                for (int i = 0; i < runtimeAllowedLayouts.Length; i++)
                {
                    BoardGenerationProfile.LayoutWeight entry = runtimeAllowedLayouts[i];
                    if (entry == null || entry.layout == null || entry.weight <= 0f)
                        continue;

                    acc += entry.weight;
                    if (pick <= acc)
                        return entry.layout;
                }
            }
        }

        if (config.layouts == null || config.layouts.Length == 0)
            return null;

        int idx = rng.Next(0, config.layouts.Length);
        return config.layouts[idx];
    }

    private bool TrySpawnPegInCell(Vector2 basePos, System.Random rng, float overlapRadius, float spacingX, float spacingY, out GameObject spawnedPeg)
    {
        spawnedPeg = null;

        for (int attempt = 0; attempt < maxTriesPerCell; attempt++)
        {
            Vector2 pos = ApplyJitter(basePos, rng, spacingX, spacingY);

            if (Physics2D.OverlapCircle(pos, overlapRadius, pegOverlapMask) != null)
                continue;

            spawnedPeg = Instantiate(pegPrefab, pos, Quaternion.identity, boardRoot);
            return true;
        }

        return false;
    }

    private Vector2 ApplyJitter(Vector2 basePos, System.Random rng, float spacingX, float spacingY)
    {
        float jx = (float)(rng.NextDouble() * 2.0 - 1.0) * runtimeJitter * spacingX;
        float jy = (float)(rng.NextDouble() * 2.0 - 1.0) * runtimeJitter * spacingY;
        return new Vector2(basePos.x + jx, basePos.y + jy);
    }

    private float GetPegWorldRadius()
    {
        var col = pegPrefab.GetComponent<CircleCollider2D>();
        if (col == null) return 0.15f;

        float scale = pegPrefab.transform.localScale.x;
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

    private Rect CalculatePlayableBounds()
    {
        Rect visibleWorld = CameraWorldRect.GetVisibleWorldRect(cam);

        float minWorldX = Mathf.Lerp(visibleWorld.xMin, visibleWorld.xMax, config.viewportMinX);
        float maxWorldX = Mathf.Lerp(visibleWorld.xMin, visibleWorld.xMax, config.viewportMaxX);
        float minWorldY = Mathf.Lerp(visibleWorld.yMin, visibleWorld.yMax, config.viewportMinY);
        float maxWorldY = Mathf.Lerp(visibleWorld.yMin, visibleWorld.yMax, config.viewportMaxY);

        Vector2 minW = new Vector2(minWorldX, minWorldY);
        Vector2 maxW = new Vector2(maxWorldX, maxWorldY);

        Vector2 worldMin = new Vector2(Mathf.Min(minW.x, maxW.x), Mathf.Min(minW.y, maxW.y));
        Vector2 worldMax = new Vector2(Mathf.Max(minW.x, maxW.x), Mathf.Max(minW.y, maxW.y));

        worldMin += Vector2.one * config.marginWorld;
        worldMax -= Vector2.one * config.marginWorld;

        Rect bounds = Rect.MinMaxRect(worldMin.x, worldMin.y, worldMax.x, worldMax.y);
        playableBounds = bounds;
        hasPlayableBounds = true;

        if (boundsProvider != null)
            boundsProvider.SetBounds(bounds);

        return bounds;
    }
}