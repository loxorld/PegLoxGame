using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HealthBarUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text label; // opcional: "HP 10/30"

    [Header("Format")]
    [SerializeField] private string prefix = "HP";

    private int lastValue = -1;

    private void Awake()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>();
    }

    public void Set(int current, int max)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);

        if (slider != null)
        {
            slider.minValue = 0;
            slider.maxValue = max;
            slider.value = current;

            if (current != lastValue && slider.fillRect != null)
                slider.fillRect.DOPunchScale(new Vector3(0.05f, 0.05f, 0f), 0.2f);
        }

        if (label != null)
            label.text = $"{prefix}: {current}/{max}";

        lastValue = current;
    }
}