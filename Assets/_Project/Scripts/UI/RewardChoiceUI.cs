using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardChoiceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RewardManager rewards;
    [SerializeField] private GameFlowManager flow;

    [Header("Overlay Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private OverlayAnimator overlayAnim;

    [Header("Overlay Summary")]
    [SerializeField] private TMP_Text overlayTitleText;
    [SerializeField] private TMP_Text overlaySubtitleText;
    [SerializeField] private TMP_Text overlayBreakdownText;

    [Header("Choice 1")]
    [SerializeField] private Button choice1Button;
    [SerializeField] private Image choice1Icon;
    [SerializeField] private TMP_Text choice1Title;
    [SerializeField] private TMP_Text choice1Desc;

    [Header("Choice 2")]
    [SerializeField] private Button choice2Button;
    [SerializeField] private Image choice2Icon;
    [SerializeField] private TMP_Text choice2Title;
    [SerializeField] private TMP_Text choice2Desc;

    [Header("Choice 3")]
    [SerializeField] private Button choice3Button;
    [SerializeField] private Image choice3Icon;
    [SerializeField] private TMP_Text choice3Title;
    [SerializeField] private TMP_Text choice3Desc;

    private bool flowSubscribed;
    private EncounterRewardPreview currentPreview;

    private struct RewardChoicePalette
    {
        public Color CardColor;
        public Color HighlightColor;
        public Color PressedColor;
        public Color TextColor;
        public Color AccentColor;
        public Color IconTint;
    }

    private void ResolveReferences(bool suppressFallbackLogging = false)
    {
        if (rewards == null)
            rewards = suppressFallbackLogging
                ? ResolveReferenceWithoutLogging<RewardManager>(includeInactive: true)
                : ServiceRegistry.ResolveWithFallback(nameof(RewardChoiceUI), nameof(rewards), () => ServiceRegistry.LegacyFind<RewardManager>(true));

        if (flow == null)
            flow = suppressFallbackLogging
                ? ResolveReferenceWithoutLogging(() => GameFlowManager.Instance, includeInactive: true)
                : ServiceRegistry.ResolveWithFallback(nameof(RewardChoiceUI), nameof(flow), () => GameFlowManager.Instance ?? ServiceRegistry.LegacyFind<GameFlowManager>(true));

        if (root == null && overlayAnim != null)
            root = overlayAnim.gameObject;
    }

    private void Awake()
    {
        ResolveReferences(suppressFallbackLogging: true);
        EnsureOverlayPresentation();

        if (root != null)
            root.SetActive(false);

        if (choice1Button != null) choice1Button.onClick.AddListener(() => OnChoose(1));
        if (choice2Button != null) choice2Button.onClick.AddListener(() => OnChoose(2));
        if (choice3Button != null) choice3Button.onClick.AddListener(() => OnChoose(3));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences(suppressFallbackLogging: true);
        EnsureOverlayPresentation();
    }
#endif

    private void Start()
    {
        ResolveReferences();
        EnsureOverlayPresentation();
        TrySubscribeFlow();
        SyncStateAndChoices();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureOverlayPresentation();

        if (rewards != null)
        {
            rewards.RewardChoicesDetailedPresented += OnRewardChoicesDetailedPresented;
            rewards.RewardResolved += OnRewardResolved;
        }

        TrySubscribeFlow();

        SyncStateAndChoices();
    }

    private void Update()
    {
        if (flow == null || rewards == null)
        {
            ResolveReferences();
            TrySubscribeFlow();
        }
    }

    private void OnDisable()
    {
        if (rewards != null)
        {
            rewards.RewardChoicesDetailedPresented -= OnRewardChoicesDetailedPresented;
            rewards.RewardResolved -= OnRewardResolved;
        }

        if (flow != null && flowSubscribed)
        {
            flow.OnStateChanged -= OnStateChanged;
            flowSubscribed = false;
        }
    }

    private void SyncStateAndChoices()
    {
        ResolveReferences();
        bool shouldBeVisible = flow != null && flow.State == GameState.RewardChoice;

        if (shouldBeVisible)
            ShowOverlay();
        else
            HideOverlayImmediate();

        if (rewards != null && rewards.IsAwaitingChoice)
        {
            IReadOnlyList<RewardOption> choices = rewards.CurrentChoices;
            if (choices != null && choices.Count > 0)
            {
                var copiedChoices = new RewardOption[choices.Count];
                for (int i = 0; i < choices.Count; i++)
                    copiedChoices[i] = choices[i];

                OnRewardChoicesDetailedPresented(rewards.LastRewardPreview, copiedChoices);
            }
        }
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.RewardChoice)
            ShowOverlay();
        else
            HideOverlayImmediate();
    }

    private void OnRewardChoicesDetailedPresented(EncounterRewardPreview preview, RewardOption[] choices)
    {
        currentPreview = preview;
        EnsureOverlayPresentation();
        ApplyRewardSummary(preview);

        SetChoice(choice1Button, choice1Icon, choice1Title, choice1Desc, choices, 0);
        SetChoice(choice2Button, choice2Icon, choice2Title, choice2Desc, choices, 1);
        SetChoice(choice3Button, choice3Icon, choice3Title, choice3Desc, choices, 2);
        ShowOverlay();
    }

    private void SetChoice(Button button, Image icon, TMP_Text title, TMP_Text desc, RewardOption[] choices, int index)
    {
        bool valid = choices != null && index >= 0 && index < choices.Length && choices[index].IsValid;

        if (button != null)
            button.interactable = valid;

        if (!valid)
        {
            ApplyInvalidChoiceVisuals(button, icon, title, desc);
            return;
        }

        RewardOption option = choices[index];
        RewardChoicePalette palette = ResolvePalette(option);
        ApplyChoiceVisuals(button, icon, title, desc, option, palette, index == 0);
    }

    private void ApplyInvalidChoiceVisuals(Button button, Image icon, TMP_Text title, TMP_Text desc)
    {
        if (title != null)
            title.text = "VACIO";

        if (desc != null)
            desc.text = string.Empty;

        if (icon != null)
        {
            icon.sprite = null;
            icon.enabled = false;
        }

        if (button == null)
            return;

        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image != null)
            ForceImageStyle(image, new Color(0.14f, 0.15f, 0.15f, 0.9f), true);
    }

    private void ApplyChoiceVisuals(Button button, Image icon, TMP_Text title, TMP_Text desc, RewardOption option, RewardChoicePalette palette, bool emphasize)
    {
        if (button != null)
        {
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                LayoutElement layout = EnsureComponent<LayoutElement>(button.gameObject);
                layout.minHeight = emphasize ? 122f : 112f;
                layout.preferredHeight = emphasize ? 128f : 116f;
            }

            ConfigureChoiceButtonLayout(button, icon, title, desc, emphasize);

            Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image != null)
            {
                ForceImageStyle(image, palette.CardColor, true);

                if (UIArtUtility.AllowsGeneratedDecor(button))
                {
                    Shadow shadow = EnsureComponent<Shadow>(button.gameObject);
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
                    shadow.effectDistance = new Vector2(0f, -6f);
                    shadow.useGraphicAlpha = true;

                    Outline outline = EnsureComponent<Outline>(button.gameObject);
                    outline.effectColor = palette.AccentColor;
                    outline.effectDistance = new Vector2(1f, -1f);
                    outline.useGraphicAlpha = true;
                }
            }

            if (!UIArtUtility.ShouldPreserveButtonTransitions(button))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = palette.CardColor;
                colors.highlightedColor = palette.HighlightColor;
                colors.pressedColor = palette.PressedColor;
                colors.selectedColor = palette.HighlightColor;
                colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 0.45f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }

            UIButtonMotion.Attach(rect, emphasize ? 1.045f : 1.03f, emphasize ? 0.955f : 0.965f, 0.12f);
        }

        if (icon != null)
        {
            icon.sprite = option.DisplayIcon;
            icon.enabled = option.DisplayIcon != null;
            icon.preserveAspect = true;
            if (!UIArtUtility.ShouldPreserveColor(icon))
                icon.color = palette.IconTint;
        }

        if (title != null)
        {
            string plainTitle = $"{option.KindLabel}: {option.DisplayName}";
            string richTitle = $"<size=62%><color=#{ColorUtility.ToHtmlStringRGB(palette.AccentColor)}>{option.SourceTag}</color></size>\n<b>{option.DisplayName}</b>";
            title.text = UIArtUtility.ResolveDynamicText(title, plainTitle, richTitle);

            if (!UIArtUtility.ShouldPreserveTextStyling(title))
            {
                title.alignment = TextAlignmentOptions.Left;
                title.enableAutoSizing = true;
                title.fontSizeMin = 17f;
                title.fontSizeMax = 26f;
                title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                title.color = palette.TextColor;
                title.lineSpacing = 1f;
                title.margin = Vector4.zero;
                title.outlineWidth = 0.12f;
                title.outlineColor = new Color(0.05f, 0.05f, 0.05f, 0.75f);
                title.textWrappingMode = TextWrappingModes.Normal;
                title.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        if (desc != null)
        {
            string compactDescription = BuildCompactRewardDescription(option);
            string body = compactDescription;
            string richBody = $"<size=84%><color=#F0EEE7>{compactDescription}</color></size>";

            desc.text = UIArtUtility.ResolveDynamicText(desc, body, richBody);

            if (!UIArtUtility.ShouldPreserveTextStyling(desc))
            {
                desc.alignment = TextAlignmentOptions.Left;
                desc.enableAutoSizing = true;
                desc.fontSizeMin = 11f;
                desc.fontSizeMax = 16f;
                desc.fontStyle = FontStyles.Bold;
                desc.color = new Color(0.92f, 0.92f, 0.9f, 1f);
                desc.lineSpacing = 2f;
                desc.margin = Vector4.zero;
                desc.outlineWidth = 0.08f;
                desc.outlineColor = new Color(0.03f, 0.03f, 0.03f, 0.68f);
                desc.textWrappingMode = TextWrappingModes.Normal;
                desc.overflowMode = TextOverflowModes.Ellipsis;
            }
        }
    }

    private void ApplyRewardSummary(EncounterRewardPreview preview)
    {
        if (overlayTitleText != null)
            overlayTitleText.text = RewardManager.BuildRewardOverlayTitle(preview);

        if (overlaySubtitleText != null)
            overlaySubtitleText.text = RewardManager.BuildRewardOverlaySubtitle(preview);

        if (overlayBreakdownText != null)
        {
            string plain = RewardManager.BuildRewardPreviewText(preview);
            string rich = $"<b><color=#F8E7B0>{plain}</color></b>";
            overlayBreakdownText.text = UIArtUtility.ResolveDynamicText(overlayBreakdownText, plain, rich);
        }
    }

    private void OnChoose(int index)
    {
        if (rewards == null)
            return;

        rewards.Choose(index);
    }

    private void OnRewardResolved()
    {
        if (overlayAnim != null)
            overlayAnim.Hide();
        else if (root != null)
            root.SetActive(false);
    }

    private void ShowOverlay()
    {
        EnsureOverlayPresentation();

        if (overlayAnim != null)
        {
            overlayAnim.Show();
            return;
        }

        if (root != null && !root.activeSelf)
            root.SetActive(true);
    }

    private void HideOverlayImmediate()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void TrySubscribeFlow()
    {
        if (flow != null && !flowSubscribed)
        {
            flow.OnStateChanged += OnStateChanged;
            flowSubscribed = true;
        }
    }

    private void EnsureOverlayPresentation()
    {
        RectTransform card = ResolveCardRoot();
        if (root == null || card == null)
            return;

        OverlayVisualStyler.StyleRewardOverlay(root, card, choice1Button, choice2Button, choice3Button);
        EnsureSummaryTexts(card);

        if (currentPreview.ChoiceCount > 0)
            ApplyRewardSummary(currentPreview);
    }

    private void EnsureSummaryTexts(RectTransform card)
    {
        if (card == null)
            return;

        if (overlayTitleText == null)
            overlayTitleText = FindDescendant(card, "OverlayTitleText")?.GetComponent<TMP_Text>();

        if (overlaySubtitleText == null)
            overlaySubtitleText = FindDescendant(card, "OverlaySubtitleText")?.GetComponent<TMP_Text>();

        if (overlayBreakdownText == null)
            overlayBreakdownText = FindDescendant(card, "RewardBreakdownText")?.GetComponent<TMP_Text>();

        if (overlayBreakdownText == null)
        {
            GameObject breakdownObject = new GameObject("RewardBreakdownText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform breakdownRect = breakdownObject.GetComponent<RectTransform>();
            breakdownRect.SetParent(card, false);
            overlayBreakdownText = breakdownObject.GetComponent<TextMeshProUGUI>();
        }

        overlayBreakdownText.transform.SetSiblingIndex(Mathf.Max(0, Mathf.Min(3, card.childCount - 1)));

        if (!UIArtUtility.ShouldPreserveTextStyling(overlayBreakdownText))
        {
            overlayBreakdownText.alignment = TextAlignmentOptions.Center;
            overlayBreakdownText.enableAutoSizing = true;
            overlayBreakdownText.fontSizeMin = 14f;
            overlayBreakdownText.fontSizeMax = 20f;
            overlayBreakdownText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            overlayBreakdownText.characterSpacing = 1.5f;
            overlayBreakdownText.color = new Color(0.97f, 0.93f, 0.8f, 1f);
            overlayBreakdownText.outlineWidth = 0.1f;
            overlayBreakdownText.outlineColor = new Color(0.05f, 0.04f, 0.03f, 0.72f);
            overlayBreakdownText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (UIArtUtility.AllowsProceduralLayout(overlayBreakdownText))
        {
            LayoutElement layout = EnsureComponent<LayoutElement>(overlayBreakdownText.gameObject);
            layout.preferredHeight = 48f;
            layout.minHeight = 42f;
        }
    }

    private static void ConfigureChoiceButtonLayout(Button button, Image icon, TMP_Text title, TMP_Text desc, bool emphasize)
    {
        if (button == null)
            return;

        EnsureComponent<RectMask2D>(button.gameObject);

        HorizontalLayoutGroup horizontal = EnsureComponent<HorizontalLayoutGroup>(button.gameObject);
        horizontal.padding = new RectOffset(18, 18, emphasize ? 14 : 12, emphasize ? 14 : 12);
        horizontal.spacing = 14f;
        horizontal.childAlignment = TextAnchor.UpperLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;
        horizontal.childScaleWidth = false;
        horizontal.childScaleHeight = false;

        if (icon != null)
        {
            LayoutElement iconLayout = EnsureComponent<LayoutElement>(icon.gameObject);
            float iconSize = emphasize ? 74f : 66f;
            iconLayout.minWidth = iconSize;
            iconLayout.minHeight = iconSize;
            iconLayout.preferredWidth = iconSize;
            iconLayout.preferredHeight = iconSize;
            iconLayout.flexibleWidth = 0f;
            iconLayout.flexibleHeight = 0f;
        }

        RectTransform textRoot = title != null ? title.transform.parent as RectTransform : null;
        if (textRoot == null && desc != null)
            textRoot = desc.transform.parent as RectTransform;

        if (textRoot == null)
            return;

        LayoutElement textRootLayout = EnsureComponent<LayoutElement>(textRoot.gameObject);
        textRootLayout.flexibleWidth = 1f;
        textRootLayout.minWidth = 0f;
        textRootLayout.preferredWidth = -1f;
        textRootLayout.flexibleHeight = 0f;

        VerticalLayoutGroup vertical = EnsureComponent<VerticalLayoutGroup>(textRoot.gameObject);
        vertical.padding = new RectOffset(0, 0, 0, 0);
        vertical.spacing = 5f;
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = false;
        vertical.childForceExpandHeight = false;
        vertical.childScaleWidth = false;
        vertical.childScaleHeight = false;

        if (title != null)
        {
            LayoutElement titleLayout = EnsureComponent<LayoutElement>(title.gameObject);
            titleLayout.minHeight = emphasize ? 42f : 38f;
            titleLayout.preferredHeight = emphasize ? 46f : 40f;
            titleLayout.flexibleWidth = 1f;
            titleLayout.flexibleHeight = 0f;
            title.raycastTarget = false;
        }

        if (desc != null)
        {
            LayoutElement descLayout = EnsureComponent<LayoutElement>(desc.gameObject);
            descLayout.minHeight = emphasize ? 32f : 28f;
            descLayout.preferredHeight = emphasize ? 40f : 34f;
            descLayout.flexibleWidth = 1f;
            descLayout.flexibleHeight = 0f;
            desc.raycastTarget = false;
        }
    }

    private static void ForceImageStyle(Image image, Color color, bool raycastTarget)
    {
        if (image == null)
            return;

        image.sprite = UIArtUtility.BuiltinPanelSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = raycastTarget;
        image.preserveAspect = false;
    }

    private static string BuildCompactRewardDescription(RewardOption option)
    {
        if (!string.IsNullOrWhiteSpace(option.DisplayDescription))
            return option.DisplayDescription;

        return option.kind switch
        {
            RewardKind.Orb => "Agrega este orbe a tu rotacion.",
            RewardKind.OrbUpgrade => "Mejora uno de tus orbes en la run.",
            RewardKind.Heal => "Recupera vida antes del siguiente nodo.",
            _ => "Activa una reliquia pasiva para el resto de la run."
        };
    }

    private RectTransform ResolveCardRoot()
    {
        if (overlayAnim != null && overlayAnim.Card != null)
            return overlayAnim.Card;

        if (choice1Button != null)
        {
            RectTransform candidate = choice1Button.transform.parent as RectTransform;
            if (candidate != null)
                return candidate;
        }

        if (root == null)
            return null;

        RectTransform rootRect = root.transform as RectTransform;
        if (rootRect == null)
            return null;

        for (int i = 0; i < rootRect.childCount; i++)
        {
            if (rootRect.GetChild(i) is RectTransform rect)
                return rect;
        }

        return rootRect;
    }

    private static RewardChoicePalette ResolvePalette(RewardOption option)
    {
        RewardChoicePalette palette = option.kind switch
        {
            RewardKind.Orb => new RewardChoicePalette
            {
                CardColor = new Color(0.12f, 0.26f, 0.22f, 0.96f),
                HighlightColor = new Color(0.18f, 0.38f, 0.31f, 1f),
                PressedColor = new Color(0.09f, 0.19f, 0.16f, 1f),
                TextColor = new Color(0.93f, 0.98f, 0.94f, 1f),
                AccentColor = new Color(0.46f, 0.86f, 0.68f, 0.92f),
                IconTint = Color.white
            },
            RewardKind.OrbUpgrade => new RewardChoicePalette
            {
                CardColor = new Color(0.1f, 0.2f, 0.31f, 0.96f),
                HighlightColor = new Color(0.15f, 0.3f, 0.46f, 1f),
                PressedColor = new Color(0.08f, 0.14f, 0.23f, 1f),
                TextColor = new Color(0.92f, 0.97f, 1f, 1f),
                AccentColor = new Color(0.46f, 0.74f, 0.96f, 0.94f),
                IconTint = Color.white
            },
            RewardKind.Heal => new RewardChoicePalette
            {
                CardColor = new Color(0.16f, 0.23f, 0.12f, 0.96f),
                HighlightColor = new Color(0.24f, 0.36f, 0.17f, 1f),
                PressedColor = new Color(0.1f, 0.16f, 0.08f, 1f),
                TextColor = new Color(0.94f, 0.99f, 0.91f, 1f),
                AccentColor = new Color(0.68f, 0.95f, 0.56f, 0.94f),
                IconTint = Color.white
            },
            _ => new RewardChoicePalette
            {
                CardColor = new Color(0.29f, 0.19f, 0.1f, 0.96f),
                HighlightColor = new Color(0.42f, 0.28f, 0.15f, 1f),
                PressedColor = new Color(0.21f, 0.13f, 0.08f, 1f),
                TextColor = new Color(1f, 0.95f, 0.88f, 1f),
                AccentColor = new Color(0.96f, 0.76f, 0.32f, 0.94f),
                IconTint = Color.white
            }
        };

        if (!option.IsGuaranteed)
            return palette;

        palette.HighlightColor = Color.Lerp(palette.HighlightColor, Color.white, 0.12f);
        palette.AccentColor = Color.Lerp(palette.AccentColor, Color.white, 0.18f);
        return palette;
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, name, System.StringComparison.Ordinal))
                return child;

            Transform nested = FindDescendant(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        if (gameObject == null)
            return null;

        T component = gameObject.GetComponent<T>();
        if (component == null)
            component = gameObject.AddComponent<T>();

        return component;
    }

    private static T ResolveReferenceWithoutLogging<T>(System.Func<T> instanceResolver = null, bool includeInactive = false) where T : Component
    {
        if (ServiceRegistry.TryResolve(out T registered))
            return registered;

        T singleton = instanceResolver != null ? instanceResolver.Invoke() : null;
        if (singleton != null)
            return singleton;

        FindObjectsInactive inactiveMode = includeInactive
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;
        return Object.FindAnyObjectByType<T>(inactiveMode);
    }
}
