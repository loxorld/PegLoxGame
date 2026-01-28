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

    private readonly Queue<DamagePopup> pool = new();
    private bool subscribed;
    private Coroutine subscribeRoutine;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;

        for (int i = 0; i < prewarm; i++)
            pool.Enqueue(CreatePopup());
    }

    private void OnEnable()
    {
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

    private void OnShotResolved(ShotSummary s)
    {
        if (battle == null || battle.CurrentEnemy == null) return;

        int damage = s.PredictedDamage;
        Spawn(battle.CurrentEnemy.transform.position, damage);
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
}
