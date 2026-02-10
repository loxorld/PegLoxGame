using UnityEngine;
using UnityEngine.UI;

public class StageBackgroundController : MonoBehaviour
{
    private const int WorldBackgroundSortingOrder = -1000;
    private const float WorldBackgroundZ = 15f;

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
        new StageBackgroundStyle { sprite = null, tint = Color.white }, // Bosque
        new StageBackgroundStyle { sprite = null, tint = Color.white }, // Pantano
        new StageBackgroundStyle { sprite = null, tint = Color.white }  // Castillo
    };

    private int lastStageIndex = -1;
    private SpriteRenderer worldBackgroundRenderer;

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
        if (!IsValidBackgroundImage(backgroundImage))
            backgroundImage = ResolveBackgroundImage();

        GameFlowManager flow = GameFlowManager.Instance;
        int stageIndex = flow != null ? Mathf.Max(0, flow.CurrentStageIndex) : 0;
        StageBackgroundStyle style = ResolveStyle(stageIndex);

        Canvas parentCanvas = backgroundImage != null ? backgroundImage.canvas : null;
        bool isOverlayCanvas = parentCanvas != null
            && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay;

        if (isOverlayCanvas)
        {
            ApplyToWorldBackground(style);
            if (backgroundImage != null)
                backgroundImage.enabled = false;
        }
        else
        {
            DisableWorldBackground();
            if (backgroundImage != null)
            {
                if (style.sprite != null)
                    backgroundImage.sprite = style.sprite;
                backgroundImage.color = style.tint;
                backgroundImage.enabled = true;
            }
        }

        lastStageIndex = stageIndex;
    }

    private void ApplyToWorldBackground(StageBackgroundStyle style)
    {
        if (style.sprite == null)
        {
            DisableWorldBackground();
            return;
        }

        SpriteRenderer sr = EnsureWorldBackgroundRenderer();
        if (sr == null)
            return;

        sr.enabled = true;
        sr.sprite = style.sprite;
        sr.color = style.tint;

        Camera targetCam = Camera.main;
        if (targetCam == null)
            return;

        Vector3 camPos = targetCam.transform.position;
        sr.transform.position = new Vector3(camPos.x, camPos.y, WorldBackgroundZ);

        Vector2 spriteSize = sr.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
            return;

        float viewHeight = targetCam.orthographicSize * 2f;
        float viewWidth = viewHeight * targetCam.aspect;
        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private SpriteRenderer EnsureWorldBackgroundRenderer()
    {
        if (worldBackgroundRenderer != null)
            return worldBackgroundRenderer;

        Transform existing = transform.Find("StageBackground_World");
        if (existing != null)
            worldBackgroundRenderer = existing.GetComponent<SpriteRenderer>();

        if (worldBackgroundRenderer == null)
        {
            GameObject go = new GameObject("StageBackground_World");
            go.transform.SetParent(transform, false);
            worldBackgroundRenderer = go.AddComponent<SpriteRenderer>();
            worldBackgroundRenderer.sortingOrder = WorldBackgroundSortingOrder;
        }

        return worldBackgroundRenderer;
    }

    private void DisableWorldBackground()
    {
        if (worldBackgroundRenderer != null)
            worldBackgroundRenderer.enabled = false;
    }


    private bool IsValidBackgroundImage(Image img)
    {
        if (img == null)
            return false;

        bool looksLikeBackground = img.name == "Background" || img.name == "BackgroundImage";
        if (!looksLikeBackground)
            return false;

        if (img.GetComponentInParent<HealthBarUI>() != null)
            return false;
        if (img.GetComponentInParent<Slider>() != null)
            return false;

        return true;
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

            if (!IsValidBackgroundImage(img))
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