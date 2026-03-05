using UnityEngine;

public static class PegSizingUtility
{
    private const float DefaultFallbackRadius = 0.15f;

    public static float CalculateVisualScaleMultiplier(PegDefinition definition, Sprite fallbackSprite)
    {
        float fallbackMaxDimension = GetSpriteMaxDimension(fallbackSprite);
        if (fallbackMaxDimension <= Mathf.Epsilon)
            return 1f;

        Sprite referenceSprite = definition != null && definition.idleSprite != null
            ? definition.idleSprite
            : fallbackSprite;

        float referenceDimension = GetSpriteMaxDimension(referenceSprite);
        if (referenceDimension <= Mathf.Epsilon)
            return 1f;

        return fallbackMaxDimension / referenceDimension;
    }

    public static float CalculateEffectiveWorldRadius(
        PegDefinition definition,
        Sprite fallbackSprite,
        float baseScale,
        float colliderRadius,
        float separationMargin)
    {
        float safeBaseScale = Mathf.Max(Mathf.Abs(baseScale), Mathf.Epsilon);
        float scaleMultiplier = CalculateVisualScaleMultiplier(definition, fallbackSprite);

        Sprite referenceSprite = definition != null && definition.idleSprite != null
            ? definition.idleSprite
            : fallbackSprite;

        float visualRadius = GetSpriteMaxDimension(referenceSprite) * 0.5f * safeBaseScale * scaleMultiplier;
        float desiredColliderRadius = Mathf.Max(colliderRadius, DefaultFallbackRadius) * safeBaseScale * scaleMultiplier;

        float effectiveRadius = Mathf.Max(visualRadius, desiredColliderRadius) + Mathf.Max(0f, separationMargin);
        return Mathf.Max(DefaultFallbackRadius, effectiveRadius);
    }

    public static bool CirclesOverlap(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB)
    {
        float combined = radiusA + radiusB;
        return (centerA - centerB).sqrMagnitude < combined * combined;
    }

    private static float GetSpriteMaxDimension(Sprite sprite)
    {
        if (sprite == null)
            return 0f;

        Vector2 size = sprite.bounds.size;
        return Mathf.Max(size.x, size.y);
    }
}
