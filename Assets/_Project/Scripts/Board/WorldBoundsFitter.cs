using UnityEngine;

public class WorldBoundsFitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform ceiling;
    [SerializeField] private Transform leftWall;
    [SerializeField] private Transform rightWall;

    [Header("Board Area Source (reusa lo del BoardManager)")]
    [SerializeField] private BoardConfig boardConfig;

    [Header("Sizing")]
    [SerializeField] private float wallThickness = 1f;
    [SerializeField] private float ceilingThickness = 1f;

    [Header("Padding")]
    [SerializeField] private float topPadding = 0.25f;
    [SerializeField] private float sidePadding = 0.25f;
    [SerializeField] private float bottomPadding = 0.5f;

    [Header("World Height")]
    [SerializeField] private float worldBottomY = -6f;

    [Header("Auto Refit")]
    [SerializeField] private bool autoRefitOnChanges = true;

    private Rect lastCamRect;
    private int lastW;
    private int lastH;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        CacheState();
    }

    private void OnEnable()
    {
        Fit();
        CacheState();
    }

    private void Update()
    {
        if (!autoRefitOnChanges || cam == null) return;

        if (Screen.width != lastW || Screen.height != lastH)
        {
            Fit();
            CacheState();
            return;
        }

        if (cam.rect != lastCamRect)
        {
            Fit();
            CacheState();
        }
    }

    private void CacheState()
    {
        lastW = Screen.width;
        lastH = Screen.height;
        if (cam != null) lastCamRect = cam.rect;
    }

    [ContextMenu("Fit Now")]
    public void Fit()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Rect vr = cam.rect;

        

        // --- TOP: respeta BoardConfig (debajo del HUD) ---
        float vTopY = vr.yMax;
        if (boardConfig != null)
        {
            vTopY = Mathf.Lerp(vr.yMin, vr.yMax, boardConfig.viewportMaxY);
        }

        // --- LEFT / RIGHT: pantalla completa ---
        float vLeftX = vr.xMin;
        float vRightX = vr.xMax;

        // Convertir a mundo
        float topY = cam.ViewportToWorldPoint(new Vector3(0.5f, vTopY, 0f)).y;
        float leftX = cam.ViewportToWorldPoint(new Vector3(vLeftX, 0.5f, 0f)).x;
        float rightX = cam.ViewportToWorldPoint(new Vector3(vRightX, 0.5f, 0f)).x;

        // Aplicar padding
        float targetTopY = topY + topPadding;
        float targetLeftX = leftX - sidePadding;
        float targetRightX = rightX + sidePadding;

        float bottomY = worldBottomY - bottomPadding;

        // Techo
        if (ceiling != null)
        {
            ceiling.position = new Vector3(0f, targetTopY, 0f);

            float width = (targetRightX - targetLeftX) + wallThickness;
            ceiling.localScale = new Vector3(width, ceilingThickness, 1f);
        }

        // Paredes
        float wallHeight = Mathf.Max(1f, (targetTopY - bottomY));
        float wallCenterY = bottomY + wallHeight * 0.5f;

        if (leftWall != null)
        {
            leftWall.position = new Vector3(targetLeftX, wallCenterY, 0f);
            leftWall.localScale = new Vector3(wallThickness, wallHeight, 1f);
        }

        if (rightWall != null)
        {
            rightWall.position = new Vector3(targetRightX, wallCenterY, 0f);
            rightWall.localScale = new Vector3(wallThickness, wallHeight, 1f);
        }
    }
}
