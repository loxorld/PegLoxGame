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

    private float runtimeJitter;
    private float runtimeCriticalChance;
    private float runtimeSpecialChance;
    private float runtimeTargetDensity;
    private BoardGenerationProfile.SymmetryRule runtimeSymmetry;
    private string runtimeProfileId;

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

        float pegRadius = GetPegWorldRadius();
        float overlapRadius = pegRadius + extraSeparation;

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

                if (!TrySpawnPegInCell(basePos, rng, overlapRadius, spacingX, spacingY, out GameObject go))
                    continue;

                spawned.Add(go);

                Peg peg = go.GetComponent<Peg>();
                if (peg == null)
                {
                    Debug.LogError("[Board] Spawned peg prefab missing Peg component.");
                    continue;
                }

                PegDefinition def = ChoosePegDefinition(rng);
                peg.SetDefinition(def);
            }
        }

        PegManager.Instance?.ResetAllPegsForNewEncounter();
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

    private PegDefinition ChoosePegDefinition(System.Random rng)
    {
        if (config.specialPegs != null && config.specialPegs.Length > 0)
        {
            double roll = rng.NextDouble();
            if (roll < runtimeSpecialChance)
            {
                PegDefinition special = PickWeightedSpecial(rng);
                if (special != null) return special;
            }
        }

        bool isCrit = rng.NextDouble() < runtimeCriticalChance;
        return isCrit ? criticalPegDef : normalPegDef;
    }

    private PegDefinition PickWeightedSpecial(System.Random rng)
    {
        float totalWeight = 0f;

        for (int i = 0; i < config.specialPegs.Length; i++)
        {
            var entry = config.specialPegs[i];
            if (entry == null || entry.definition == null) continue;
            if (entry.weight <= 0f) continue;

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

            int maxPerBoard = GetMaxPerBoard(entry.definition, entry.maxPerBoard);
            if (maxPerBoard > 0)
            {
                int current = specialCounts.TryGetValue(entry.definition, out int v) ? v : 0;
                if (current >= maxPerBoard) continue;
            }

            acc += entry.weight;
            if (pick <= acc)
            {
                if (!specialCounts.ContainsKey(entry.definition)) specialCounts[entry.definition] = 0;
                specialCounts[entry.definition]++;

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

    private BoardLayout PickLayout(System.Random rng)
    {
        if (generationProfile != null && !string.IsNullOrEmpty(runtimeProfileId)
            && generationProfile.TryGetProfile(
                battle != null ? battle.CurrentStageIndex : -1,
                battle != null ? battle.EncounterIndex : -1,
                battle != null ? battle.EncounterInStage : -1,
                out BoardGenerationProfile.EncounterProfile profile)
            && profile.allowedLayouts != null
            && profile.allowedLayouts.Length > 0)
        {
            float totalWeight = 0f;
            for (int i = 0; i < profile.allowedLayouts.Length; i++)
            {
                BoardGenerationProfile.LayoutWeight entry = profile.allowedLayouts[i];
                if (entry == null || entry.layout == null || entry.weight <= 0f)
                    continue;

                totalWeight += entry.weight;
            }

            if (totalWeight > 0f)
            {
                float pick = (float)(rng.NextDouble() * totalWeight);
                float acc = 0f;
                for (int i = 0; i < profile.allowedLayouts.Length; i++)
                {
                    BoardGenerationProfile.LayoutWeight entry = profile.allowedLayouts[i];
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