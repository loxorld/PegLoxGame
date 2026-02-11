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

    [Header("Animation")]
    [Tooltip("Animator Controller opcional por tipo de peg (Normal/Bomb/Teleport/Durable/etc).")]
    public RuntimeAnimatorController animatorController;

    [Tooltip("Delay para ocultar el sprite después de trigger Consume cuando hay Animator.")]
    [Min(0f)] public float consumeHideDelay = 0.12f;

    [Header("Optional FX")]
    [Tooltip("Prefab opcional que se instancia al hit.")]
    public GameObject hitVfxPrefab;

    [Tooltip("Prefab opcional que se instancia al consumir.")]
    public GameObject consumeVfxPrefab;

    [Tooltip("Prefab opcional que se instancia al restaurar (Refresh).")]
    public GameObject restoreVfxPrefab;

    [Header("Optional Audio")]
    [Tooltip("SFX opcional por tipo para hit. Si no está, se usa AudioManager.PegHit global.")]
    public AudioClip customHitSfx;

    [Tooltip("SFX opcional por tipo al consumir.")]
    public AudioClip customConsumeSfx;

    [Tooltip("SFX opcional por tipo al restaurar.")]
    public AudioClip customRestoreSfx;

    [Header("Behaviors (data-driven)")]
    public PegBehaviorBase[] behaviors;

    [Header("Rules (future-proof)")]
    [Tooltip("Si true, este peg cuenta para el multiplicador (por ejemplo críticos).")]
    public bool countsForMultiplier = false;
}