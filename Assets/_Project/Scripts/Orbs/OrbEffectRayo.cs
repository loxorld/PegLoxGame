using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/Rayo", fileName = "OrbEffect_Rayo")]
public class OrbEffectRayo : ShotEffectBase
{
    [SerializeField, Range(0f, 1f)] private float procChance = 0.2f;
    [SerializeField, Min(1)] private int bonusDamage = 3;

    public override void OnPegHit(ShotContext ctx, PegType pegType)
    {
        if (Random.value > procChance) return;

        ctx.BonusDamage += bonusDamage;

        if (DebugConfig.LogOrbEffects)
            Debug.Log($"[OrbEffect][Rayo] +{bonusDamage} bonus damage (proc)");
    }
}