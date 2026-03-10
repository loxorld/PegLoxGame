using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Enemy Data", fileName = "EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName = "Goblin";
    public string epithet;
    [TextArea(2, 4)] public string combatHint;

    [Header("Base Stats")]
    public int maxHP = 50;
    public int attackDamage = 5;
    public Enemy enemyPrefab;

    [Header("Visuals")]
    [SerializeField] private Sprite worldSpriteOverride;

    [Header("Encounter Availability")]
    [SerializeField, Min(-1)] private int minStageIndex = -1;
    [SerializeField, Min(-1)] private int maxStageIndex = -1;
    [SerializeField, Min(0)] private int minEncounterInStage = 0;
    [SerializeField] private bool allowRegularEncounters = true;
    [SerializeField, Min(1)] private int encounterWeight = 1;

    [Header("Special Encounter Availability")]
    [SerializeField] private bool allowEliteEncounters;
    [SerializeField, Min(0)] private int minEliteEncounterInStage = 1;
    [SerializeField, Min(1)] private int eliteEncounterWeight = 1;
    [SerializeField] private bool allowMiniBossEncounters;
    [SerializeField, Min(0)] private int minMiniBossEncounterInStage = 2;
    [SerializeField, Min(1)] private int miniBossEncounterWeight = 1;

    [Header("Combat Pattern")]
    [SerializeField, Min(0)] private int flatDamageReduction;
    [SerializeField, Min(0)] private int rageDamageOnSurvive;
    [SerializeField, Min(0)] private int healOnSurvive;
    [SerializeField, Range(0f, 1f)] private float desperationThreshold;
    [SerializeField, Min(0)] private int desperationAttackBonus;
    [SerializeField, Min(0)] private int desperationHeal;

    public string DisplayName => string.IsNullOrWhiteSpace(epithet)
        ? enemyName
        : $"{enemyName} - {epithet}";

    public int MinStageIndex => minStageIndex;
    public int MaxStageIndex => maxStageIndex;
    public int MinEncounterInStage => minEncounterInStage;
    public bool AllowRegularEncounters => allowRegularEncounters;
    public int EncounterWeight => Mathf.Max(1, encounterWeight);
    public bool AllowEliteEncounters => allowEliteEncounters;
    public int EliteEncounterWeight => Mathf.Max(1, eliteEncounterWeight);
    public bool AllowMiniBossEncounters => allowMiniBossEncounters;
    public int MiniBossEncounterWeight => Mathf.Max(1, miniBossEncounterWeight);
    public int FlatDamageReduction => Mathf.Max(0, flatDamageReduction);
    public int RageDamageOnSurvive => Mathf.Max(0, rageDamageOnSurvive);
    public int HealOnSurvive => Mathf.Max(0, healOnSurvive);
    public float DesperationThreshold => Mathf.Clamp01(desperationThreshold);
    public int DesperationAttackBonus => Mathf.Max(0, desperationAttackBonus);
    public int DesperationHeal => Mathf.Max(0, desperationHeal);
    public Sprite WorldSpriteOverride => worldSpriteOverride;

    public bool IsAvailableForRegularEncounter(int stageIndex, int encounterInStage)
    {
        if (!allowRegularEncounters)
            return false;

        if (!IsAvailableForStage(stageIndex))
            return false;

        return encounterInStage >= minEncounterInStage;
    }

    public bool IsAvailableForEliteEncounter(int stageIndex, int encounterInStage)
    {
        if (!allowEliteEncounters)
            return false;

        if (!IsAvailableForStage(stageIndex))
            return false;

        return encounterInStage >= minEliteEncounterInStage;
    }

    public bool IsAvailableForMiniBossEncounter(int stageIndex, int encounterInStage)
    {
        if (!allowMiniBossEncounters)
            return false;

        if (!IsAvailableForStage(stageIndex))
            return false;

        return encounterInStage >= minMiniBossEncounterInStage;
    }

    public bool IsAvailableForStage(int stageIndex)
    {
        if (minStageIndex >= 0 && stageIndex < minStageIndex)
            return false;

        if (maxStageIndex >= 0 && stageIndex > maxStageIndex)
            return false;

        return true;
    }
}
