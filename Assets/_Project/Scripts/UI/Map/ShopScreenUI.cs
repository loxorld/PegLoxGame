using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopScreenUI : MonoBehaviour, IMapShopView
{
    private static ShopScreenUI instance;

    private Canvas rootCanvas;
    private Image dimmer;
    private Image frame;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private Transform buttonsContainer;
    private Button buttonTemplate;

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
        dimmer = dimmerObject.AddComponent<Image>();
        dimmer.color = new Color(0.05f, 0.05f, 0.07f, 0.92f);
        StretchRect(dimmerObject.GetComponent<RectTransform>());

        GameObject frameObject = CreateUiObject("Frame", dimmerObject.transform);
        frame = frameObject.AddComponent<Image>();
        frame.color = new Color(0.16f, 0.16f, 0.2f, 0.98f);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(1100f, 740f);
        frameRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup frameLayout = frameObject.AddComponent<VerticalLayoutGroup>();
        frameLayout.padding = new RectOffset(48, 48, 42, 42);
        frameLayout.spacing = 20f;
        frameLayout.childControlHeight = false;
        frameLayout.childControlWidth = true;
        frameLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = frameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        GameObject titleObject = CreateText("Title", frameObject.transform, 56, FontStyles.Bold);
        titleText = titleObject.GetComponent<TMP_Text>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.98f, 0.93f, 0.78f);
        LayoutElement titleLayout = titleObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 80f;

        GameObject descriptionObject = CreateText("Description", frameObject.transform, 34, FontStyles.Normal);
        descriptionText = descriptionObject.GetComponent<TMP_Text>();
        descriptionText.alignment = TextAlignmentOptions.TopLeft;
        descriptionText.enableWordWrapping = true;
        descriptionText.color = new Color(0.88f, 0.9f, 0.95f);
        LayoutElement descriptionLayout = descriptionObject.AddComponent<LayoutElement>();
        descriptionLayout.preferredHeight = 180f;

        GameObject optionsRoot = CreateUiObject("Options", frameObject.transform);
        buttonsContainer = optionsRoot.transform;
        VerticalLayoutGroup optionsLayout = optionsRoot.AddComponent<VerticalLayoutGroup>();
        optionsLayout.spacing = 16f;
        optionsLayout.childControlHeight = false;
        optionsLayout.childControlWidth = true;
        optionsLayout.childForceExpandHeight = false;
        optionsLayout.childForceExpandWidth = true;

        buttonTemplate = CreateButtonTemplate(optionsRoot.transform);
        buttonTemplate.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    private Button CreateButtonTemplate(Transform parent)
    {
        GameObject buttonObject = CreateUiObject("OptionButtonTemplate", parent);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.22f, 0.34f, 0.53f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.22f, 0.34f, 0.53f, 0.95f);
        colors.highlightedColor = new Color(0.29f, 0.43f, 0.66f, 1f);
        colors.pressedColor = new Color(0.18f, 0.28f, 0.42f, 1f);
        colors.disabledColor = new Color(0.14f, 0.14f, 0.14f, 0.6f);
        button.colors = colors;

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(0f, 74f);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 74f;

        GameObject labelObject = CreateText("Label", buttonObject.transform, 30, FontStyles.Bold);
        TMP_Text labelText = labelObject.GetComponent<TMP_Text>();
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
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
            Button button = Instantiate(buttonTemplate, buttonsContainer);
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.interactable = option.IsEnabled;

            int capturedIndex = i;
            button.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                options[capturedIndex].OnSelect?.Invoke();
            });

            TMP_Text labelText = button.GetComponentInChildren<TMP_Text>();
            if (labelText != null)
                labelText.text = option.Label;

            optionButtons.Add(button);
        }
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