using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopScene : MonoBehaviour, IMapShopView
{
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

        GameObject root = new GameObject("ShopScene");
        ShopScene created = root.AddComponent<ShopScene>();
        created.BuildRuntimeUi();
        return created;
    }

    public void ShowShop(OpenParams openParams)
    {
        if (openParams == null)
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
            button.GetComponentInChildren<TMP_Text>().text = BuildShortLabel(offer);
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
        buyButton.interactable = enabled;
        if (!enabled && !string.IsNullOrWhiteSpace(reason))
            detailLabel.text += $"\n\n{reason}";
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
            && refreshesUsed < shopConfig.MaxRefreshesPerVisit;
        refreshButton.interactable = canRefresh;
    }

    private static string BuildShortLabel(ShopService.ShopOfferData offer)
    {
        return $"[{offer.Rarity}] {offer.Type} - {offer.Cost}g";
    }

    private static string BuildDetail(ShopService.ShopOfferData offer)
    {
        return $"{offer.Type}\nValor: {offer.PrimaryValue}\nStock: {offer.Stock}";
    }

    private void Awake()
    {
        if (titleLabel == null)
            BuildRuntimeUi();

        gameObject.SetActive(false);
    }

    private void BuildRuntimeUi()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.AddComponent<GraphicRaycaster>();
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        GameObject panel = CreateUi("Panel", transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1200f, 700f);

        titleLabel = CreateText("Title", panel.transform, new Vector2(0f, 300f), 42);
        coinLabel = CreateText("Coins", panel.transform, new Vector2(-430f, 245f), 28);
        stockLabel = CreateText("Stock", panel.transform, new Vector2(-430f, 205f), 24);

        itemSlotsRoot = CreateUi("Items", panel.transform).transform;
        RectTransform listRect = ((GameObject)itemSlotsRoot.gameObject).GetComponent<RectTransform>();
        listRect.anchoredPosition = new Vector2(-330f, -20f);
        listRect.sizeDelta = new Vector2(460f, 460f);
        VerticalLayoutGroup vlg = itemSlotsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        itemButtonTemplate = CreateButton("ItemTemplate", itemSlotsRoot, "Item");
        itemButtonTemplate.gameObject.SetActive(false);

        detailLabel = CreateText("Detail", panel.transform, new Vector2(260f, 120f), 24);
        priceLabel = CreateText("Price", panel.transform, new Vector2(260f, -40f), 24);
        rarityLabel = CreateText("Rarity", panel.transform, new Vector2(260f, -90f), 24);

        buyButton = CreateButton("BuyButton", panel.transform, "Comprar");
        SetPos(buyButton.GetComponent<RectTransform>(), new Vector2(260f, -210f), new Vector2(240f, 60f));

        refreshButton = CreateButton("RefreshButton", panel.transform, "Refresh");
        SetPos(refreshButton.GetComponent<RectTransform>(), new Vector2(10f, -290f), new Vector2(240f, 60f));

        exitButton = CreateButton("ExitButton", panel.transform, "Salir");
        SetPos(exitButton.GetComponent<RectTransform>(), new Vector2(510f, -290f), new Vector2(240f, 60f));
    }

    private static GameObject CreateUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TMP_Text CreateText(string name, Transform parent, Vector2 anchoredPos, int size)
    {
        GameObject go = CreateUi(name, parent);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        SetPos(go.GetComponent<RectTransform>(), anchoredPos, new Vector2(460f, 50f));
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        GameObject go = CreateUi(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.2f, 0.3f, 0.45f, 1f);
        Button button = go.AddComponent<Button>();
        SetPos(go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(430f, 56f));

        TMP_Text text = CreateText("Label", go.transform, Vector2.zero, 22);
        text.text = label;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private static void SetPos(RectTransform rect, Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
    }
}