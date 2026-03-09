using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class ShopScene : MonoBehaviour, IMapShopView
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
        public Action<string> OnShopMessage;
        public Action OnRequestReopen;
        public Action OnExit;
    }

#pragma warning disable CS0649
    [Serializable]
    private struct ButtonArtTheme
    {
        public Sprite normalSprite;
        public Sprite highlightedSprite;
        public Sprite pressedSprite;
        public Sprite disabledSprite;
        public Color fallbackColor;
    }
#pragma warning restore CS0649

    private enum SelectionVisualMode
    {
        SelectableState,
        Outline
    }

    [Header("Shop Config")]
    [SerializeField] private ShopConfig shopConfig;
    [SerializeField, Min(0)] private int fallbackHealCost = 10;
    [SerializeField, Min(1)] private int fallbackHealAmount = 4;
    [SerializeField, Min(0)] private int fallbackUpgradeCost = 15;

    [Header("Scene Wiring")]
    [SerializeField] private bool preferSceneLayout = true;

    [Header("Audio Customization")]
    [SerializeField] private AudioClip shopMusicOverride;

    [Header("Background Art Customization")]
    [SerializeField] private Image dimmerBackgroundImage;
    [SerializeField] private Sprite dimmerBackgroundSprite;
    [SerializeField] private Image windowBackgroundImage;
    [SerializeField] private Sprite windowBackgroundSprite;
    [SerializeField] private Image offersBackgroundImage;
    [SerializeField] private Sprite offersBackgroundSprite;

    [Header("Button Art Customization")]
    [SerializeField] private ButtonArtTheme offerButtonTheme = new ButtonArtTheme { fallbackColor = new Color(0.2f, 0.31f, 0.48f, 1f) };
    [SerializeField] private ButtonArtTheme actionButtonTheme = new ButtonArtTheme { fallbackColor = new Color(0.19f, 0.3f, 0.48f, 1f) };

    [Header("Selection Visual Customization")]
    [SerializeField] private SelectionVisualMode selectionVisualMode = SelectionVisualMode.SelectableState;
    [SerializeField] private Color selectionTintColor = new Color(0.55f, 0.95f, 0.85f, 1f);
    [SerializeField] private Sprite selectionSprite;
    [SerializeField] private Color selectionOutlineColor = new Color(0.25f, 1f, 0.85f, 1f);
    [SerializeField] private Vector2 selectionOutlineDistance = new Vector2(3f, -3f);
    [SerializeField] private bool selectionOutlineUseGraphicAlpha = true;

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

    private Image runtimeDimmerImage;
    private Image runtimeWindowImage;
    private Image runtimeOffersImage;

    private readonly List<Button> spawnedButtons = new List<Button>();

    private OpenParams current;
    private List<ShopService.ShopOfferData> activeCatalog = new List<ShopService.ShopOfferData>();
    private int selectedIndex = -1;
    private int refreshesUsed;
    private readonly Dictionary<string, int> refreshesByShopId = new Dictionary<string, int>();
    public static ShopScene GetOrCreate()
    {
        ShopScene found = FindAnyObjectByType<ShopScene>(FindObjectsInactive.Include);
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
        ApplyVisualCustomization();
        PlayShopMusic();

        titleLabel.text = string.IsNullOrWhiteSpace(current.ShopOutcome.Title) ? "Tienda" : current.ShopOutcome.Title;
        refreshesUsed = GetRefreshesForCurrentShop();
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

        ApplyButtonArt(buyButton, actionButtonTheme);
        ApplyButtonArt(refreshButton, actionButtonTheme);
        ApplyButtonArt(exitButton, actionButtonTheme);

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

            ApplyButtonArt(button, offerButtonTheme);

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null && offerButtonTheme.normalSprite == null)
                buttonImage.color = GetRarityButtonColor(offer.Rarity);

            SyncButtonColorBlockWithImage(button);

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
        current?.OnShopMessage?.Invoke(result);

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
            current?.OnShopMessage?.Invoke("Refresh deshabilitado por configuración.");
            return;
        }

        int currentRefreshesUsed = GetRefreshesForCurrentShop();
        if (currentRefreshesUsed >= shopConfig.MaxRefreshesPerVisit)
        {
            current?.OnShopMessage?.Invoke("No quedan refreshes disponibles.");
            return;
        }

        if (current.Flow != null && !current.Flow.SpendCoins(shopConfig.RefreshCost))
        {
            current?.OnShopMessage?.Invoke("No alcanzan las monedas para refrescar.");
            return;
        }

        SetRefreshesForCurrentShop(currentRefreshesUsed + 1);
        LoadCatalog(forceRefresh: true);
        current?.OnShopMessage?.Invoke("Tienda refrescada.");
    }

    private void UpdateMetaLabels()
    {
        int coins = current?.Flow != null ? Mathf.Max(0, current.Flow.Coins) : 0;
        coinLabel.text = $"Monedas: {coins}";

        int stock = 0;
        for (int i = 0; i < activeCatalog.Count; i++)
            stock += Mathf.Max(0, activeCatalog[i].Stock);

        stockLabel.text = $"Stock total: {stock}";

        refreshesUsed = GetRefreshesForCurrentShop();
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
        return $"{GetRarityPrefix(offer.Rarity)} {GetOfferTypeLabel(offer.Type)} · {offer.Cost}g";
    }

    private int GetRefreshesForCurrentShop()
    {
        string shopId = current?.ShopId;
        if (string.IsNullOrWhiteSpace(shopId))
            return refreshesUsed;

        if (refreshesByShopId.TryGetValue(shopId, out int savedRefreshes))
            return Mathf.Max(0, savedRefreshes);

        return 0;
    }

    private void SetRefreshesForCurrentShop(int value)
    {
        refreshesUsed = Mathf.Max(0, value);

        string shopId = current?.ShopId;
        if (string.IsNullOrWhiteSpace(shopId))
            return;

        refreshesByShopId[shopId] = refreshesUsed;
    }


    private static string GetOfferTypeLabel(ShopService.ShopOfferType type)
    {
        switch (type)
        {
            case ShopService.ShopOfferType.Heal:
                return "Curación";
            case ShopService.ShopOfferType.OrbUpgrade:
                return "Mejora de orbe";
            case ShopService.ShopOfferType.OrbUpgradeDiscount:
                return "Mejora barata";
            case ShopService.ShopOfferType.RecoveryPack:
                return "Botiquín";
            case ShopService.ShopOfferType.VitalityBoost:
                return "Vitalidad";
            default:
                return "Oferta";
        }
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
            case ShopService.ShopOfferType.RecoveryPack:
                description = $"Recupera {offer.PrimaryValue} HP y además suma +1 de vida máxima.";
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


    private void PlayShopMusic()
    {
        if (shopMusicOverride != null)
        {
            AudioManager.Instance?.PlayMusic(shopMusicOverride, true);
            return;
        }

        AudioManager.Instance?.PlayShopMusic();
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
        text.textWrappingMode = TextWrappingModes.NoWrap;
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
   
    private void UpdateSelectionVisual()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            Button button = spawnedButtons[i];
            bool isSelected = i == selectedIndex;
            Outline outline = button.GetComponent<Outline>();

            if (selectionVisualMode == SelectionVisualMode.Outline)
            {
                if (outline == null)
                    outline = button.gameObject.AddComponent<Outline>();

                outline.effectColor = selectionOutlineColor;
                outline.effectDistance = selectionOutlineDistance;
                outline.useGraphicAlpha = selectionOutlineUseGraphicAlpha;
                outline.enabled = isSelected;
                continue;
            }

            if (outline != null)
                outline.enabled = false;

            if (selectionSprite != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState spriteState = button.spriteState;
                spriteState.selectedSprite = selectionSprite;
                button.spriteState = spriteState;
            }
            else
            {
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colorBlock = button.colors;
                colorBlock.selectedColor = selectionTintColor;
                colorBlock.colorMultiplier = 1f;
                button.colors = colorBlock;
            }
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        if (selectedIndex >= 0 && selectedIndex < spawnedButtons.Count)
        {
            GameObject selectedObject = spawnedButtons[selectedIndex].gameObject;
            if (eventSystem.currentSelectedGameObject != selectedObject)
                eventSystem.SetSelectedGameObject(selectedObject);
        }
        else if (eventSystem.currentSelectedGameObject != null)
        {
            eventSystem.SetSelectedGameObject(null);
        }
    }
}
