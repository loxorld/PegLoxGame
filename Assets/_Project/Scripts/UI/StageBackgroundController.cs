using UnityEngine;
using UnityEngine.UI;

public class StageBackgroundController : MonoBehaviour
{
    private const int FarBackgroundSortingOrder = -1002;
    private const int MidBackgroundSortingOrder = -1001;
    private const int WorldBackgroundSortingOrder = -1000;
    private const float FarBackgroundZ = 17f;
    private const float MidBackgroundZ = 16f;
    private const float WorldBackgroundZ = 15f;
    private const float LayerBobAmplitude = 0.3f;
    private const float LayerScrollAmplitude = 0.5f;
    private const float MainLayerParallaxFactor = 0.9f;
    private const float DefaultMidLayerParallaxFactor = 0.45f;
    private const float DefaultFarLayerParallaxFactor = 0.3f;
    private const float DefaultMidLayerSpeed = 0.18f;
    private const float DefaultFarLayerSpeed = 0.1f;
    private const int AmbientVfxSortingOrder = -999;
    private const float ViewportCoveragePadding = 0.2f;

    [System.Serializable]
    private struct StageBackgroundStyle
    {
        public Sprite sprite;
        public Sprite midLayer;
        public Sprite farLayer;
        public Color tint;
        public float scrollSpeedFar;
        public float scrollSpeedMid;
        public GameObject ambientVfxPrefab;
    }

    [SerializeField] private Image backgroundImage;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool enforceAmbientVfxBehindGameplay = true;
    [SerializeField]
    private StageBackgroundStyle[] stageStyles = new StageBackgroundStyle[]
    {
        new StageBackgroundStyle { sprite = null, tint = Color.white }, // Bosque
        new StageBackgroundStyle { sprite = null, tint = Color.white }, // Pantano
        new StageBackgroundStyle { sprite = null, tint = Color.white }  // Castillo
    };

    private int lastStageIndex = -1;
    private SpriteRenderer worldBackgroundRenderer;
    private SpriteRenderer midBackgroundRenderer;
    private SpriteRenderer farBackgroundRenderer;
    private Transform ambientVfxRoot;
    private GameObject ambientVfxInstance;

    private void Start()
    {
        if (applyOnStart)
            ApplyForCurrentStage();
    }

    private void Update()
    {
        AnimateBackgroundLayers();

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
        ValidateStageConsistency(stageIndex);
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

            DisableLayer(midBackgroundRenderer);
            DisableLayer(farBackgroundRenderer);
        }

        ReplaceAmbientVfx(style.ambientVfxPrefab);

        lastStageIndex = stageIndex;
    }

    private void ValidateStageConsistency(int flowStageIndex)
    {
        BattleManager battle = FindObjectOfType<BattleManager>();
        if (battle != null && battle.CurrentStageIndex != flowStageIndex)
        {
            Debug.LogWarning($"[StageBackground] Visual stage mismatch with battle scaling. FlowStage={flowStageIndex}, BattleStage={battle.CurrentStageIndex}.");
        }
    }

    private void ApplyToWorldBackground(StageBackgroundStyle style)
    {
        if (style.sprite == null && style.midLayer == null && style.farLayer == null)
        {
            DisableWorldBackground();
            return;
        }

        ApplyLayer(EnsureFarBackgroundRenderer(), style.farLayer, style.tint, FarBackgroundZ);
        ApplyLayer(EnsureMidBackgroundRenderer(), style.midLayer, style.tint, MidBackgroundZ);
        ApplyLayer(EnsureWorldBackgroundRenderer(), style.sprite, style.tint, WorldBackgroundZ);
    }

    private void ApplyLayer(SpriteRenderer renderer, Sprite sprite, Color tint, float zPosition)
    {
        if (renderer == null)
            return;

        if (sprite == null)
        {
            renderer.enabled = false;
            return;
        }

        renderer.enabled = true;
        renderer.sprite = sprite;
        renderer.color = tint;

        Camera targetCam = ResolveTargetCamera();
        if (targetCam == null)
            return;

        Vector3 camPos = targetCam.transform.position;
        renderer.transform.position = new Vector3(camPos.x, camPos.y, zPosition);

        FitLayerToCamera(renderer, targetCam, 0f, 0f);
    }

    private Camera ResolveTargetCamera()
    {
        Camera targetCam = Camera.main;
        if (targetCam != null)
            return targetCam;

        targetCam = FindObjectOfType<Camera>();
        if (targetCam == null)
            return null;

        return targetCam;
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
        }

        worldBackgroundRenderer.sortingOrder = WorldBackgroundSortingOrder;

        return worldBackgroundRenderer;
    }

    private SpriteRenderer EnsureMidBackgroundRenderer()
    {
        if (midBackgroundRenderer != null)
            return midBackgroundRenderer;

        Transform existing = transform.Find("StageBackground_Mid");
        if (existing != null)
            midBackgroundRenderer = existing.GetComponent<SpriteRenderer>();

        if (midBackgroundRenderer == null)
        {
            GameObject go = new GameObject("StageBackground_Mid");
            go.transform.SetParent(transform, false);
            midBackgroundRenderer = go.AddComponent<SpriteRenderer>();
        }

        midBackgroundRenderer.sortingOrder = MidBackgroundSortingOrder;
        return midBackgroundRenderer;
    }

    private SpriteRenderer EnsureFarBackgroundRenderer()
    {
        if (farBackgroundRenderer != null)
            return farBackgroundRenderer;

        Transform existing = transform.Find("StageBackground_Far");
        if (existing != null)
            farBackgroundRenderer = existing.GetComponent<SpriteRenderer>();

        if (farBackgroundRenderer == null)
        {
            GameObject go = new GameObject("StageBackground_Far");
            go.transform.SetParent(transform, false);
            farBackgroundRenderer = go.AddComponent<SpriteRenderer>();
        }

        farBackgroundRenderer.sortingOrder = FarBackgroundSortingOrder;
        return farBackgroundRenderer;
    }

    private void DisableWorldBackground()
    {
        DisableLayer(worldBackgroundRenderer);
        DisableLayer(midBackgroundRenderer);
        DisableLayer(farBackgroundRenderer);
    }

    private void DisableLayer(SpriteRenderer renderer)
    {
        if (renderer != null)
            renderer.enabled = false;
    }

    private void AnimateBackgroundLayers()
    {
        Camera targetCam = ResolveTargetCamera();
        if (targetCam == null)
            return;

        StageBackgroundStyle style = ResolveStyle(Mathf.Max(0, lastStageIndex));
        float midSpeed = style.scrollSpeedMid > 0f ? style.scrollSpeedMid : DefaultMidLayerSpeed;
        float farSpeed = style.scrollSpeedFar > 0f ? style.scrollSpeedFar : DefaultFarLayerSpeed;

        FitLayerToCamera(worldBackgroundRenderer, targetCam, LayerScrollAmplitude * MainLayerParallaxFactor, LayerBobAmplitude * MainLayerParallaxFactor);
        FitLayerToCamera(midBackgroundRenderer, targetCam, LayerScrollAmplitude * DefaultMidLayerParallaxFactor, LayerBobAmplitude * DefaultMidLayerParallaxFactor);
        FitLayerToCamera(farBackgroundRenderer, targetCam, LayerScrollAmplitude * DefaultFarLayerParallaxFactor, LayerBobAmplitude * DefaultFarLayerParallaxFactor);

        UpdateLayerParallax(worldBackgroundRenderer, targetCam, WorldBackgroundZ, MainLayerParallaxFactor, Mathf.Max(midSpeed, 0.05f));
        UpdateLayerParallax(midBackgroundRenderer, targetCam, MidBackgroundZ, DefaultMidLayerParallaxFactor, midSpeed);
        UpdateLayerParallax(farBackgroundRenderer, targetCam, FarBackgroundZ, DefaultFarLayerParallaxFactor, farSpeed);
    }

    private void FitLayerToCamera(SpriteRenderer renderer, Camera targetCam, float extraHorizontal, float extraVertical)
    {
        if (renderer == null || !renderer.enabled || renderer.sprite == null || targetCam == null)
            return;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
            return;

        float viewHeight = targetCam.orthographicSize * 2f;
        float viewWidth = viewHeight * targetCam.aspect;
        float paddedWidth = viewWidth + (extraHorizontal * 2f);
        float paddedHeight = viewHeight + (extraVertical * 2f);
        float scale = Mathf.Max(paddedWidth / spriteSize.x, paddedHeight / spriteSize.y);
        scale *= 1f + ViewportCoveragePadding;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void UpdateLayerParallax(SpriteRenderer renderer, Camera targetCam, float baseZ, float amplitudeFactor, float speed)
    {
        if (renderer == null || !renderer.enabled || renderer.sprite == null)
            return;

        Vector3 camPos = targetCam.transform.position;
        float t = Time.time;
        float bob = Mathf.Sin(t * speed * 0.75f) * LayerBobAmplitude * amplitudeFactor;
        float scroll = Mathf.Cos(t * speed) * LayerScrollAmplitude * amplitudeFactor;
        renderer.transform.position = new Vector3(camPos.x + scroll, camPos.y + bob, baseZ);
    }

    private Transform EnsureAmbientVfxRoot()
    {
        if (ambientVfxRoot != null)
            return ambientVfxRoot;

        Transform existing = transform.Find("StageAmbientVfx");
        if (existing != null)
        {
            ambientVfxRoot = existing;
            return ambientVfxRoot;
        }

        GameObject root = new GameObject("StageAmbientVfx");
        root.transform.SetParent(transform, false);
        ambientVfxRoot = root.transform;
        return ambientVfxRoot;
    }

    private void ReplaceAmbientVfx(GameObject vfxPrefab)
    {
        if (ambientVfxInstance != null)
            Destroy(ambientVfxInstance);

        ambientVfxInstance = null;
        if (vfxPrefab == null)
            return;

        Transform root = EnsureAmbientVfxRoot();
        ambientVfxInstance = Instantiate(vfxPrefab, root);
        ambientVfxInstance.name = vfxPrefab.name;
        ambientVfxInstance.transform.localPosition = Vector3.zero;
        ambientVfxInstance.transform.localRotation = Quaternion.identity;
        ambientVfxInstance.transform.localScale = Vector3.one;

        if (enforceAmbientVfxBehindGameplay)
            ConfigureAmbientVfxSorting(ambientVfxInstance);
    }

    private void ConfigureAmbientVfxSorting(GameObject root)
    {
        if (root == null)
            return;

        SpriteRenderer baseRenderer = EnsureWorldBackgroundRenderer();
        int sortingLayerId = baseRenderer != null ? baseRenderer.sortingLayerID : 0;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = AmbientVfxSortingOrder;
        }
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
        if (clamped != stageIndex)
            Debug.LogWarning($"[StageBackground] Stage style fallback. FlowStage={stageIndex}, AppliedStyleIndex={clamped}.");
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