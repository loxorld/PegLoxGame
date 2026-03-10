using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public partial class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats player;
    [SerializeField] private BattleManager battle;
    [SerializeField] private OrbManager orbs;
    [SerializeField] private RewardManager rewards;
    [SerializeField] private GameFlowManager flow;

    [Header("UI Text (optional)")]
    [SerializeField] private TMP_Text orbNameText;
    [SerializeField] private TMP_Text stateText;

    [Header("Encounter / Difficulty (optional)")]
    [SerializeField] private TMP_Text encounterText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text rewardPreviewText;
    [SerializeField] private TMP_Text coinsText;

    [Header("Bars")]
    [SerializeField] private HealthBarUI playerBar;
    [SerializeField] private HealthBarUI enemyBar;

    [Header("Enemy bar follow")]
    [SerializeField] private bool followEnemyInWorldSpace = false;
    [SerializeField] private Vector3 enemyHeadOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private Vector2 enemyBarScreenOffset = new Vector2(0f, 28f);
    [SerializeField] private Vector2 enemyBarMinimumSize = new Vector2(280f, 32f);

    [Header("Update")]
    [SerializeField, Range(0.05f, 1f)] private float refreshInterval = 0.15f;

    [Header("Tween")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform hudTransform;

    private float timer;
    private GameState lastState;
    private RectTransform enemyBarRect;
    private Canvas rootCanvas;
    private Camera worldCamera;
    private Transform originalEnemyBarParent;
    private int originalEnemyBarSiblingIndex;
    private RectTransform combatHudRoot;
    private RectTransform topBarRoot;
    private RectTransform bottomHudRoot;
    private RectTransform playerPanelRoot;
    private RectTransform statsPanelRoot;
    private RectTransform orbSwitchRoot;
    private RectTransform enemyBarRowRoot;
    private Image stateBadgeImage;
    private TMP_Text playerNameText;
    private TMP_Text enemyNameText;
    private TMP_Text equippedOrbLabel;

    private void ResolveReferences()
    {
        if (player == null)
            player = ServiceRegistry.ResolveWithFallback(nameof(HUDController), nameof(player), () => ServiceRegistry.LegacyFind<PlayerStats>(true));

        if (battle == null)
            battle = ServiceRegistry.ResolveWithFallback(nameof(HUDController), nameof(battle), () => ServiceRegistry.LegacyFind<BattleManager>(true));

        if (orbs == null)
            orbs = ServiceRegistry.ResolveWithFallback(nameof(HUDController), nameof(orbs), () => OrbManager.Instance ?? ServiceRegistry.LegacyFind<OrbManager>(true));

        if (rewards == null)
            rewards = ServiceRegistry.ResolveWithFallback(nameof(HUDController), nameof(rewards), () => ServiceRegistry.LegacyFind<RewardManager>(true));

        if (flow == null)
            flow = ServiceRegistry.ResolveWithFallback(nameof(HUDController), nameof(flow), () => GameFlowManager.Instance ?? ServiceRegistry.LegacyFind<GameFlowManager>(true));
    }

    private void Awake()
    {
        ResolveReferences();

        if (enemyBar != null)
            enemyBarRect = enemyBar.GetComponent<RectTransform>();

        if (enemyBarRect != null)
        {
            rootCanvas = enemyBarRect.GetComponentInParent<Canvas>();
            if (followEnemyInWorldSpace && rootCanvas != null)
            {
                originalEnemyBarParent = enemyBarRect.parent;
                originalEnemyBarSiblingIndex = enemyBarRect.GetSiblingIndex();
                enemyBarRect.SetParent(rootCanvas.transform, true);
                enemyBarRect.localScale = Vector3.one;
                enemyBarRect.anchorMin = enemyBarRect.anchorMax = new Vector2(0.5f, 0.5f);
                enemyBarRect.pivot = new Vector2(0.5f, 0.5f);

                Vector2 size = enemyBarRect.sizeDelta;
                enemyBarRect.sizeDelta = new Vector2(
                    Mathf.Max(enemyBarMinimumSize.x, size.x),
                    Mathf.Max(enemyBarMinimumSize.y, size.y));
            }

            worldCamera = Camera.main;
        }

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (hudTransform == null)
            hudTransform = GetComponent<RectTransform>();

        lastState = flow != null ? flow.State : GameState.Combat;
        ConfigureHudLayout();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (hudTransform != null)
            hudTransform.localScale = Vector3.one * 0.95f;
        
        RefreshStateBadge(lastState);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureHudLayout();

        if (canvasGroup != null)
            canvasGroup.DOFade(1f, 0.25f);

        if (hudTransform != null)
            hudTransform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
    }

    private void OnDisable()
    {
        if (enemyBarRect == null || originalEnemyBarParent == null)
            return;

        enemyBarRect.SetParent(originalEnemyBarParent, true);
        enemyBarRect.SetSiblingIndex(originalEnemyBarSiblingIndex);
    }

    private void Update()
    {
        if (flow == null || orbs == null || player == null || battle == null)
            ResolveReferences();

        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;

        Refresh();
    }

    private void LateUpdate()
    {
        if (!followEnemyInWorldSpace)
            return;

        Enemy e = (battle != null) ? battle.CurrentEnemy : null;
        UpdateEnemyBarPosition(e);
    }

    private void Refresh()
    {
        bool layoutDirty = false;

        if (playerBar != null && player != null)
            playerBar.Set(player.CurrentHP, player.MaxHP);

        Enemy e = (battle != null) ? battle.CurrentEnemy : null;
        bool enemyVisible = e != null && e.gameObject.activeSelf;
        RefreshCombatantLabels(e);

        if (enemyBar != null)
        {
            if (!enemyVisible)
                enemyBar.Set(0, 1);
            else
                enemyBar.Set(e.CurrentHP, e.MaxHP);
        }

        if (followEnemyInWorldSpace)
            UpdateEnemyBarPosition(e);

        if (orbNameText != null)
        {
            OrbInstance orb = (orbs != null) ? orbs.CurrentOrb : null;
            string nextText = orb != null
                ? $"Orbe activo\n{orb.OrbName}  Lv {orb.Level}  |  DMG {orb.DamagePerHit}"
                : "Orbe activo\n-";
            if (orbNameText.text != nextText)
            {
                orbNameText.text = nextText;
                layoutDirty = true;
            }
        }

        if (stateText != null)
        {
            GameState s = (flow != null) ? flow.State : GameState.Combat;
            string nextText = s switch
            {
                GameState.Combat => "",
                GameState.RewardChoice => "RECOMPENSA",
                GameState.Paused => "PAUSA",
                GameState.GameOver => "DERROTA",
                _ => s.ToString()
            };
            if (stateText.text != nextText)
            {
                stateText.text = nextText;
                layoutDirty = true;
            }

            if (s != lastState && (s == GameState.Paused || s == GameState.GameOver))
                stateText.rectTransform.DOShakePosition(0.3f, 6f, 15, 90);

            RefreshStateBadge(s);
            lastState = s;
        }

        if (coinsText != null)
        {
            int coins = flow != null ? flow.Coins : 0;
            string nextText = $"Oro: {coins}";
            if (coinsText.text != nextText)
            {
                coinsText.text = nextText;
                layoutDirty = true;
            }
        }

        if (encounterText != null)
        {
            string nextText;
            if (battle == null)
            {
                nextText = string.Empty;
            }
            else
            {
                string encounterLabel = battle.CurrentEncounterType switch
                {
                    CombatEncounterType.Elite => "Elite",
                    CombatEncounterType.MiniBoss => "Miniboss",
                    CombatEncounterType.Boss => "Boss",
                    _ => $"Encuentro {battle.EncounterInStage + 1}"
                };

                nextText = battle.CurrentEncounterType == CombatEncounterType.Regular
                    ? encounterLabel
                    : $"{encounterLabel}\nNodo {battle.EncounterInStage + 1}";
            }

            if (encounterText.text != nextText)
            {
                encounterText.text = nextText;
                layoutDirty = true;
            }
        }

        if (difficultyText != null)
        {
            string nextText;
            if (battle == null)
                nextText = string.Empty;
            else if (battle.HasBalanceConfig)
                nextText = $"{battle.StageName}  |  {battle.CurrentEncounterLabel}\nHP x{battle.EnemyHpMultiplier:0.##}  |  DMG x{battle.EnemyDamageMultiplier:0.##}  |  N {battle.EnemiesToDefeat}";
            else
                nextText = $"{battle.StageName}  |  {battle.CurrentEncounterLabel}";

            if (difficultyText.text != nextText)
            {
                difficultyText.text = nextText;
                layoutDirty = true;
            }
        }

        if (rewardPreviewText != null)
        {
            string nextText = string.Empty;
            if (rewards != null && battle != null)
                nextText = RewardManager.BuildRewardPreviewText(rewards.CurrentEncounterPreview);

            if (rewardPreviewText.text != nextText)
            {
                rewardPreviewText.text = nextText;
                layoutDirty = true;
            }
        }

        if (layoutDirty)
        {
            Canvas.ForceUpdateCanvases();
            if (hudTransform != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(hudTransform);
        }
    }

    private void UpdateEnemyBarPosition(Enemy enemy)
    {
        if (enemyBarRect == null || !followEnemyInWorldSpace)
            return;

        bool enemyVisible = enemy != null && enemy.gameObject.activeInHierarchy;
        enemyBarRect.gameObject.SetActive(enemyVisible);
        if (!enemyVisible)
            return;

        if (worldCamera == null)
            worldCamera = Camera.main;
        if (worldCamera == null)
            return;

        Vector3 enemyHead = enemy.transform.position + enemyHeadOffset;
        SpriteRenderer enemyRenderer = enemy.GetComponent<SpriteRenderer>();
        if (enemyRenderer != null)
            enemyHead = enemyRenderer.bounds.center + Vector3.up * (enemyRenderer.bounds.extents.y + enemyHeadOffset.y);

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(enemyHead);
        if (screenPoint.z <= 0f)
        {
            enemyBarRect.gameObject.SetActive(false);
            return;
        }

        screenPoint.x += enemyBarScreenOffset.x;
        screenPoint.y += enemyBarScreenOffset.y;

        if (rootCanvas == null)
            rootCanvas = enemyBarRect.GetComponentInParent<Canvas>();
        if (rootCanvas == null)
            return;

        Camera canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        RectTransform canvasRect = rootCanvas.transform as RectTransform;

        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 localPoint))
            enemyBarRect.anchoredPosition = localPoint;
    }

    private void ConfigureHudLayout()
    {
        if (!TryResolveHudRoots())
            return;

        RectTransform canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        float width = canvasRect != null ? canvasRect.rect.width : Screen.width;
        float height = canvasRect != null ? canvasRect.rect.height : Screen.height;
        bool portraitLayout = width <= height * 1.08f;
        float margin = Mathf.Clamp(width * 0.0225f, 18f, 32f);
        float headerHeight = portraitLayout ? 132f : 118f;
        float bottomHeight = portraitLayout ? 332f : 240f;
        float playerWidth = portraitLayout ? Mathf.Min(width * 0.48f, 500f) : Mathf.Min(width * 0.27f, 460f);
        float playerHeight = portraitLayout ? 184f : 190f;
        float orbWidth = portraitLayout ? Mathf.Min(width * 0.40f, 392f) : Mathf.Min(width * 0.23f, 360f);
        float orbHeight = portraitLayout ? 184f : 190f;
        float statsWidth = portraitLayout ? Mathf.Min(width * 0.34f, 320f) : Mathf.Min(width * 0.19f, 304f);
        float statsHeight = portraitLayout ? 164f : 164f;
        float headerLeftWidth = portraitLayout ? Mathf.Min(width * 0.45f, 430f) : Mathf.Min(width * 0.29f, 460f);
        float headerRightWidth = portraitLayout ? Mathf.Min(width * 0.47f, 450f) : Mathf.Min(width * 0.33f, 560f);
        float enemyWidth = portraitLayout ? Mathf.Min(width * 0.8f, 780f) : Mathf.Min(width * 0.42f, 720f);
        float enemyHeight = portraitLayout ? 90f : 94f;

        ConfigureHudRoot(combatHudRoot);
        ConfigureTopAndBottomRoots(margin, headerHeight, bottomHeight);
        ConfigureBackdrop();
        ConfigureHeaderCards(margin, headerHeight, headerLeftWidth, headerRightWidth);
        ConfigureEnemyBarCard(margin, headerHeight, enemyWidth, enemyHeight);
        ConfigurePlayerPanel(margin, playerWidth, playerHeight);
        ConfigureStatsPanel(margin, statsWidth, statsHeight, portraitLayout);
        ConfigureOrbSwitchPanel(margin, orbWidth, orbHeight);
        StyleHealthBars();
        StyleCombatStatsTexts();
        StyleOrbSwitchPanel();
        StylePauseButton();
        RefreshCombatantLabels(battle != null ? battle.CurrentEnemy : null);
        RefreshStateBadge(lastState);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(combatHudRoot);
    }

    private void RefreshStateBadge(GameState state)
    {
        if (stateText == null)
            return;

        if (stateBadgeImage == null)
        {
            RectTransform badgeRect = stateText.transform.parent as RectTransform;
            if (badgeRect != null)
                stateBadgeImage = badgeRect.GetComponent<Image>();
        }

        bool showBadge = state != GameState.Combat && !string.IsNullOrWhiteSpace(stateText.text);
        Transform badgeRoot = stateText.transform.parent;
        if (badgeRoot != null)
            badgeRoot.gameObject.SetActive(showBadge);
        else
            stateText.gameObject.SetActive(showBadge);

        if (!showBadge)
            return;

        Color badgeColor = state switch
        {
            GameState.RewardChoice => new Color(0.12f, 0.36f, 0.37f, 0.94f),
            GameState.Paused => new Color(0.52f, 0.34f, 0.12f, 0.94f),
            GameState.GameOver => new Color(0.49f, 0.15f, 0.15f, 0.94f),
            _ => new Color(0.2f, 0.2f, 0.2f, 0.9f)
        };

        if (stateBadgeImage != null && !UIArtUtility.ShouldPreserveColor(stateBadgeImage))
            stateBadgeImage.color = badgeColor;

        stateText.gameObject.SetActive(true);
        if (!UIArtUtility.ShouldPreserveTextStyling(stateText))
        {
            stateText.alignment = TextAlignmentOptions.Center;
            stateText.color = new Color(0.98f, 0.96f, 0.88f, 1f);
            stateText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            stateText.enableAutoSizing = true;
            stateText.fontSizeMin = 18f;
            stateText.fontSizeMax = 28f;
            stateText.textWrappingMode = TextWrappingModes.NoWrap;
            stateText.outlineWidth = 0.18f;
            stateText.outlineColor = new Color(0.07f, 0.05f, 0.03f, 0.75f);
        }

        StretchInsideCard(stateText.rectTransform, 18f, 10f, 18f, 12f);
    }

    private void RefreshCombatantLabels(Enemy enemy)
    {
        if (playerNameText != null)
            playerNameText.text = "Jugador";

        if (enemyNameText != null)
        {
            if (enemy != null && enemy.gameObject.activeSelf)
            {
                string intent = enemy.CurrentIntentText;
                bool phaseActive = enemy.DesperationTriggered;
                string intentColor = phaseActive ? "#FF8A9A" : "#9FD9D8";
                string headerColor = phaseActive ? "#FFF1E7" : "#F7E8B9";
                string plainText = string.IsNullOrWhiteSpace(intent)
                    ? enemy.EnemyName
                    : $"{enemy.EnemyName}\n{intent}";
                string richText = string.IsNullOrWhiteSpace(intent)
                    ? $"<color={headerColor}>{enemy.EnemyName}</color>"
                    : $"<color={headerColor}>{enemy.EnemyName}</color>\n<size=65%><color={intentColor}>{intent}</color></size>";
                enemyNameText.text = UIArtUtility.ResolveDynamicText(enemyNameText, plainText, richText);
            }
            else
            {
                enemyNameText.text = "Enemigo";
            }
        }

        if (equippedOrbLabel != null)
        {
            OrbInstance orb = orbs != null ? orbs.CurrentOrb : null;
            equippedOrbLabel.text = orb != null
                ? $"Orbe: {orb.OrbName}  Lv {orb.Level}"
                : "Orbe: -";
        }
    }

    private bool TryResolveHudRoots()
    {
        if (rootCanvas == null)
        {
            if (enemyBarRect != null)
                rootCanvas = enemyBarRect.GetComponentInParent<Canvas>();

            if (rootCanvas == null)
                rootCanvas = GetComponentInChildren<Canvas>(true);

            if (rootCanvas == null)
                rootCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        if (rootCanvas == null)
            return false;

        if (combatHudRoot == null)
            combatHudRoot = FindDescendant(rootCanvas.transform, "CombatHUD") as RectTransform;
        if (combatHudRoot == null)
            combatHudRoot = rootCanvas.transform as RectTransform;

        if (topBarRoot == null)
            topBarRoot = FindDescendant(combatHudRoot, "TopBar") as RectTransform;
        if (bottomHudRoot == null)
            bottomHudRoot = FindDescendant(combatHudRoot, "BottomHUD") as RectTransform;
        if (playerPanelRoot == null)
            playerPanelRoot = FindDescendant(combatHudRoot, "PlayerHUDPanel") as RectTransform;
        if (statsPanelRoot == null)
            statsPanelRoot = FindDescendant(combatHudRoot, "CombatStatsHUD") as RectTransform;
        if (orbSwitchRoot == null)
            orbSwitchRoot = FindDescendant(combatHudRoot, "OrbSwitchUI") as RectTransform;
        if (enemyBarRowRoot == null)
            enemyBarRowRoot = FindDescendant(combatHudRoot, "EnemyBarRow") as RectTransform;
        if (playerNameText == null && playerPanelRoot != null)
            playerNameText = FindDescendant(playerPanelRoot, "Player")?.GetComponent<TMP_Text>();
        if (enemyNameText == null && enemyBarRowRoot != null)
            enemyNameText = FindDescendant(enemyBarRowRoot, "Enemy")?.GetComponent<TMP_Text>();
        if (equippedOrbLabel == null && playerPanelRoot != null)
            equippedOrbLabel = FindDescendant(playerPanelRoot, "EquippedOrbLabel")?.GetComponent<TMP_Text>();

        if (combatHudRoot == null)
            return false;

        canvasGroup = EnsureCanvasGroup(combatHudRoot.gameObject);
        hudTransform = combatHudRoot;

        DisableIfPresent<HorizontalLayoutGroup>(combatHudRoot);
        DisableIfPresent<ContentSizeFitter>(combatHudRoot);
        DisableIfPresent<VerticalLayoutGroup>(topBarRoot);

        return true;
    }

    private void ConfigureHudRoot(RectTransform root)
    {
        if (root == null)
            return;

        root.gameObject.SetActive(true);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0.5f);
    }

    private void ConfigureTopAndBottomRoots(float margin, float headerHeight, float bottomHeight)
    {
        if (topBarRoot != null)
        {
            topBarRoot.anchorMin = new Vector2(0f, 1f);
            topBarRoot.anchorMax = new Vector2(1f, 1f);
            topBarRoot.pivot = new Vector2(0.5f, 1f);
            topBarRoot.anchoredPosition = new Vector2(0f, -margin);
            topBarRoot.sizeDelta = new Vector2(0f, headerHeight);
        }

        if (bottomHudRoot != null)
        {
            bottomHudRoot.anchorMin = new Vector2(0f, 0f);
            bottomHudRoot.anchorMax = new Vector2(1f, 0f);
            bottomHudRoot.pivot = new Vector2(0.5f, 0f);
            bottomHudRoot.anchoredPosition = new Vector2(0f, margin);
            bottomHudRoot.sizeDelta = new Vector2(0f, bottomHeight);
        }
    }

    private void ConfigureBackdrop()
    {
        RectTransform background = FindDescendant(combatHudRoot, "BackgroundImage") as RectTransform;
        if (background == null)
            return;

        background.SetSiblingIndex(0);
        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;

        Image backgroundImage = background.GetComponent<Image>();
        if (backgroundImage != null)
        {
            if (!UIArtUtility.ShouldPreserveColor(backgroundImage))
                backgroundImage.color = new Color(0.08f, 0.06f, 0.04f, 0.24f);
            backgroundImage.raycastTarget = false;
        }
    }

    private void ConfigureHeaderCards(float margin, float headerHeight, float leftWidth, float rightWidth)
    {
        if (combatHudRoot == null)
            return;

        RectTransform leftCard = EnsureCardRoot(combatHudRoot, "HeaderLeftCard", new Color(0.09f, 0.08f, 0.06f, 0.82f), new Color(0.68f, 0.55f, 0.28f, 0.95f));
        leftCard.anchorMin = new Vector2(0f, 1f);
        leftCard.anchorMax = new Vector2(0f, 1f);
        leftCard.pivot = new Vector2(0f, 1f);
        leftCard.anchoredPosition = new Vector2(margin, -margin);
        leftCard.sizeDelta = new Vector2(leftWidth, headerHeight);
        leftCard.SetSiblingIndex(combatHudRoot.childCount - 1);

        RectTransform rightCard = EnsureCardRoot(combatHudRoot, "HeaderRightCard", new Color(0.1f, 0.085f, 0.065f, 0.84f), new Color(0.3f, 0.49f, 0.34f, 0.95f));
        rightCard.anchorMin = new Vector2(1f, 1f);
        rightCard.anchorMax = new Vector2(1f, 1f);
        rightCard.pivot = new Vector2(1f, 1f);
        rightCard.anchoredPosition = new Vector2(-margin, -margin);
        rightCard.sizeDelta = new Vector2(rightWidth, headerHeight);
        rightCard.SetSiblingIndex(combatHudRoot.childCount - 1);

        RectTransform stateBadge = EnsureCardRoot(combatHudRoot, "StateBadgeCard", new Color(0.24f, 0.2f, 0.14f, 0.92f), new Color(0.78f, 0.65f, 0.3f, 0.98f));
        stateBadge.anchorMin = new Vector2(0.5f, 1f);
        stateBadge.anchorMax = new Vector2(0.5f, 1f);
        stateBadge.pivot = new Vector2(0.5f, 1f);
        stateBadge.anchoredPosition = new Vector2(0f, -(margin + headerHeight + 14f));
        stateBadge.sizeDelta = new Vector2(248f, 62f);
        stateBadge.gameObject.SetActive(false);
        stateBadge.SetSiblingIndex(combatHudRoot.childCount - 1);
        stateBadgeImage = stateBadge.GetComponent<Image>();

        if (encounterText != null)
        {
            encounterText.transform.SetParent(leftCard, false);
            StyleHeaderPrimaryText(encounterText);
            StretchInsideCard(encounterText.rectTransform, 20f, 12f, 120f, 58f);
        }

        if (difficultyText != null)
        {
            difficultyText.transform.SetParent(leftCard, false);
            StyleHeaderSecondaryText(difficultyText);
            StretchInsideCard(difficultyText.rectTransform, 20f, 50f, 20f, 40f);
        }

        if (rewardPreviewText == null)
            rewardPreviewText = EnsureHeaderTextElement(leftCard, "RewardPreviewText");

        if (rewardPreviewText != null)
        {
            rewardPreviewText.transform.SetParent(leftCard, false);
            StyleHeaderTertiaryText(rewardPreviewText);
            StretchInsideCard(rewardPreviewText.rectTransform, 20f, 86f, 20f, 14f);
        }

        if (orbNameText != null)
        {
            orbNameText.transform.SetParent(rightCard, false);
            StyleOrbInfoText(orbNameText);
            StretchInsideCard(orbNameText.rectTransform, 18f, 14f, 142f, 16f);
        }

        if (stateText != null)
        {
            stateText.transform.SetParent(stateBadge, false);
            stateText.gameObject.SetActive(false);
        }

        RectTransform pauseRoot = FindDescendant(combatHudRoot, "PauseButton") as RectTransform;
        if (pauseRoot != null)
        {
            pauseRoot.SetParent(rightCard, false);
            pauseRoot.anchorMin = new Vector2(1f, 1f);
            pauseRoot.anchorMax = new Vector2(1f, 1f);
            pauseRoot.pivot = new Vector2(1f, 1f);
            pauseRoot.anchoredPosition = new Vector2(-16f, -16f);
            pauseRoot.sizeDelta = new Vector2(128f, 52f);
        }
    }

    private void ConfigureEnemyBarCard(float margin, float headerHeight, float width, float height)
    {
        if (enemyBarRowRoot == null)
            return;

        enemyBarRowRoot.anchorMin = new Vector2(0.5f, 1f);
        enemyBarRowRoot.anchorMax = new Vector2(0.5f, 1f);
        enemyBarRowRoot.pivot = new Vector2(0.5f, 1f);
        enemyBarRowRoot.anchoredPosition = new Vector2(0f, -(margin + headerHeight + 76f));
        enemyBarRowRoot.sizeDelta = new Vector2(width, height);
        enemyBarRowRoot.SetSiblingIndex(combatHudRoot.childCount - 1);

        HorizontalLayoutGroup rowLayout = EnsureComponent<HorizontalLayoutGroup>(enemyBarRowRoot.gameObject);
        rowLayout.padding = new RectOffset(16, 16, 16, 16);
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        StyleCardSurface(enemyBarRowRoot, new Color(0.14f, 0.08f, 0.08f, 0.84f), new Color(0.62f, 0.23f, 0.18f, 0.96f));

        float contentWidth = Mathf.Max(0f, width - rowLayout.padding.left - rowLayout.padding.right);
        float labelWidth = Mathf.Clamp(contentWidth * 0.3f, 84f, 150f);
        float barWidth = Mathf.Max(96f, contentWidth - labelWidth - rowLayout.spacing);

        if (enemyNameText != null)
        {
            if (!UIArtUtility.ShouldPreserveTextStyling(enemyNameText))
            {
                enemyNameText.enableAutoSizing = true;
                enemyNameText.fontSizeMin = 14f;
                enemyNameText.fontSizeMax = 22f;
                enemyNameText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                enemyNameText.alignment = TextAlignmentOptions.Left;
                enemyNameText.color = new Color(0.98f, 0.86f, 0.78f, 1f);
                enemyNameText.outlineWidth = 0.18f;
                enemyNameText.outlineColor = new Color(0.09f, 0.04f, 0.04f, 0.82f);
                enemyNameText.textWrappingMode = TextWrappingModes.NoWrap;
                enemyNameText.overflowMode = TextOverflowModes.Ellipsis;
            }

            LayoutElement labelLayout = EnsureLayoutElement(enemyNameText.gameObject, false);
            labelLayout.preferredWidth = labelWidth;
            labelLayout.minWidth = Mathf.Max(72f, labelWidth * 0.72f);
            labelLayout.flexibleWidth = 0f;
        }

        if (enemyBarRect != null)
        {
            LayoutElement barLayout = EnsureLayoutElement(enemyBarRect.gameObject, false);
            barLayout.flexibleWidth = 1f;
            barLayout.preferredWidth = barWidth;
            barLayout.minWidth = Mathf.Max(88f, barWidth * 0.72f);
        }
    }

    private void ConfigurePlayerPanel(float margin, float width, float height)
    {
        if (playerPanelRoot == null)
            return;

        playerPanelRoot.anchorMin = new Vector2(0f, 0f);
        playerPanelRoot.anchorMax = new Vector2(0f, 0f);
        playerPanelRoot.pivot = new Vector2(0f, 0f);
        playerPanelRoot.anchoredPosition = new Vector2(margin, margin);
        playerPanelRoot.sizeDelta = new Vector2(width, height);

        VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(playerPanelRoot.gameObject);
        layout.padding = new RectOffset(18, 18, 36, 12);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        StyleCardSurface(playerPanelRoot, new Color(0.08f, 0.095f, 0.085f, 0.84f), new Color(0.25f, 0.49f, 0.34f, 0.96f));
        ConfigurePanelTitle(playerPanelRoot, "PlayerPanelTitle", "Estado del aventurero");

        ConfigureInfoRow(playerPanelRoot, "PlayerBarRow", 42f, 12f);
        ConfigureInfoRow(playerPanelRoot, "CoinsRow", 26f, 8f);
        ConfigureInfoRow(playerPanelRoot, "EquippedOrbRow", 26f, 8f);

        float contentWidth = Mathf.Max(0f, width - layout.padding.left - layout.padding.right);
        float playerNameWidth = Mathf.Clamp(contentWidth * 0.34f, 76f, 118f);
        float playerBarWidth = Mathf.Max(88f, contentWidth - playerNameWidth - 12f);

        if (playerNameText != null)
        {
            if (!UIArtUtility.ShouldPreserveTextStyling(playerNameText))
            {
                playerNameText.enableAutoSizing = true;
                playerNameText.fontSizeMin = 14f;
                playerNameText.fontSizeMax = 20f;
                playerNameText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                playerNameText.alignment = TextAlignmentOptions.Left;
                playerNameText.color = new Color(0.97f, 0.93f, 0.82f, 1f);
                playerNameText.outlineWidth = 0.18f;
                playerNameText.outlineColor = new Color(0.05f, 0.08f, 0.05f, 0.75f);
                playerNameText.textWrappingMode = TextWrappingModes.NoWrap;
                playerNameText.overflowMode = TextOverflowModes.Ellipsis;
            }

            LayoutElement nameLayout = EnsureLayoutElement(playerNameText.gameObject, false);
            nameLayout.preferredWidth = playerNameWidth;
            nameLayout.minWidth = Mathf.Max(64f, playerNameWidth * 0.72f);
            nameLayout.flexibleWidth = 0f;
        }

        if (coinsText != null)
        {
            if (!UIArtUtility.ShouldPreserveTextStyling(coinsText))
            {
                coinsText.alignment = TextAlignmentOptions.Left;
                coinsText.fontStyle = FontStyles.Bold;
                coinsText.color = new Color(0.98f, 0.88f, 0.52f, 1f);
                coinsText.enableAutoSizing = true;
                coinsText.fontSizeMin = 12f;
                coinsText.fontSizeMax = 18f;
                coinsText.textWrappingMode = TextWrappingModes.NoWrap;
                coinsText.overflowMode = TextOverflowModes.Ellipsis;
            }

            LayoutElement coinsLayout = EnsureLayoutElement(coinsText.gameObject, false);
            coinsLayout.minWidth = 0f;
            coinsLayout.preferredWidth = 0f;
            coinsLayout.flexibleWidth = 1f;
        }

        if (equippedOrbLabel != null)
        {
            if (!UIArtUtility.ShouldPreserveTextStyling(equippedOrbLabel))
            {
                equippedOrbLabel.alignment = TextAlignmentOptions.Left;
                equippedOrbLabel.fontStyle = FontStyles.Bold;
                equippedOrbLabel.color = new Color(0.82f, 0.95f, 0.88f, 1f);
                equippedOrbLabel.enableAutoSizing = true;
                equippedOrbLabel.fontSizeMin = 12f;
                equippedOrbLabel.fontSizeMax = 18f;
                equippedOrbLabel.textWrappingMode = TextWrappingModes.NoWrap;
                equippedOrbLabel.overflowMode = TextOverflowModes.Ellipsis;
            }

            LayoutElement equippedLayout = EnsureLayoutElement(equippedOrbLabel.gameObject, false);
            equippedLayout.minWidth = 0f;
            equippedLayout.preferredWidth = 0f;
            equippedLayout.flexibleWidth = 1f;
        }

        if (playerBar != null && playerBar.Slider != null)
        {
            LayoutElement barLayout = EnsureLayoutElement(playerBar.Slider.gameObject, false);
            barLayout.preferredWidth = playerBarWidth;
            barLayout.minWidth = Mathf.Max(82f, playerBarWidth * 0.72f);
            barLayout.flexibleWidth = 1f;
        }

        StyleIconInRow(playerPanelRoot, "CoinIcon", new Color(0.95f, 0.78f, 0.24f, 1f), 22f);
        StyleIconInRow(playerPanelRoot, "EquippedOrbIcon", new Color(0.55f, 0.89f, 0.78f, 1f), 22f);
    }

    private void ConfigureStatsPanel(float margin, float width, float height, bool portraitLayout)
    {
        if (statsPanelRoot == null)
            return;

        statsPanelRoot.anchorMin = new Vector2(0.5f, 0f);
        statsPanelRoot.anchorMax = new Vector2(0.5f, 0f);
        statsPanelRoot.pivot = new Vector2(0.5f, 0f);
        statsPanelRoot.anchoredPosition = portraitLayout
            ? new Vector2(0f, margin + 198f)
            : new Vector2(0f, margin);
        statsPanelRoot.sizeDelta = new Vector2(width, height);

        VerticalLayoutGroup layout = EnsureComponent<VerticalLayoutGroup>(statsPanelRoot.gameObject);
        layout.padding = new RectOffset(16, 16, 38, 14);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        StyleCardSurface(statsPanelRoot, new Color(0.07f, 0.09f, 0.105f, 0.86f), new Color(0.22f, 0.45f, 0.58f, 0.98f));
        ConfigurePanelTitle(statsPanelRoot, "StatsPanelTitle", "Lectura del tiro");

        TMP_Text[] texts = statsPanelRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || text.gameObject.name == "StatsPanelTitle")
                continue;

            if (!UIArtUtility.ShouldPreserveTextStyling(text))
            {
                text.alignment = TextAlignmentOptions.Left;
                text.enableAutoSizing = true;
                text.fontSizeMin = 11f;
                text.fontSizeMax = 15f;
                text.fontStyle = FontStyles.Bold;
                text.color = new Color(0.88f, 0.95f, 0.97f, 1f);
                text.outlineWidth = 0.14f;
                text.outlineColor = new Color(0.03f, 0.05f, 0.07f, 0.75f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            LayoutElement layoutElement = EnsureLayoutElement(text.gameObject, false);
            layoutElement.preferredHeight = 18f;
            layoutElement.minHeight = 18f;
            layoutElement.flexibleWidth = 1f;
        }
    }

    private void ConfigureOrbSwitchPanel(float margin, float width, float height)
    {
        if (orbSwitchRoot == null)
            return;

        orbSwitchRoot.anchorMin = new Vector2(1f, 0f);
        orbSwitchRoot.anchorMax = new Vector2(1f, 0f);
        orbSwitchRoot.pivot = new Vector2(1f, 0f);
        orbSwitchRoot.anchoredPosition = new Vector2(-margin, margin);
        orbSwitchRoot.sizeDelta = new Vector2(width, height);

        StyleCardSurface(orbSwitchRoot, new Color(0.1f, 0.085f, 0.06f, 0.84f), new Color(0.68f, 0.44f, 0.22f, 0.98f));
        ConfigurePanelTitle(orbSwitchRoot, "OrbSwitchTitle", "Rotacion de orbes");

        TMP_Text title = FindDescendant(orbSwitchRoot, "OrbSwitchTitle")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(18f, -42f);
            titleRect.offsetMax = new Vector2(-18f, -16f);

            if (!UIArtUtility.ShouldPreserveTextStyling(title))
            {
                title.alignment = TextAlignmentOptions.Left;
                title.fontSizeMin = 12f;
                title.fontSizeMax = 18f;
            }
        }

        TMP_Text label = FindDescendant(orbSwitchRoot, "OrbNameLabel")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            if (!UIArtUtility.ShouldPreserveTextStyling(label))
            {
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 22f;
                label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.95f, 0.96f, 0.9f, 1f);
                label.outlineWidth = 0.18f;
                label.outlineColor = new Color(0.08f, 0.05f, 0.03f, 0.82f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
            }

            label.raycastTarget = false;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(16f, 62f);
            labelRect.offsetMax = new Vector2(-16f, -48f);
        }

        float buttonWidth = Mathf.Clamp((width - 68f) * 0.35f, 68f, 92f);
        ConfigureOrbSwitchButton("ButtonPrev", "ANT", buttonWidth, 44f);
        ConfigureOrbSwitchButton("ButtonNext", "SIG", buttonWidth, 44f);
    }

    private void StyleHealthBars()
    {
        StyleHealthBar(playerBar, new Color(0.34f, 0.76f, 0.45f, 1f), new Color(0.12f, 0.18f, 0.12f, 0.96f), new Color(0.97f, 0.96f, 0.88f, 1f));
        StyleHealthBar(enemyBar, new Color(0.88f, 0.28f, 0.24f, 1f), new Color(0.22f, 0.08f, 0.08f, 0.96f), new Color(0.98f, 0.9f, 0.86f, 1f));
    }

    private void StyleCombatStatsTexts()
    {
        if (statsPanelRoot == null)
            return;

        CanvasGroup statsGroup = statsPanelRoot.GetComponent<CanvasGroup>();
        if (statsGroup != null && statsGroup.alpha <= 0.01f)
            statsGroup.alpha = 0.68f;

        if (UIArtUtility.AllowsGeneratedDecor(statsPanelRoot))
        {
            Shadow shadow = EnsureComponent<Shadow>(statsPanelRoot.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;
        }
    }

    private void StyleOrbSwitchPanel()
    {
        if (orbSwitchRoot == null)
            return;

        Button[] buttons = orbSwitchRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            Image image = EnsureComponent<Image>(button.gameObject);
            UIArtUtility.ApplyImageStyle(image, new Color(0.27f, 0.18f, 0.1f, 0.96f), true, Image.Type.Sliced, GetBuiltinPanelSprite());

            if (UIArtUtility.AllowsGeneratedDecor(button))
            {
                Shadow shadow = EnsureComponent<Shadow>(button.gameObject);
                shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
                shadow.effectDistance = new Vector2(0f, -4f);
                shadow.useGraphicAlpha = true;
            }

            UIButtonMotion.Attach(button.transform as RectTransform, 1.03f, 0.965f, 0.12f);

            if (!UIArtUtility.ShouldPreserveButtonTransitions(button))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = new Color(0.27f, 0.18f, 0.1f, 1f);
                colors.highlightedColor = new Color(0.39f, 0.27f, 0.16f, 1f);
                colors.pressedColor = new Color(0.2f, 0.13f, 0.08f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.25f, 0.2f, 0.16f, 0.55f);
                colors.colorMultiplier = 1f;
                button.colors = colors;
            }

            TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null)
            {
                if (!UIArtUtility.ShouldPreserveTextStyling(buttonText))
                {
                    buttonText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                    buttonText.color = new Color(0.98f, 0.95f, 0.86f, 1f);
                    buttonText.enableAutoSizing = true;
                    buttonText.fontSizeMin = 14f;
                    buttonText.fontSizeMax = 20f;
                    buttonText.characterSpacing = 2f;
                    buttonText.outlineWidth = 0.16f;
                    buttonText.outlineColor = new Color(0.08f, 0.05f, 0.03f, 0.82f);
                }
            }
        }
    }

    private void StylePauseButton()
    {
        RectTransform pauseRoot = FindDescendant(combatHudRoot, "PauseButton") as RectTransform;
        if (pauseRoot == null)
            return;

        Button button = pauseRoot.GetComponent<Button>();
        Image image = EnsureComponent<Image>(pauseRoot.gameObject);
        UIArtUtility.ApplyImageStyle(image, new Color(0.34f, 0.24f, 0.14f, 0.96f), true, Image.Type.Sliced, GetBuiltinPanelSprite());

        if (UIArtUtility.AllowsGeneratedDecor(pauseRoot))
        {
            Shadow shadow = EnsureComponent<Shadow>(pauseRoot.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            shadow.effectDistance = new Vector2(0f, -5f);
            shadow.useGraphicAlpha = true;
        }

        UIButtonMotion.Attach(pauseRoot, 1.03f, 0.96f, 0.12f);

        if (button != null && !UIArtUtility.ShouldPreserveButtonTransitions(button))
        {
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.34f, 0.24f, 0.14f, 1f);
            colors.highlightedColor = new Color(0.48f, 0.34f, 0.2f, 1f);
            colors.pressedColor = new Color(0.24f, 0.16f, 0.1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.25f, 0.2f, 0.16f, 0.55f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        TMP_Text pauseLabel = pauseRoot.GetComponentInChildren<TMP_Text>(true);
        if (pauseLabel != null)
        {
            pauseLabel.text = "PAUSA";
            if (!UIArtUtility.ShouldPreserveTextStyling(pauseLabel))
            {
                pauseLabel.alignment = TextAlignmentOptions.Center;
                pauseLabel.enableAutoSizing = true;
                pauseLabel.fontSizeMin = 16f;
                pauseLabel.fontSizeMax = 24f;
                pauseLabel.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
                pauseLabel.color = new Color(0.98f, 0.95f, 0.86f, 1f);
                pauseLabel.characterSpacing = 2.5f;
                pauseLabel.outlineWidth = 0.18f;
                pauseLabel.outlineColor = new Color(0.1f, 0.07f, 0.03f, 0.8f);
            }
        }
    }

    private void StyleHealthBar(HealthBarUI bar, Color fillColor, Color trackColor, Color labelColor)
    {
        if (bar == null)
            return;

        Slider slider = bar.Slider;
        if (slider != null)
        {
            LayoutElement layout = EnsureLayoutElement(slider.gameObject, false);
            layout.flexibleWidth = 1f;
            layout.preferredWidth = 136f;
            layout.minWidth = 88f;
            layout.preferredHeight = 28f;
            layout.minHeight = 28f;
        }

        bar.ApplyTheme(fillColor, trackColor, labelColor, Color.Lerp(fillColor, Color.white, 0.3f));
    }

    private static TMP_Text EnsureHeaderTextElement(RectTransform parent, string childName)
    {
        TMP_Text text = FindDescendant(parent, childName)?.GetComponent<TMP_Text>();
        if (text != null)
            return text;

        GameObject textObject = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        return textObject.GetComponent<TextMeshProUGUI>();
    }

    private void ConfigurePanelTitle(RectTransform root, string childName, string title)
    {
        TMP_Text titleText = FindDescendant(root, childName)?.GetComponent<TMP_Text>();
        if (titleText == null)
        {
            GameObject titleObject = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.SetParent(root, false);
            titleRect.SetSiblingIndex(0);
            titleText = titleObject.GetComponent<TextMeshProUGUI>();
        }

        titleText.text = title;
        if (!UIArtUtility.ShouldPreserveTextStyling(titleText))
        {
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 14f;
            titleText.fontSizeMax = 20f;
            titleText.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            titleText.color = new Color(0.98f, 0.92f, 0.78f, 1f);
            titleText.outlineWidth = 0.16f;
            titleText.outlineColor = new Color(0.08f, 0.06f, 0.03f, 0.75f);
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
        }

        titleText.raycastTarget = false;

        LayoutElement layout = EnsureLayoutElement(titleText.gameObject, false);
        layout.preferredHeight = 22f;
        layout.minHeight = 22f;
    }

    private void ConfigureInfoRow(RectTransform panelRoot, string rowName, float preferredHeight, float spacing)
    {
        RectTransform row = FindDescendant(panelRoot, rowName) as RectTransform;
        if (row == null)
            return;

        LayoutElement rowLayout = EnsureLayoutElement(row.gameObject, false);
        rowLayout.preferredHeight = preferredHeight;
        rowLayout.minHeight = preferredHeight;
        rowLayout.flexibleWidth = 1f;

        HorizontalLayoutGroup layoutGroup = EnsureComponent<HorizontalLayoutGroup>(row.gameObject);
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);
        layoutGroup.spacing = spacing;
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
    }

    private void StyleIconInRow(RectTransform panelRoot, string iconName, Color tint, float size)
    {
        Transform iconTransform = FindDescendant(panelRoot, iconName);
        if (iconTransform == null)
            return;

        RectTransform rect = iconTransform as RectTransform;
        if (rect != null)
            rect.sizeDelta = new Vector2(size, size);

        Image image = iconTransform.GetComponent<Image>();
        if (image != null)
        {
            if (!UIArtUtility.ShouldPreserveColor(image))
                image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }

    private void ConfigureOrbSwitchButton(string name, string labelText, float width, float height)
    {
        RectTransform buttonRoot = FindDescendant(orbSwitchRoot, name) as RectTransform;
        if (buttonRoot == null)
            return;

        bool isLeft = string.Equals(name, "ButtonPrev", System.StringComparison.Ordinal);
        buttonRoot.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
        buttonRoot.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f);
        buttonRoot.pivot = new Vector2(isLeft ? 0f : 1f, 0f);
        buttonRoot.anchoredPosition = new Vector2(isLeft ? 16f : -16f, 16f);
        buttonRoot.sizeDelta = new Vector2(width, height);

        TMP_Text buttonText = buttonRoot.GetComponentInChildren<TMP_Text>(true);
        if (buttonText != null)
            buttonText.text = labelText;
    }

    private static void StyleCardSurface(RectTransform root, Color backgroundColor, Color accentColor)
    {
        if (root == null)
            return;

        Image rootImage = EnsureComponent<Image>(root.gameObject);
        UIArtUtility.ApplyImageStyle(rootImage, backgroundColor, false, Image.Type.Sliced, GetBuiltinPanelSprite());

        RectTransform accent = FindDescendant(root, "AccentBar") as RectTransform;
        if (accent == null && !UIArtUtility.AllowsGeneratedDecor(root))
            return;

        if (accent == null)
        {
            GameObject accentObject = new GameObject("AccentBar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            accent = accentObject.GetComponent<RectTransform>();
            accent.SetParent(root, false);
            accent.SetAsFirstSibling();
            EnsureLayoutElement(accentObject, true);
        }

        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.anchoredPosition = new Vector2(0f, -6f);
        accent.sizeDelta = new Vector2(-24f, 6f);

        Image accentImage = EnsureComponent<Image>(accent.gameObject);
        UIArtUtility.ApplyImageStyle(accentImage, accentColor, false, Image.Type.Sliced, GetBuiltinPanelSprite());
    }

    private static RectTransform EnsureCardRoot(RectTransform parent, string name, Color backgroundColor, Color accentColor)
    {
        RectTransform card = FindDescendant(parent, name) as RectTransform;
        if (card == null)
        {
            GameObject cardObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            card = cardObject.GetComponent<RectTransform>();
            card.SetParent(parent, false);
        }

        StyleCardSurface(card, backgroundColor, accentColor);
        return card;
    }

    private static void StyleHeaderPrimaryText(TMP_Text text)
    {
        if (text == null || UIArtUtility.ShouldPreserveTextStyling(text))
            return;

        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = 30f;
        text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        text.color = new Color(0.99f, 0.93f, 0.78f, 1f);
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0.1f, 0.07f, 0.03f, 0.82f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void StyleHeaderSecondaryText(TMP_Text text)
    {
        if (text == null || UIArtUtility.ShouldPreserveTextStyling(text))
            return;

        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 20f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.84f, 0.85f, 0.8f, 1f);
        text.outlineWidth = 0.12f;
        text.outlineColor = new Color(0.06f, 0.06f, 0.06f, 0.72f);
        text.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void StyleHeaderTertiaryText(TMP_Text text)
    {
        if (text == null || UIArtUtility.ShouldPreserveTextStyling(text))
            return;

        text.alignment = TextAlignmentOptions.BottomLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 15f;
        text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        text.color = new Color(0.86f, 0.95f, 0.89f, 1f);
        text.outlineWidth = 0.1f;
        text.outlineColor = new Color(0.04f, 0.06f, 0.05f, 0.7f);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.lineSpacing = 2f;
    }

    private static void StyleOrbInfoText(TMP_Text text)
    {
        if (text == null || UIArtUtility.ShouldPreserveTextStyling(text))
            return;

        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 22f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.92f, 0.97f, 0.92f, 1f);
        text.outlineWidth = 0.14f;
        text.outlineColor = new Color(0.05f, 0.08f, 0.05f, 0.8f);
        text.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void StretchInsideCard(RectTransform rect, float left, float top, float right, float bottom)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.pivot = new Vector2(0.5f, 0.5f);
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

    private static CanvasGroup EnsureCanvasGroup(GameObject gameObject)
    {
        if (gameObject == null)
            return null;

        CanvasGroup group = gameObject.GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        return group;
    }

    private static LayoutElement EnsureLayoutElement(GameObject gameObject, bool ignoreLayout)
    {
        LayoutElement layout = EnsureComponent<LayoutElement>(gameObject);
        if (layout != null)
            layout.ignoreLayout = ignoreLayout;
        return layout;
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

    private static void DisableIfPresent<T>(Component component) where T : Behaviour
    {
        if (component == null)
            return;

        T behaviour = component.GetComponent<T>();
        if (behaviour != null)
            behaviour.enabled = false;
    }

    private static Sprite GetBuiltinPanelSprite()
    {
        return UIArtUtility.BuiltinPanelSprite;
    }
}
