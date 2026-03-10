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

    public RectTransform Card => card;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        Initialize();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        Initialize();
    }

    private void Initialize()
    {
        if (cg == null)
            cg = GetComponent<CanvasGroup>();

        if (cg == null || !Application.isPlaying)
            return;

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        if (card != null)
            card.localScale = Vector3.one;
    }

    private void OnDisable()
    {
        tween?.Kill(false);
        tween = null;
    }

    private void OnDestroy()
    {
        tween?.Kill(false);
        tween = null;
    }

    public void Show()
    {
        if (!Application.isPlaying)
            return;

        Initialize();
        if (cg == null) return;

        tween?.Kill();
        gameObject.SetActive(true);

        cg.blocksRaycasts = true;
        cg.interactable = true;

        Sequence s = DOTween.Sequence();
        s.SetUpdate(true);
        s.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        cg.alpha = 0f;

        s.Join(cg.DOFade(1f, fadeIn));

        if (card != null)
        {
            card.localScale = Vector3.one * popFromScale;
            s.Join(card.DOScale(1f, popDuration).SetEase(Ease.OutBack));
        }

        tween = s;
    }

    public void Hide()
    {
        if (!Application.isPlaying)
            return;

        Initialize();
        if (cg == null) return;

        tween?.Kill();

        cg.interactable = false;
        cg.blocksRaycasts = false;

        Sequence s = DOTween.Sequence();
        s.SetUpdate(true);
        s.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        s.Join(cg.DOFade(0f, fadeOut));

        if (card != null)
            s.Join(card.DOScale(popFromScale, fadeOut).SetEase(Ease.InOutSine));

        s.OnComplete(() =>
        {
            if (this == null || gameObject == null) return;
            gameObject.SetActive(false);
        });

        tween = s;
    }
}
