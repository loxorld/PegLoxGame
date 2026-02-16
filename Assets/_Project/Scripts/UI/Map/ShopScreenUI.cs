using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopScreenUI : MonoBehaviour, IMapShopView
{
    private static ShopScreenUI instance;

    private Canvas rootCanvas;
    private Image frame;
    private TMP_Text titleText;
    private TMP_Text coinText;
    private TMP_Text descriptionText;
    private Transform buttonsContainer;
    private Button buttonTemplate;
    private RectTransform optionsRootRect;
    private VerticalLayoutGroup optionsLayoutGroup;

    private readonly List<Button> optionButtons = new List<Button>();

    public static ShopScreenUI GetOrCreate()
    {
        if (instance != null && !instance.Equals(null))
            return instance;

        GameObject root = new GameObject("ShopScreenUI");
        instance = root.AddComponent<ShopScreenUI>();
        return instance;
    }

    public void ShowShop(MapDomainService.ShopOutcome shopOutcome, IReadOnlyList<ShopService.ShopOptionData> options)
    {
        gameObject.SetActive(true);
        titleText.text = string.IsNullOrWhiteSpace(shopOutcome.Title) ? "Tienda" : shopOutcome.Title;
        coinText.text = $"Monedas: {Mathf.Max(0, shopOutcome.CurrentCoins)}";
        descriptionText.text = shopOutcome.Description ?? string.Empty;
        RebuildOptions(options);
    }

    private void BuildUi()
    {
        if (rootCanvas != null)
            return;

        DontDestroyOnLoad(gameObject);

        rootCanvas = gameObject.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 400;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        GameObject dimmerObject = CreateUiObject("Dimmer", transform);
        Image dimmer = dimmerObject.AddComponent<Image>();
        dimmer.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
        StretchRect(dimmerObject.GetComponent<RectTransform>());

        GameObject frameObject = CreateUiObject("Frame", dimmerObject.transform);
        frame = frameObject.AddComponent<Image>();
        frame.color = new Color(0.12f, 0.13f, 0.17f, 0.98f);
        Outline frameOutline = frameObject.AddComponent<Outline>();
        frameOutline.effectColor = new Color(0.95f, 0.83f, 0.46f, 0.9f);
        frameOutline.effectDistance = new Vector2(3f, -3f);

        Shadow frameShadow = frameObject.AddComponent<Shadow>();
        frameShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        frameShadow.effectDistance = new Vector2(10f, -10f);

        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(1150f, 760f);
        frameRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup frameLayout = frameObject.AddComponent<VerticalLayoutGroup>();
        frameLayout.padding = new RectOffset(50, 50, 36, 36);
        frameLayout.spacing = 16f;
        frameLayout.childControlHeight = true;
        frameLayout.childControlWidth = true;
        frameLayout.childForceExpandHeight = false;

        GameObject headerObject = CreateUiObject("Header", frameObject.transform);
        HorizontalLayoutGroup headerLayout = headerObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 20f;
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandHeight = false;
        headerLayout.childForceExpandWidth = false;

        LayoutElement headerElement = headerObject.AddComponent<LayoutElement>();
        headerElement.preferredHeight = 90f;

        GameObject titleObject = CreateText("Title", headerObject.transform, 54, FontStyles.Bold);
        titleText = titleObject.GetComponent<TMP_Text>();
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = new Color(0.98f, 0.93f, 0.78f);

        LayoutElement titleLayout = titleObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;

        GameObject coinBadgeObject = CreateUiObject("CoinBadge", headerObject.transform);
        Image coinBadge = coinBadgeObject.AddComponent<Image>();
        coinBadge.color = new Color(0.22f, 0.16f, 0.04f, 1f);
        Outline coinOutline = coinBadgeObject.AddComponent<Outline>();
        coinOutline.effectColor = new Color(0.95f, 0.83f, 0.46f, 0.8f);
        coinOutline.effectDistance = new Vector2(2f, -2f);

        LayoutElement coinLayout = coinBadgeObject.AddComponent<LayoutElement>();
        coinLayout.preferredWidth = 270f;
        coinLayout.preferredHeight = 64f;

        coinText = CreateText("CoinText", coinBadgeObject.transform, 30, FontStyles.Bold).GetComponent<TMP_Text>();
        coinText.alignment = TextAlignmentOptions.Center;
        coinText.color = new Color(1f, 0.93f, 0.62f);
        StretchRect(coinText.rectTransform);

        GameObject descriptionObject = CreateText("Description", frameObject.transform, 32, FontStyles.Normal);
        descriptionText = descriptionObject.GetComponent<TMP_Text>();
        descriptionText.alignment = TextAlignmentOptions.TopLeft;
        descriptionText.enableWordWrapping = true;
        descriptionText.color = new Color(0.88f, 0.9f, 0.95f);
        LayoutElement descriptionLayout = descriptionObject.AddComponent<LayoutElement>();
        descriptionLayout.preferredHeight = 100f;

        GameObject optionsRoot = CreateUiObject("OptionsRoot", frameObject.transform);
        LayoutElement optionsRootLayout = optionsRoot.AddComponent<LayoutElement>();
        optionsRootLayout.flexibleHeight = 1f;
        optionsRootLayout.minHeight = 360f;

        Image optionsBackground = optionsRoot.AddComponent<Image>();
        optionsBackground.color = new Color(0.07f, 0.08f, 0.11f, 0.85f);

        GameObject contentObject = CreateUiObject("OptionsContent", optionsRoot.transform);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup optionsLayout = contentObject.AddComponent<VerticalLayoutGroup>();
        optionsLayout.padding = new RectOffset(18, 18, 20, 20);
        optionsLayout.spacing = 12f;
        optionsLayout.childControlHeight = true;
        optionsLayout.childControlWidth = true;
        optionsLayout.childForceExpandHeight = false;
        optionsLayout.childForceExpandWidth = true;

        optionsRootRect = optionsRoot.GetComponent<RectTransform>();
        optionsLayoutGroup = optionsLayout;
        buttonsContainer = contentObject.transform;

        buttonTemplate = CreateButtonTemplate(contentObject.transform);
        buttonTemplate.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    private Button CreateButtonTemplate(Transform parent)
    {
        GameObject buttonObject = CreateUiObject("OptionButtonTemplate", parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.22f, 0.34f, 0.53f, 0.95f);

        Outline border = buttonObject.AddComponent<Outline>();
        border.effectColor = new Color(0.85f, 0.9f, 1f, 0.35f);
        border.effectDistance = new Vector2(1f, -1f);

        Button button = buttonObject.AddComponent<Button>();

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0f, 82f);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 82f;

        GameObject labelObject = CreateText("Label", buttonObject.transform, 24, FontStyles.Bold);
        TMP_Text labelText = labelObject.GetComponent<TMP_Text>();
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.enableWordWrapping = true;
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 14f;
        labelText.fontSizeMax = 24f;
        StretchRect(labelObject.GetComponent<RectTransform>());

        return button;
    }

    private void RebuildOptions(IReadOnlyList<ShopService.ShopOptionData> options)
    {
        for (int i = 0; i < optionButtons.Count; i++)
            Destroy(optionButtons[i].gameObject);
        optionButtons.Clear();

        if (options == null || options.Count == 0)
            return;

        for (int i = 0; i < options.Count; i++)
        {
            ShopService.ShopOptionData option = options[i];
            if (option == null)
                continue;

            Button button = Instantiate(buttonTemplate, buttonsContainer);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.interactable = option.IsEnabled;

            ApplyButtonVisualStyle(button, option);

            int capturedIndex = i;
            button.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                options[capturedIndex].OnSelect?.Invoke();
            });

            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>();
            if (labelText != null)
            {
                string icon = GetRarityIcon(option);
                labelText.text = string.IsNullOrEmpty(icon) ? option.Label : $"{icon} {option.Label}";
            }

            optionButtons.Add(button);
        }

        ResizeOptionButtons(optionButtons.Count);
    }

    private void ResizeOptionButtons(int optionCount)
    {
        if (optionCount <= 0 || optionsRootRect == null || optionsLayoutGroup == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(optionsRootRect);

        float availableHeight = optionsRootRect.rect.height
            - optionsLayoutGroup.padding.top
            - optionsLayoutGroup.padding.bottom
            - optionsLayoutGroup.spacing * Mathf.Max(0, optionCount - 1);

        if (availableHeight <= 0f)
            return;

        float targetHeight = Mathf.Clamp(availableHeight / optionCount, 54f, 82f);

        for (int i = 0; i < optionButtons.Count; i++)
        {
            Button button = optionButtons[i];
            if (button == null)
                continue;

            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = targetHeight;
                layout.minHeight = targetHeight;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, targetHeight);
        }
    }

    private static string GetRarityIcon(ShopService.ShopOptionData option)
    {
        if (option.IsExitOption)
            return "<";

        if (!option.Rarity.HasValue)
            return string.Empty;

        switch (option.Rarity.Value)
        {
            case ShopService.ShopOfferRarity.Common:
                return "[C]";
            case ShopService.ShopOfferRarity.Rare:
                return "[R]";
            case ShopService.ShopOfferRarity.Epic:
                return "[E]";
            case ShopService.ShopOfferRarity.Legendary:
                return "[L]";
            default:
                return string.Empty;
        }
    }

    private static void ApplyButtonVisualStyle(Button button, ShopService.ShopOptionData option)
    {
        Image image = button.GetComponent<Image>();
        Outline border = button.GetComponent<Outline>();

        Color normal;
        Color highlighted;
        Color pressed;
        Color borderColor;

        if (option.IsExitOption)
        {
            normal = new Color(0.28f, 0.2f, 0.2f, 0.95f);
            highlighted = new Color(0.36f, 0.26f, 0.26f, 1f);
            pressed = new Color(0.22f, 0.15f, 0.15f, 1f);
            borderColor = new Color(0.9f, 0.55f, 0.55f, 0.45f);
        }
        else if (!option.Rarity.HasValue)
        {
            normal = new Color(0.23f, 0.33f, 0.5f, 0.95f);
            highlighted = new Color(0.3f, 0.42f, 0.63f, 1f);
            pressed = new Color(0.18f, 0.27f, 0.4f, 1f);
            borderColor = new Color(0.8f, 0.89f, 1f, 0.35f);
        }
        else
        {
            switch (option.Rarity.Value)
            {
                case ShopService.ShopOfferRarity.Common:
                    normal = new Color(0.23f, 0.33f, 0.5f, 0.95f);
                    highlighted = new Color(0.3f, 0.42f, 0.63f, 1f);
                    pressed = new Color(0.18f, 0.27f, 0.4f, 1f);
                    borderColor = new Color(0.8f, 0.89f, 1f, 0.35f);
                    break;
                case ShopService.ShopOfferRarity.Rare:
                    normal = new Color(0.18f, 0.4f, 0.34f, 0.95f);
                    highlighted = new Color(0.24f, 0.5f, 0.43f, 1f);
                    pressed = new Color(0.14f, 0.32f, 0.27f, 1f);
                    borderColor = new Color(0.54f, 0.95f, 0.83f, 0.45f);
                    break;
                case ShopService.ShopOfferRarity.Epic:
                    normal = new Color(0.37f, 0.22f, 0.5f, 0.95f);
                    highlighted = new Color(0.47f, 0.29f, 0.64f, 1f);
                    pressed = new Color(0.29f, 0.16f, 0.38f, 1f);
                    borderColor = new Color(0.87f, 0.66f, 1f, 0.48f);
                    break;
                case ShopService.ShopOfferRarity.Legendary:
                    normal = new Color(0.53f, 0.33f, 0.12f, 0.96f);
                    highlighted = new Color(0.67f, 0.43f, 0.17f, 1f);
                    pressed = new Color(0.41f, 0.24f, 0.08f, 1f);
                    borderColor = new Color(1f, 0.88f, 0.52f, 0.52f);
                    break;
                default:
                    normal = new Color(0.23f, 0.33f, 0.5f, 0.95f);
                    highlighted = new Color(0.3f, 0.42f, 0.63f, 1f);
                    pressed = new Color(0.18f, 0.27f, 0.4f, 1f);
                    borderColor = new Color(0.8f, 0.89f, 1f, 0.35f);
                    break;
            }
        }

        if (image != null)
            image.color = normal;

        if (border != null)
            border.effectColor = borderColor;

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlighted;
        colors.pressedColor = pressed;
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.55f);
        button.colors = colors;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName);
        RectTransform rect = obj.AddComponent<RectTransform>();
        obj.transform.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return obj;
    }

    private static GameObject CreateText(string objectName, Transform parent, int fontSize, FontStyles fontStyle)
    {
        GameObject textObj = CreateUiObject(objectName, parent);
        TMP_Text text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.text = string.Empty;
        text.raycastTarget = false;
        return textObj;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (rootCanvas == null)
            BuildUi();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}