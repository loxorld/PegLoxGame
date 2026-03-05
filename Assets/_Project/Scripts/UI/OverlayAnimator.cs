using System.Collections;
using UnityEngine;

public class OverlayAnimator : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Min(0f)] private float fadeDuration = 0.15f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Show()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StartFade(targetAlpha: 1f, disableOnComplete: false);
    }

    public void Hide()
    {
        StartFade(targetAlpha: 0f, disableOnComplete: true);
    }

    private void StartFade(float targetAlpha, bool disableOnComplete)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (canvasGroup == null)
        {
            if (disableOnComplete)
                gameObject.SetActive(false);
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, disableOnComplete));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool disableOnComplete)
    {
        float startAlpha = canvasGroup.alpha;

        if (fadeDuration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        if (disableOnComplete && Mathf.Approximately(targetAlpha, 0f))
            gameObject.SetActive(false);

        fadeRoutine = null;
    }
}