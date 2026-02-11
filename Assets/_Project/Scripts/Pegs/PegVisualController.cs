using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PegVisualController : MonoBehaviour
{
    private static readonly int HitTrigger = Animator.StringToHash("Hit");
    private static readonly int ConsumeTrigger = Animator.StringToHash("Consume");
    private static readonly int RestoreTrigger = Animator.StringToHash("Restore");

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private PegDefinition definition;
    private Sprite fallbackSprite;
    private Coroutine hideRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        fallbackSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    public void SetDefinition(PegDefinition pegDefinition)
    {
        definition = pegDefinition;

        if (animator != null)
        {
            animator.runtimeAnimatorController = definition != null ? definition.animatorController : null;
        }
    }

    public void PlayIdle()
    {
        StopHideRoutine();

        if (spriteRenderer == null) return;

        spriteRenderer.enabled = true;
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

        TriggerAnimator(HitTrigger);
        SpawnVfx(definition != null ? definition.hitVfxPrefab : null);
    }

    public void PlayConsume()
    {
        if (spriteRenderer == null) return;

        SpawnVfx(definition != null ? definition.consumeVfxPrefab : null);
        TriggerAnimator(ConsumeTrigger);

        if (ShouldUseAnimatorConsume())
        {
            StopHideRoutine();
            hideRoutine = StartCoroutine(HideAfterDelay(definition.consumeHideDelay));
            return;
        }

        spriteRenderer.enabled = false;
    }

    public void PlayRestore(bool withFeedback)
    {
        StopHideRoutine();

        if (spriteRenderer == null) return;

        spriteRenderer.enabled = true;
        PlayIdle();

        if (!withFeedback) return;

        TriggerAnimator(RestoreTrigger);
        SpawnVfx(definition != null ? definition.restoreVfxPrefab : null);
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

    private IEnumerator HideAfterDelay(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        hideRoutine = null;
    }

    private bool ShouldUseAnimatorConsume()
    {
        return animator != null
            && animator.runtimeAnimatorController != null
            && definition != null
            && definition.consumeHideDelay > 0f;
    }

    private void TriggerAnimator(int triggerHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetTrigger(triggerHash);
    }

    private void SpawnVfx(GameObject prefab)
    {
        if (prefab == null) return;
        Instantiate(prefab, transform.position, Quaternion.identity);
    }

    private void StopHideRoutine()
    {
        if (hideRoutine == null) return;

        StopCoroutine(hideRoutine);
        hideRoutine = null;
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