using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PegVisualController : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private PegDefinition definition;
    private Sprite fallbackSprite;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        fallbackSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    public void SetDefinition(PegDefinition pegDefinition)
    {
        definition = pegDefinition;
    }

    public void PlayIdle()
    {
        if (spriteRenderer == null) return;

        ApplySprite(definition != null ? definition.idleSprite : null, fallbackSprite);
        spriteRenderer.color = definition != null ? definition.idleColor : Color.cyan;
    }

    public void PlayHit()
    {
        if (spriteRenderer == null) return;

        Sprite idleSprite = definition != null ? definition.idleSprite : null;
        Sprite hitSprite = definition != null ? definition.hitSprite : null;

        ApplySprite(hitSprite, idleSprite, fallbackSprite);
        spriteRenderer.color = definition != null ? definition.hitColor : Color.gray;
    }

    public void PlayConsume()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.enabled = false;
    }

    public void PlayRestore()
    {
        if (spriteRenderer == null) return;

        spriteRenderer.enabled = true;
        PlayIdle();
    }

    public void SetColor(Color color)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = color;
    }

    public void SetColorToIdle()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.color = definition != null ? definition.idleColor : Color.cyan;
    }

    private void ApplySprite(params Sprite[] candidates)
    {
        if (spriteRenderer == null || candidates == null) return;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == null) continue;
            spriteRenderer.sprite = candidates[i];
            return;
        }
    }
}