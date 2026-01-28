using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Behaviors/Bomb", fileName = "PegBehavior_Bomb")]
public class PegBehavior_Bomb : PegBehaviorBase
{
    [Header("Explosion")]
    [SerializeField, Min(0.1f)] private float radius = 1.25f;

    [Tooltip("Capa donde están los pegs (misma que usás para overlap).")]
    [SerializeField] private LayerMask pegMask;

    public override bool OnBallHit(Peg peg)
    {
        if (peg == null) return true;

        // Consumir vecinos (incluye críticos si están dentro del radio)
        PegManager.Instance?.ConsumePegsInRadius(
            center: peg.transform.position,
            radius: radius,
            mask: pegMask,
            exclude: peg,
            countHits: true
        );

        // Este peg también se consume en este hit
        return true;
    }
}
