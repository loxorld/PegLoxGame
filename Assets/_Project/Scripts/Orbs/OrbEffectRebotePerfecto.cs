using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/Rebote Perfecto", fileName = "OrbEffect_RebotePerfecto")]
public class OrbEffectRebotePerfecto : ShotEffectBase
{
    [SerializeField, Min(0)] private int bonusDamagePerHit = 1;

    private int hitCount;

    public override void OnShotStart(ShotContext ctx)
    {
        hitCount = 0;
    }

    public override void OnPegHit(ShotContext ctx, PegType pegType)
    {
        hitCount++;
        ctx.DamagePerHit += bonusDamagePerHit;

        if (DebugConfig.LogOrbEffects)
            Debug.Log($"[OrbEffect][RebotePerfecto] +{bonusDamagePerHit} DPH -> {ctx.DamagePerHit} (hit #{hitCount})");
    }
}