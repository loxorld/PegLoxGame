using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Peg Definition", fileName = "PegDef_")]
public class PegDefinition : ScriptableObject
{
    public PegType type = PegType.Normal;

    [Header("Visuals")]
    [Tooltip("Sprite base del peg. Si no se asigna, se mantiene el sprite del prefab.")]
    public Sprite idleSprite;

    [Tooltip("Sprite para feedback de golpe. Si no se asigna, se usa idleSprite.")]
    public Sprite hitSprite;

    public Color idleColor = Color.cyan;

    [Tooltip("Color que se usa si querés feedback de 'hit' estándar")]
    public Color hitColor = Color.gray;

    [Header("Behaviors (data-driven)")]
    public PegBehaviorBase[] behaviors;

    [Header("Rules (future-proof)")]
    [Tooltip("Si true, este peg cuenta para el multiplicador (por ejemplo críticos).")]
    public bool countsForMultiplier = false;
}
