using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Behaviors/Volatil", fileName = "PegBehavior_Volatil")]
public class PegBehavior_Volatil : PegBehaviorBase
{
    [SerializeField, Range(0f, 1f)] private float doubleHitChance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float surviveChance = 0.15f;

    public override bool OnBallHit(Peg peg, Collision2D collision)
    {
        float roll = Random.value;

        if (roll < doubleHitChance)
        {
            ShotManager.Instance?.RegisterPegHit(peg.Type);
            return true;
        }

        if (roll < doubleHitChance + surviveChance)
            return false;

        return true;
    }
}