using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Peg Definition", fileName = "PegDef_")]
public class PegDefinition : ScriptableObject
{
    public PegType type = PegType.Normal;

    [Header("Visuals")]
    public Color idleColor = Color.cyan;
    public Color hitColor = Color.gray;

    [Header("Rules (future-proof)")]
    [Tooltip("Si true, este peg cuenta para el multiplicador (por ejemplo críticos).")]
    public bool countsForMultiplier = false;
}
