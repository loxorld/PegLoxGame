using UnityEngine;

public static class PegSizingUtility
{
    private const float DefaultFallbackRadius = 0.15f;

    public static float CalculateVisualScaleMultiplier(PegDefinition definition, Sprite fallbackSprite)
    {
        float fallbackMaxDimension = GetSpriteMaxDimension(fallbackSprite);
        if (fallbackMaxDimension <= Mathf.Epsilon)
            return 1f;

        Sprite referenceSprite = GetReferenceSprite(definition, fallbackSprite);
        float referenceDimension = GetSpriteMaxDimension(referenceSprite);
        if (referenceDimension <= Mathf.Epsilon)
            return 1f;

        return fallbackMaxDimension / referenceDimension;
    }

    public static float CalculateVisualWorldRadius(PegDefinition definition, Sprite fallbackSprite, float baseScale)
    {
        Sprite referenceSprite = GetReferenceSprite(definition, fallbackSprite);
        float referenceRadius = GetSpriteRadius(referenceSprite);
        if (referenceRadius <= Mathf.Epsilon)
            return 0f;

        float safeBaseScale = Mathf.Max(Mathf.Abs(baseScale), Mathf.Epsilon);
        float worldScale = safeBaseScale * CalculateVisualScaleMultiplier(definition, fallbackSprite);
        return referenceRadius * worldScale;
    }

    public static float CalculateColliderWorldRadius(
        PegDefinition definition,
        Sprite fallbackSprite,
        float baseScale,
        float fallbackLocalRadius)
    {
        float safeBaseScale = Mathf.Max(Mathf.Abs(baseScale), Mathf.Epsilon);
        float localRadius = CalculateConfiguredLocalColliderRadius(definition, fallbackSprite, fallbackLocalRadius);
        float normalizedScale = ShouldNormalizeCollider(definition)
            ? CalculateVisualScaleMultiplier(definition, fallbackSprite)
            : 1f;

        return Mathf.Max(DefaultFallbackRadius, localRadius * safeBaseScale * normalizedScale);
    }

    public static float CalculateLocalCircleRadius(
        PegDefinition definition,
        Sprite fallbackSprite,
        float currentUniformScale,
        float baseUniformScale,
        float fallbackLocalRadius)
    {
        float safeCurrentScale = Mathf.Max(Mathf.Abs(currentUniformScale), Mathf.Epsilon);
        float worldRadius = CalculateColliderWorldRadius(definition, fallbackSprite, baseUniformScale, fallbackLocalRadius);
        return Mathf.Max(0.01f, worldRadius / safeCurrentScale);
    }

    public static float CalculateEffectiveWorldRadius(
        PegDefinition definition,
        Sprite fallbackSprite,
        float baseScale,
        float colliderRadius,
        float separationMargin)
    {
        float safeBaseScale = Mathf.Max(Mathf.Abs(baseScale), Mathf.Epsilon);
        float visualRadius = CalculateVisualWorldRadius(definition, fallbackSprite, safeBaseScale);
        float desiredColliderRadius = CalculateColliderWorldRadius(definition, fallbackSprite, safeBaseScale, colliderRadius);

        float effectiveRadius = Mathf.Max(visualRadius, desiredColliderRadius) + Mathf.Max(0f, separationMargin);
        return Mathf.Max(DefaultFallbackRadius, effectiveRadius);
    }

    public static bool CirclesOverlap(Vector2 centerA, float radiusA, Vector2 centerB, float radiusB)
    {
        float combined = radiusA + radiusB;
        return (centerA - centerB).sqrMagnitude < combined * combined;
    }

    private static bool ShouldNormalizeCollider(PegDefinition definition)
    {
        return definition != null
            && definition.collision != null
            && definition.collision.useNormalizedVisualSize;
    }

    private static float CalculateConfiguredLocalColliderRadius(
        PegDefinition definition,
        Sprite fallbackSprite,
        float fallbackLocalRadius)
    {
        PegDefinition.CollisionSettings settings = definition != null ? definition.collision : null;
        if (settings == null)
            return Mathf.Max(0.01f, fallbackLocalRadius > 0f ? fallbackLocalRadius : DefaultFallbackRadius);

        if (settings.autoFitToSprite)
        {
            float spriteRadius = GetReferenceSpriteRadius(definition, fallbackSprite);
            if (spriteRadius > 0f)
                return Mathf.Max(0.01f, spriteRadius * Mathf.Max(0.1f, settings.autoFitScale));
        }

        float configuredRadius = settings.circleRadius > 0f ? settings.circleRadius : fallbackLocalRadius;
        return Mathf.Max(0.01f, configuredRadius);
    }

    private static Sprite GetReferenceSprite(PegDefinition definition, Sprite fallbackSprite)
    {
        return definition != null && definition.idleSprite != null
            ? definition.idleSprite
            : fallbackSprite;
    }

    private static float GetReferenceSpriteRadius(PegDefinition definition, Sprite fallbackSprite)
    {
        return GetSpriteRadius(GetReferenceSprite(definition, fallbackSprite));
    }

    private static float GetSpriteRadius(Sprite sprite)
    {
        return GetSpriteMaxDimension(sprite) * 0.5f;
    }

    private static float GetSpriteMaxDimension(Sprite sprite)
    {
        if (sprite == null)
            return 0f;

        Vector2 size = sprite.bounds.size;
        return Mathf.Max(size.x, size.y);
    }
}
