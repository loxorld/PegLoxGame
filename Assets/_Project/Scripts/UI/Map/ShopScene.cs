using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopScene : MonoBehaviour, IMapShopView
{
    private const string RuntimeRootName = "ShopSceneRuntime";

    public sealed class OpenParams
    {
        public MapDomainService.ShopOutcome ShopOutcome;
        public ShopConfig Config;
        public ShopService Service;
        public GameFlowManager Flow;
        public OrbManager OrbManager;
        public RunBalanceConfig Balance;
        public int StageIndex;
        public string ShopId;
        public Action<string> OnRefreshMessage;
        public Action OnExit;
    }

    [Header("Shop Config")]
    [SerializeField] private ShopConfig shopConfig;
    [SerializeField, Min(0)] private int fallbackHealCost = 10;
    [SerializeField, Min(1)] private int fallbackHealAmount = 4;
    [SerializeField, Min(0)] private int fallbackUpgradeCost = 15;

    [Header("Scene Wiring")]
    [SerializeField] private bool preferSceneLayout = true;

    [Header("Nodes")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text coinLabel;
    [SerializeField] private TMP_Text stockLabel;
    [SerializeField] private Transform itemSlotsRoot;
    [SerializeField] private Button itemButtonTemplate;
    [SerializeField] private TMP_Text detailLabel;
    [SerializeField] private TMP_Text priceLabel;
    [SerializeField] private TMP_Text rarityLabel;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button exitButton;

    private readonly List<Button> spawnedButtons = new List<Button>();

    private OpenParams current;
    private List<ShopService.ShopOfferData> activeCatalog = new List<ShopService.ShopOfferData>();
    private int selectedIndex = -1;
    private int refreshesUsed;

    public static ShopScene GetOrCreate()
    {
        ShopScene found = FindObjectOfType<ShopScene>(true);
        if (found != null)
            return found;

        GameObject root = new GameObject(RuntimeRootName);
        ShopScene runtimeShop = root.AddComponent<ShopScene>();
        runtimeShop.preferSceneLayout = false;
        runtimeShop.BuildRuntimeUiIfNeeded();

        Debug.LogWarning("[ShopScene] No se encontró ShopScene cableada en la escena. Se creó un fallback runtime.");
        return runtimeShop;
    }

    public void ShowShop(OpenParams openParams)
    {
        if (openParams == null)
            return;

        EnsureUiReady();
        if (!HasAllReferences())
            return;

        current = openParams;
        if (current.Config != null)
            shopConfig = current.Config;
        else if (shopConfig == null)
            shopConfig = Resources.Load<ShopConfig>("ShopConfig_Default");

        gameObject.SetActive(true);
        titleLabel.text = string.IsNullOrWhiteSpace(current.ShopOutcome.Title) ? "Tienda" : current.ShopOutcome.Title;
        refreshesUsed = 0;

        LoadCatalog(forceRefresh: false);
        BindStaticButtons();
    }

    private void BindStaticButtons()
    {
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(BuySelected);

        refreshButton.onClick.RemoveAllListeners();
        refreshButton.onClick.AddListener(RefreshCatalog);

        exitButton.onClick.RemoveAllListeners();
        exitButton.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
            current?.OnExit?.Invoke();
        });

        ApplyStandardButtonTextStyle(buyButton);
        ApplyStandardButtonTextStyle(refreshButton);
        ApplyStandardButtonTextStyle(exitButton);
    }

    private void LoadCatalog(bool forceRefresh)
    {
        if (current == null || current.Service == null)
            return;

        activeCatalog = current.Service.BuildOrLoadCatalog(
            current.Flow,
            shopConfig,
            current.Balance,
            current.StageIndex,
            current.ShopId,
            fallbackHealCost,
            fallbackHealAmount,
            fallbackUpgradeCost,
            forceRefresh);

        RebuildItemList();
        UpdateMetaLabels();
        SelectIndex(activeCatalog.Count > 0 ? 0 : -1);
    }

    private void RebuildItemList()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
            Destroy(spawnedButtons[i].gameObject);

        spawnedButtons.Clear();

        if (activeCatalog == null)
            return;

        for (int i = 0; i < activeCatalog.Count; i++)
        {
            int idx = i;
            ShopService.ShopOfferData offer = activeCatalog[i];
            Button button = Instantiate(itemButtonTemplate, itemSlotsRoot);
            button.gameObject.SetActive(true);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = BuildShortLabel(offer);
                label.color = GetRarityTextColor(offer.Rarity);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(8f, 0f, 8f, 0f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 24f;
            }

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
                buttonImage.color = GetRarityButtonColor(offer.Rarity);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectIndex(idx));
            spawnedButtons.Add(button);
        }
    }

    private void SelectIndex(int index)
    {
        selectedIndex = index;
        if (index < 0 || index >= activeCatalog.Count)
        {
            detailLabel.text = "Sin selección";
            priceLabel.text = string.Empty;
            rarityLabel.text = string.Empty;
            buyButton.interactable = false;
            return;
        }

        ShopService.ShopOfferData offer = activeCatalog[index];
        ShopService.PlayerShopState state = current.Service.BuildPlayerState(current.Flow, current.OrbManager);
        bool enabled = current.Service.IsOfferEnabled(state, offer, out string reason);

        detailLabel.text = BuildDetail(offer);
        priceLabel.text = $"Precio: {offer.Cost}";
        rarityLabel.text = $"Rareza: {offer.Rarity}";
        rarityLabel.color = GetRarityTextColor(offer.Rarity);
        buyButton.interactable = enabled;
        if (!enabled && !string.IsNullOrWhiteSpace(reason))
            detailLabel.text += $"\n\n{reason}";

        UpdateSelectionVisual();
    }

    private void BuySelected()
    {
        if (selectedIndex < 0 || selectedIndex >= activeCatalog.Count)
            return;

        ShopService.ShopOfferData offer = activeCatalog[selectedIndex];
        bool ok = current.Service.TryPurchaseOffer(current.Flow, current.OrbManager, current.ShopId, offer, out string result);
        current?.OnRefreshMessage?.Invoke(result);

        if (!ok)
        {
            SelectIndex(selectedIndex);
            UpdateMetaLabels();
            return;
        }

        LoadCatalog(forceRefresh: false);
    }

    private void RefreshCatalog()
    {
        if (shopConfig == null || !shopConfig.AllowManualRefresh)
        {
            current?.OnRefreshMessage?.Invoke("Refresh deshabilitado por configuración.");
            return;
        }

        if (refreshesUsed >= shopConfig.MaxRefreshesPerVisit)
        {
            current?.OnRefreshMessage?.Invoke("No quedan refreshes disponibles.");
            return;
        }

        if (current.Flow != null && !current.Flow.SpendCoins(shopConfig.RefreshCost))
        {
            current?.OnRefreshMessage?.Invoke("No alcanzan las monedas para refrescar.");
            return;
        }

        refreshesUsed++;
        LoadCatalog(forceRefresh: true);
        current?.OnRefreshMessage?.Invoke("Tienda refrescada.");
    }

    private void UpdateMetaLabels()
    {
        int coins = current?.Flow != null ? Mathf.Max(0, current.Flow.Coins) : 0;
        coinLabel.text = $"Monedas: {coins}";

        int stock = 0;
        for (int i = 0; i < activeCatalog.Count; i++)
            stock += Mathf.Max(0, activeCatalog[i].Stock);

        stockLabel.text = $"Stock total: {stock}";

        bool canRefresh = shopConfig != null
            && shopConfig.AllowManualRefresh
            && refreshesUsed < shopConfig.MaxRefreshesPerVisit
            && (current?.Flow == null || current.Flow.Coins >= shopConfig.RefreshCost);
        refreshButton.interactable = canRefresh;

        int refreshesRemaining = shopConfig != null ? Mathf.Max(0, shopConfig.MaxRefreshesPerVisit - refreshesUsed) : 0;
        if (refreshButton != null)
        {
            TMP_Text refreshText = refreshButton.GetComponentInChildren<TMP_Text>();
            if (refreshText != null)
            {
                int refreshCost = shopConfig != null ? Mathf.Max(0, shopConfig.RefreshCost) : 0;
                refreshText.text = $"Refrescar ({refreshCost}g) [{refreshesRemaining}]";
            }
        }

        ApplyStandardButtonTextStyle(refreshButton);
    }

    private static string BuildShortLabel(ShopService.ShopOfferData offer)
    {
        return $"{GetRarityPrefix(offer.Rarity)} {offer.Type} · {offer.Cost}g";
    }

    private static string BuildDetail(ShopService.ShopOfferData offer)
    {
        string description;
        switch (offer.Type)
        {
            case ShopService.ShopOfferType.Heal:
                description = $"Recupera {offer.PrimaryValue} HP del personaje.";
                break;
            case ShopService.ShopOfferType.OrbUpgrade:
                description = "Mejora un orbe aleatorio que todavía pueda subir de nivel.";
                break;
            case ShopService.ShopOfferType.OrbUpgradeDiscount:
                description = "Mejora un orbe con precio reducido.";
                break;
            case ShopService.ShopOfferType.CoinCache:
                description = $"Entrega {offer.PrimaryValue} monedas instantáneamente.";
                break;
            case ShopService.ShopOfferType.VitalityBoost:
                description = $"Aumenta HP máximo en {offer.PrimaryValue}.";
                break;
            default:
                description = "Oferta especial.";
                break;
        }

        return $"{description}\n\nValor: {offer.PrimaryValue}\nStock disponible: {offer.Stock}";
    }

    private void Awake()
    {
        EnsureUiReady();
        gameObject.SetActive(false);
    }

    private void EnsureUiReady()
    {
        if (!preferSceneLayout || !HasAllReferences())
            BuildRuntimeUiIfNeeded();
        if (HasAllReferences())
            return;

        Debug.LogError("[ShopScene] Falta cablear la escena de shop. Asigná todos los campos del componente ShopScene en ShopScene.unity.");
    }

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
        StretchRect(dimmer.GetComponent<RectTransform>());

        GameObject panel = CreateUiObject("Panel", dimmer.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.12f, 0.16f, 0.98f);
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
        detailLabel.enableWordWrapping = true;
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


    private bool HasAllReferences()
    {
        bool hasRefs = titleLabel != null
            && coinLabel != null
            && stockLabel != null
            && itemSlotsRoot != null
            && itemButtonTemplate != null
            && detailLabel != null
            && priceLabel != null
            && rarityLabel != null
            && buyButton != null
            && refreshButton != null
            && exitButton != null;

        return hasRefs;
    }

    private static Color GetRarityTextColor(ShopService.ShopOfferRarity rarity)
    {
        switch (rarity)
        {
            case ShopService.ShopOfferRarity.Common:
                return new Color(0.08f, 0.1f, 0.12f);
            case ShopService.ShopOfferRarity.Rare:
                return new Color(0.07f, 0.18f, 0.22f);
            case ShopService.ShopOfferRarity.Epic:
                return new Color(0.2f, 0.1f, 0.35f);
            case ShopService.ShopOfferRarity.Legendary:
                return new Color(0.25f, 0.2f, 0.02f);
            default:
                return new Color(0.08f, 0.1f, 0.12f);
        }
    }

    private static void ApplyStandardButtonTextStyle(Button button)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text == null)
            return;

        text.alignment = TextAlignmentOptions.Center;
        text.margin = Vector4.zero;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 24f;
    }

    private static Color GetRarityButtonColor(ShopService.ShopOfferRarity rarity)
    {
        switch (rarity)
        {
            case ShopService.ShopOfferRarity.Common:
                return new Color(0.9f, 0.9f, 0.9f); // Gris claro
            case ShopService.ShopOfferRarity.Rare:
                return new Color(0.7f, 0.95f, 1f); // Celeste claro
            case ShopService.ShopOfferRarity.Epic:
                return new Color(0.8f, 0.7f, 1f); // Violeta claro
            case ShopService.ShopOfferRarity.Legendary:
                return new Color(1f, 1f, 0.7f); // Amarillo claro
            default:
                return Color.white;
        }
    }

    // Agregar este método privado para resolver CS0103
    private static string GetRarityPrefix(ShopService.ShopOfferRarity rarity)
    {
        switch (rarity)
        {
            case ShopService.ShopOfferRarity.Common:
                return "[Común]";
            case ShopService.ShopOfferRarity.Rare:
                return "[Rara]";
            case ShopService.ShopOfferRarity.Epic:
                return "[Épica]";
            case ShopService.ShopOfferRarity.Legendary:
                return "[Legendaria]";
            default:
                return "[?]";
        }
    }

    // Agregar este método privado a la clase ShopScene para resolver CS0103
    private void UpdateSelectionVisual()
    {
        // Si deseas resaltar el botón seleccionado, puedes implementar aquí la lógica.
        // Por ejemplo, cambiar el color de fondo del botón seleccionado y restaurar los demás.
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            var button = spawnedButtons[i];
            var image = button.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                if (i == selectedIndex)
                    image.color = Color.green; // O el color que prefieras para selección
                else
                    image.color = GetRarityButtonColor(activeCatalog[i].Rarity);
            }
        }
    }
}