using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class DamagePopupSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battle;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private DamagePopup popupPrefab;

    [Header("Pool")]
    [SerializeField] private int prewarm = 10;

    [Header("Alert Popups")]
    [SerializeField] private Vector2 alertOffset = new Vector2(0f, 52f);
    [SerializeField] private Color blockColor = new Color(0.72f, 0.88f, 1f, 1f);
    [SerializeField] private Color healColor = new Color(0.58f, 1f, 0.7f, 1f);
    [SerializeField] private Color rageColor = new Color(1f, 0.63f, 0.38f, 1f);
    [SerializeField] private Color phaseColor = new Color(1f, 0.42f, 0.54f, 1f);

    private readonly Queue<DamagePopup> pool = new();
    private bool subscribed;
    private Coroutine subscribeRoutine;
    private Enemy subscribedEnemy;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;

        for (int i = 0; i < prewarm; i++)
            pool.Enqueue(CreatePopup());
    }

    private void OnEnable()
    {
        SubscribeToBattle();
        TrySubscribe();

        if (!subscribed && subscribeRoutine == null)
            subscribeRoutine = StartCoroutine(WaitAndSubscribe());
    }

    private void Start()
    {
        if (!subscribed)
            TrySubscribe();
    }

    private void OnDisable()
    {
        if (subscribeRoutine != null)
        {
            StopCoroutine(subscribeRoutine);
            subscribeRoutine = null;
        }

        Unsubscribe();
        UnsubscribeFromEnemy();
        UnsubscribeFromBattle();
    }

    private IEnumerator WaitAndSubscribe()
    {
        while (ShotManager.Instance == null)
            yield return null;

        TrySubscribe();
        subscribeRoutine = null;
    }

    private void TrySubscribe()
    {
        var sm = ShotManager.Instance;
        if (sm == null || subscribed) return;

        sm.ShotResolved += OnShotResolved;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        var sm = ShotManager.Instance;
        if (sm == null || !subscribed) { subscribed = false; return; }

        sm.ShotResolved -= OnShotResolved;
        subscribed = false;
    }

    private void SubscribeToBattle()
    {
        if (battle == null)
            battle = ServiceRegistry.ResolveWithFallback(nameof(DamagePopupSpawner), nameof(battle), () => ServiceRegistry.LegacyFind<BattleManager>(true));

        if (battle == null)
            return;

        battle.CurrentEnemyChanged -= OnCurrentEnemyChanged;
        battle.CurrentEnemyChanged += OnCurrentEnemyChanged;
        OnCurrentEnemyChanged(battle.CurrentEnemy);
    }

    private void UnsubscribeFromBattle()
    {
        if (battle != null)
            battle.CurrentEnemyChanged -= OnCurrentEnemyChanged;
    }

    private void OnCurrentEnemyChanged(Enemy enemy)
    {
        if (enemy == subscribedEnemy)
            return;

        UnsubscribeFromEnemy();
        subscribedEnemy = enemy;

        if (subscribedEnemy != null)
            subscribedEnemy.CombatAlertRaised += OnEnemyCombatAlertRaised;
    }

    private void UnsubscribeFromEnemy()
    {
        if (subscribedEnemy != null)
            subscribedEnemy.CombatAlertRaised -= OnEnemyCombatAlertRaised;

        subscribedEnemy = null;
    }

    private void OnShotResolved(ShotSummary s)
    {
        if (battle == null || battle.CurrentEnemy == null) return;

        int damage = s.PredictedDamage;
        Spawn(battle.CurrentEnemy.transform.position, damage);
    }

    private void OnEnemyCombatAlertRaised(Enemy.CombatAlert alert)
    {
        if (subscribedEnemy == null)
            return;

        SpawnMessage(subscribedEnemy.transform.position, alertOffset, alert.Message, ResolveAlertColor(alert.Type), ResolveAlertFontSize(alert.Type));
    }

    private void Spawn(Vector3 worldPos, int damage)
    {
        if (popupPrefab == null || canvasRoot == null || worldCamera == null) return;

        DamagePopup p = pool.Count > 0 ? pool.Dequeue() : CreatePopup();
        p.gameObject.SetActive(true);

        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot,
            screenPos,
            null, // Canvas overlay
            out var localPoint
        );

        var rt = (RectTransform)p.transform;
        rt.SetParent(canvasRoot, false);
        rt.anchoredPosition = localPoint;

        p.Show(damage);

        StartCoroutine(ReturnWhenHidden(p));
    }

    private void SpawnMessage(Vector3 worldPos, Vector2 screenOffset, string message, Color color, float size)
    {
        if (string.IsNullOrWhiteSpace(message) || popupPrefab == null || canvasRoot == null || worldCamera == null)
            return;

        DamagePopup popup = pool.Count > 0 ? pool.Dequeue() : CreatePopup();
        popup.gameObject.SetActive(true);

        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screenPos, null, out Vector2 localPoint);

        RectTransform rectTransform = (RectTransform)popup.transform;
        rectTransform.SetParent(canvasRoot, false);
        rectTransform.anchoredPosition = localPoint + screenOffset;

        popup.ShowMessage(message, color, size);
        StartCoroutine(ReturnWhenHidden(popup));
    }

    private IEnumerator ReturnWhenHidden(DamagePopup p)
    {
        while (p.gameObject.activeSelf) yield return null;
        pool.Enqueue(p);
    }

    private DamagePopup CreatePopup()
    {
        DamagePopup p = Instantiate(popupPrefab, canvasRoot);
        p.gameObject.SetActive(false);
        return p;
    }

    private Color ResolveAlertColor(Enemy.CombatAlertType type)
    {
        return type switch
        {
            Enemy.CombatAlertType.Block => blockColor,
            Enemy.CombatAlertType.Heal => healColor,
            Enemy.CombatAlertType.Rage => rageColor,
            Enemy.CombatAlertType.Phase => phaseColor,
            _ => Color.white
        };
    }

    private float ResolveAlertFontSize(Enemy.CombatAlertType type)
    {
        return type == Enemy.CombatAlertType.Phase ? 34f : 26f;
    }
}
