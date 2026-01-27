using UnityEngine;
using TMPro;

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

    private bool hasEverReceivedStats;

    private void Awake()
    {
        
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetTextsDefault();

        // Importante: NO desactivar el GameObject.
        // Solo ocultamos visualmente.
        if (hideWhenNoShot)
            SetVisible(false);
        else
            SetVisible(true);
    }

    private void OnEnable()
    {
        if (ShotManager.Instance != null)
        {
            ShotManager.Instance.ShotStatsChanged += OnShotStatsChanged;
            ShotManager.Instance.ShotResolved += OnShotResolved;
        }
    }

    private void OnDisable()
    {
        if (ShotManager.Instance != null)
        {
            ShotManager.Instance.ShotStatsChanged -= OnShotStatsChanged;
            ShotManager.Instance.ShotResolved -= OnShotResolved;
        }
    }

    private void OnShotStatsChanged(ShotSummary s)
    {
        hasEverReceivedStats = true;

        if (hideWhenNoShot) SetVisible(true);

        if (hitsText) hitsText.text = $"Hits: {s.TotalHits}";
        if (critText) critText.text = $"Crit: {s.CriticalHits}";
        if (multText) multText.text = $"x{s.Multiplier}";
        if (dmgText) dmgText.text = $"DMG: {s.PredictedDamage}";
    }

    private void OnShotResolved(ShotSummary s)
    {
        // Dejamos el valor final mostrado (opcional: ocultar luego)
        if (dmgText) dmgText.text = $"DMG: {s.PredictedDamage}";
    }

    private void SetTextsDefault()
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
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
