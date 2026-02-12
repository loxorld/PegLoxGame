using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Microinteracciones para el menú principal: hover/press por botón,
/// pulso opcional del CTA principal y secuencia de entrada escalonada.
/// </summary>
public class MenuMicroInteractions : MonoBehaviour
{
    [Serializable]
    public class MenuButtonRefs
    {
        [Tooltip("Raíz visual del botón a animar.")]
        public RectTransform rect;

        [Tooltip("Imagen opcional del botón (para fade/color si se necesita).")]
        public Image image;

        [Tooltip("Marcar true en el botón principal (Play/New Run) para pulso idle.")]
        public bool isPrimary;

        [NonSerialized] public Vector3 baseScale = Vector3.one;
        [NonSerialized] public Vector2 baseAnchoredPos;
        [NonSerialized] public CanvasGroup canvasGroup;
        [NonSerialized] public Tween idleTween;
    }

    [Header("References")]
    [SerializeField] private RectTransform titleOrLogo;
    [SerializeField] private Image titleOrLogoImage;
    [SerializeField] private List<MenuButtonRefs> buttons = new();

    [Header("Motion")]
    [SerializeField] private bool reducedMotion = false;

    [Header("Hover / Press")]
    [SerializeField] private float hoverScale = 1.04f;
    [SerializeField] private float hoverDuration = 0.18f;
    [SerializeField] private float pressDownScale = 0.98f;
    [SerializeField] private float pressBounceScale = 1.02f;
    [SerializeField] private float pressDownDuration = 0.07f;
    [SerializeField] private float pressBounceDuration = 0.10f;

    [Header("Idle Pulse (Primary)")]
    [SerializeField] private bool enableIdlePulse = true;
    [SerializeField] private float idlePulseScale = 1.03f;
    [SerializeField] private float idlePulseHalfCycle = 0.90f;

    [Header("Intro Sequence")]
    [SerializeField] private float introFade = 0.18f;
    [SerializeField] private float introMove = 0.20f;
    [SerializeField] private float introStagger = 0.06f;
    [SerializeField] private float introOffsetY = 20f;

    private readonly Dictionary<RectTransform, MenuButtonRefs> buttonByRect = new();

    private RectTransform CachedTitleRect => titleOrLogo;

    private void Awake()
    {
        CacheReferences();
        SetupEventForwarders();
    }

    private void OnEnable()
    {
        RefreshLayoutBeforeIntro();
        PlayIntroSequence();
        StartIdlePulseIfNeeded();
    }

    private void OnDisable()
    {
        KillAllTweens();
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    private void CacheReferences()
    {
        buttonByRect.Clear();

        for (int i = 0; i < buttons.Count; i++)
        {
            MenuButtonRefs entry = buttons[i];
            if (entry == null || entry.rect == null)
            {
                continue;
            }

            entry.baseScale = entry.rect.localScale;
            entry.baseAnchoredPos = entry.rect.anchoredPosition;
            entry.canvasGroup = entry.rect.GetComponent<CanvasGroup>();
            if (entry.canvasGroup == null)
            {
                entry.canvasGroup = entry.rect.gameObject.AddComponent<CanvasGroup>();
            }

            buttonByRect[entry.rect] = entry;
        }
    }

    private void SetupEventForwarders()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            MenuButtonRefs entry = buttons[i];
            if (entry == null || entry.rect == null)
            {
                continue;
            }

            ButtonEventForwarder forwarder = entry.rect.GetComponent<ButtonEventForwarder>();
            if (forwarder == null)
            {
                forwarder = entry.rect.gameObject.AddComponent<ButtonEventForwarder>();
            }

            forwarder.Initialize(this, entry.rect);
        }
    }

    public void NotifyHoverEnter(RectTransform target)
    {
        if (target == null || !buttonByRect.TryGetValue(target, out MenuButtonRefs entry))
        {
            return;
        }

        KillElementTweens(entry);

        target.DOScale(entry.baseScale * hoverScale, hoverDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
    }

    public void NotifyHoverExit(RectTransform target)
    {
        if (target == null || !buttonByRect.TryGetValue(target, out MenuButtonRefs entry))
        {
            return;
        }

        KillElementTweens(entry);

        target.DOScale(entry.baseScale, hoverDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);

        StartIdlePulseFor(entry);
    }

    public void NotifyPress(RectTransform target)
    {
        if (target == null || !buttonByRect.TryGetValue(target, out MenuButtonRefs entry))
        {
            return;
        }

        KillElementTweens(entry);

        if (reducedMotion)
        {
            target.DOScale(entry.baseScale, 0.10f)
                .SetEase(Ease.OutQuad)
                .SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
            return;
        }

        Sequence press = DOTween.Sequence();
        press.SetLink(target.gameObject, LinkBehaviour.KillOnDestroy);
        press.Append(target.DOScale(entry.baseScale * pressDownScale, pressDownDuration).SetEase(Ease.OutQuad));
        press.Append(target.DOScale(entry.baseScale * pressBounceScale, pressBounceDuration).SetEase(Ease.OutQuad));
        press.Append(target.DOScale(entry.baseScale * hoverScale, pressBounceDuration).SetEase(Ease.OutQuad));
    }

    private void PlayIntroSequence()
    {
        Sequence intro = DOTween.Sequence();
        intro.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        intro.SetUpdate(true);

        float cumulativeDelay = 0f;

        if (CachedTitleRect != null)
        {
            CanvasGroup titleCg = CachedTitleRect.GetComponent<CanvasGroup>();
            if (titleCg == null)
            {
                titleCg = CachedTitleRect.gameObject.AddComponent<CanvasGroup>();
            }

            CachedTitleRect.DOKill(false);
            titleCg.DOKill(false);

            Vector2 basePos = CachedTitleRect.anchoredPosition;
            bool animateTitlePosition = !IsLayoutDriven(CachedTitleRect);
            titleCg.alpha = 0f;

            if (animateTitlePosition)
            {
                CachedTitleRect.anchoredPosition = basePos + (Vector2.up * introOffsetY);
            }

            intro.Insert(cumulativeDelay, titleCg.DOFade(1f, introFade).SetEase(Ease.OutQuad));
            if (animateTitlePosition)
            {
                intro.Insert(cumulativeDelay, CachedTitleRect.DOAnchorPos(basePos, introMove).SetEase(Ease.OutQuad));
            }

            cumulativeDelay += introStagger;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            MenuButtonRefs entry = buttons[i];
            if (entry == null || entry.rect == null)
            {
                continue;
            }

            KillElementTweens(entry);

            entry.baseScale = entry.rect.localScale;
            entry.baseAnchoredPos = entry.rect.anchoredPosition;
            bool animateButtonPosition = !IsLayoutDriven(entry.rect);

            entry.canvasGroup.alpha = 0f;

            if (animateButtonPosition)
            {
                entry.rect.anchoredPosition = entry.baseAnchoredPos + (Vector2.up * introOffsetY);
            }

            intro.Insert(cumulativeDelay, entry.canvasGroup.DOFade(1f, introFade).SetEase(Ease.OutQuad));
            if (animateButtonPosition)
            {
                intro.Insert(cumulativeDelay, entry.rect.DOAnchorPos(entry.baseAnchoredPos, introMove).SetEase(Ease.OutQuad));
            }

            cumulativeDelay += introStagger;
        }

        intro.Play();
    }

    private void RefreshLayoutBeforeIntro()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }
    }

    private static bool IsLayoutDriven(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        LayoutElement layoutElement = rect.GetComponent<LayoutElement>();
        if (layoutElement != null && layoutElement.ignoreLayout)
        {
            return false;
        }

        return rect.GetComponentInParent<LayoutGroup>() != null;
    }

    private void StartIdlePulseIfNeeded()
    {
        if (reducedMotion || !enableIdlePulse)
        {
            return;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            StartIdlePulseFor(buttons[i]);
        }
    }

    private void StartIdlePulseFor(MenuButtonRefs entry)
    {
        if (entry == null || entry.rect == null || !entry.isPrimary)
        {
            return;
        }

        if (reducedMotion || !enableIdlePulse)
        {
            return;
        }


        entry.idleTween?.Kill(false);
        entry.idleTween = entry.rect.DOScale(entry.baseScale * idlePulseScale, idlePulseHalfCycle)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(entry.rect.gameObject, LinkBehaviour.KillOnDestroy);
    }

    private void KillElementTweens(MenuButtonRefs entry)
    {
        if (entry == null)
        {
            return;
        }

        entry.idleTween?.Kill(false);
        entry.idleTween = null;

        if (entry.rect != null)
        {
            entry.rect.DOKill(false);
        }

        if (entry.image != null)
        {
            entry.image.DOKill(false);
        }

        if (entry.canvasGroup != null)
        {
            entry.canvasGroup.DOKill(false);
        }
    }

    private void KillAllTweens()
    {
        if (CachedTitleRect != null)
        {
            CachedTitleRect.DOKill(false);
            CanvasGroup titleCg = CachedTitleRect.GetComponent<CanvasGroup>();
            titleCg?.DOKill(false);
        }

        if (titleOrLogoImage != null)
        {
            titleOrLogoImage.DOKill(false);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            KillElementTweens(buttons[i]);
        }
    }

    private class ButtonEventForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private MenuMicroInteractions owner;
        private RectTransform target;

        public void Initialize(MenuMicroInteractions ownerComponent, RectTransform rect)
        {
            owner = ownerComponent;
            target = rect;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.NotifyHoverEnter(target);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.NotifyHoverExit(target);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            owner?.NotifyPress(target);
        }
    }
}