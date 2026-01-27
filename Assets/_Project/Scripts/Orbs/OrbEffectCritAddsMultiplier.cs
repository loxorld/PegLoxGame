using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/Crit Adds Multiplier", fileName = "OrbEffect_CritAddsMultiplier")]
public class OrbEffectCritAddsMultiplier : ShotEffectBase
{
    [SerializeField] private int bonusPerCrit = 1;

    public override void OnShotEnd(ShotContext ctx)
    {
        ctx.Multiplier += ctx.CriticalHits * bonusPerCrit;
    }
}
