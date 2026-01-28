using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Behaviors/Durable", fileName = "PegBehavior_Durable")]
public class PegBehavior_Durable : PegBehaviorBase
{
    [Header("Durability")]
    [SerializeField, Min(1)] private int hitsToBreak = 2;

    [Header("Visuals")]
    [SerializeField] private Color firstHitColor = new Color(1f, 0.75f, 0.25f, 1f); // "dañado"

    public override void OnResetForEncounter(Peg peg)
    {
        if (peg == null) return;
        peg.SetHitPoints(hitsToBreak);
        peg.SetColorToIdle(); // asegura idle al reset
    }

    public override bool OnBallHit(Peg peg)
    {
        if (peg == null) return true;

        // Restamos 1 HP
        int remaining = peg.ConsumeOneHitPoint();

        // Si todavía aguanta, NO se consume y cambia color
        if (remaining > 0)
        {
            peg.SetColor(firstHitColor);
            return false;
        }

        // 0 => se rompe
        return true;
    }
}
