using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class OverlayAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform card;

    [Header("Timings")]
    [SerializeField] private float fadeIn = 0.18f;
    [SerializeField] private float fadeOut = 0.12f;
    [SerializeField] private float popDuration = 0.20f;

    [Header("Pop")]
    [SerializeField] private float popFromScale = 0.96f;

    private CanvasGroup cg;
    private Tween tween;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        if (card != null)
            card.localScale = Vector3.one;
    }

    public void Show()
    {
        tween?.Kill();
        gameObject.SetActive(true);

        cg.blocksRaycasts = true;
        cg.interactable = true;

        Sequence s = DOTween.Sequence();
        cg.alpha = 0f;

        s.Join(cg.DOFade(1f, fadeIn).SetUpdate(true));

        if (card != null)
        {
            card.localScale = Vector3.one * popFromScale;
            s.Join(card.DOScale(1f, popDuration).SetEase(Ease.OutBack).SetUpdate(true));
        }

        tween = s;
    }

    public void Hide()
    {
        tween?.Kill();

        cg.interactable = false;
        cg.blocksRaycasts = false;

        Sequence s = DOTween.Sequence();
        s.Join(cg.DOFade(0f, fadeOut).SetUpdate(true));

        if (card != null)
            s.Join(card.DOScale(popFromScale, fadeOut).SetEase(Ease.InOutSine).SetUpdate(true));

        s.OnComplete(() => gameObject.SetActive(false));

        tween = s;
    }
}
