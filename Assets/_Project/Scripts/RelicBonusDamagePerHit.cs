using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Relics/Bonus Damage Per Hit", fileName = "Relic_BonusDamagePerHit")]
public class RelicBonusDamagePerHit : ShotEffectBase
{
    [SerializeField] private int bonus = 1;

    public override void OnShotStart(ShotContext ctx)
    {
        ctx.DamagePerHit += bonus;
    }
}
