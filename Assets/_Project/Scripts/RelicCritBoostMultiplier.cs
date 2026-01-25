using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Relics/Crit Boost Multiplier", fileName = "Relic_CritBoostMultiplier")]
public class Relic_CritBoostMultiplier : ShotEffectBase
{
    [SerializeField] private int extraPerCrit = 1;

    public override void OnShotEnd(ShotContext ctx)
    {
        // ctx.Multiplier ya fue seteado como 1 + CriticalHits
        ctx.Multiplier += ctx.CriticalHits * extraPerCrit;
    }
}
