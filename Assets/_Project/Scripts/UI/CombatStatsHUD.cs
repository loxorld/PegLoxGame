using UnityEngine;
using TMPro;
using System.Collections;
using DG.Tweening;

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
    private RectTransform rectTransform;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        rectTransform = transform as RectTransform;

        SetDefaultTexts();

        
        // Solo ocultamos visualmente, asi sigue escuchando eventos.
        SetVisible(!hideWhenNoShot);
    }

    private void OnEnable()
    {
        // Intento inmediato
        TrySubscribe();

        // Si no existe ShotManager todavia, reintentamos hasta que aparezca
        if (!subscribed && subscribeRoutine == null)
            subscribeRoutine = StartCoroutine(WaitAndSubscribe());
    }

    private void Start()
    {
        // Reintento extra por orden de ejecucion
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
        rectTransform?.DOKill(false);
        canvasGroup?.DOKill(false);
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

        if (hitsText) hitsText.text = BuildMetric(hitsText, "IMPACTOS", s.TotalHits.ToString(), "#F6DE9A");
        if (critText) critText.text = BuildMetric(critText, "CRITICOS", s.CriticalHits.ToString(), "#F2A38D");
        if (multText) multText.text = BuildMetric(multText, "MULT", $"x{s.Multiplier}", "#9FD9D8");
        if (dmgText) dmgText.text = BuildMetric(dmgText, "DANO", s.PredictedDamage.ToString(), "#F3C574");

        PulsePanel();
    }

    private void OnShotResolved(ShotSummary s)
    {
        // Dejamos el final visible
        if (dmgText) dmgText.text = BuildMetric(dmgText, "DANO", s.PredictedDamage.ToString(), "#F3C574");
    }

    private void SetDefaultTexts()
    {
        if (hitsText) hitsText.text = BuildMetric(hitsText, "IMPACTOS", "0", "#F6DE9A");
        if (critText) critText.text = BuildMetric(critText, "CRITICOS", "0", "#F2A38D");
        if (multText) multText.text = BuildMetric(multText, "MULT", "x1", "#9FD9D8");
        if (dmgText) dmgText.text = BuildMetric(dmgText, "DANO", "0", "#F3C574");
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;

        canvasGroup.alpha = visible ? 1f : (hideWhenNoShot ? 0.62f : 1f);

        // HUD informativo: no debe bloquear input
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void PulsePanel()
    {
        if (canvasGroup != null)
        {
            canvasGroup.DOKill(false);
            canvasGroup.alpha = 0.82f;
            canvasGroup.DOFade(1f, 0.12f).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        if (rectTransform == null)
            return;

        rectTransform.DOKill(false);
        rectTransform.localScale = Vector3.one;
        rectTransform.DOPunchScale(new Vector3(0.035f, 0.05f, 0f), 0.2f, 5, 0.65f)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    private static string BuildMetric(TMP_Text target, string label, string value, string accentHex)
    {
        string plainText = $"{label}  {value}";
        string richText = $"<color=#DCE4E7CC>{label}</color>  <b><color={accentHex}>{value}</color></b>";
        return UIArtUtility.ResolveDynamicText(target, plainText, richText);
    }
}

