public class ShotContext
{
    public OrbData Orb { get; private set; }



    public int NormalHits { get; private set; }
    public int CriticalHits { get; private set; }

    public int BaseDamagePerHit { get; set; } = 2; // default si no hay orbe
    public int DamagePerHit { get; set; } = 2;     // puede ser modificado por efectos

    public int Multiplier { get; set; } = 1;       // puede ser modificado por efectos

    public int HitsAppliedForThisShot { get; set; } // contador genérico para efectos “por primeros N hits”

    public bool FirstCritBonusApplied { get; set; }
    public int TotalHits => NormalHits + CriticalHits;

    public ShotContext(OrbData orb)
    {
        Orb = orb;
        if (orb != null)
        {
            BaseDamagePerHit = orb.damagePerHit;
            DamagePerHit = orb.damagePerHit;
        }
    }

    public void RegisterHit(PegType type)
    {
        if (type == PegType.Critical) CriticalHits++;
        else NormalHits++;
    }
}
