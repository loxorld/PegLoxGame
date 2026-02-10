using System;
using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Run/Balance Config", fileName = "RunBalanceConfig_")]
public class RunBalanceConfig : ScriptableObject
{
    [Header("Reward Curves (Stage Index)")]
    public AnimationCurve stageCoinsMinCurve = AnimationCurve.Linear(0f, 4f, 2f, 10f);
    public AnimationCurve stageCoinsMaxCurve = AnimationCurve.Linear(0f, 8f, 2f, 16f);

    [Header("Reward Chances (Stage Index)")]
    public AnimationCurve stageChanceOrbCurve = AnimationCurve.Linear(0f, 0.6f, 2f, 0.45f);
    public AnimationCurve stageChanceOrbUpgradeCurve = AnimationCurve.Linear(0f, 0.15f, 2f, 0.3f);

    [Header("Event Curves (Stage Index)")]
    public AnimationCurve stageEventGoodChanceCurve = AnimationCurve.Linear(0f, 0.55f, 2f, 0.45f);
    public AnimationCurve stageEventCoinsRewardMinCurve = AnimationCurve.Linear(0f, 5f, 2f, 9f);
    public AnimationCurve stageEventCoinsRewardMaxCurve = AnimationCurve.Linear(0f, 12f, 2f, 18f);
    public AnimationCurve stageEventCoinsPenaltyMinCurve = AnimationCurve.Linear(0f, 3f, 2f, 6f);
    public AnimationCurve stageEventCoinsPenaltyMaxCurve = AnimationCurve.Linear(0f, 8f, 2f, 12f);
    public AnimationCurve stageEventHealMinCurve = AnimationCurve.Linear(0f, 3f, 2f, 6f);
    public AnimationCurve stageEventHealMaxCurve = AnimationCurve.Linear(0f, 6f, 2f, 10f);
    public AnimationCurve stageEventDamageMinCurve = AnimationCurve.Linear(0f, 2f, 2f, 4f);
    public AnimationCurve stageEventDamageMaxCurve = AnimationCurve.Linear(0f, 5f, 2f, 8f);

    [Header("Shop Curves (Stage Index)")]
    public AnimationCurve stageShopHealCostCurve = AnimationCurve.Linear(0f, 10f, 2f, 18f);
    public AnimationCurve stageShopHealAmountCurve = AnimationCurve.Linear(0f, 8f, 2f, 12f);
    public AnimationCurve stageShopOrbUpgradeCostCurve = AnimationCurve.Linear(0f, 15f, 2f, 25f);

    [Header("Boss & Event Frequency (Stage Index)")]
    public AnimationCurve stageBossAfterNodesCurve = AnimationCurve.Linear(0f, 10f, 2f, 8f);

    [Header("Combat Stage Profiles")]
    public CombatStageBalance[] combatStageBalances =
    {
        CombatStageBalance.CreateDefault("Bosque", 1f, 1f, 1),
        CombatStageBalance.CreateDefault("Pantano", 1.9f, 1.5f, 2),
        CombatStageBalance.CreateDefault("Castillo", 2.7f, 1.95f, 3)
    };

    public static RunBalanceConfig LoadDefault()
    {
        return Resources.Load<RunBalanceConfig>("RunBalanceConfig_");
    }

    public int GetEncounterCoinsMin(int stageIndex, int fallback)
    {
        return EvaluateInt(stageCoinsMinCurve, stageIndex, fallback, 0);
    }

    public int GetEncounterCoinsMax(int stageIndex, int fallback)
    {
        return EvaluateInt(stageCoinsMaxCurve, stageIndex, fallback, 0);
    }

    public float GetChanceOrb(int stageIndex, float fallback)
    {
        return EvaluateChance(stageChanceOrbCurve, stageIndex, fallback);
    }

    public float GetChanceOrbUpgrade(int stageIndex, float fallback)
    {
        return EvaluateChance(stageChanceOrbUpgradeCurve, stageIndex, fallback);
    }

    public float GetEventPositiveChance(int stageIndex, float fallback)
    {
        return EvaluateChance(stageEventGoodChanceCurve, stageIndex, fallback);
    }

    public int GetEventCoinsRewardMin(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventCoinsRewardMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventCoinsRewardMax(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventCoinsRewardMaxCurve, stageIndex, fallback, 0);
    }

    public int GetEventCoinsPenaltyMin(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventCoinsPenaltyMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventCoinsPenaltyMax(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventCoinsPenaltyMaxCurve, stageIndex, fallback, 0);
    }

    public int GetEventHealMin(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventHealMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventHealMax(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventHealMaxCurve, stageIndex, fallback, 0);
    }

    public int GetEventDamageMin(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventDamageMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventDamageMax(int stageIndex, int fallback)
    {
        return EvaluateInt(stageEventDamageMaxCurve, stageIndex, fallback, 0);
    }

    public int GetShopHealCost(int stageIndex, int fallback)
    {
        return EvaluateInt(stageShopHealCostCurve, stageIndex, fallback, 0);
    }

    public int GetShopHealAmount(int stageIndex, int fallback)
    {
        return EvaluateInt(stageShopHealAmountCurve, stageIndex, fallback, 1);
    }

    public int GetShopOrbUpgradeCost(int stageIndex, int fallback)
    {
        return EvaluateInt(stageShopOrbUpgradeCostCurve, stageIndex, fallback, 0);
    }

    public float GetEnemyHpMultiplier(int stageIndex, int encounterInStage, float fallback)
    {
        CombatStageBalance stageBalance = ResolveCombatStage(stageIndex);
        float stageScale = stageBalance != null ? Mathf.Max(0.1f, stageBalance.enemyHpScaleByStage) : 1f;
        float encounterScale = stageBalance != null ? EvaluateFloat(stageBalance.enemyHpScaleByEncounterCurve, encounterInStage, 1f, 0.1f) : 1f;
        return Mathf.Max(0.1f, fallback * stageScale * encounterScale);
    }

    public float GetEnemyDamageMultiplier(int stageIndex, int encounterInStage, float fallback)
    {
        CombatStageBalance stageBalance = ResolveCombatStage(stageIndex);
        float stageScale = stageBalance != null ? Mathf.Max(0.1f, stageBalance.enemyDamageScaleByStage) : 1f;
        float encounterScale = stageBalance != null ? EvaluateFloat(stageBalance.enemyDamageScaleByEncounterCurve, encounterInStage, 1f, 0.1f) : 1f;
        return Mathf.Max(0.1f, fallback * stageScale * encounterScale);
    }

    public int GetEnemiesToDefeat(int stageIndex, int encounterInStage, int fallback)
    {
        CombatStageBalance stageBalance = ResolveCombatStage(stageIndex);
        int baseEnemies = fallback > 0 ? fallback : (stageBalance != null ? stageBalance.enemiesToDefeatByStage : 1);
        float encounterScale = stageBalance != null ? EvaluateFloat(stageBalance.enemiesToDefeatByEncounterCurve, encounterInStage, 1f, 0.1f) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseEnemies * encounterScale));
    }

    public string GetStageDisplayName(int stageIndex, string fallback = null)
    {
        CombatStageBalance stageBalance = ResolveCombatStage(stageIndex);
        if (stageBalance == null || string.IsNullOrWhiteSpace(stageBalance.stageName))
            return string.IsNullOrWhiteSpace(fallback) ? $"Stage {stageIndex + 1}" : fallback;

        return stageBalance.stageName;
    }

    public int GetBossAfterNodes(int stageIndex, int fallback)
    {
        return EvaluateInt(stageBossAfterNodesCurve, stageIndex, fallback, 1);
    }

    private CombatStageBalance ResolveCombatStage(int stageIndex)
    {
        if (combatStageBalances == null || combatStageBalances.Length == 0)
            return null;

        int clamped = Mathf.Clamp(stageIndex, 0, combatStageBalances.Length - 1);
        return combatStageBalances[clamped];
    }

    private static float EvaluateChance(AnimationCurve curve, int stageIndex, float fallback)
    {
        float value = EvaluateFloat(curve, stageIndex, fallback, 0f);
        return Mathf.Clamp01(value);
    }

    private static float EvaluateFloat(AnimationCurve curve, int sampleIndex, float fallback, float min)
    {
        if (curve == null || curve.length == 0)
            return Mathf.Max(min, fallback);

        return Mathf.Max(min, curve.Evaluate(sampleIndex));
    }

    private static int EvaluateInt(AnimationCurve curve, int sampleIndex, int fallback, int min)
    {
        if (curve == null || curve.length == 0)
            return Mathf.Max(min, fallback);

        return Mathf.Max(min, Mathf.RoundToInt(curve.Evaluate(sampleIndex)));
    }
}

[Serializable]
public class CombatStageBalance
{
    public string stageName;

    [Header("Stage Scale")]
    [Min(0.1f)] public float enemyHpScaleByStage = 1f;
    [Min(0.1f)] public float enemyDamageScaleByStage = 1f;
    [Min(1)] public int enemiesToDefeatByStage = 3;

    [Header("Encounter In Stage Scale")]
    public AnimationCurve enemyHpScaleByEncounterCurve = AnimationCurve.Linear(0f, 1f, 2f, 1f);
    public AnimationCurve enemyDamageScaleByEncounterCurve = AnimationCurve.Linear(0f, 1f, 2f, 1f);
    public AnimationCurve enemiesToDefeatByEncounterCurve = AnimationCurve.Linear(0f, 1f, 2f, 1f);

    public static CombatStageBalance CreateDefault(string name, float hpScale, float damageScale, int enemiesToDefeat)
    {
        return new CombatStageBalance
        {
            stageName = name,
            enemyHpScaleByStage = hpScale,
            enemyDamageScaleByStage = damageScale,
            enemiesToDefeatByStage = Mathf.Max(1, enemiesToDefeat),
            enemyHpScaleByEncounterCurve = AnimationCurve.Linear(0f, 1f, 2f, 1f),
            enemyDamageScaleByEncounterCurve = AnimationCurve.Linear(0f, 1f, 2f, 1f),
            enemiesToDefeatByEncounterCurve = AnimationCurve.Linear(0f, 1f, 2f, 1f)
        };
    }
}