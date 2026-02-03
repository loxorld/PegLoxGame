using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/Sangrado", fileName = "OrbEffect_Sangrado")]
public class OrbEffectSangrado : ShotEffectBase
{
    [SerializeField, Min(0)] private int bleedDamagePerHit = 1;

    public override void OnShotEnd(ShotContext ctx)
    {
        if (ctx == null) return;

        int bonus = ctx.TotalHits * bleedDamagePerHit;
        ctx.BonusDamage += bonus;

        if (DebugConfig.LogOrbEffects && bonus > 0)
            Debug.Log($"[OrbEffect][Sangrado] +{bonus} bonus damage ({ctx.TotalHits} hits)");
    }
}