using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Pegs/Peg Definition", fileName = "PegDef_")]
public class PegDefinition : ScriptableObject
{
    public enum ColliderShape
    {
        Circle = 0,
        Capsule = 1,
        Polygon = 2
    }

    [System.Serializable]
    public class CollisionSettings
    {
        [Tooltip("Tipo de collider deseado. Por ahora se aplica Circle; Capsule/Polygon quedan reservados.")]
        public ColliderShape shape = ColliderShape.Circle;

        [Tooltip("Radio local para CircleCollider2D (antes de auto-fit).")]
        [Min(0.01f)] public float circleRadius = 0.5f;

        [Tooltip("Tamao local para colliders no circulares (future-proof).")]
        public Vector2 size = Vector2.one;

        [Tooltip("Offset local del collider.")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("Si true, ajusta automticamente el radio segn bounds visuales del sprite.")]
        public bool autoFitToSprite = true;

        [Tooltip("Escala extra aplicada sobre el auto-fit (1 = mismo tamao visual).")]
        [Min(0.1f)] public float autoFitScale = 1f;

        [Tooltip("Usa escala visual normalizada del peg para mantener tamao jugable consistente.")]
        public bool useNormalizedVisualSize = true;
    }

    public PegType type = PegType.Normal;

    [Header("Visuals")]
    [Tooltip("Sprite base del peg. Si no se asigna, se mantiene el sprite del prefab.")]
    public Sprite idleSprite;

    [Tooltip("Sprite para feedback de golpe. Si no se asigna, se usa idleSprite.")]
    public Sprite hitSprite;

    public Color idleColor = Color.cyan;

    [Tooltip("Color que se usa si quers feedback de 'hit' estndar")]
    public Color hitColor = Color.gray;

    [Header("Animation")]
    [Tooltip("Animator Controller opcional por tipo de peg (Normal/Bomb/Teleport/Durable/etc).")]
    public RuntimeAnimatorController animatorController;

    [Tooltip("Delay para ocultar el sprite despus de trigger Consume cuando hay Animator.")]
    [Min(0f)] public float consumeHideDelay = 0.12f;

    [Header("Optional FX")]
    [Tooltip("Prefab opcional que se instancia al hit.")]
    public GameObject hitVfxPrefab;

    [Tooltip("Prefab opcional que se instancia al consumir.")]
    public GameObject consumeVfxPrefab;

    [Tooltip("Prefab opcional que se instancia al restaurar (Refresh).")]
    public GameObject restoreVfxPrefab;

    [Header("Optional Audio")]
    [Tooltip("SFX opcional por tipo para hit. Si no est, se usa AudioManager.PegHit global.")]
    public AudioClip customHitSfx;

    [Tooltip("SFX opcional por tipo al consumir.")]
    public AudioClip customConsumeSfx;

    [Tooltip("SFX opcional por tipo al restaurar.")]
    public AudioClip customRestoreSfx;

    [Header("Behaviors (data-driven)")]
    public PegBehaviorBase[] behaviors;

    [Header("Rules (future-proof)")]
    [Tooltip("Si true, este peg cuenta para el multiplicador (por ejemplo crticos).")]
    public bool countsForMultiplier = false;

    [Header("Collision")]
    public CollisionSettings collision = new CollisionSettings();
}