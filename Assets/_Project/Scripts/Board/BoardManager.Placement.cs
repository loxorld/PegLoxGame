using UnityEngine;

public partial class BoardManager
{
    private bool TrySpawnPegInCell(Vector2 basePos, PegDefinition candidateDefinition, System.Random rng, float spacingX, float spacingY, out GameObject spawnedPeg)
    {
        spawnedPeg = null;
        float candidateRadius = GetEffectivePegRadius(candidateDefinition);

        for (int attempt = 0; attempt < maxTriesPerCell; attempt++)
        {
            Vector2 pos = ApplyJitter(basePos, rng, spacingX, spacingY);

            if (OverlapsPlacedPeg(pos, candidateRadius))
                continue;

            if (Physics2D.OverlapCircle(pos, candidateRadius, pegOverlapMask) != null)
                continue;

            spawnedPeg = Instantiate(pegPrefab, pos, Quaternion.identity, boardRoot);
            placedPegs.Add(new PlacedPegData { position = pos, radius = candidateRadius });
            return true;
        }

        return false;
    }

    private Vector2 ApplyJitter(Vector2 basePos, System.Random rng, float spacingX, float spacingY)
    {
        float jx = (float)(rng.NextDouble() * 2.0 - 1.0) * runtimeJitter * spacingX;
        float jy = (float)(rng.NextDouble() * 2.0 - 1.0) * runtimeJitter * spacingY;
        return new Vector2(basePos.x + jx, basePos.y + jy);
    }

    private bool OverlapsPlacedPeg(Vector2 candidateCenter, float candidateRadius)
    {
        for (int i = 0; i < placedPegs.Count; i++)
        {
            PlacedPegData placed = placedPegs[i];
            if (PegSizingUtility.CirclesOverlap(candidateCenter, candidateRadius, placed.position, placed.radius))
                return true;
        }

        return false;
    }

    private float GetEffectivePegRadius(PegDefinition definition)
    {
        if (effectiveRadiusByDefinition.TryGetValue(definition, out float cachedRadius))
            return cachedRadius;

        SpriteRenderer spriteRenderer = pegPrefab.GetComponent<SpriteRenderer>();
        CircleCollider2D circleCollider = pegPrefab.GetComponent<CircleCollider2D>();

        Sprite fallbackSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        float baseScale = Mathf.Max(Mathf.Abs(pegPrefab.transform.localScale.x), Mathf.Abs(pegPrefab.transform.localScale.y));
        float colliderRadius = circleCollider != null ? circleCollider.radius : 0f;

        float calculatedRadius = PegSizingUtility.CalculateEffectiveWorldRadius(definition, fallbackSprite, baseScale, colliderRadius, extraSeparation);
        effectiveRadiusByDefinition[definition] = calculatedRadius;
        return calculatedRadius;
    }

    private Rect CalculatePlayableBounds()
    {
        Rect visibleWorld = CameraWorldRect.GetVisibleWorldRect(cam);

        float minWorldX = Mathf.Lerp(visibleWorld.xMin, visibleWorld.xMax, config.viewportMinX);
        float maxWorldX = Mathf.Lerp(visibleWorld.xMin, visibleWorld.xMax, config.viewportMaxX);
        float minWorldY = Mathf.Lerp(visibleWorld.yMin, visibleWorld.yMax, config.viewportMinY);
        float maxWorldY = Mathf.Lerp(visibleWorld.yMin, visibleWorld.yMax, config.viewportMaxY);

        Vector2 minW = new Vector2(minWorldX, minWorldY);
        Vector2 maxW = new Vector2(maxWorldX, maxWorldY);

        Vector2 worldMin = new Vector2(Mathf.Min(minW.x, maxW.x), Mathf.Min(minW.y, maxW.y));
        Vector2 worldMax = new Vector2(Mathf.Max(minW.x, maxW.x), Mathf.Max(minW.y, maxW.y));

        worldMin += Vector2.one * config.marginWorld;
        worldMax -= Vector2.one * config.marginWorld;

        Rect bounds = Rect.MinMaxRect(worldMin.x, worldMin.y, worldMax.x, worldMax.y);
        playableBounds = bounds;
        hasPlayableBounds = true;

        if (boundsProvider != null)
            boundsProvider.SetBounds(bounds);

        return bounds;
    }
}
