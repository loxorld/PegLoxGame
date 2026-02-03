using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Behaviors/Enfriado", fileName = "PegBehavior_Enfriado")]
public class PegBehavior_Enfriado : PegBehaviorBase
{
    [SerializeField, Range(0.1f, 1f)] private float speedMultiplier = 0.7f;

    public override bool OnBallHit(Peg peg, Collision2D collision)
    {
        if (collision == null) return true;

        Rigidbody2D rb = collision.rigidbody;
        if (rb == null) return true;

        rb.linearVelocity *= speedMultiplier;
        return true;
    }
}