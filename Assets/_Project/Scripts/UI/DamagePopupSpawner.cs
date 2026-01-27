using UnityEngine;
using System.Collections.Generic;

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

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;

        for (int i = 0; i < prewarm; i++)
            pool.Enqueue(CreatePopup());
    }

    private void OnEnable()
    {
        if (ShotManager.Instance != null)
            ShotManager.Instance.ShotResolved += OnShotResolved;
    }

    private void OnDisable()
    {
        if (ShotManager.Instance != null)
            ShotManager.Instance.ShotResolved -= OnShotResolved;
    }

    private void OnShotResolved(ShotSummary s)
    {
        if (battle == null || battle.CurrentEnemy == null) return;
        var enemyTf = battle.CurrentEnemy.transform;

        int damage = s.PredictedDamage;
        Spawn(enemyTf.position, damage);
    }

    private void Spawn(Vector3 worldPos, int damage)
    {
        if (popupPrefab == null || canvasRoot == null || worldCamera == null) return;

        DamagePopup p = pool.Count > 0 ? pool.Dequeue() : CreatePopup();
        p.gameObject.SetActive(true);

        // Convertir posición del mundo a posición en canvas
        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRoot,
            screenPos,
            null, // overlay
            out var localPoint
        );

        var rt = (RectTransform)p.transform;
        rt.SetParent(canvasRoot, false);
        rt.anchoredPosition = localPoint;

        p.Show(damage);

        // Cuando se desactiva, vuelve al pool (polling simple)
        // (lo hago con un watcher liviano)
        StartCoroutine(ReturnWhenHidden(p));
    }

    private System.Collections.IEnumerator ReturnWhenHidden(DamagePopup p)
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
