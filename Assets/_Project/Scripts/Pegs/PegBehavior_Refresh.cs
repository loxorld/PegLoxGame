using UnityEngine;

public class PegBehavior_Refresh : PegBehaviorBase
{
    [SerializeField] private PegDefinition refreshDefinition; // PegDef_Refresh

    public override bool OnBallHit(Peg peg)
    {
        PegManager.Instance?.RefreshEncounterPegs(peg, refreshDefinition);
        return true; // normalmente el refresh se consume al pegarlo
    }
}
