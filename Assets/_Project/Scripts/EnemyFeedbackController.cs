using UnityEngine;
using System.Collections;

/// <summary>
/// Da feedback visual cuando el enemigo recibe daño: aclara temporalmente su color
/// actual (no lo blanquea del todo) y lo escala ligeramente. Al terminar, restaura
/// el color y la escala originales. Desacoplado de la lógica de daño.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyFeedbackController : MonoBehaviour
{
    [SerializeField, Tooltip("Duración del destello en segundos.")]
    private float flashDuration = 0.1f;

    [SerializeField, Tooltip("Multiplicador de escala durante el flash (1 = sin cambio).")]
    private float scaleMultiplier = 1.05f;

    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private Coroutine flashRoutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    /// <summary>
    /// Ejecuta el efecto de flash. Si hay uno en curso, lo reinicia.
    /// </summary>
    public void Flash()
    {
        if (!gameObject.activeInHierarchy) return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        flashRoutine = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        // Capturar el color actual (ya tintado por la vida) y la escala actual
        Color baseColor = spriteRenderer.color;
        Vector3 baseScale = originalScale;

        // Generar un color aclarado hacia blanco (50 % de intensidad)
        Color flashColor = Color.Lerp(baseColor, Color.white, 0.5f);

        // Aplicar flash: color aclarado y escala aumentada
        spriteRenderer.color = flashColor;
        transform.localScale = baseScale * scaleMultiplier;

        // Esperar la duración indicada (sin afectar por Time.timeScale)
        yield return new WaitForSecondsRealtime(flashDuration);

        // Restaurar color y escala originales
        spriteRenderer.color = baseColor;
        transform.localScale = baseScale;

        flashRoutine = null;
    }
}
