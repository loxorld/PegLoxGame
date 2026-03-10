using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class InventoryOverlayUI : MonoBehaviour
{
    private enum InventoryTab
    {
        Orbs,
        Relics
    }

    private readonly List<Button> entryButtons = new();
    private readonly List<OrbInstance> orbEntries = new();
    private readonly List<ShotEffectBase> relicEntries = new();

    private GameFlowManager flow;
    private OrbManager orbManager;
    private RelicManager relicManager;
    private Canvas boundCanvas;
    private RectTransform hudRoot;
    private RectTransform overlayRoot;
    private RectTransform cardRoot;
    private RectTransform itemListContent;
    private RectTransform inventoryButtonRoot;
    private Button inventoryButton;
    private Button closeButton;
    private Button orbsTabButton;
    private Button relicsTabButton;
    private ScrollRect itemListScrollRect;
    private Image detailIconImage;
    private TMP_Text listTitleText;
    private TMP_Text detailNameText;
    private TMP_Text detailMetaText;
    private TMP_Text detailStatsText;
    private TMP_Text detailDescriptionText;
    private TMP_Text emptyListText;
    private CanvasGroup overlayCanvasGroup;
    private InventoryTab activeTab = InventoryTab.Orbs;
    private int selectedIndex = -1;
    private Vector2 lastCanvasSize = new Vector2(-1f, -1f);
    private Vector2 lastOverlayCanvasSize = new Vector2(-1f, -1f);
    private bool flowSubscribed;
    private bool overlayInitialized;

    public void Bind(Canvas canvas, RectTransform combatHudRoot)
    {
        if (canvas != null)
            boundCanvas = canvas;

        if (combatHudRoot != null)
            hudRoot = combatHudRoot;

        ResolveReferences();
        EnsureRuntimeUi();
        RefreshOverlayLayout(force: true);
        RefreshButtonPlacement(force: true);
        RefreshButtonAvailability();
        SyncState();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureRuntimeUi();
        RefreshOverlayLayout(force: true);
        SyncState();
    }

    private void OnDisable()
    {
        UnsubscribeFlow();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        EnsureRuntimeUi();
        RefreshOverlayLayout();
        RefreshButtonPlacement();
        RefreshButtonAvailability();

        if (flow != null && flow.State == GameState.Inventory && WasCloseRequestedThisFrame())
            flow.CloseInventory();
    }

    private void ResolveReferences()
    {
        GameFlowManager resolvedFlow = flow != null
            ? flow
            : GameFlowManager.Instance ?? ServiceRegistry.LegacyFind<GameFlowManager>(true);
        if (resolvedFlow != flow)
        {
            UnsubscribeFlow();
            flow = resolvedFlow;
            SubscribeFlow();
        }

        if (orbManager == null)
            orbManager = OrbManager.Instance ?? ServiceRegistry.LegacyFind<OrbManager>(true);

        if (relicManager == null)
            relicManager = RelicManager.Instance ?? ServiceRegistry.LegacyFind<RelicManager>(true);

        if (boundCanvas == null)
            boundCanvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (hudRoot == null && boundCanvas != null)
        {
            Transform combatHud = FindDescendant(boundCanvas.transform, "CombatHUD");
            hudRoot = combatHud as RectTransform;
        }
    }

    private void SubscribeFlow()
    {
        if (flow == null || flowSubscribed)
            return;

        flow.OnStateChanged += OnStateChanged;
        flowSubscribed = true;
    }

    private void UnsubscribeFlow()
    {
        if (flow == null || !flowSubscribed)
            return;

        flow.OnStateChanged -= OnStateChanged;
        flowSubscribed = false;
    }

    private void EnsureRuntimeUi()
    {
        if (boundCanvas == null)
            return;

        EnsureInventoryButton();
        EnsureOverlay();
    }

    private void EnsureInventoryButton()
    {
        if (boundCanvas == null)
            return;

        if (inventoryButtonRoot == null)
        {
            Transform existing = FindDescendant(boundCanvas.transform, "InventoryButton");
            inventoryButtonRoot = existing as RectTransform;
        }

        if (inventoryButtonRoot == null)
        {
            GameObject buttonObject = new GameObject("InventoryButton", typeof(RectTransform), typeof(Image), typeof(Button));
            inventoryButtonRoot = buttonObject.GetComponent<RectTransform>();
            inventoryButtonRoot.SetParent(boundCanvas.transform, false);
        }

        inventoryButton = inventoryButtonRoot.GetComponent<Button>();
        if (inventoryButton == null)
            inventoryButton = inventoryButtonRoot.gameObject.AddComponent<Button>();

        Image buttonImage = inventoryButtonRoot.GetComponent<Image>();
        UIArtUtility.ApplyButtonStyle(
            inventoryButton,
            new Color(0.18f, 0.2f, 0.34f, 1f),
            new Color(0.26f, 0.3f, 0.5f, 1f),
            new Color(0.14f, 0.16f, 0.28f, 1f),
            new Color(0.22f, 0.22f, 0.24f, 0.55f),
            true,
            Image.Type.Sliced,
            UIArtUtility.BuiltinPanelSprite,
            0.08f);
        buttonImage.raycastTarget = true;

        Shadow shadow = EnsureComponent<Shadow>(inventoryButtonRoot.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(0f, -5f);
        shadow.useGraphicAlpha = true;

        TMP_Text label = FindDescendant(inventoryButtonRoot, "Label")?.GetComponent<TMP_Text>();
        if (label == null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(inventoryButtonRoot, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        label.text = "BOLSA";
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 12f;
        label.fontSizeMax = 20f;
        label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        label.color = new Color(0.97f, 0.96f, 0.9f, 1f);
        label.outlineWidth = 0.18f;
        label.outlineColor = new Color(0.08f, 0.07f, 0.14f, 0.8f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        StretchRect(label.rectTransform, new Vector2(8f, 6f), new Vector2(-8f, -6f));

        inventoryButton.onClick.RemoveListener(OnInventoryButtonPressed);
        inventoryButton.onClick.AddListener(OnInventoryButtonPressed);
        UIButtonMotion.Attach(inventoryButtonRoot, 1.03f, 0.965f, 0.12f);
    }

    private void EnsureOverlay()
    {
        if (boundCanvas == null)
            return;

        if (overlayRoot == null)
        {
            Transform existing = FindDescendant(boundCanvas.transform, "InventoryOverlayRoot");
            overlayRoot = existing as RectTransform;
        }

        if (overlayRoot == null)
        {
            GameObject overlayObject = new GameObject("InventoryOverlayRoot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlayRoot = overlayObject.GetComponent<RectTransform>();
            overlayRoot.SetParent(boundCanvas.transform, false);
        }

        overlayRoot.SetParent(boundCanvas.transform, false);
        overlayRoot.SetAsLastSibling();
        StretchRect(overlayRoot, Vector2.zero, Vector2.zero);

        Image overlayImage = overlayRoot.GetComponent<Image>();
        UIArtUtility.ApplyImageStyle(overlayImage, new Color(0.02f, 0.03f, 0.04f, 0.84f), true, Image.Type.Sliced, UIArtUtility.BuiltinPanelSprite);

        overlayCanvasGroup = overlayRoot.GetComponent<CanvasGroup>();
        if (overlayCanvasGroup == null)
            overlayCanvasGroup = overlayRoot.gameObject.AddComponent<CanvasGroup>();

        if (cardRoot == null)
        {
            Transform existingCard = overlayRoot.Find("InventoryCard");
            cardRoot = existingCard as RectTransform;
        }

        if (cardRoot == null)
        {
            GameObject cardObject = new GameObject("InventoryCard", typeof(RectTransform), typeof(Image), typeof(Shadow));
            cardRoot = cardObject.GetComponent<RectTransform>();
            cardRoot.SetParent(overlayRoot, false);
        }

        cardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cardRoot.pivot = new Vector2(0.5f, 0.5f);
        cardRoot.sizeDelta = new Vector2(1160f, 720f);
        cardRoot.anchoredPosition = Vector2.zero;

        Image cardImage = cardRoot.GetComponent<Image>();
        UIArtUtility.ApplyImageStyle(cardImage, new Color(0.08f, 0.09f, 0.1f, 0.98f), true, Image.Type.Sliced, UIArtUtility.BuiltinPanelSprite);

        Shadow cardShadow = cardRoot.GetComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.38f);
        cardShadow.effectDistance = new Vector2(0f, -16f);
        cardShadow.useGraphicAlpha = true;

        TMP_Text title = EnsureText(cardRoot, "Title", "Pertenencias");
        title.alignment = TextAlignmentOptions.Left;
        title.enableAutoSizing = true;
        title.fontSizeMin = 24f;
        title.fontSizeMax = 38f;
        title.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        title.color = new Color(0.99f, 0.95f, 0.84f, 1f);
        title.outlineWidth = 0.18f;
        title.outlineColor = new Color(0.07f, 0.05f, 0.03f, 0.8f);
        title.raycastTarget = false;
        SetAnchors(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -18f), new Vector2(-180f, -66f));

        closeButton = EnsureButton(cardRoot, "CloseButton", "CERRAR", OnClosePressed);
        StyleTopButton(closeButton, new Color(0.36f, 0.2f, 0.16f, 1f), new Color(0.5f, 0.28f, 0.22f, 1f), new Color(0.28f, 0.15f, 0.12f, 1f));
        SetAnchors((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-146f, -22f), new Vector2(-24f, -68f));

        orbsTabButton = EnsureButton(cardRoot, "TabOrbs", "ORBES", () => SetTab(InventoryTab.Orbs));
        relicsTabButton = EnsureButton(cardRoot, "TabRelics", "RELIQUIAS", () => SetTab(InventoryTab.Relics));
        SetAnchors((RectTransform)orbsTabButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -84f), new Vector2(188f, -132f));
        SetAnchors((RectTransform)relicsTabButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(198f, -84f), new Vector2(398f, -132f));

        RectTransform listPanel = EnsurePanel(cardRoot, "ListPanel", new Color(0.11f, 0.1f, 0.07f, 0.97f));
        SetAnchors(listPanel, new Vector2(0f, 0f), new Vector2(0.4f, 1f), new Vector2(28f, 28f), new Vector2(-12f, -148f));

        RectTransform detailPanel = EnsurePanel(cardRoot, "DetailPanel", new Color(0.06f, 0.11f, 0.12f, 0.97f));
        SetAnchors(detailPanel, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(12f, 28f), new Vector2(-28f, -148f));

        listTitleText = EnsureText(listPanel, "ListTitle");
        listTitleText.alignment = TextAlignmentOptions.Left;
        listTitleText.enableAutoSizing = true;
        listTitleText.fontSizeMin = 18f;
        listTitleText.fontSizeMax = 28f;
        listTitleText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        listTitleText.color = new Color(0.99f, 0.95f, 0.84f, 1f);
        listTitleText.outlineWidth = 0.16f;
        listTitleText.outlineColor = new Color(0.08f, 0.06f, 0.03f, 0.72f);
        listTitleText.raycastTarget = false;
        SetAnchors(listTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -18f), new Vector2(-18f, -56f));

        RectTransform scrollRoot = EnsureRect(listPanel, "ScrollRoot");
        SetAnchors(scrollRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(14f, 16f), new Vector2(-14f, -64f));

        itemListScrollRect = EnsureComponent<ScrollRect>(scrollRoot.gameObject);
        itemListScrollRect.horizontal = false;
        itemListScrollRect.vertical = true;
        itemListScrollRect.movementType = ScrollRect.MovementType.Clamped;
        itemListScrollRect.scrollSensitivity = 30f;
        itemListScrollRect.inertia = true;
        itemListScrollRect.decelerationRate = 0.1f;

        RectTransform viewport = EnsureRect(scrollRoot, "Viewport");
        StretchRect(viewport, Vector2.zero, Vector2.zero);
        Image viewportImage = EnsureComponent<Image>(viewport.gameObject);
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;
        EnsureComponent<RectMask2D>(viewport.gameObject);

        itemListContent = EnsureRect(viewport, "Content");
        itemListContent.anchorMin = new Vector2(0f, 1f);
        itemListContent.anchorMax = new Vector2(1f, 1f);
        itemListContent.pivot = new Vector2(0.5f, 1f);
        itemListContent.anchoredPosition = Vector2.zero;
        itemListContent.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup listLayout = EnsureComponent<VerticalLayoutGroup>(itemListContent.gameObject);
        listLayout.padding = new RectOffset(0, 0, 0, 0);
        listLayout.spacing = 12f;
        listLayout.childAlignment = TextAnchor.UpperCenter;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = EnsureComponent<ContentSizeFitter>(itemListContent.gameObject);
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        itemListScrollRect.viewport = viewport;
        itemListScrollRect.content = itemListContent;

        emptyListText = EnsureText(viewport, "EmptyListText", "Todavia no hay nada que mostrar.");
        emptyListText.alignment = TextAlignmentOptions.Center;
        emptyListText.enableAutoSizing = true;
        emptyListText.fontSizeMin = 14f;
        emptyListText.fontSizeMax = 22f;
        emptyListText.fontStyle = FontStyles.Bold;
        emptyListText.color = new Color(0.82f, 0.83f, 0.8f, 0.92f);
        emptyListText.textWrappingMode = TextWrappingModes.Normal;
        emptyListText.raycastTarget = false;
        StretchRect(emptyListText.rectTransform, new Vector2(22f, 22f), new Vector2(-22f, -22f));

        RectTransform iconFrame = EnsurePanel(detailPanel, "DetailIconFrame", new Color(0.17f, 0.14f, 0.08f, 1f));
        SetAnchors(iconFrame, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(140f, -140f));

        RectTransform iconRect = EnsureRect(iconFrame, "DetailIcon");
        StretchRect(iconRect, new Vector2(12f, 12f), new Vector2(-12f, -12f));
        detailIconImage = EnsureComponent<Image>(iconRect.gameObject);
        detailIconImage.preserveAspect = true;
        detailIconImage.raycastTarget = false;

        detailNameText = EnsureText(detailPanel, "DetailName");
        detailNameText.alignment = TextAlignmentOptions.TopLeft;
        detailNameText.enableAutoSizing = true;
        detailNameText.fontSizeMin = 22f;
        detailNameText.fontSizeMax = 36f;
        detailNameText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        detailNameText.color = new Color(0.99f, 0.95f, 0.85f, 1f);
        detailNameText.outlineWidth = 0.18f;
        detailNameText.outlineColor = new Color(0.07f, 0.05f, 0.03f, 0.8f);
        detailNameText.raycastTarget = false;
        SetAnchors(detailNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(156f, -20f), new Vector2(-20f, -60f));

        detailMetaText = EnsureText(detailPanel, "DetailMeta");
        detailMetaText.alignment = TextAlignmentOptions.TopLeft;
        detailMetaText.enableAutoSizing = true;
        detailMetaText.fontSizeMin = 12f;
        detailMetaText.fontSizeMax = 18f;
        detailMetaText.fontStyle = FontStyles.Bold;
        detailMetaText.color = new Color(0.82f, 0.93f, 0.92f, 1f);
        detailMetaText.outlineWidth = 0.1f;
        detailMetaText.outlineColor = new Color(0.04f, 0.05f, 0.06f, 0.72f);
        detailMetaText.raycastTarget = false;
        SetAnchors(detailMetaText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(156f, -62f), new Vector2(-20f, -98f));

        detailStatsText = EnsureText(detailPanel, "DetailStats");
        detailStatsText.alignment = TextAlignmentOptions.TopLeft;
        detailStatsText.enableAutoSizing = true;
        detailStatsText.fontSizeMin = 12f;
        detailStatsText.fontSizeMax = 18f;
        detailStatsText.fontStyle = FontStyles.Bold;
        detailStatsText.color = new Color(0.9f, 0.94f, 0.96f, 1f);
        detailStatsText.outlineWidth = 0.11f;
        detailStatsText.outlineColor = new Color(0.04f, 0.05f, 0.06f, 0.72f);
        detailStatsText.textWrappingMode = TextWrappingModes.Normal;
        detailStatsText.raycastTarget = false;
        SetAnchors(detailStatsText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -156f), new Vector2(-20f, -286f));

        detailDescriptionText = EnsureText(detailPanel, "DetailDescription");
        detailDescriptionText.alignment = TextAlignmentOptions.TopLeft;
        detailDescriptionText.enableAutoSizing = true;
        detailDescriptionText.fontSizeMin = 14f;
        detailDescriptionText.fontSizeMax = 22f;
        detailDescriptionText.fontStyle = FontStyles.Normal;
        detailDescriptionText.color = new Color(0.95f, 0.95f, 0.92f, 1f);
        detailDescriptionText.outlineWidth = 0.08f;
        detailDescriptionText.outlineColor = new Color(0.03f, 0.03f, 0.03f, 0.6f);
        detailDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        detailDescriptionText.overflowMode = TextOverflowModes.Overflow;
        detailDescriptionText.raycastTarget = false;
        SetAnchors(detailDescriptionText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 20f), new Vector2(-20f, -298f));

        ApplyTopButtonStyle();
        RefreshOverlayLayout(force: true);

        if (!overlayInitialized)
        {
            overlayInitialized = true;
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.interactable = false;
            overlayCanvasGroup.blocksRaycasts = false;
            overlayRoot.gameObject.SetActive(false);
        }
    }

    private static bool WasCloseRequestedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return true;
#endif

        return false;
    }

    private void ApplyTopButtonStyle()
    {
        StyleTopButton(orbsTabButton, new Color(0.18f, 0.26f, 0.34f, 1f), new Color(0.24f, 0.35f, 0.46f, 1f), new Color(0.14f, 0.2f, 0.27f, 1f));
        StyleTopButton(relicsTabButton, new Color(0.26f, 0.21f, 0.15f, 1f), new Color(0.35f, 0.29f, 0.2f, 1f), new Color(0.2f, 0.16f, 0.12f, 1f));
        RefreshTabStyles();
    }

    private void StyleTopButton(Button button, Color normalColor, Color highlightedColor, Color pressedColor)
    {
        if (button == null)
            return;

        UIArtUtility.ApplyButtonStyle(
            button,
            normalColor,
            highlightedColor,
            pressedColor,
            new Color(0.24f, 0.24f, 0.24f, 0.5f),
            true,
            Image.Type.Sliced,
            UIArtUtility.BuiltinPanelSprite,
            0.08f);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 20f;
            label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            label.color = new Color(0.98f, 0.95f, 0.86f, 1f);
            label.outlineWidth = 0.14f;
            label.outlineColor = new Color(0.06f, 0.05f, 0.04f, 0.72f);
            label.raycastTarget = false;
            StretchRect(label.rectTransform, new Vector2(10f, 6f), new Vector2(-10f, -6f));
        }

        UIButtonMotion.Attach(button.transform as RectTransform, 1.02f, 0.975f, 0.1f);
    }

    private void OnInventoryButtonPressed()
    {
        ResolveReferences();
        flow?.ToggleInventory();
    }

    private void OnClosePressed()
    {
        ResolveReferences();
        flow?.CloseInventory();
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Inventory)
            ShowOverlay();
        else
            CloseOverlayImmediate();
    }

    private void SyncState()
    {
        if (flow != null && flow.State == GameState.Inventory)
            ShowOverlay();
        else
            CloseOverlayImmediate();
    }

    private void ShowOverlay()
    {
        EnsureRuntimeUi();
        if (overlayRoot == null)
            return;

        overlayRoot.SetAsLastSibling();
        overlayRoot.gameObject.SetActive(true);
        overlayCanvasGroup.alpha = 1f;
        overlayCanvasGroup.interactable = true;
        overlayCanvasGroup.blocksRaycasts = true;
        RebuildEntries(selectPreferred: true);
    }

    private void CloseOverlayImmediate()
    {
        if (overlayRoot == null || overlayCanvasGroup == null)
            return;

        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = false;
        overlayRoot.gameObject.SetActive(false);
    }

    private void RefreshButtonPlacement(bool force = false)
    {
        if (boundCanvas == null || inventoryButtonRoot == null)
            return;

        RectTransform canvasRect = boundCanvas.transform as RectTransform;
        Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
        if (!force && canvasSize == lastCanvasSize)
            return;

        lastCanvasSize = canvasSize;

        RectTransform pauseRoot = FindDescendant(hudRoot != null ? hudRoot : boundCanvas.transform, "PauseButton") as RectTransform;
        RectTransform targetParent = pauseRoot != null
            ? pauseRoot.parent as RectTransform
            : (hudRoot != null ? hudRoot : boundCanvas.transform as RectTransform);
        if (targetParent == null)
            return;

        inventoryButtonRoot.SetParent(targetParent, false);
        inventoryButtonRoot.anchorMin = new Vector2(1f, 1f);
        inventoryButtonRoot.anchorMax = new Vector2(1f, 1f);
        inventoryButtonRoot.pivot = new Vector2(1f, 1f);
        inventoryButtonRoot.sizeDelta = new Vector2(96f, 52f);

        if (pauseRoot != null)
        {
            inventoryButtonRoot.anchoredPosition = new Vector2(
                pauseRoot.anchoredPosition.x - pauseRoot.sizeDelta.x - 12f,
                pauseRoot.anchoredPosition.y);
        }
        else
        {
            inventoryButtonRoot.anchoredPosition = new Vector2(-164f, -18f);
        }

        inventoryButtonRoot.SetSiblingIndex(Mathf.Max(0, targetParent.childCount - 1));
    }

    private void RefreshOverlayLayout(bool force = false)
    {
        if (boundCanvas == null || overlayRoot == null || cardRoot == null)
            return;

        RectTransform canvasRect = boundCanvas.transform as RectTransform;
        Vector2 canvasSize = canvasRect != null && canvasRect.rect.size.sqrMagnitude > 0f
            ? canvasRect.rect.size
            : new Vector2(Screen.width, Screen.height);

        if (!force && canvasSize == lastOverlayCanvasSize)
            return;

        lastOverlayCanvasSize = canvasSize;

        float width = Mathf.Max(320f, canvasSize.x);
        float height = Mathf.Max(240f, canvasSize.y);
        bool compactLayout = width < 980f || width < height * 1.45f;
        float outerMargin = compactLayout ? 14f : Mathf.Clamp(width * 0.024f, 20f, 42f);
        float cardBottomMargin = compactLayout ? 14f : 20f;

        cardRoot.anchorMin = Vector2.zero;
        cardRoot.anchorMax = Vector2.one;
        cardRoot.pivot = new Vector2(0.5f, 0.5f);
        cardRoot.offsetMin = new Vector2(outerMargin, cardBottomMargin);
        cardRoot.offsetMax = new Vector2(-outerMargin, -outerMargin);
        cardRoot.anchoredPosition = Vector2.zero;

        float headerTop = compactLayout ? 14f : 18f;
        float closeWidth = compactLayout ? 94f : 122f;
        float closeHeight = compactLayout ? 34f : 46f;
        float titleRightReserve = closeWidth + (compactLayout ? 28f : 54f);
        float tabsTop = compactLayout ? 58f : 84f;
        float tabHeight = compactLayout ? 38f : 48f;
        float sectionTopInset = compactLayout ? 108f : 148f;
        float sectionOuterInset = compactLayout ? 16f : 28f;

        TMP_Text title = FindDescendant(cardRoot, "Title")?.GetComponent<TMP_Text>();
        if (title != null)
            SetTopStretch(title.rectTransform, 24f, headerTop, titleRightReserve, headerTop + 40f);

        if (closeButton != null)
            SetTopRight((RectTransform)closeButton.transform, 18f, headerTop, closeWidth, closeHeight);

        if (compactLayout)
        {
            float totalTabsWidth = Mathf.Max(180f, cardRoot.rect.width - 32f);
            float tabGap = 10f;
            float tabWidth = Mathf.Max(82f, (totalTabsWidth - tabGap) * 0.5f);
            float tabLeft = Mathf.Max(16f, (cardRoot.rect.width - (tabWidth * 2f + tabGap)) * 0.5f);
            SetTopLeft((RectTransform)orbsTabButton.transform, tabLeft, tabsTop, tabWidth, tabHeight);
            SetTopLeft((RectTransform)relicsTabButton.transform, tabLeft + tabWidth + tabGap, tabsTop, tabWidth, tabHeight);
        }
        else
        {
            float tabGap = 12f;
            float tabWidth = Mathf.Clamp((cardRoot.rect.width - 56f - tabGap) * 0.5f, 148f, 216f);
            SetTopLeft((RectTransform)orbsTabButton.transform, 28f, tabsTop, tabWidth, tabHeight);
            SetTopLeft((RectTransform)relicsTabButton.transform, 28f + tabWidth + tabGap, tabsTop, tabWidth, tabHeight);
        }

        RectTransform listPanel = FindDescendant(cardRoot, "ListPanel") as RectTransform;
        RectTransform detailPanel = FindDescendant(cardRoot, "DetailPanel") as RectTransform;
        if (listPanel != null && detailPanel != null)
        {
            if (compactLayout)
            {
                listPanel.anchorMin = new Vector2(0f, 0.5f);
                listPanel.anchorMax = new Vector2(1f, 1f);
                listPanel.offsetMin = new Vector2(sectionOuterInset, 10f);
                listPanel.offsetMax = new Vector2(-sectionOuterInset, -sectionTopInset);

                detailPanel.anchorMin = new Vector2(0f, 0f);
                detailPanel.anchorMax = new Vector2(1f, 0.48f);
                detailPanel.offsetMin = new Vector2(sectionOuterInset, sectionOuterInset);
                detailPanel.offsetMax = new Vector2(-sectionOuterInset, -6f);
            }
            else
            {
                listPanel.anchorMin = new Vector2(0f, 0f);
                listPanel.anchorMax = new Vector2(0.4f, 1f);
                listPanel.offsetMin = new Vector2(sectionOuterInset, sectionOuterInset);
                listPanel.offsetMax = new Vector2(-12f, -sectionTopInset);

                detailPanel.anchorMin = new Vector2(0.42f, 0f);
                detailPanel.anchorMax = new Vector2(1f, 1f);
                detailPanel.offsetMin = new Vector2(12f, sectionOuterInset);
                detailPanel.offsetMax = new Vector2(-sectionOuterInset, -sectionTopInset);
            }
        }

        if (listTitleText != null)
            SetTopStretch(listTitleText.rectTransform, 18f, 16f, 18f, 48f);

        RectTransform scrollRoot = itemListScrollRect != null ? itemListScrollRect.transform as RectTransform : null;
        if (scrollRoot != null)
            SetFill(scrollRoot, 14f, 14f, 14f, 56f);

        RectTransform iconFrame = detailIconImage != null ? detailIconImage.transform.parent as RectTransform : null;
        if (iconFrame != null)
        {
            float iconSize = compactLayout ? 78f : 120f;
            SetTopLeft(iconFrame, 20f, 20f, iconSize, iconSize);
        }

        if (detailIconImage != null)
            SetFill(detailIconImage.rectTransform, 12f, 12f, 12f, 12f);

        float detailTextLeft = compactLayout ? 112f : 156f;
        float detailStatsTop = compactLayout ? 112f : 156f;
        float detailStatsBottom = compactLayout ? 206f : 286f;
        float detailDescriptionTop = detailStatsBottom + 14f;

        if (detailNameText != null)
            SetTopStretch(detailNameText.rectTransform, detailTextLeft, 20f, 20f, compactLayout ? 52f : 60f);

        if (detailMetaText != null)
            SetTopStretch(detailMetaText.rectTransform, detailTextLeft, compactLayout ? 54f : 62f, 20f, compactLayout ? 88f : 98f);

        if (detailStatsText != null)
            SetTopStretch(detailStatsText.rectTransform, 20f, detailStatsTop, 20f, detailStatsBottom);

        if (detailDescriptionText != null)
            SetFill(detailDescriptionText.rectTransform, 20f, 20f, 20f, detailDescriptionTop);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRoot);
    }

    private void RefreshButtonAvailability()
    {
        if (inventoryButtonRoot == null || inventoryButton == null || flow == null)
            return;

        bool canShow = flow.State == GameState.Combat || flow.State == GameState.Inventory;
        inventoryButtonRoot.gameObject.SetActive(canShow);
        inventoryButton.interactable = canShow;
    }

    private void SetTab(InventoryTab tab)
    {
        if (activeTab == tab && selectedIndex >= 0)
            return;

        activeTab = tab;
        selectedIndex = -1;
        RefreshTabStyles();
        RebuildEntries(selectPreferred: true);
    }

    private void RefreshTabStyles()
    {
        StyleTabSelection(orbsTabButton, activeTab == InventoryTab.Orbs);
        StyleTabSelection(relicsTabButton, activeTab == InventoryTab.Relics);
    }

    private static void StyleTabSelection(Button button, bool selected)
    {
        if (button == null)
            return;

        ColorBlock colors = button.colors;
        if (selected)
        {
            colors.normalColor = new Color(0.35f, 0.44f, 0.58f, 1f);
            colors.highlightedColor = new Color(0.43f, 0.53f, 0.68f, 1f);
            colors.pressedColor = new Color(0.28f, 0.36f, 0.48f, 1f);
        }
        else
        {
            colors.normalColor = new Color(0.18f, 0.19f, 0.23f, 1f);
            colors.highlightedColor = new Color(0.26f, 0.28f, 0.34f, 1f);
            colors.pressedColor = new Color(0.15f, 0.17f, 0.21f, 1f);
        }

        button.colors = colors;
    }

    private void RebuildEntries(bool selectPreferred)
    {
        ResolveReferences();
        EnsureRuntimeUi();

        ClearEntryButtons();
        RebuildEntryCollections();

        if (listTitleText != null)
        {
            listTitleText.text = activeTab == InventoryTab.Orbs
                ? $"Orbes ({orbEntries.Count})"
                : $"Reliquias ({relicEntries.Count})";
        }

        int entryCount = activeTab == InventoryTab.Orbs ? orbEntries.Count : relicEntries.Count;
        bool hasEntries = entryCount > 0;
        if (emptyListText != null)
            emptyListText.gameObject.SetActive(!hasEntries);

        if (!hasEntries)
        {
            selectedIndex = -1;
            ApplyEmptyDetail();
            return;
        }

        if (selectPreferred)
            SelectPreferredIndex();
        else
            selectedIndex = Mathf.Clamp(selectedIndex, 0, entryCount - 1);

        if (activeTab == InventoryTab.Orbs)
        {
            for (int i = 0; i < orbEntries.Count; i++)
                CreateOrbEntryButton(i, orbEntries[i]);

            ApplyOrbDetail(orbEntries[Mathf.Clamp(selectedIndex, 0, orbEntries.Count - 1)]);
        }
        else
        {
            for (int i = 0; i < relicEntries.Count; i++)
                CreateRelicEntryButton(i, relicEntries[i]);

            ApplyRelicDetail(relicEntries[Mathf.Clamp(selectedIndex, 0, relicEntries.Count - 1)]);
        }

        Canvas.ForceUpdateCanvases();
        if (itemListScrollRect != null)
            itemListScrollRect.verticalNormalizedPosition = 1f;
    }

    private void RebuildEntryCollections()
    {
        orbEntries.Clear();
        relicEntries.Clear();

        if (orbManager != null)
        {
            IReadOnlyList<OrbInstance> ownedOrbs = orbManager.OwnedOrbInstances;
            if (ownedOrbs != null)
            {
                for (int i = 0; i < ownedOrbs.Count; i++)
                {
                    OrbInstance orb = ownedOrbs[i];
                    if (orb != null)
                        orbEntries.Add(orb);
                }
            }
        }

        if (relicManager != null)
        {
            IReadOnlyList<ShotEffectBase> activeRelics = relicManager.ActiveRelics;
            if (activeRelics != null)
            {
                for (int i = 0; i < activeRelics.Count; i++)
                {
                    ShotEffectBase relic = activeRelics[i];
                    if (relic != null)
                        relicEntries.Add(relic);
                }
            }
        }
    }

    private void SelectPreferredIndex()
    {
        if (activeTab == InventoryTab.Orbs)
        {
            OrbInstance current = orbManager != null ? orbManager.CurrentOrb : null;
            selectedIndex = current != null ? orbEntries.IndexOf(current) : -1;
            if (selectedIndex < 0)
                selectedIndex = orbEntries.Count > 0 ? 0 : -1;
            return;
        }

        selectedIndex = relicEntries.Count > 0 ? 0 : -1;
    }

    private void CreateOrbEntryButton(int index, OrbInstance orb)
    {
        if (itemListContent == null || orb == null)
            return;

        Button button = CreateEntryButton($"OrbEntry_{index}", index == selectedIndex);
        button.onClick.AddListener(() =>
        {
            selectedIndex = index;
            RebuildEntries(selectPreferred: false);
        });

        RectTransform buttonRect = button.transform as RectTransform;
        LayoutElement layout = EnsureComponent<LayoutElement>(button.gameObject);
        layout.preferredHeight = 90f;
        layout.minHeight = 90f;
        buttonRect.SetParent(itemListContent, false);

        RectTransform iconFrame = EnsurePanel(buttonRect, "IconFrame", new Color(0.16f, 0.17f, 0.18f, 0.96f));
        SetAnchors(iconFrame, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -26f), new Vector2(62f, 26f));

        RectTransform iconRect = EnsureRect(iconFrame, "Icon");
        StretchRect(iconRect, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        Image iconImage = EnsureComponent<Image>(iconRect.gameObject);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        ApplyIcon(iconImage, orb.Icon, orb.Color);

        TMP_Text name = EnsureText(buttonRect, "Name", orb.OrbName);
        name.alignment = TextAlignmentOptions.TopLeft;
        name.enableAutoSizing = true;
        name.fontSizeMin = 14f;
        name.fontSizeMax = 22f;
        name.fontStyle = FontStyles.Bold;
        name.color = new Color(0.99f, 0.95f, 0.86f, 1f);
        name.outlineWidth = 0.12f;
        name.outlineColor = new Color(0.06f, 0.05f, 0.04f, 0.72f);
        name.raycastTarget = false;
        SetAnchors(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(74f, -10f), new Vector2(-110f, -38f));

        TMP_Text sub = EnsureText(buttonRect, "Sub", $"Lv {orb.Level}  |  DMG {orb.DamagePerHit}");
        sub.alignment = TextAlignmentOptions.BottomLeft;
        sub.enableAutoSizing = true;
        sub.fontSizeMin = 11f;
        sub.fontSizeMax = 16f;
        sub.fontStyle = FontStyles.Bold;
        sub.color = new Color(0.82f, 0.92f, 0.91f, 1f);
        sub.outlineWidth = 0.08f;
        sub.outlineColor = new Color(0.04f, 0.05f, 0.05f, 0.6f);
        sub.raycastTarget = false;
        SetAnchors(sub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(74f, 12f), new Vector2(-110f, 36f));

        bool equipped = orbManager != null && orbManager.CurrentOrb == orb;
        TMP_Text badge = EnsureText(buttonRect, "Badge", equipped ? "ACTIVO" : $"LV {orb.Level}");
        badge.alignment = TextAlignmentOptions.Center;
        badge.enableAutoSizing = true;
        badge.fontSizeMin = 10f;
        badge.fontSizeMax = 14f;
        badge.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        badge.color = equipped
            ? new Color(0.96f, 1f, 0.88f, 1f)
            : new Color(0.88f, 0.92f, 1f, 1f);
        badge.outlineWidth = 0.1f;
        badge.outlineColor = new Color(0.04f, 0.05f, 0.06f, 0.68f);
        badge.raycastTarget = false;
        SetAnchors(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-96f, -16f), new Vector2(-12f, 16f));

        entryButtons.Add(button);
    }

    private void CreateRelicEntryButton(int index, ShotEffectBase relic)
    {
        if (itemListContent == null || relic == null)
            return;

        Button button = CreateEntryButton($"RelicEntry_{index}", index == selectedIndex);
        button.onClick.AddListener(() =>
        {
            selectedIndex = index;
            RebuildEntries(selectPreferred: false);
        });

        RectTransform buttonRect = button.transform as RectTransform;
        LayoutElement layout = EnsureComponent<LayoutElement>(button.gameObject);
        layout.preferredHeight = 90f;
        layout.minHeight = 90f;
        buttonRect.SetParent(itemListContent, false);

        RectTransform iconFrame = EnsurePanel(buttonRect, "IconFrame", new Color(0.19f, 0.15f, 0.12f, 0.96f));
        SetAnchors(iconFrame, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -26f), new Vector2(62f, 26f));

        RectTransform iconRect = EnsureRect(iconFrame, "Icon");
        StretchRect(iconRect, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        Image iconImage = EnsureComponent<Image>(iconRect.gameObject);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        ApplyIcon(iconImage, relic.Icon, new Color(0.96f, 0.8f, 0.42f, 1f));

        TMP_Text name = EnsureText(buttonRect, "Name", relic.DisplayName);
        name.alignment = TextAlignmentOptions.TopLeft;
        name.enableAutoSizing = true;
        name.fontSizeMin = 14f;
        name.fontSizeMax = 22f;
        name.fontStyle = FontStyles.Bold;
        name.color = new Color(0.99f, 0.95f, 0.86f, 1f);
        name.outlineWidth = 0.12f;
        name.outlineColor = new Color(0.06f, 0.05f, 0.04f, 0.72f);
        name.raycastTarget = false;
        SetAnchors(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(74f, -10f), new Vector2(-110f, -38f));

        TMP_Text sub = EnsureText(buttonRect, "Sub", "Pasiva activa");
        sub.alignment = TextAlignmentOptions.BottomLeft;
        sub.enableAutoSizing = true;
        sub.fontSizeMin = 11f;
        sub.fontSizeMax = 16f;
        sub.fontStyle = FontStyles.Bold;
        sub.color = new Color(0.95f, 0.86f, 0.68f, 1f);
        sub.outlineWidth = 0.08f;
        sub.outlineColor = new Color(0.04f, 0.05f, 0.05f, 0.6f);
        sub.raycastTarget = false;
        SetAnchors(sub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(74f, 12f), new Vector2(-110f, 36f));

        TMP_Text badge = EnsureText(buttonRect, "Badge", "ACTIVA");
        badge.alignment = TextAlignmentOptions.Center;
        badge.enableAutoSizing = true;
        badge.fontSizeMin = 10f;
        badge.fontSizeMax = 14f;
        badge.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        badge.color = new Color(1f, 0.96f, 0.86f, 1f);
        badge.outlineWidth = 0.1f;
        badge.outlineColor = new Color(0.04f, 0.05f, 0.06f, 0.68f);
        badge.raycastTarget = false;
        SetAnchors(badge.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-104f, -16f), new Vector2(-12f, 16f));

        entryButtons.Add(button);
    }

    private Button CreateEntryButton(string name, bool selected)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(itemListContent, false);

        Button button = buttonObject.GetComponent<Button>();
        Color normalColor = selected
            ? new Color(0.25f, 0.35f, 0.5f, 1f)
            : new Color(0.14f, 0.15f, 0.17f, 1f);
        Color highlightedColor = selected
            ? new Color(0.31f, 0.42f, 0.6f, 1f)
            : new Color(0.2f, 0.22f, 0.27f, 1f);
        Color pressedColor = selected
            ? new Color(0.2f, 0.28f, 0.42f, 1f)
            : new Color(0.12f, 0.13f, 0.16f, 1f);

        UIArtUtility.ApplyButtonStyle(
            button,
            normalColor,
            highlightedColor,
            pressedColor,
            new Color(0.22f, 0.22f, 0.22f, 0.55f),
            true,
            Image.Type.Sliced,
            UIArtUtility.BuiltinPanelSprite,
            0.08f);

        Shadow shadow = EnsureComponent<Shadow>(buttonObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;

        UIButtonMotion.Attach(rect, 1.015f, 0.985f, 0.08f);
        return button;
    }

    private void ApplyOrbDetail(OrbInstance orb)
    {
        if (orb == null)
        {
            ApplyEmptyDetail();
            return;
        }

        bool equipped = orbManager != null && orbManager.CurrentOrb == orb;
        ApplyIcon(detailIconImage, orb.Icon, orb.Color);

        detailNameText.text = orb.OrbName;
        detailMetaText.text = equipped ? "Equipado ahora" : "Guardado en la bolsa";
        detailStatsText.text = BuildOrbStats(orb, equipped);
        detailDescriptionText.text = BuildOrbDescription(orb);
    }

    private void ApplyRelicDetail(ShotEffectBase relic)
    {
        if (relic == null)
        {
            ApplyEmptyDetail();
            return;
        }

        ApplyIcon(detailIconImage, relic.Icon, new Color(0.96f, 0.8f, 0.42f, 1f));

        detailNameText.text = relic.DisplayName;
        detailMetaText.text = "Reliquia pasiva activa";
        detailStatsText.text = "Estado: activa en esta run";
        detailDescriptionText.text = BuildRelicDescription(relic);
    }

    private void ApplyEmptyDetail()
    {
        ApplyIcon(detailIconImage, null, new Color(0.3f, 0.3f, 0.32f, 1f));
        detailNameText.text = activeTab == InventoryTab.Orbs ? "Sin orbes" : "Sin reliquias";
        detailMetaText.text = "Nada seleccionado";
        detailStatsText.text = string.Empty;
        detailDescriptionText.text = activeTab == InventoryTab.Orbs
            ? "Todavia no tenes orbes cargados para esta run."
            : "Todavia no conseguiste reliquias en esta run.";
    }

    private string BuildOrbStats(OrbInstance orb, bool equipped)
    {
        if (orb == null)
            return string.Empty;

        string levelText = orb.BaseData != null && orb.BaseData.maxLevel > 0
            ? $"{orb.Level}/{orb.BaseData.maxLevel}"
            : orb.Level.ToString();

        string effects = BuildEffectSummary(orb.OrbEffects);
        string stats = $"Estado: {(equipped ? "equipado" : "guardado")}\n"
            + $"Nivel: {levelText}\n"
            + $"Danio por golpe: {orb.DamagePerHit}\n"
            + $"Rebote: {orb.Bounciness:0.##}\n"
            + $"Arrastre: {orb.LinearDrag:0.##}";

        if (!string.IsNullOrWhiteSpace(effects))
            stats += $"\nEfectos: {effects}";

        return stats;
    }

    private string BuildOrbDescription(OrbInstance orb)
    {
        if (orb == null)
            return string.Empty;

        string description = string.IsNullOrWhiteSpace(orb.Description)
            ? "Sin descripcion cargada."
            : orb.Description.Trim();

        string effects = BuildEffectSummary(orb.OrbEffects);
        if (!string.IsNullOrWhiteSpace(effects))
            description += $"\n\nActiva: {effects}.";

        return description;
    }

    private static string BuildRelicDescription(ShotEffectBase relic)
    {
        if (relic == null)
            return string.Empty;

        string description = string.IsNullOrWhiteSpace(relic.Description)
            ? "Sin descripcion cargada. La pasiva queda activa durante toda la run."
            : relic.Description.Trim();

        return $"{description}\n\nPasiva activa.";
    }

    private static string BuildEffectSummary(IReadOnlyList<ShotEffectBase> effects)
    {
        if (effects == null || effects.Count == 0)
            return string.Empty;

        var names = new List<string>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            ShotEffectBase effect = effects[i];
            if (effect == null)
                continue;

            string label = string.IsNullOrWhiteSpace(effect.DisplayName) ? effect.name : effect.DisplayName;
            if (!string.IsNullOrWhiteSpace(label))
                names.Add(label.Trim());
        }

        return names.Count == 0 ? string.Empty : string.Join(", ", names);
    }

    private void ClearEntryButtons()
    {
        for (int i = 0; i < entryButtons.Count; i++)
        {
            if (entryButtons[i] != null)
                Destroy(entryButtons[i].gameObject);
        }

        entryButtons.Clear();
    }

    private static void ApplyIcon(Image image, Sprite sprite, Color fallbackTint)
    {
        if (image == null)
            return;

        image.sprite = sprite != null ? sprite : UIArtUtility.BuiltinPanelSprite;
        image.type = sprite != null ? Image.Type.Simple : Image.Type.Sliced;
        image.color = sprite != null ? Color.white : fallbackTint;
    }

    private static RectTransform EnsurePanel(Transform parent, string name, Color color)
    {
        RectTransform rect = EnsureRect(parent, name);
        Image image = EnsureComponent<Image>(rect.gameObject);
        UIArtUtility.ApplyImageStyle(image, color, true, Image.Type.Sliced, UIArtUtility.BuiltinPanelSprite);

        Shadow shadow = EnsureComponent<Shadow>(rect.gameObject);
        shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);
        shadow.effectDistance = new Vector2(0f, -6f);
        shadow.useGraphicAlpha = true;
        return rect;
    }

    private static RectTransform EnsureRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        RectTransform rect = existing as RectTransform;
        if (rect != null)
            return rect;

        GameObject go = new GameObject(name, typeof(RectTransform));
        rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private static TMP_Text EnsureText(Transform parent, string name)
    {
        return EnsureText(parent, name, null);
    }

    private static TMP_Text EnsureText(Transform parent, string name, string textValue)
    {
        Transform existing = parent.Find(name);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        if (textValue != null)
            text.text = textValue;

        return text;
    }

    private static Button EnsureButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = parent.Find(name);
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        if (button == null)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(parent, false);
            button = buttonObject.GetComponent<Button>();
        }

        EnsureText(button.transform, "Label", label);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        return button;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void StretchRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetFill(RectTransform rect, float left, float bottom, float right, float top)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetTopStretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(left, -bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.pivot = new Vector2(0.5f, 1f);
    }

    private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(left, -(top + height));
        rect.offsetMax = new Vector2(left + width, -top);
    }

    private static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(-(right + width), -(top + height));
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
            component = gameObject.AddComponent<T>();

        return component;
    }

    private static Transform FindDescendant(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrWhiteSpace(name))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
                return child;

            Transform nested = FindDescendant(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
