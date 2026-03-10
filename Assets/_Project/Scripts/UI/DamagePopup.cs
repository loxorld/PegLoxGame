using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DamagePopup : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float floatUp = 80f;
    [SerializeField] private Color damageColor = new Color(1f, 0.82f, 0.72f, 1f);

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
        if (!UIArtUtility.ShouldPreserveColor(text))
            text.color = damageColor;
        if (!UIArtUtility.ShouldPreserveTextStyling(text))
        {
            text.fontSize = 36f;
            text.fontStyle = FontStyles.Bold;
        }
        StopAllCoroutines();
        StartCoroutine(Animate());
    }

    public void ShowMessage(string message, Color color, float size = 28f)
    {
        text.text = message;
        if (!UIArtUtility.ShouldPreserveColor(text))
            text.color = color;
        if (!UIArtUtility.ShouldPreserveTextStyling(text))
        {
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        }
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
