using UnityEngine;

public class WorldBoundsFitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform ceiling;
    [SerializeField] private Transform leftWall;
    [SerializeField] private Transform rightWall;

    [Header("Sizing")]
    [SerializeField] private float wallThickness = 1f;      // ancho de paredes (world units)
    [SerializeField] private float ceilingThickness = 1f;   // alto del techo (world units)

    [Header("Padding")]
    [SerializeField] private float topPadding = 0.25f;
    [SerializeField] private float sidePadding = 0.25f;
    [SerializeField] private float bottomPadding = 0.5f;

    [Header("World Height")]
    [SerializeField] private float worldBottomY = -6f;      // 

    private void Start()
    {
        Fit();
    }

    [ContextMenu("Fit Now")]
    public void Fit()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Bordes visibles del mundo 
        float topY = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, 0f)).y;
        float leftX = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, 0f)).x;
        float rightX = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, 0f)).x;

        float targetTopY = topY + topPadding;
        float targetLeftX = leftX - sidePadding;
        float targetRightX = rightX + sidePadding;

        float bottomY = worldBottomY - bottomPadding;

        // Techo: ancho = distancia entre paredes + margen; alto = ceilingThickness
        if (ceiling != null)
        {
            ceiling.position = new Vector3(0f, targetTopY, 0f);

            float width = (targetRightX - targetLeftX) + wallThickness; 
            ceiling.localScale = new Vector3(width, ceilingThickness, 1f);
        }

        // Paredes: alto = desde bottom hasta techo; ancho = wallThickness
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
