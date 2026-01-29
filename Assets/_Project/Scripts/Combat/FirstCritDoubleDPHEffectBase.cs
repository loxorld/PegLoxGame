using UnityEngine;

public abstract class FirstCritDoubleDPHEffectBase : ShotEffectBase
{
    protected virtual bool ShouldLog => false;
    protected virtual string LogLabel => "FirstCritDouble";

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

        if (ShouldLog)
            Debug.Log($"[{LogLabel}] DPH doubled -> {ctx.DamagePerHit}");
    }
}