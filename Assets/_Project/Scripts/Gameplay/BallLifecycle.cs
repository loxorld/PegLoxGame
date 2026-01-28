using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BallLifecycle : MonoBehaviour
{
    [Header("Out of bounds")]
    [SerializeField] private float minY = -6f;
    [SerializeField] private float maxY = 12f;
    [SerializeField] private float minX = -7f;
    [SerializeField] private float maxX = 7f;

    [Header("Stop detection")]
    [SerializeField] private float minSpeedToConsiderStopped = 0.2f;
    [SerializeField] private float stoppedTimeToEndShot = 1.0f;

    [Header("Lifetime (hard cap)")]
    [SerializeField, Min(0.1f)] private float maxLifetimeSeconds = 10f;

    private Rigidbody2D rb;
    private float stoppedTimer;

    private float lifeTimer;     // 👈 NUEVO
    private bool hasEnded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        if (p.y < minY || p.y > maxY || p.x < minX || p.x > maxX)
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
