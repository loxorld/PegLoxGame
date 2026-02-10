using UnityEngine;
using UnityEngine.UI;

public class StageBackgroundController : MonoBehaviour
{
    [System.Serializable]
    private struct StageBackgroundStyle
    {
        public Sprite sprite;
        public Color tint;
    }

    [SerializeField] private Image backgroundImage;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField]
    private StageBackgroundStyle[] stageStyles = new StageBackgroundStyle[]
    {
        new StageBackgroundStyle { sprite = null, tint = new Color(0.20f, 0.35f, 0.24f, 1f) }, // Bosque
        new StageBackgroundStyle { sprite = null, tint = new Color(0.23f, 0.33f, 0.26f, 1f) }, // Pantano
        new StageBackgroundStyle { sprite = null, tint = new Color(0.28f, 0.28f, 0.36f, 1f) }  // Castillo
    };

    private int lastStageIndex = -1;

    private void Start()
    {
        if (applyOnStart)
            ApplyForCurrentStage();
    }

    private void Update()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null)
            return;

        int stageIndex = Mathf.Max(0, flow.CurrentStageIndex);
        if (stageIndex == lastStageIndex)
            return;

        ApplyForCurrentStage();
    }

    private void ApplyForCurrentStage()
    {
        if (backgroundImage == null)
            backgroundImage = ResolveBackgroundImage();
        if (backgroundImage == null)
            return;

        GameFlowManager flow = GameFlowManager.Instance;
        int stageIndex = flow != null ? Mathf.Max(0, flow.CurrentStageIndex) : 0;

        StageBackgroundStyle style = ResolveStyle(stageIndex);
        backgroundImage.sprite = style.sprite;
        backgroundImage.color = style.tint;
        lastStageIndex = stageIndex;
    }

    private StageBackgroundStyle ResolveStyle(int stageIndex)
    {
        if (stageStyles == null || stageStyles.Length == 0)
            return new StageBackgroundStyle { sprite = null, tint = Color.white };

        int clamped = Mathf.Clamp(stageIndex, 0, stageStyles.Length - 1);
        return stageStyles[clamped];
    }

    private Image ResolveBackgroundImage()
    {
        Image[] images = FindObjectsOfType<Image>(true);
        Image fallback = null;
        float bestArea = -1f;

        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img == null)
                continue;

            bool looksLikeBackground = img.name == "Background" || img.name == "BackgroundImage";
            if (!looksLikeBackground)
                continue;

            // Evita agarrar backgrounds internos de barras de vida/UI.
            if (img.GetComponentInParent<HealthBarUI>() != null)
                continue;
            if (img.GetComponentInParent<Slider>() != null)
                continue;

            RectTransform rect = img.transform as RectTransform;
            if (rect == null)
                continue;

            bool fullStretch = rect.anchorMin.x <= 0.001f && rect.anchorMin.y <= 0.001f
                && rect.anchorMax.x >= 0.999f && rect.anchorMax.y >= 0.999f;
            if (fullStretch)
                return img;

            float area = Mathf.Abs(rect.rect.width * rect.rect.height);
            if (area > bestArea)
            {
                bestArea = area;
                fallback = img;
            }
        }

        return fallback;
    }
}