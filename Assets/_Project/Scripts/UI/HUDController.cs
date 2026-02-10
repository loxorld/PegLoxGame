using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats player;
    [SerializeField] private BattleManager battle;
    [SerializeField] private OrbManager orbs;
    [SerializeField] private GameFlowManager flow;

    [Header("UI Text (optional)")]
    [SerializeField] private TMP_Text orbNameText;
    [SerializeField] private TMP_Text stateText;

    [Header("Encounter / Difficulty (optional)")]
    [SerializeField] private TMP_Text encounterText;
    [SerializeField] private TMP_Text difficultyText;
    [SerializeField] private TMP_Text coinsText;

    [Header("Bars")]
    [SerializeField] private HealthBarUI playerBar;
    [SerializeField] private HealthBarUI enemyBar;

    [Header("Enemy bar follow")]
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

    private void ResolveReferences()
    {
        if (player == null)
            player = FindObjectOfType<PlayerStats>(true);

        if (battle == null)
            battle = FindObjectOfType<BattleManager>(true);

        if (orbs == null)
            orbs = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);

        if (flow == null)
            flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
    }

    private void Awake()
    {
        ResolveReferences();

        if (enemyBar != null)
            enemyBarRect = enemyBar.GetComponent<RectTransform>();

        if (enemyBarRect != null)
        {
            rootCanvas = enemyBarRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                originalEnemyBarParent = enemyBarRect.parent;
                originalEnemyBarSiblingIndex = enemyBarRect.GetSiblingIndex();
                enemyBarRect.SetParent(rootCanvas.transform, true);

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

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (hudTransform != null)
            hudTransform.localScale = Vector3.one * 0.95f;

        lastState = flow != null ? flow.State : GameState.Combat;
    }

    private void OnEnable()
    {
        ResolveReferences();

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

        if (enemyBar != null)
        {
            if (!enemyVisible)
                enemyBar.Set(0, 1);
            else
                enemyBar.Set(e.CurrentHP, e.MaxHP);
        }

        UpdateEnemyBarPosition(e);

        if (orbNameText != null)
        {
            OrbInstance orb = (orbs != null) ? orbs.CurrentOrb : null;
            string nextText = orb != null
                ? $"Orb: {orb.OrbName} Lv {orb.Level} | Dano {orb.DamagePerHit}"
                : "Orb: -";
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
                GameState.RewardChoice => "REWARD",
                GameState.Paused => "PAUSED",
                GameState.GameOver => "GAME OVER",
                _ => s.ToString()
            };
            if (stateText.text != nextText)
            {
                stateText.text = nextText;
                layoutDirty = true;
            }

            if (s != lastState && (s == GameState.Paused || s == GameState.GameOver))
                stateText.rectTransform.DOShakePosition(0.3f, 6f, 15, 90);

            lastState = s;
        }

        if (coinsText != null)
        {
            int coins = flow != null ? flow.Coins : 0;
            string nextText = $"Monedas: {coins}";
            if (coinsText.text != nextText)
            {
                coinsText.text = nextText;
                layoutDirty = true;
            }
        }

        if (encounterText != null)
        {
            string nextText = battle == null ? "" : $"ENCOUNTER: {battle.EncounterIndex + 1}";
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
                nextText = "";
            else if (battle.HasDifficultyConfig)
                nextText = $"STAGE: {battle.DifficultyHudText}";
            else
                nextText = $"STAGE: {battle.StageName}";

            if (difficultyText.text != nextText)
            {
                difficultyText.text = nextText;
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
        if (enemyBarRect == null)
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
}
