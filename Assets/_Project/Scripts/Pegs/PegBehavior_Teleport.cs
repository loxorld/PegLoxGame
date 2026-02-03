using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Behaviors/Teleport", fileName = "PegBehavior_Teleport")]
public class PegBehavior_Teleport : PegBehaviorBase
{
    [SerializeField, Min(0f)] private float teleportOffsetRadius = 0.35f;

    public override bool OnBallHit(Peg peg, Collision2D collision)
    {
        if (peg == null || collision == null) return true;

        Rigidbody2D rb = collision.rigidbody;
        if (rb == null) return true;

        Peg target = PegManager.Instance?.GetRandomActivePeg(peg);
        if (target == null) return true;

        Vector2 offset = Random.insideUnitCircle * teleportOffsetRadius;
        rb.position = (Vector2)target.transform.position + offset;

        return true;
    }
}