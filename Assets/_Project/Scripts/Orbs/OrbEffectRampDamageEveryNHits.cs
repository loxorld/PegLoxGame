using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/Ramp Damage Every N Hits", fileName = "OrbEffect_RampDamageEveryNHits")]
public class OrbEffectRampDamageEveryNHits : ShotEffectBase
{
    [SerializeField] private int hitsStep = 3;
    [SerializeField] private int damageBonus = 1;

    public override void OnShotStart(ShotContext ctx)
    {
        ctx.HitsAppliedForThisShot = 0;
    }

    public override void OnPegHit(ShotContext ctx, PegType pegType)
    {
        ctx.HitsAppliedForThisShot++;

        if (hitsStep <= 0) return;

        if (ctx.HitsAppliedForThisShot % hitsStep == 0)
        {
            ctx.DamagePerHit += damageBonus;

            if (DebugConfig.LogOrbEffects)
                UnityEngine.Debug.Log($"[OrbEffect][Ramp] +{damageBonus} DPH -> {ctx.DamagePerHit} (hit #{ctx.HitsAppliedForThisShot})");
        }
    }
}
