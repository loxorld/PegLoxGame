using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Peg Definition", fileName = "PegDef_")]
public class PegDefinition : ScriptableObject
{
    public PegType type = PegType.Normal;

    [Header("Visuals")]
    public Color idleColor = Color.cyan;
    public Color hitColor = Color.gray;

    [Header("Rules (future-proof)")]
    public bool countsForMultiplier = false; // por si después metés peg especiales
}
