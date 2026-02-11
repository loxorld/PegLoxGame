using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallLifecycle : MonoBehaviour
{
    [Header("Out of bounds")]
    [SerializeField] private BoardBoundsProvider boundsProvider;
    [SerializeField] private float minY = -6f;
    [SerializeField] private float maxY = 12f;
    [SerializeField] private float minX = -7f;
    [SerializeField] private float maxX = 7f;
    [SerializeField] private bool despawnOnSideBounds = false;
    [SerializeField] private float horizontalBoundsPadding = 0.6f;

    [Header("Stop detection")]
    [SerializeField] private float minSpeedToConsiderStopped = 0.2f;
    [SerializeField] private float stoppedTimeToEndShot = 1.0f;

    [Header("Lifetime (hard cap)")]
    [SerializeField, Min(0.1f)] private float maxLifetimeSeconds = 10f;

    private Rigidbody2D rb;
    private Collider2D col;
    private float stoppedTimer;

    private float lifeTimer;     
    private bool hasEnded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (boundsProvider == null)
            boundsProvider = ServiceRegistry.ResolveWithFallback(nameof(BallLifecycle), nameof(boundsProvider), () => ServiceRegistry.LegacyFind<BoardBoundsProvider>());
    }

    private void OnEnable()
    {
        hasEnded = false;
        stoppedTimer = 0f;
        lifeTimer = 0f;
    }

    private void Update()
    {
        if (hasEnded) return;

        // 0) TTL: si pasó demasiado tiempo, terminamos igual
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifetimeSeconds)
        {
            EndShot();
            return;
        }

        // 1) Si se fue del tablero
        Vector3 p = transform.position;
        float currentMinY = minY;
        float currentMaxY = maxY;
        float currentMinX = minX;
        float currentMaxX = maxX;

        if (boundsProvider != null && boundsProvider.TryGetBounds(out Rect bounds))
        {
            currentMinX = bounds.xMin;
            currentMaxX = bounds.xMax;
            currentMinY = bounds.yMin;
            currentMaxY = bounds.yMax;
        }

        float extraX = 0f;
        if (despawnOnSideBounds)
        {
            extraX = horizontalBoundsPadding;
            if (col != null)
                extraX += col.bounds.extents.x;
        }

        if (p.y < currentMinY || p.y > currentMaxY || (despawnOnSideBounds && (p.x < currentMinX - extraX || p.x > currentMaxX + extraX)))
        {
            EndShot();
            return;
        }

        // 2) Si está casi quieta durante un tiempo
        if (rb.linearVelocity.magnitude < minSpeedToConsiderStopped)
        {
            stoppedTimer += Time.deltaTime;
            if (stoppedTimer >= stoppedTimeToEndShot)
            {
                EndShot();
            }
        }
        else
        {
            stoppedTimer = 0f;
        }
    }

    private void EndShot()
    {
        if (hasEnded) return;
        hasEnded = true;

        ShotManager.Instance?.OnShotEnded();
        Destroy(gameObject);
    }
}
