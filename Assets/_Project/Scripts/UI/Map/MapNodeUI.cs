using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapNodeUI : MonoBehaviour
{
    public readonly struct Presentation
    {
        public Presentation(
            Vector2 size,
            Color normalColor,
            Color highlightedColor,
            Color pressedColor,
            Color disabledColor,
            Color labelColor,
            bool showLabel,
            bool isInteractable,
            float scale)
        {
            Size = size;
            NormalColor = normalColor;
            HighlightedColor = highlightedColor;
            PressedColor = pressedColor;
            DisabledColor = disabledColor;
            LabelColor = labelColor;
            ShowLabel = showLabel;
            IsInteractable = isInteractable;
            Scale = Mathf.Max(0.1f, scale);
        }

        public Vector2 Size { get; }
        public Color NormalColor { get; }
        public Color HighlightedColor { get; }
        public Color PressedColor { get; }
        public Color DisabledColor { get; }
        public Color LabelColor { get; }
        public bool ShowLabel { get; }
        public bool IsInteractable { get; }
        public float Scale { get; }
    }

    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image labelBackdropImage;
    [SerializeField] private Image nodeIconImage;
    [SerializeField] private Sprite combatSprite;
    [SerializeField] private Sprite eventSprite;
    [SerializeField] private Sprite shopSprite;
    [SerializeField] private Sprite bossSprite;

    private MapNodeData nodeData;

    public void Setup(MapNodeData data, System.Action<MapNodeData> callback)
    {
        Setup(data, callback, new Presentation(
            new Vector2(600f, 160f),
            Color.white,
            new Color(0.96f, 0.96f, 0.96f, 1f),
            new Color(0.78f, 0.78f, 0.78f, 1f),
            new Color(0.78f, 0.78f, 0.78f, 0.5f),
            new Color(0.2f, 0.2f, 0.2f, 1f),
            true,
            true,
            1f));
    }

    public void Setup(MapNodeData data, System.Action<MapNodeData> callback, Presentation presentation)
    {
        nodeData = data;
        if (label != null)
        {
            label.gameObject.SetActive(presentation.ShowLabel);
            if (presentation.ShowLabel)
            {
                ConfigureLabelBackdrop(presentation);
                label.text = data != null ? data.title : string.Empty;
                label.color = presentation.LabelColor;
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = Mathf.Clamp(presentation.Size.y * 0.42f, 14f, 26f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.alignment = TextAlignmentOptions.Center;
                label.outlineWidth = 0.14f;
                label.outlineColor = new Color(0.1f, 0.06f, 0.03f, 0.44f);
            }
        }

        if (labelBackdropImage != null)
            labelBackdropImage.gameObject.SetActive(presentation.ShowLabel);

        Sprite selectedSprite = data.nodeType switch
        {
            NodeType.Combat => combatSprite,
            NodeType.Event => eventSprite,
            NodeType.Shop => shopSprite,
            NodeType.Boss => bossSprite,
            _ => null
        };

        if (nodeIconImage != null)
        {
            nodeIconImage.sprite = selectedSprite;
            nodeIconImage.preserveAspect = true;
            nodeIconImage.color = presentation.IsInteractable ? presentation.NormalColor : presentation.DisabledColor;
        }

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
            rectTransform.sizeDelta = presentation.Size;

        transform.localScale = Vector3.one * presentation.Scale;

        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
            layoutElement.preferredWidth = presentation.Size.x;
            layoutElement.preferredHeight = presentation.Size.y;
        }

        if (button == null)
            return;

        ColorBlock colors = button.colors;
        colors.normalColor = presentation.NormalColor;
        colors.highlightedColor = presentation.HighlightedColor;
        colors.selectedColor = presentation.HighlightedColor;
        colors.pressedColor = presentation.PressedColor;
        colors.disabledColor = presentation.DisabledColor;
        button.colors = colors;
        button.interactable = presentation.IsInteractable;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            AudioManager.Instance?.PlaySfx(AudioEventId.UiClick);
            callback?.Invoke(nodeData);
        });
    }

    private void ConfigureLabelBackdrop(Presentation presentation)
    {
        if (labelBackdropImage == null)
            return;

        RectTransform backdropRect = labelBackdropImage.rectTransform;
        if (backdropRect != null)
        {
            backdropRect.anchorMin = new Vector2(0.5f, 0f);
            backdropRect.anchorMax = new Vector2(0.5f, 0f);
            backdropRect.pivot = new Vector2(0.5f, 0.5f);
            backdropRect.anchoredPosition = new Vector2(0f, -Mathf.Max(18f, presentation.Size.y * 0.46f));
            backdropRect.sizeDelta = new Vector2(
                Mathf.Max(96f, presentation.Size.x * 1.05f),
                Mathf.Clamp(presentation.Size.y * 0.42f, 24f, 40f));
        }

        labelBackdropImage.raycastTarget = false;
        labelBackdropImage.color = new Color(0.16f, 0.09f, 0.05f, presentation.IsInteractable ? 0.8f : 0.64f);
    }
}
