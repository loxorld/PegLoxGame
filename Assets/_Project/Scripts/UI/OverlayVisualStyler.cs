using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class OverlayVisualStyler
{
    private struct OverlayPalette
    {
        public Color OverlayColor;
        public Color GlowColor;
        public Color CardColor;
        public Color AccentColor;
        public Color FrameColor;
        public Color TitleColor;
        public Color SubtitleColor;
        public Color DividerColor;
    }

    public static void StylePauseOverlay(GameObject root, RectTransform card, Button resumeButton, Button restartButton, Button menuButton)
    {
        OverlayPalette palette = new OverlayPalette
        {
            OverlayColor = new Color(0.03f, 0.03f, 0.03f, 0.78f),
            GlowColor = new Color(0.88f, 0.68f, 0.24f, 0.18f),
            CardColor = new Color(0.12f, 0.095f, 0.07f, 0.96f),
            AccentColor = new Color(0.92f, 0.72f, 0.28f, 1f),
            FrameColor = new Color(0.55f, 0.38f, 0.15f, 0.7f),
            TitleColor = new Color(0.99f, 0.95f, 0.85f, 1f),
            SubtitleColor = new Color(0.82f, 0.8f, 0.74f, 1f),
            DividerColor = new Color(0.92f, 0.72f, 0.28f, 0.7f)
        };

        StyleOverlayShell(root, card, palette, "PAUSA TACTICA", "Respira, revisa la tirada y volve cuando quieras.");
        StyleOverlayButton(resumeButton, "REANUDAR", new Color(0.22f, 0.45f, 0.32f, 1f), new Color(0.3f, 0.58f, 0.41f, 1f), new Color(0.17f, 0.34f, 0.24f, 1f), new Color(0.96f, 0.98f, 0.9f, 1f), true);
        StyleOverlayButton(menuButton, "VOLVER AL MENU", new Color(0.29f, 0.24f, 0.18f, 1f), new Color(0.39f, 0.31f, 0.21f, 1f), new Color(0.22f, 0.18f, 0.13f, 1f), new Color(0.98f, 0.94f, 0.84f, 1f), false);
        StyleOverlayButton(restartButton, "REINICIAR COMBATE", new Color(0.47f, 0.21f, 0.14f, 1f), new Color(0.62f, 0.28f, 0.18f, 1f), new Color(0.37f, 0.16f, 0.11f, 1f), new Color(0.99f, 0.92f, 0.86f, 1f), false);
    }

    public static void StyleGameOverOverlay(GameObject root, RectTransform card, Button restartButton)
    {
        OverlayPalette palette = new OverlayPalette
        {
            OverlayColor = new Color(0.02f, 0.015f, 0.02f, 0.84f),
            GlowColor = new Color(0.82f, 0.18f, 0.14f, 0.2f),
            CardColor = new Color(0.13f, 0.055f, 0.055f, 0.97f),
            AccentColor = new Color(0.9f, 0.3f, 0.2f, 1f),
            FrameColor = new Color(0.58f, 0.17f, 0.12f, 0.74f),
            TitleColor = new Color(0.99f, 0.92f, 0.9f, 1f),
            SubtitleColor = new Color(0.86f, 0.78f, 0.76f, 1f),
            DividerColor = new Color(0.9f, 0.3f, 0.2f, 0.72f)
        };

        StyleOverlayShell(root, card, palette, "DERROTA", "La expedicion cayo, pero todavia podes intentarlo otra vez.");
        StyleOverlayButton(restartButton, "INTENTAR DE NUEVO", new Color(0.66f, 0.22f, 0.14f, 1f), new Color(0.82f, 0.3f, 0.18f, 1f), new Color(0.52f, 0.17f, 0.12f, 1f), new Color(1f, 0.95f, 0.9f, 1f), true);
    }

    public static void StyleRewardOverlay(GameObject root, RectTransform card, Button choice1Button, Button choice2Button, Button choice3Button)
    {
        OverlayPalette palette = new OverlayPalette
        {
            OverlayColor = new Color(0.02f, 0.03f, 0.03f, 0.8f),
            GlowColor = new Color(0.22f, 0.72f, 0.62f, 0.16f),
            CardColor = new Color(0.08f, 0.1f, 0.09f, 0.97f),
            AccentColor = new Color(0.88f, 0.72f, 0.28f, 1f),
            FrameColor = new Color(0.24f, 0.58f, 0.5f, 0.6f),
            TitleColor = new Color(0.99f, 0.96f, 0.88f, 1f),
            SubtitleColor = new Color(0.84f, 0.88f, 0.84f, 1f),
            DividerColor = new Color(0.3f, 0.74f, 0.66f, 0.66f)
        };

        StyleOverlayShell(root, card, palette, "BOTIN", "Elige una recompensa y sigue empujando la run.");
        StyleOverlayButton(choice1Button, "BOTIN 1", new Color(0.15f, 0.21f, 0.18f, 1f), new Color(0.21f, 0.32f, 0.27f, 1f), new Color(0.11f, 0.15f, 0.13f, 1f), new Color(0.98f, 0.96f, 0.9f, 1f), true, overwriteLabel: false);
        StyleOverlayButton(choice2Button, "BOTIN 2", new Color(0.15f, 0.21f, 0.18f, 1f), new Color(0.21f, 0.32f, 0.27f, 1f), new Color(0.11f, 0.15f, 0.13f, 1f), new Color(0.98f, 0.96f, 0.9f, 1f), false, overwriteLabel: false);
        StyleOverlayButton(choice3Button, "BOTIN 3", new Color(0.15f, 0.21f, 0.18f, 1f), new Color(0.21f, 0.32f, 0.27f, 1f), new Color(0.11f, 0.15f, 0.13f, 1f), new Color(0.98f, 0.96f, 0.9f, 1f), false, overwriteLabel: false);
    }

    private static void StyleOverlayShell(GameObject root, RectTransform card, OverlayPalette palette, string title, string subtitle)
    {
        if (root == null || card == null)
            return;

        RectTransform rootRect = root.transform as RectTransform;
        if (rootRect == null)
            return;

        Image rootImage = EnsureComponent<Image>(root);
        UIArtUtility.ApplyImageStyle(rootImage, palette.OverlayColor, true, Image.Type.Sliced, GetBuiltinSprite());

        EnsureBackdropGlow(rootRect, palette.GlowColor);

        if (UIArtUtility.AllowsProceduralLayout(card))
        {
            VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(card.gameObject);
            layout.padding = new RectOffset(34, 34, 34, 34);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = EnsureComponent<ContentSizeFitter>(card.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            LayoutElement cardLayout = EnsureComponent<LayoutElement>(card.gameObject);
            cardLayout.minWidth = 420f;
            cardLayout.minHeight = 320f;
            cardLayout.preferredHeight = 0f;
        }

        Image cardImage = EnsureComponent<Image>(card.gameObject);
        UIArtUtility.ApplyImageStyle(cardImage, palette.CardColor, true, Image.Type.Sliced, GetBuiltinSprite());

        if (UIArtUtility.AllowsGeneratedDecor(card))
        {
            Shadow cardShadow = EnsureComponent<Shadow>(card.gameObject);
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
            cardShadow.effectDistance = new Vector2(0f, -18f);
            cardShadow.useGraphicAlpha = true;

            Outline cardOutline = EnsureComponent<Outline>(card.gameObject);
            cardOutline.effectColor = palette.FrameColor;
            cardOutline.effectDistance = new Vector2(2f, -2f);
            cardOutline.useGraphicAlpha = true;
        }

        EnsureAccentBar(card, palette.AccentColor);

        TMP_Text titleText = EnsureHeaderText(card, "OverlayTitleText", 0);
        titleText.text = title;
        if (!UIArtUtility.ShouldPreserveTextStyling(titleText))
        {
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 26f;
            titleText.fontSizeMax = 42f;
            titleText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            titleText.color = palette.TitleColor;
            titleText.characterSpacing = 5f;
            titleText.outlineWidth = 0.18f;
            titleText.outlineColor = new Color(0.06f, 0.04f, 0.03f, 0.85f);
            titleText.margin = new Vector4(18f, 6f, 18f, 0f);
        }

        if (UIArtUtility.AllowsProceduralLayout(titleText))
            EnsureComponent<LayoutElement>(titleText.gameObject).preferredHeight = 60f;

        TMP_Text subtitleText = EnsureBodyText(card, "OverlaySubtitleText", 1);
        subtitleText.text = subtitle;
        if (!UIArtUtility.ShouldPreserveTextStyling(subtitleText))
        {
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.enableAutoSizing = true;
            subtitleText.fontSizeMin = 14f;
            subtitleText.fontSizeMax = 21f;
            subtitleText.fontStyle = FontStyles.Bold;
            subtitleText.color = palette.SubtitleColor;
            subtitleText.lineSpacing = 8f;
            subtitleText.textWrappingMode = TextWrappingModes.Normal;
            subtitleText.outlineWidth = 0.1f;
            subtitleText.outlineColor = new Color(0.04f, 0.03f, 0.03f, 0.75f);
            subtitleText.margin = new Vector4(24f, 0f, 24f, 0f);
        }

        if (UIArtUtility.AllowsProceduralLayout(subtitleText))
            EnsureComponent<LayoutElement>(subtitleText.gameObject).preferredHeight = 56f;

        EnsureDivider(card, 2, palette.DividerColor);
    }

    private static void StyleOverlayButton(Button button, string labelText, Color normalColor, Color highlightedColor, Color pressedColor, Color labelColor, bool primary, bool overwriteLabel = true)
    {
        if (button == null)
            return;

        RectTransform rect = button.transform as RectTransform;
        if (rect != null)
        {
            if (UIArtUtility.AllowsProceduralLayout(button))
            {
                LayoutElement layout = EnsureComponent<LayoutElement>(button.gameObject);
                layout.minHeight = primary ? 86f : 78f;
                layout.preferredHeight = primary ? 90f : 82f;
            }
        }

        UIArtUtility.ApplyButtonStyle(
            button,
            normalColor,
            highlightedColor,
            pressedColor,
            new Color(0.32f, 0.32f, 0.32f, 0.5f),
            true,
            Image.Type.Sliced,
            GetBuiltinSprite());

        if (UIArtUtility.AllowsGeneratedDecor(button))
        {
            Shadow shadow = EnsureComponent<Shadow>(button.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = true;

            Outline outline = EnsureComponent<Outline>(button.gameObject);
            outline.effectColor = new Color(1f, 1f, 1f, primary ? 0.14f : 0.09f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            if (overwriteLabel)
                label.text = labelText;
            if (overwriteLabel && !UIArtUtility.ShouldPreserveTextStyling(label))
            {
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = primary ? 18f : 16f;
                label.fontSizeMax = primary ? 28f : 24f;
                label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                label.characterSpacing = primary ? 3.5f : 2.5f;
                label.color = labelColor;
                label.outlineWidth = 0.18f;
                label.outlineColor = new Color(0.08f, 0.05f, 0.04f, 0.85f);
                label.textWrappingMode = TextWrappingModes.Normal;
            }

            if (UIArtUtility.AllowsProceduralLayout(button))
            {
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(22f, 0f);
                labelRect.offsetMax = new Vector2(-22f, 0f);
            }
        }

        UIButtonMotion.Attach(rect, primary ? 1.045f : 1.032f, primary ? 0.955f : 0.965f, 0.12f);
    }

    private static void EnsureBackdropGlow(RectTransform root, Color glowColor)
    {
        if (!UIArtUtility.AllowsGeneratedDecor(root))
            return;

        RectTransform glow = FindDescendant(root, "OverlayGlow") as RectTransform;
        if (glow == null)
        {
            GameObject glowObject = new GameObject("OverlayGlow", typeof(RectTransform), typeof(Image));
            glow = glowObject.GetComponent<RectTransform>();
            glow.SetParent(root, false);
            glow.SetAsFirstSibling();
        }

        glow.anchorMin = new Vector2(0.5f, 0.5f);
        glow.anchorMax = new Vector2(0.5f, 0.5f);
        glow.pivot = new Vector2(0.5f, 0.5f);
        glow.anchoredPosition = new Vector2(0f, 18f);
        glow.sizeDelta = new Vector2(920f, 680f);

        Image glowImage = EnsureComponent<Image>(glow.gameObject);
        UIArtUtility.ApplyImageStyle(glowImage, glowColor, false, Image.Type.Sliced, GetBuiltinSprite());
    }

    private static void EnsureAccentBar(RectTransform card, Color accentColor)
    {
        if (!UIArtUtility.AllowsGeneratedDecor(card))
            return;

        RectTransform accent = FindDescendant(card, "OverlayAccentBar") as RectTransform;
        if (accent == null)
        {
            GameObject accentObject = new GameObject("OverlayAccentBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            accent = accentObject.GetComponent<RectTransform>();
            accent.SetParent(card, false);
            accent.SetAsFirstSibling();
        }

        if (UIArtUtility.AllowsProceduralLayout(accent))
        {
            LayoutElement layout = EnsureComponent<LayoutElement>(accent.gameObject);
            layout.ignoreLayout = true;
        }

        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.anchoredPosition = new Vector2(0f, -12f);
        accent.sizeDelta = new Vector2(-42f, 8f);

        Image accentImage = EnsureComponent<Image>(accent.gameObject);
        UIArtUtility.ApplyImageStyle(accentImage, accentColor, false, Image.Type.Sliced, GetBuiltinSprite());
    }

    private static TMP_Text EnsureHeaderText(RectTransform card, string objectName, int siblingIndex)
    {
        TMP_Text text = FindDescendant(card, objectName)?.GetComponent<TMP_Text>();
        if (text == null)
        {
            TMP_Text existing = FindFirstNonButtonText(card);
            if (existing != null)
            {
                existing.gameObject.name = objectName;
                text = existing;
            }
        }

        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(card, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        text.transform.SetSiblingIndex(siblingIndex);
        return text;
    }

    private static TMP_Text EnsureBodyText(RectTransform card, string objectName, int siblingIndex)
    {
        TMP_Text text = FindDescendant(card, objectName)?.GetComponent<TMP_Text>();
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(card, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        text.transform.SetSiblingIndex(siblingIndex);
        return text;
    }

    private static void EnsureDivider(RectTransform card, int siblingIndex, Color color)
    {
        if (!UIArtUtility.AllowsGeneratedDecor(card))
            return;

        RectTransform divider = FindDescendant(card, "OverlayDivider") as RectTransform;
        if (divider == null)
        {
            GameObject dividerObject = new GameObject("OverlayDivider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider = dividerObject.GetComponent<RectTransform>();
            divider.SetParent(card, false);
        }

        divider.transform.SetSiblingIndex(siblingIndex);

        if (UIArtUtility.AllowsProceduralLayout(divider))
        {
            LayoutElement layout = EnsureComponent<LayoutElement>(divider.gameObject);
            layout.preferredHeight = 8f;
            layout.minHeight = 8f;
        }

        Image image = EnsureComponent<Image>(divider.gameObject);
        UIArtUtility.ApplyImageStyle(image, color, false, Image.Type.Sliced, GetBuiltinSprite());
    }

    private static TMP_Text FindFirstNonButtonText(RectTransform root)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            if (text.GetComponentInParent<Button>() == null)
                return text;
        }

        return null;
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

    private static Sprite GetBuiltinSprite()
    {
        return UIArtUtility.BuiltinPanelSprite;
    }
}
