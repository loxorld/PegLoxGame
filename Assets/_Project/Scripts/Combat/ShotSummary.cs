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
    public int PredictedDamage;

    public ShotSummary(
        string orbName,
        int normalHits,
        int criticalHits,
        int damagePerHit,
        int multiplier)
    {
        OrbName = orbName;
        NormalHits = normalHits;
        CriticalHits = criticalHits;
        TotalHits = normalHits + criticalHits;
        DamagePerHit = damagePerHit;
        Multiplier = multiplier;
        PredictedDamage = TotalHits * DamagePerHit * Multiplier;
    }
}
