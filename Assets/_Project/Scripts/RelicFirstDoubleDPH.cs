using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Relics/First Crit Double DPH", fileName = "Relic_FirstCritDoubleDPH")]
public class RelicFirstCritDoubleDPH : ShotEffectBase
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
        Debug.Log($"[Relic] First crit hit: DPH doubled to {ctx.DamagePerHit}");

    }
}
