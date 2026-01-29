using UnityEngine;
using System.Collections;

/// <summary>
/// Controla un fundido a negro breve al cambiar de estado.
/// Requiere un CanvasGroup sobre un panel negro de pantalla completa.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameFlowManager flow;

    [Header("Settings")]
    [Tooltip("Duración del fade in y fade out (segundos)")]
    [SerializeField] private float fadeDuration = 0.35f;
    [Tooltip("Tiempo que se mantiene la pantalla negra (segundos)")]
    [SerializeField] private float holdDuration = 0.10f;

    private void Awake()
    {
        if (flow == null) flow = GameFlowManager.Instance;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        if (flow != null) flow.OnStateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        if (flow != null) flow.OnStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.RewardChoice) return;
        if (state == GameState.Paused) return;

        StartCoroutine(FadeRoutine());
    }



    private IEnumerator FadeRoutine()
    {
        if (canvasGroup == null) yield break;

        // Fade in (negro)
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Pequeña pausa
        yield return new WaitForSecondsRealtime(holdDuration);

        // Fade out (transparente)
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }
}
