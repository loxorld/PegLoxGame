using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DamagePopup : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float floatUp = 80f;

    private TextMeshProUGUI text;
    private RectTransform rt;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        rt = GetComponent<RectTransform>();
    }

    public void Show(int damage)
    {
        text.text = $"-{damage}";
        StopAllCoroutines();
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float t = 0f;
        Vector2 start = rt.anchoredPosition;
        Vector2 end = start + Vector2.up * floatUp;

        while (t < lifetime)
        {
            t += Time.unscaledDeltaTime;
            float a = t / lifetime;
            rt.anchoredPosition = Vector2.Lerp(start, end, a);
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
