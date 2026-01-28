using UnityEngine;

public class WorldBoundsFitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform ceiling;
    [SerializeField] private Transform leftWall;
    [SerializeField] private Transform rightWall;

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

        // Si cambia la resolución (rotación, notch, etc.)
        if (Screen.width != lastW || Screen.height != lastH)
        {
            Fit();
            CacheState();
            return;
        }

        // Si cambia el viewport real de la cámara (cam.rect)
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

        Rect vis = CameraWorldRect.GetVisibleWorldRect(cam);

        float topY = vis.yMax;
        float leftX = vis.xMin;
        float rightX = vis.xMax;

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
