using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text label;

    [Header("Format")]
    [SerializeField] private string prefix = "HP";
    [SerializeField] private bool showPercent = true;

    private int lastValue = -1;
    private int lastMax = 1;

    private Image trackImage;
    private Image fillImage;
    private Image highlightImage;
    private Image frameImage;
    private TMP_Text valueLabel;

    private Color fillColor = new Color(0.34f, 0.76f, 0.45f, 1f);
    private Color trackColor = new Color(0.12f, 0.18f, 0.12f, 0.96f);
    private Color labelColor = new Color(0.97f, 0.96f, 0.88f, 1f);
    private Color accentColor = new Color(0.86f, 0.96f, 0.88f, 0.9f);

    public Slider Slider => slider;
    public TMP_Text Label => valueLabel;
    public string Prefix => prefix;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        EnsureReferences();
        EnsureVisuals();
        RefreshTheme();
    }

    private void OnDisable()
    {
        if (slider != null && slider.fillRect != null)
            slider.fillRect.DOKill(false);
    }

    public void ApplyTheme(Color fill, Color track, Color labelTint, Color accent)
    {
        fillColor = fill;
        trackColor = track;
        labelColor = labelTint;
        accentColor = accent;

        EnsureReferences();
        EnsureVisuals();
        RefreshTheme();
        RefreshLabelColor(lastMax > 0 ? lastValue / (float)lastMax : 1f);
    }

    public void Set(int current, int max)
    {
        EnsureReferences();

        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);
        lastMax = max;

        EnsureVisuals();

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = max;
            slider.value = current;
        }

        float ratio = current / (float)max;
        UpdateLabel(current, max, ratio);
        RefreshLabelColor(ratio);
        RefreshFillColor(ratio);

        if (Application.isPlaying && current != lastValue && slider != null && slider.fillRect != null)
        {
            slider.fillRect.DOKill(false);
            slider.fillRect.localScale = Vector3.one;
            slider.fillRect.DOPunchScale(new Vector3(0.04f, 0.055f, 0f), 0.18f, 5, 0.7f)
                .SetUpdate(true)
                .SetLink(slider.fillRect.gameObject, LinkBehaviour.KillOnDestroy);
        }

        lastValue = current;
    }

    private void EnsureReferences()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        ResolveValueLabel();
    }

    private void EnsureVisuals()
    {
        if (slider == null)
            return;

        RectTransform sliderRect = slider.transform as RectTransform;
        if (sliderRect == null)
            return;

        trackImage = FindTrackImage(sliderRect);
        fillImage = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;

        RectTransform fillArea = slider.fillRect != null ? slider.fillRect.parent as RectTransform : null;
        if (fillArea != null)
        {
            fillArea.anchorMin = new Vector2(0f, 0f);
            fillArea.anchorMax = new Vector2(1f, 1f);
            fillArea.offsetMin = new Vector2(6f, 4f);
            fillArea.offsetMax = new Vector2(-6f, -4f);
            fillArea.anchoredPosition = Vector2.zero;
        }

        bool allowGeneratedDecor = UIArtUtility.AllowsGeneratedDecor(sliderRect)
            && !UIArtUtility.HasCustomSprite(trackImage)
            && !UIArtUtility.HasCustomSprite(fillImage);

        if (slider.fillRect != null)
        {
            slider.fillRect.anchorMin = new Vector2(0f, 0f);
            slider.fillRect.anchorMax = new Vector2(0f, 1f);
            slider.fillRect.offsetMin = Vector2.zero;
            slider.fillRect.offsetMax = Vector2.zero;
            slider.fillRect.pivot = new Vector2(0f, 0.5f);

            highlightImage = allowGeneratedDecor
                ? EnsureNamedImage(slider.fillRect, "Highlight", 0)
                : FindNamedImage(slider.fillRect, "Highlight");
            if (highlightImage != null)
            {
                highlightImage.rectTransform.anchorMin = new Vector2(0f, 0.58f);
                highlightImage.rectTransform.anchorMax = new Vector2(1f, 1f);
                highlightImage.rectTransform.offsetMin = new Vector2(0f, -1f);
                highlightImage.rectTransform.offsetMax = new Vector2(0f, -1f);
                highlightImage.raycastTarget = false;
            }
        }

        frameImage = allowGeneratedDecor
            ? EnsureNamedImage(sliderRect, "Frame", sliderRect.childCount)
            : FindNamedImage(sliderRect, "Frame");
        if (frameImage != null)
        {
            frameImage.rectTransform.anchorMin = Vector2.zero;
            frameImage.rectTransform.anchorMax = Vector2.one;
            frameImage.rectTransform.offsetMin = Vector2.zero;
            frameImage.rectTransform.offsetMax = Vector2.zero;
            frameImage.raycastTarget = false;
        }

        if (valueLabel != null)
        {
            RectTransform valueLabelRect = valueLabel.rectTransform;
            valueLabelRect.SetParent(sliderRect, false);
            valueLabelRect.SetAsLastSibling();
            valueLabelRect.anchorMin = Vector2.zero;
            valueLabelRect.anchorMax = Vector2.one;
            valueLabelRect.offsetMin = new Vector2(12f, 0f);
            valueLabelRect.offsetMax = new Vector2(-12f, 0f);
            valueLabelRect.pivot = new Vector2(0.5f, 0.5f);

            if (!UIArtUtility.ShouldPreserveTextStyling(valueLabel))
            {
                valueLabel.fontStyle = FontStyles.Bold;
                valueLabel.enableAutoSizing = true;
                valueLabel.fontSizeMin = 10f;
                valueLabel.fontSizeMax = 15f;
                valueLabel.alignment = TextAlignmentOptions.Center;
                valueLabel.outlineWidth = 0.18f;
                valueLabel.outlineColor = new Color(0.05f, 0.05f, 0.05f, 0.82f);
            }

            valueLabel.raycastTarget = false;
            if (!UIArtUtility.ShouldPreserveTextStyling(valueLabel))
            {
                valueLabel.textWrappingMode = TextWrappingModes.NoWrap;
                valueLabel.overflowMode = TextOverflowModes.Ellipsis;
            }
        }
    }

    private void RefreshTheme()
    {
        if (trackImage != null)
            UIArtUtility.ApplyImageStyle(trackImage, trackColor, false, Image.Type.Sliced, UIArtUtility.BuiltinPanelSprite);

        if (fillImage != null)
            UIArtUtility.ApplyImageStyle(fillImage, fillColor, false, Image.Type.Sliced, UIArtUtility.BuiltinPanelSprite);

        if (highlightImage != null)
        {
            UIArtUtility.ApplyImageStyle(highlightImage, new Color(1f, 1f, 1f, 0.18f), false, Image.Type.Sliced, UIArtUtility.BuiltinPanelSprite);
            if (!UIArtUtility.ShouldPreserveSprite(highlightImage))
                highlightImage.fillCenter = true;
        }

        if (frameImage != null)
        {
            UIArtUtility.ApplyImageStyle(frameImage, new Color(accentColor.r, accentColor.g, accentColor.b, 0.42f), false, Image.Type.Sliced, UIArtUtility.BuiltinPanelSprite);
            if (!UIArtUtility.ShouldPreserveSprite(frameImage))
                frameImage.fillCenter = false;
        }

        if (valueLabel != null && !UIArtUtility.ShouldPreserveTextStyling(valueLabel))
            valueLabel.color = labelColor;
    }

    private void UpdateLabel(int current, int max, float ratio)
    {
        if (valueLabel == null)
            return;

        if (showPercent && max > 0 && CanShowPercent())
        {
            int percent = Mathf.RoundToInt(ratio * 100f);
            valueLabel.text = $"{current}/{max}  {percent}%";
            return;
        }

        valueLabel.text = $"{current}/{max}";
    }

    private bool CanShowPercent()
    {
        RectTransform sliderRect = slider != null ? slider.transform as RectTransform : null;
        return sliderRect == null || sliderRect.rect.width >= 150f;
    }

    private void RefreshLabelColor(float ratio)
    {
        if (valueLabel == null || UIArtUtility.ShouldPreserveColor(valueLabel))
            return;

        bool critical = ratio > 0f && ratio <= 0.3f;
        valueLabel.color = critical
            ? Color.Lerp(labelColor, new Color(1f, 0.82f, 0.8f, 1f), 0.45f)
            : labelColor;
    }

    private void RefreshFillColor(float ratio)
    {
        if (fillImage == null || UIArtUtility.ShouldPreserveColor(fillImage))
            return;

        bool critical = ratio > 0f && ratio <= 0.3f;
        fillImage.color = critical
            ? Color.Lerp(fillColor, new Color(1f, 0.36f, 0.3f, 1f), 0.4f)
            : fillColor;
    }

    private static Image FindTrackImage(RectTransform sliderRect)
    {
        Transform background = sliderRect.Find("Background");
        if (background != null)
        {
            Image image = background.GetComponent<Image>();
            if (image != null)
                return image;
        }

        return sliderRect.GetComponent<Image>();
    }

    private void ResolveValueLabel()
    {
        if (slider == null)
            return;

        if (label != null && label.transform.IsChildOf(slider.transform))
        {
            valueLabel = label;
            return;
        }

        Transform existing = slider.transform.Find("ValueLabel");
        if (existing == null)
        {
            GameObject valueLabelObject = new GameObject("ValueLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = valueLabelObject.GetComponent<RectTransform>();
            rect.SetParent(slider.transform, false);
            valueLabel = valueLabelObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            valueLabel = existing.GetComponent<TextMeshProUGUI>();
            if (valueLabel == null)
                valueLabel = existing.gameObject.AddComponent<TextMeshProUGUI>();
        }
    }

    private static Image EnsureNamedImage(RectTransform parent, string objectName, int siblingIndex)
    {
        Transform existing = parent.Find(objectName);
        RectTransform rect;

        if (existing == null)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
        }
        else
        {
            rect = existing as RectTransform;
        }

        rect.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
        return EnsureComponent<Image>(rect.gameObject);
    }

    private static Image FindNamedImage(RectTransform parent, string objectName)
    {
        Transform existing = parent != null ? parent.Find(objectName) : null;
        return existing != null ? existing.GetComponent<Image>() : null;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        if (gameObject == null)
            return null;

        T component = gameObject.GetComponent<T>();
        if (component == null)
            component = gameObject.AddComponent<T>();

        return component;
    }
}
