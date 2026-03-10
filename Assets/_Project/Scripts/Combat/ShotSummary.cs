using System;

[Serializable]
public struct ShotSummary
{
    public string OrbName;
    public int NormalHits;
    public int CriticalHits;
    public int TotalHits;
    public int DamagePerHit;
    public int Multiplier;
    public int BonusDamage;
    public int PredictedDamage;

    public ShotSummary(
        string orbName,
        int normalHits,
        int criticalHits,
        int damagePerHit,
        int multiplier,
        int bonusDamage,
        int predictedDamageOverride = -1)
    {
        OrbName = orbName;
        NormalHits = normalHits;
        CriticalHits = criticalHits;
        TotalHits = normalHits + criticalHits;
        DamagePerHit = damagePerHit;
        Multiplier = multiplier;
        BonusDamage = bonusDamage;
        PredictedDamage = predictedDamageOverride >= 0
            ? predictedDamageOverride
            : (TotalHits * DamagePerHit * Multiplier) + BonusDamage;
    }
}
