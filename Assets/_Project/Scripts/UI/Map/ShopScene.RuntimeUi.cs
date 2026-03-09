using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ShopScene
{
    private void BuildRuntimeUiIfNeeded()
    {
        if (HasAllReferences())
            return;

        Canvas existingCanvas = GetComponentInChildren<Canvas>(true);
        if (existingCanvas == null)
        {
            GameObject canvasObject = new GameObject("ShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            CreateRuntimePanel(canvasObject.transform);
        }

        if (itemButtonTemplate != null)
            itemButtonTemplate.gameObject.SetActive(false);
    }

    private void CreateRuntimePanel(Transform parent)
    {
        GameObject dimmer = CreateUiObject("Dimmer", parent);
        Image dimmerImage = dimmer.AddComponent<Image>();
        dimmerImage.color = new Color(0f, 0f, 0f, 0.72f);
        runtimeDimmerImage = dimmerImage;
        StretchRect(dimmer.GetComponent<RectTransform>());

        GameObject panel = CreateUiObject("Panel", dimmer.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.98f);
        runtimeWindowImage = panelImage;
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1100f, 720f);
        panelRect.anchoredPosition = Vector2.zero;

        titleLabel = CreateText("Title", panel.transform, 44, FontStyles.Bold);
        titleLabel.alignment = TextAlignmentOptions.TopLeft;
        SetAnchors(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -24f), new Vector2(-30f, -90f));

        coinLabel = CreateText("Coins", panel.transform, 30, FontStyles.Bold);
        coinLabel.alignment = TextAlignmentOptions.TopLeft;
        SetAnchors(coinLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0.45f, 1f), new Vector2(30f, -92f), new Vector2(-10f, -140f));

        stockLabel = CreateText("Stock", panel.transform, 30, FontStyles.Bold);
        stockLabel.alignment = TextAlignmentOptions.TopRight;
        SetAnchors(stockLabel.rectTransform, new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(10f, -92f), new Vector2(-30f, -140f));

        GameObject itemsRoot = CreateUiObject("ItemsRoot", panel.transform);
        Image itemsBg = itemsRoot.AddComponent<Image>();
        itemsBg.color = new Color(0.05f, 0.07f, 0.1f, 0.92f);
        runtimeOffersImage = itemsBg;
        RectTransform itemsRootRect = itemsRoot.GetComponent<RectTransform>();
        SetAnchors(itemsRootRect, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(30f, 95f), new Vector2(-10f, -150f));

        GameObject scrollObject = CreateUiObject("ItemsScroll", itemsRoot.transform);
        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        Image scrollBg = scrollObject.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0f);
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        SetAnchors(scrollRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 12f), new Vector2(-12f, -12f));

        GameObject viewport = CreateUiObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        SetAnchors(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        itemSlotsRoot = content.transform;

        itemButtonTemplate = CreateButton("ItemButtonTemplate", content.transform, "Oferta");
        LayoutElement itemLayout = itemButtonTemplate.gameObject.AddComponent<LayoutElement>();
        itemLayout.preferredHeight = 64f;
        itemButtonTemplate.gameObject.SetActive(false);

        detailLabel = CreateText("Detail", panel.transform, 26, FontStyles.Normal);
        detailLabel.alignment = TextAlignmentOptions.TopLeft;
        detailLabel.textWrappingMode = TextWrappingModes.Normal;
        SetAnchors(detailLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0.7f), new Vector2(12f, 175f), new Vector2(-30f, -12f));

        priceLabel = CreateText("Price", panel.transform, 30, FontStyles.Bold);
        priceLabel.alignment = TextAlignmentOptions.TopLeft;
        SetAnchors(priceLabel.rectTransform, new Vector2(0.5f, 0.7f), new Vector2(1f, 0.78f), new Vector2(12f, -6f), new Vector2(-30f, -8f));

        rarityLabel = CreateText("Rarity", panel.transform, 28, FontStyles.Bold);
        rarityLabel.alignment = TextAlignmentOptions.TopLeft;
        SetAnchors(rarityLabel.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(1f, 0.86f), new Vector2(12f, -6f), new Vector2(-30f, -8f));

        buyButton = CreateButton("BuyButton", panel.transform, "Comprar");
        SetAnchors((RectTransform)buyButton.transform, new Vector2(0.5f, 0f), new Vector2(0.72f, 0f), new Vector2(12f, 24f), new Vector2(-8f, 82f));

        refreshButton = CreateButton("RefreshButton", panel.transform, "Refrescar");
        SetAnchors((RectTransform)refreshButton.transform, new Vector2(0.72f, 0f), new Vector2(0.89f, 0f), new Vector2(8f, 24f), new Vector2(-8f, 82f));

        exitButton = CreateButton("ExitButton", panel.transform, "Salir");
        SetAnchors((RectTransform)exitButton.transform, new Vector2(0.89f, 0f), new Vector2(1f, 0f), new Vector2(8f, 24f), new Vector2(-30f, 82f));
    }

    private void ApplyVisualCustomization()
    {
        if (dimmerBackgroundImage == null)
            dimmerBackgroundImage = runtimeDimmerImage;
        if (windowBackgroundImage == null)
            windowBackgroundImage = runtimeWindowImage;
        if (offersBackgroundImage == null)
            offersBackgroundImage = runtimeOffersImage;

        ApplyImageSprite(dimmerBackgroundImage, dimmerBackgroundSprite);
        ApplyImageSprite(windowBackgroundImage, windowBackgroundSprite);
        ApplyImageSprite(offersBackgroundImage, offersBackgroundSprite);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TMP_Text CreateText(string name, Transform parent, int fontSize, FontStyles style)
    {
        GameObject go = CreateUiObject(name, parent);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.35f, 0.5f, 1f);
        Button button = buttonObject.AddComponent<Button>();

        TMP_Text buttonText = CreateText("Label", buttonObject.transform, 24, FontStyles.Bold);
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.text = label;
        StretchRect(buttonText.rectTransform);

        return button;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void ApplyImageSprite(Image target, Sprite sprite)
    {
        if (target == null || sprite == null)
            return;

        target.sprite = sprite;
        target.type = Image.Type.Sliced;
        target.color = Color.white;
    }

    private static void ApplyButtonArt(Button button, ButtonArtTheme theme)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image == null)
            return;

        if (theme.normalSprite != null)
        {
            image.sprite = theme.normalSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = theme.fallbackColor;
        }

        SpriteState state = button.spriteState;
        state.highlightedSprite = theme.highlightedSprite;
        state.pressedSprite = theme.pressedSprite;
        state.selectedSprite = theme.highlightedSprite;
        state.disabledSprite = theme.disabledSprite;
        button.spriteState = state;

        if (theme.highlightedSprite != null || theme.pressedSprite != null || theme.disabledSprite != null)
            button.transition = Selectable.Transition.SpriteSwap;

        SyncButtonColorBlockWithImage(button);
    }

    private static void SyncButtonColorBlockWithImage(Button button)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        button.colors = colors;
    }
}
