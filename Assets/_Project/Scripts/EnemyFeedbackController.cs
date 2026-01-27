using UnityEngine;
using System.Collections;

/// <summary>
/// Proporciona feedback visual simple (flash y escala) cuando el enemigo recibe daño.
/// Adjunta este script a cualquier GameObject del enemigo que tenga un SpriteRenderer.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyFeedbackController : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private float scaleMultiplier = 1.1f;

    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private Coroutine currentRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    /// <summary>
    /// Dispara el efecto visual. Llama a este método desde tu código de daño.
    /// </summary>
    public void Flash()
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        Color originalColor = spriteRenderer.color;
        // Aplica flash y escala
        spriteRenderer.color = Color.white;
        transform.localScale = originalScale * scaleMultiplier;

        yield return new WaitForSeconds(flashDuration);

        // Vuelve a la normalidad
        spriteRenderer.color = originalColor;
        transform.localScale = originalScale;
    }
}
