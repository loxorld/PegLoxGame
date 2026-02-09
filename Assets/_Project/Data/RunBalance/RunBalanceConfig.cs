using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Run/Balance Config", fileName = "RunBalanceConfig_")]
public class RunBalanceConfig : ScriptableObject
{
    [Header("Reward Curves (Encounter Index) ")]
    public AnimationCurve encounterCoinsMinCurve = AnimationCurve.Linear(0f, 4f, 5f, 8f);
    public AnimationCurve encounterCoinsMaxCurve = AnimationCurve.Linear(0f, 8f, 5f, 14f);

    [Header("Reward Chances (Encounter Index)")]
    public AnimationCurve chanceOrbCurve = AnimationCurve.Linear(0f, 0.6f, 5f, 0.45f);
    public AnimationCurve chanceOrbUpgradeCurve = AnimationCurve.Linear(0f, 0.15f, 5f, 0.3f);

    [Header("Event Curves (Encounter Index)")]
    public AnimationCurve eventGoodChanceCurve = AnimationCurve.Linear(0f, 0.55f, 5f, 0.45f);
    public AnimationCurve eventCoinsRewardMinCurve = AnimationCurve.Linear(0f, 5f, 5f, 9f);
    public AnimationCurve eventCoinsRewardMaxCurve = AnimationCurve.Linear(0f, 12f, 5f, 18f);
    public AnimationCurve eventCoinsPenaltyMinCurve = AnimationCurve.Linear(0f, 3f, 5f, 6f);
    public AnimationCurve eventCoinsPenaltyMaxCurve = AnimationCurve.Linear(0f, 8f, 5f, 12f);
    public AnimationCurve eventHealMinCurve = AnimationCurve.Linear(0f, 3f, 5f, 6f);
    public AnimationCurve eventHealMaxCurve = AnimationCurve.Linear(0f, 6f, 5f, 10f);
    public AnimationCurve eventDamageMinCurve = AnimationCurve.Linear(0f, 2f, 5f, 4f);
    public AnimationCurve eventDamageMaxCurve = AnimationCurve.Linear(0f, 5f, 5f, 8f);

    [Header("Shop Curves (Encounter Index)")]
    public AnimationCurve shopHealCostCurve = AnimationCurve.Linear(0f, 10f, 5f, 18f);
    public AnimationCurve shopHealAmountCurve = AnimationCurve.Linear(0f, 8f, 5f, 12f);
    public AnimationCurve shopOrbUpgradeCostCurve = AnimationCurve.Linear(0f, 15f, 5f, 25f);

    [Header("Enemy Curves (Encounter Index)")]
    public AnimationCurve enemyHpMultiplierCurve = AnimationCurve.Linear(0f, 1f, 5f, 1.2f);
    public AnimationCurve enemyDamageMultiplierCurve = AnimationCurve.Linear(0f, 1f, 5f, 1.1f);

    [Header("Boss & Event Frequency (Encounter Index)")]
    public AnimationCurve bossAfterNodesCurve = AnimationCurve.Linear(0f, 10f, 5f, 8f);

    public static RunBalanceConfig LoadDefault()
    {
        return Resources.Load<RunBalanceConfig>("RunBalanceConfig_");
    }

    public int GetEncounterCoinsMin(int stageIndex, int fallback)
    {
        return EvaluateInt(encounterCoinsMinCurve, stageIndex, fallback, 0);
    }

    public int GetEncounterCoinsMax(int stageIndex, int fallback)
    {
        return EvaluateInt(encounterCoinsMaxCurve, stageIndex, fallback, 0);
    }

    public float GetChanceOrb(int stageIndex, float fallback)
    {
        return EvaluateChance(chanceOrbCurve, stageIndex, fallback);
    }

    public float GetChanceOrbUpgrade(int stageIndex, float fallback)
    {
        return EvaluateChance(chanceOrbUpgradeCurve, stageIndex, fallback);
    }

    public float GetEventPositiveChance(int stageIndex, float fallback)
    {
        return EvaluateChance(eventGoodChanceCurve, stageIndex, fallback);
    }

    public int GetEventCoinsRewardMin(int stageIndex, int fallback)
    {
        return EvaluateInt(eventCoinsRewardMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventCoinsRewardMax(int stageIndex, int fallback)
    {
        return EvaluateInt(eventCoinsRewardMaxCurve, stageIndex, fallback, 0);
    }

    public int GetEventCoinsPenaltyMin(int stageIndex, int fallback)
    {
        return EvaluateInt(eventCoinsPenaltyMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventCoinsPenaltyMax(int stageIndex, int fallback)
    {
        return EvaluateInt(eventCoinsPenaltyMaxCurve, stageIndex, fallback, 0);
    }

    public int GetEventHealMin(int stageIndex, int fallback)
    {
        return EvaluateInt(eventHealMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventHealMax(int stageIndex, int fallback)
    {
        return EvaluateInt(eventHealMaxCurve, stageIndex, fallback, 0);
    }

    public int GetEventDamageMin(int stageIndex, int fallback)
    {
        return EvaluateInt(eventDamageMinCurve, stageIndex, fallback, 0);
    }

    public int GetEventDamageMax(int stageIndex, int fallback)
    {
        return EvaluateInt(eventDamageMaxCurve, stageIndex, fallback, 0);
    }

    public int GetShopHealCost(int stageIndex, int fallback)
    {
        return EvaluateInt(shopHealCostCurve, stageIndex, fallback, 0);
    }

    public int GetShopHealAmount(int stageIndex, int fallback)
    {
        return EvaluateInt(shopHealAmountCurve, stageIndex, fallback, 1);
    }

    public int GetShopOrbUpgradeCost(int stageIndex, int fallback)
    {
        return EvaluateInt(shopOrbUpgradeCostCurve, stageIndex, fallback, 0);
    }

    public float GetEnemyHpMultiplier(int stageIndex, float fallback)
    {
        return EvaluateFloat(enemyHpMultiplierCurve, stageIndex, fallback, 0.1f);
    }

    public float GetEnemyDamageMultiplier(int stageIndex, float fallback)
    {
        return EvaluateFloat(enemyDamageMultiplierCurve, stageIndex, fallback, 0.1f);
    }

    public int GetBossAfterNodes(int stageIndex, int fallback)
    {
        return EvaluateInt(bossAfterNodesCurve, stageIndex, fallback, 1);
    }

    private static float EvaluateChance(AnimationCurve curve, int stageIndex, float fallback)
    {
        float value = EvaluateFloat(curve, stageIndex, fallback, 0f);
        return Mathf.Clamp01(value);
    }

    private static float EvaluateFloat(AnimationCurve curve, int stageIndex, float fallback, float min)
    {
        if (curve == null || curve.length == 0)
            return Mathf.Max(min, fallback);

        return Mathf.Max(min, curve.Evaluate(stageIndex));
    }

    private static int EvaluateInt(AnimationCurve curve, int stageIndex, int fallback, int min)
    {
        if (curve == null || curve.length == 0)
            return Mathf.Max(min, fallback);

        return Mathf.Max(min, Mathf.RoundToInt(curve.Evaluate(stageIndex)));
    }
}