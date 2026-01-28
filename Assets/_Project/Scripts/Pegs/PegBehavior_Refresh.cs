using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Behaviors/Refresh", fileName = "PegBehavior_Refresh")]
public class PegBehavior_Refresh : PegBehaviorBase
{
    public override bool OnBallHit(Peg peg)
    {
        // Consumimos este peg, y refrescamos el resto
        PegManager.Instance?.RefreshEncounterPegs(exclude: peg);
        return true;
    }
}
