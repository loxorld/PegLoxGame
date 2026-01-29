using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orbs/Effects/First Crit Double DPH", fileName = "OrbEffect_FirstCritDoubleDPH")]
public class OrbEffectFirstCritDoubleDPH : FirstCritDoubleDPHEffectBase
{
    protected override bool ShouldLog => DebugConfig.LogOrbEffects;
    protected override string LogLabel => "OrbEffect FirstCritDouble";
}
