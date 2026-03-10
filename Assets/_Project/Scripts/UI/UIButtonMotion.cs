using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UIButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform target;
    [SerializeField, Range(1f, 1.15f)] private float hoverScale = 1.035f;
    [SerializeField, Range(0.85f, 1f)] private float pressScale = 0.965f;
    [SerializeField] private float tweenDuration = 0.12f;
    [SerializeField] private bool useUnscaledTime = true;

    private Vector3 baseScale = Vector3.one;

    public static UIButtonMotion Attach(RectTransform targetRect, float hover = 1.035f, float press = 0.965f, float duration = 0.12f)
    {
        if (targetRect == null)
            return null;

        UIButtonMotion motion = targetRect.GetComponent<UIButtonMotion>();
        if (motion == null && !Application.isPlaying)
            return null;

        if (motion == null)
            motion = targetRect.gameObject.AddComponent<UIButtonMotion>();

        motion.target = targetRect;
        motion.hoverScale = hover;
        motion.pressScale = press;
        motion.tweenDuration = duration;
        motion.CacheBaseScale();
        return motion;
    }

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        if (target == null)
            target = transform as RectTransform;

        CacheBaseScale();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        CacheBaseScale();
    }

    private void OnDisable()
    {
        if (target == null)
            return;

        target.DOKill(false);
        target.localScale = baseScale;
    }

    private void OnDestroy()
    {
        if (target != null)
            target.DOKill(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanAnimate())
            return;

        AnimateTo(baseScale * hoverScale, Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (target == null)
            return;

        AnimateTo(baseScale, Ease.OutQuad);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAnimate())
            return;

        AnimateTo(baseScale * pressScale, Ease.OutQuad);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!CanAnimate())
            return;

        AnimateTo(baseScale * hoverScale, Ease.OutQuad);
    }

    private bool CanAnimate()
    {
        if (target == null)
            return false;

        Selectable selectable = GetComponent<Selectable>();
        return selectable == null || selectable.IsInteractable();
    }

    private void CacheBaseScale()
    {
        if (target != null)
            baseScale = target.localScale;
    }

    private void AnimateTo(Vector3 scale, Ease ease)
    {
        if (target == null)
            return;

        target.DOKill(false);
        target.DOScale(scale, tweenDuration)
            .SetEase(ease)
            .SetUpdate(useUnscaledTime)
            .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
    }
}
