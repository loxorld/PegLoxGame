using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))]
public class PegColliderController : MonoBehaviour
{
    private Collider2D activeCollider;
    private SpriteRenderer spriteRenderer;
    private PegVisualController visualController;
    private PegDefinition definition;
    private Sprite fallbackSprite;
    private float baseUniformScale = 1f;
    private float fallbackLocalRadius;
    private bool hasCapturedFallbackLocalRadius;

    private void Awake()
    {
        activeCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        visualController = GetComponent<PegVisualController>();
        fallbackSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        CaptureFallbackLocalRadius();

        baseUniformScale = ResolveBaseUniformScale();
    }

    public void SetDefinition(PegDefinition pegDefinition)
    {
        if (activeCollider == null) activeCollider = GetComponent<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (visualController == null) visualController = GetComponent<PegVisualController>();
        if (fallbackSprite == null && spriteRenderer != null) fallbackSprite = spriteRenderer.sprite;
        CaptureFallbackLocalRadius();

        definition = pegDefinition;
        baseUniformScale = ResolveBaseUniformScale();

        ApplyCollisionSettings();
    }

    private void ApplyCollisionSettings()
    {
        if (activeCollider == null || definition == null || definition.collision == null)
            return;

        var settings = definition.collision;

        switch (settings.shape)
        {
            case PegDefinition.ColliderShape.Circle:
                ApplyCircle(settings);
                break;
            case PegDefinition.ColliderShape.Capsule:
            case PegDefinition.ColliderShape.Polygon:
                Debug.LogWarning($"{name}: collider shape {settings.shape} todavía no está implementado. Se mantiene el collider activo actual.", this);
                break;
        }
    }

    private void ApplyCircle(PegDefinition.CollisionSettings settings)
    {
        CircleCollider2D circle = activeCollider as CircleCollider2D;
        if (circle == null)
        {
            Debug.LogWarning($"{name}: se pidió CircleCollider2D pero el collider activo es {activeCollider.GetType().Name}.", this);
            return;
        }

        circle.offset = settings.offset;
        circle.radius = PegSizingUtility.CalculateLocalCircleRadius(
            definition,
            fallbackSprite,
            GetCurrentUniformScale(),
            baseUniformScale,
            fallbackLocalRadius
        );
    }

    private float ResolveBaseUniformScale()
    {
        if (visualController != null)
            return visualController.BaseUniformScale;

        return GetCurrentUniformScale();
    }

    private void CaptureFallbackLocalRadius()
    {
        if (hasCapturedFallbackLocalRadius)
            return;

        if (activeCollider is CircleCollider2D circle)
        {
            fallbackLocalRadius = circle.radius;
            hasCapturedFallbackLocalRadius = true;
        }
    }

    private float GetCurrentUniformScale()
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Epsilon);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (activeCollider == null) activeCollider = GetComponent<Collider2D>();

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.9f);
            Bounds spriteBounds = spriteRenderer.bounds;
            Gizmos.DrawWireCube(spriteBounds.center, spriteBounds.size);
        }

        if (activeCollider is CircleCollider2D circle)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.95f);
            Vector3 center = transform.TransformPoint(circle.offset);
            float maxScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            Gizmos.DrawWireSphere(center, circle.radius * maxScale);
        }
        else if (activeCollider != null)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.95f);
            Gizmos.DrawWireCube(activeCollider.bounds.center, activeCollider.bounds.size);
        }
    }
#endif
}
