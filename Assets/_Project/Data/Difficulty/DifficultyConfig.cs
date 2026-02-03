using System.Globalization;
using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Run/Difficulty Config", fileName = "DifficultyConfig_")]
public class DifficultyConfig : ScriptableObject
{
    [Header("Encounter Progression")]
    [Tooltip("Si hay menos etapas que encounters, se usa la última.")]
    public DifficultyStage[] stages;

    public DifficultyStage GetStage(int encounterIndex)
    {
        if (stages == null || stages.Length == 0)
            return DifficultyStage.Default;

        int idx = Mathf.Clamp(encounterIndex, 0, stages.Length - 1);
        return stages[idx];
    }
}

[System.Serializable]
public struct DifficultyStage
{
    [Min(1)] public int enemiesToDefeat;

    [Header("Enemy Scaling")]
    [Min(0.1f)] public float enemyHpMultiplier;
    [Min(0.1f)] public float enemyDamageMultiplier;

    [Header("Optional Additive (after multiplier)")]
    public int enemyHpBonus;
    public int enemyDamageBonus;

    [Header("UI")]
    public string stageName;

    public string GetDisplayName(int encounterIndex)
    {
        return string.IsNullOrWhiteSpace(stageName) ? $"Stage {encounterIndex + 1}" : stageName;
    }

    public string GetHudText(int encounterIndex, int enemiesToDefeat)
    {
        string hpMultiplier = enemyHpMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
        string dmgMultiplier = enemyDamageMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{GetDisplayName(encounterIndex)} | HP x{hpMultiplier} ({FormatSigned(enemyHpBonus)}) | DMG x{dmgMultiplier} ({FormatSigned(enemyDamageBonus)}) | N={enemiesToDefeat}";
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
    }

    public static DifficultyStage Default => new DifficultyStage
    {
        stageName = "Default",
        enemiesToDefeat = 3,
        enemyHpMultiplier = 1f,
        enemyDamageMultiplier = 1f,
        enemyHpBonus = 0,
        enemyDamageBonus = 0
    };
}