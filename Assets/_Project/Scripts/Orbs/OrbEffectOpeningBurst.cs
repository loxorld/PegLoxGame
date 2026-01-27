using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/Opening Burst", fileName = "OrbEffect_OpeningBurst")]
public class OrbEffectOpeningBurst : ShotEffectBase
{
    [SerializeField] private int firstNHits = 5;
    [SerializeField] private int bonusDamagePerHit = 2;

    public override void OnShotStart(ShotContext ctx)
    {
        ctx.HitsAppliedForThisShot = 0;
    }

    public override void OnPegHit(ShotContext ctx, PegType pegType)
    {
        ctx.HitsAppliedForThisShot++;

        if(ctx.HitsAppliedForThisShot <= firstNHits)
{
            ctx.DamagePerHit += bonusDamagePerHit;

            if (DebugConfig.LogOrbEffects)
                UnityEngine.Debug.Log($"[OrbEffect][OpeningBurst] +{bonusDamagePerHit} DPH -> {ctx.DamagePerHit} (hit #{ctx.HitsAppliedForThisShot}/{firstNHits})");
        }



    }
}
