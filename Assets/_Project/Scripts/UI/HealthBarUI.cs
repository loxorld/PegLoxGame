using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text label; // opcional: "HP 10/30"

    [Header("Format")]
    [SerializeField] private string prefix = "HP";

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
        }

        if (label != null)
            label.text = $"{prefix}: {current}/{max}";
    }
}
