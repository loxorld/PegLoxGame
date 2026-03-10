using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatEncounterType
{
    Regular,
    Elite,
    MiniBoss,
    Boss
}

public readonly struct CombatEncounterProfile
{
    public CombatEncounterProfile(
        CombatEncounterType type,
        string label,
        float hpMultiplier,
        float damageMultiplier,
        int bonusCoins,
        bool singleEnemy,
        bool guaranteesRelicChoice,
        bool guaranteesUpgradeChoice)
    {
        Type = type;
        Label = label ?? string.Empty;
        HpMultiplier = Mathf.Max(0.1f, hpMultiplier);
        DamageMultiplier = Mathf.Max(0.1f, damageMultiplier);
        BonusCoins = Mathf.Max(0, bonusCoins);
        SingleEnemy = singleEnemy;
        GuaranteesRelicChoice = guaranteesRelicChoice;
        GuaranteesUpgradeChoice = guaranteesUpgradeChoice;
    }

    public CombatEncounterType Type { get; }
    public string Label { get; }
    public float HpMultiplier { get; }
    public float DamageMultiplier { get; }
    public int BonusCoins { get; }
    public bool SingleEnemy { get; }
    public bool GuaranteesRelicChoice { get; }
    public bool GuaranteesUpgradeChoice { get; }
    public bool IsSpecial => Type == CombatEncounterType.Elite || Type == CombatEncounterType.MiniBoss;

    public static CombatEncounterProfile CreateRegular()
    {
        return new CombatEncounterProfile(CombatEncounterType.Regular, "Combate", 1f, 1f, 0, false, false, false);
    }

    public static CombatEncounterProfile CreateBoss()
    {
        return new CombatEncounterProfile(CombatEncounterType.Boss, "Boss", 1f, 1f, 0, true, true, true);
    }
}

public sealed class CombatEncounterProfilePlanner
{
    public CombatEncounterProfile Resolve(
        RunBalanceConfig balance,
        int stageIndex,
        int encounterInStage,
        bool isBossEncounter,
        bool hasEliteCandidates,
        bool hasMiniBossCandidates)
    {
        if (isBossEncounter)
            return CombatEncounterProfile.CreateBoss();

        int eliteStart = balance != null ? balance.GetEliteFirstEncounterInStage(stageIndex, 1) : 1;
        int eliteCadence = balance != null ? balance.GetEliteEncounterCadence(stageIndex, 2) : 2;
        int miniBossEncounter = balance != null ? balance.GetMiniBossEncounterInStage(stageIndex, 2) : 2;

        if (hasMiniBossCandidates && encounterInStage == miniBossEncounter)
        {
            float hpMultiplier = balance != null ? balance.GetMiniBossHpMultiplier(stageIndex, 1.8f) : 1.8f;
            float damageMultiplier = balance != null ? balance.GetMiniBossDamageMultiplier(stageIndex, 1.35f) : 1.35f;
            int bonusCoins = balance != null ? balance.GetMiniBossCoinsBonus(stageIndex, 10) : 10;
            return new CombatEncounterProfile(CombatEncounterType.MiniBoss, "Miniboss", hpMultiplier, damageMultiplier, bonusCoins, true, true, true);
        }

        bool eliteEligible = hasEliteCandidates
            && encounterInStage >= eliteStart
            && eliteCadence > 0
            && ((encounterInStage - eliteStart) % eliteCadence == 0);
        if (eliteEligible)
        {
            float hpMultiplier = balance != null ? balance.GetEliteHpMultiplier(stageIndex, 1.35f) : 1.35f;
            float damageMultiplier = balance != null ? balance.GetEliteDamageMultiplier(stageIndex, 1.15f) : 1.15f;
            int bonusCoins = balance != null ? balance.GetEliteCoinsBonus(stageIndex, 6) : 6;
            return new CombatEncounterProfile(CombatEncounterType.Elite, "Elite", hpMultiplier, damageMultiplier, bonusCoins, true, true, false);
        }

        return CombatEncounterProfile.CreateRegular();
    }
}

public sealed class EnemyEncounterSelector
{
    public List<EnemyData> BuildRegularEncounterCandidates(IReadOnlyList<EnemyData> source, int stageIndex, int encounterInStage)
    {
        return BuildCandidates(
            source,
            enemy => enemy.IsAvailableForRegularEncounter(stageIndex, encounterInStage),
            enemy => enemy.AllowRegularEncounters);
    }

    public List<EnemyData> BuildEliteEncounterCandidates(IReadOnlyList<EnemyData> source, int stageIndex, int encounterInStage)
    {
        return BuildCandidates(
            source,
            enemy => enemy.IsAvailableForEliteEncounter(stageIndex, encounterInStage),
            enemy => enemy.AllowEliteEncounters && enemy.IsAvailableForStage(stageIndex));
    }

    public List<EnemyData> BuildMiniBossEncounterCandidates(IReadOnlyList<EnemyData> source, int stageIndex, int encounterInStage)
    {
        return BuildCandidates(
            source,
            enemy => enemy.IsAvailableForMiniBossEncounter(stageIndex, encounterInStage),
            enemy => enemy.AllowMiniBossEncounters && enemy.IsAvailableForStage(stageIndex));
    }

    public EnemyData SelectRegularEncounterEnemy(
        IReadOnlyList<EnemyData> source,
        int stageIndex,
        int encounterInStage,
        Func<int, int, int> randomRange = null)
    {
        List<EnemyData> candidates = BuildRegularEncounterCandidates(source, stageIndex, encounterInStage);
        if (candidates.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Mathf.Max(1, candidates[i].EncounterWeight);

        Func<int, int, int> range = randomRange ?? ((min, max) => UnityEngine.Random.Range(min, max));
        int roll = range(0, totalWeight);
        int runningWeight = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyData candidate = candidates[i];
            runningWeight += Mathf.Max(1, candidate.EncounterWeight);
            if (roll < runningWeight)
                return candidate;
        }

        return candidates[candidates.Count - 1];
    }

    public EnemyData SelectEliteEncounterEnemy(
        IReadOnlyList<EnemyData> source,
        int stageIndex,
        int encounterInStage,
        Func<int, int, int> randomRange = null)
    {
        List<EnemyData> candidates = BuildEliteEncounterCandidates(source, stageIndex, encounterInStage);
        return SelectWeighted(candidates, randomRange, enemy => enemy.EliteEncounterWeight);
    }

    public EnemyData SelectMiniBossEncounterEnemy(
        IReadOnlyList<EnemyData> source,
        int stageIndex,
        int encounterInStage,
        Func<int, int, int> randomRange = null)
    {
        List<EnemyData> candidates = BuildMiniBossEncounterCandidates(source, stageIndex, encounterInStage);
        return SelectWeighted(candidates, randomRange, enemy => enemy.MiniBossEncounterWeight);
    }

    private static List<EnemyData> BuildCandidates(
        IReadOnlyList<EnemyData> source,
        Func<EnemyData, bool> availabilityPredicate,
        Func<EnemyData, bool> fallbackPredicate)
    {
        var candidates = new List<EnemyData>();
        if (source == null)
            return candidates;

        var seen = new HashSet<EnemyData>();
        for (int i = 0; i < source.Count; i++)
        {
            EnemyData enemy = source[i];
            if (enemy == null || !seen.Add(enemy))
                continue;

            if (availabilityPredicate(enemy))
                candidates.Add(enemy);
        }

        if (candidates.Count > 0)
            return candidates;

        for (int i = 0; i < source.Count; i++)
        {
            EnemyData enemy = source[i];
            if (enemy == null || !fallbackPredicate(enemy) || candidates.Contains(enemy))
                continue;

            candidates.Add(enemy);
        }

        return candidates;
    }

    private static EnemyData SelectWeighted(
        IReadOnlyList<EnemyData> candidates,
        Func<int, int, int> randomRange,
        Func<EnemyData, int> weightSelector)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Mathf.Max(1, weightSelector(candidates[i]));

        Func<int, int, int> range = randomRange ?? ((min, max) => UnityEngine.Random.Range(min, max));
        int roll = range(0, totalWeight);
        int runningWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            EnemyData candidate = candidates[i];
            runningWeight += Mathf.Max(1, weightSelector(candidate));
            if (roll < runningWeight)
                return candidate;
        }

        return candidates[candidates.Count - 1];
    }
}
