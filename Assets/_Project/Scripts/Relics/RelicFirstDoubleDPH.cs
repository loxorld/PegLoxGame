using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Relics/First Crit Double DPH", fileName = "Relic_FirstCritDoubleDPH")]
public class RelicFirstCritDoubleDPH : FirstCritDoubleDPHEffectBase
{
    protected override bool ShouldLog => DebugConfig.LogOrbEffects;
    protected override string LogLabel => "Relic FirstCritDouble";
}
