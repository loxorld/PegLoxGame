using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/First Crit Double DPH", fileName = "OrbEffect_FirstCritDoubleDPH")]
public class OrbEffectFirstCritDoubleDPH : ShotEffectBase
{
    public override void OnShotStart(ShotContext ctx)
    {
        ctx.FirstCritBonusApplied = false;
    }

    public override void OnPegHit(ShotContext ctx, PegType pegType)
    {
        if (ctx.FirstCritBonusApplied) return;
        if (pegType != PegType.Critical) return;

        ctx.DamagePerHit *= 2;
        ctx.FirstCritBonusApplied = true;
        if (DebugConfig.LogOrbEffects)
            UnityEngine.Debug.Log($"[OrbEffect][FirstCritDouble] DPH doubled -> {ctx.DamagePerHit}");
    }
}
