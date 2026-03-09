using System;
using System.Collections.Generic;
using UnityEngine;

public partial class BoardManager
{
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
        int band = GetVerticalBand(currentCell.row, rows);
        if (!CanAttemptSpecialPlacement(currentCell, cellPlans, assignedUntilIndex, band))
            return null;

        BoardConfig.SpecialPegSpawn selectedEntry = null;
        float totalWeight = 0f;
        for (int i = 0; i < config.specialPegs.Length; i++)
        {
            var entry = config.specialPegs[i];
            if (!IsEligibleSpecialEntry(entry)) continue;

            totalWeight += entry.weight;
            // Weighted reservoir sampling keeps the same probabilities without needing a second pass.
            if (rng.NextDouble() * totalWeight <= entry.weight)
                selectedEntry = entry;
        }

        return selectedEntry != null ? selectedEntry.definition : null;
    }

    private bool CanAttemptSpecialPlacement(CellPlan currentCell, List<CellPlan> cellPlans, int assignedUntilIndex, int band)
    {
        if (!CanPlaceSpecialInBand(band))
            return false;

        return CanPlaceSpecialByDistance(currentCell, cellPlans, assignedUntilIndex);
    }

    private bool IsEligibleSpecialEntry(BoardConfig.SpecialPegSpawn entry)
    {
        if (entry == null || entry.definition == null)
            return false;

        if (entry.weight <= 0f)
            return false;

        int maxPerBoard = GetMaxPerBoard(entry.definition, entry.maxPerBoard);
        if (maxPerBoard <= 0)
            return true;

        int current = specialCounts.TryGetValue(entry.definition, out int value) ? value : 0;
        return current < maxPerBoard;
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
}
