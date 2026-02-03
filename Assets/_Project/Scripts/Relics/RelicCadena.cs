using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Relics/Cadena", fileName = "Relic_Cadena")]
public class RelicCadena : ShotEffectBase
{
    [SerializeField, Min(2)] private int chainThreshold = 4;
    [SerializeField, Min(1)] private int bonusDamage = 4;

    private PegType lastType;
    private int chainCount;
    private bool hasLastType;

    public override void OnShotStart(ShotContext ctx)
    {
        chainCount = 0;
        hasLastType = false;
    }

    public override void OnPegHit(ShotContext ctx, PegType pegType)
    {
        if (!hasLastType || pegType != lastType)
        {
            lastType = pegType;
            chainCount = 1;
            hasLastType = true;
        }
        else
        {
            chainCount++;
        }

        if (chainThreshold <= 0) return;

        if (chainCount >= chainThreshold && chainCount % chainThreshold == 0)
        {
            ctx.BonusDamage += bonusDamage;
            if (DebugConfig.LogOrbEffects)
                Debug.Log($"[Relic][Cadena] +{bonusDamage} bonus damage (chain {chainCount})");
        }
    }
}