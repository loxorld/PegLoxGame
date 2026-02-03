using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Relics/Recuperacion", fileName = "Relic_Recuperacion")]
public class RelicRecuperacion : ShotEffectBase
{
    private int lastEncounterIndex = -1;
    private bool usedThisEncounter;

    public override void OnShotStart(ShotContext ctx)
    {
        var flow = GameFlowManager.Instance;
        if (flow == null)
        {
            usedThisEncounter = false;
            lastEncounterIndex = -1;
            return;
        }

        if (flow.EncounterIndex != lastEncounterIndex)
        {
            lastEncounterIndex = flow.EncounterIndex;
            usedThisEncounter = false;
        }
    }

    public override void OnShotEnd(ShotContext ctx)
    {
        if (ctx == null) return;
        if (usedThisEncounter) return;
        if (ctx.TotalHits > 0) return;

        ctx.SkipCounterattack = true;
        usedThisEncounter = true;

        if (DebugConfig.LogOrbEffects)
            Debug.Log("[Relic][Recuperacion] Counterattack skipped (no hits). One use per encounter.");
    }
}