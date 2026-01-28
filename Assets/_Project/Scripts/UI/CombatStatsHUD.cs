using UnityEngine;
using TMPro;
using System.Collections;

public class CombatStatsHUD : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text hitsText;
    [SerializeField] private TMP_Text critText;
    [SerializeField] private TMP_Text multText;
    [SerializeField] private TMP_Text dmgText;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenNoShot = true;
    [SerializeField] private CanvasGroup canvasGroup;

    private bool subscribed;
    private Coroutine subscribeRoutine;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetDefaultTexts();

        
        // Solo ocultamos visualmente, así sigue escuchando eventos.
        SetVisible(!hideWhenNoShot);
    }

    private void OnEnable()
    {
        // Intento inmediato
        TrySubscribe();

        // Si no existe ShotManager todavía, reintentamos hasta que aparezca
        if (!subscribed && subscribeRoutine == null)
            subscribeRoutine = StartCoroutine(WaitAndSubscribe());
    }

    private void Start()
    {
        // Reintento extra por orden de ejecució
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
        // Espera a que exista el singleton
        while (ShotManager.Instance == null)
            yield return null;

        TrySubscribe();
        subscribeRoutine = null;
    }

    private void TrySubscribe()
    {
        var sm = ShotManager.Instance;
        if (sm == null || subscribed) return;

        sm.ShotStatsChanged += OnShotStatsChanged;
        sm.ShotResolved += OnShotResolved;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        var sm = ShotManager.Instance;
        if (sm == null || !subscribed) { subscribed = false; return; }

        sm.ShotStatsChanged -= OnShotStatsChanged;
        sm.ShotResolved -= OnShotResolved;
        subscribed = false;
    }

    private void OnShotStatsChanged(ShotSummary s)
    {
        if (hideWhenNoShot) SetVisible(true);

        if (hitsText) hitsText.text = $"Hits: {s.TotalHits}";
        if (critText) critText.text = $"Crit: {s.CriticalHits}";
        if (multText) multText.text = $"x{s.Multiplier}";
        if (dmgText) dmgText.text = $"DMG: {s.PredictedDamage}";
    }

    private void OnShotResolved(ShotSummary s)
    {
        // Dejamos el final visible
        if (dmgText) dmgText.text = $"DMG: {s.PredictedDamage}";
    }

    private void SetDefaultTexts()
    {
        if (hitsText) hitsText.text = "Hits: 0";
        if (critText) critText.text = "Crit: 0";
        if (multText) multText.text = "x1";
        if (dmgText) dmgText.text = "DMG: 0";
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : 0f;

        // HUD informativo: no debe bloquear input
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
