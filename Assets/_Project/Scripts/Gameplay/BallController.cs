using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BallController : MonoBehaviour
{
    [Header("Bounce Assist")]
    [SerializeField, Min(0f)] private float minSpeedForBounceAssist = 2.25f;
    [SerializeField, Range(0f, 1f)] private float bounceAssistThreshold = 0.92f;

    private static readonly Dictionary<int, PhysicsMaterial2D> RuntimeMaterialsByBounciness = new Dictionary<int, PhysicsMaterial2D>();

    public OrbInstance Orb { get; private set; }

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private float activeBounciness = 0.9f;
    private Vector2 previousVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        activeBounciness = BallPhysicsUtility.GetColliderBounciness(col, 0.9f);
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void OnEnable()
    {
        previousVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
    }

    private void FixedUpdate()
    {
        if (rb != null)
            previousVelocity = rb.linearVelocity;
    }

    public void Init(OrbInstance orb)
    {
        Orb = orb;
        if (sr != null)
            sr.color = Orb != null ? Orb.Color : Color.white;

        if (rb != null && Orb != null)
            rb.linearDamping = Orb.LinearDrag;

        activeBounciness = Orb != null
            ? Orb.Bounciness
            : BallPhysicsUtility.GetColliderBounciness(col, activeBounciness);

        if (col != null && Orb != null)
            col.sharedMaterial = GetOrCreateRuntimeMaterial(activeBounciness);

        previousVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ApplyBounceAssist(collision);
    }

    private void ApplyBounceAssist(Collision2D collision)
    {
        if (rb == null || collision == null || collision.contactCount == 0)
            return;

        float incomingSpeed = previousVelocity.magnitude;
        if (incomingSpeed < minSpeedForBounceAssist)
            return;

        Vector2 contactNormal = BallPhysicsUtility.GetBestContactNormal(collision, previousVelocity);
        if (contactNormal.sqrMagnitude < 0.0001f)
            return;

        float surfaceBounciness = BallPhysicsUtility.GetColliderBounciness(collision.collider);
        Vector2 assistedVelocity = BallPhysicsUtility.CalculateBounceVelocity(
            previousVelocity,
            contactNormal,
            activeBounciness,
            surfaceBounciness
        );

        if (assistedVelocity.sqrMagnitude < 0.0001f)
            return;

        float threshold = Mathf.Clamp01(bounceAssistThreshold);
        if (rb.linearVelocity.sqrMagnitude >= assistedVelocity.sqrMagnitude * threshold * threshold)
            return;

        rb.linearVelocity = assistedVelocity;
        previousVelocity = assistedVelocity;
    }

    private static PhysicsMaterial2D GetOrCreateRuntimeMaterial(float bounciness)
    {
        float clampedBounciness = Mathf.Clamp01(bounciness);
        int key = Mathf.RoundToInt(clampedBounciness * 1000f);

        if (!RuntimeMaterialsByBounciness.TryGetValue(key, out PhysicsMaterial2D runtimeMaterial) || runtimeMaterial == null)
        {
            runtimeMaterial = new PhysicsMaterial2D($"BallMat_Runtime_{key}")
            {
                bounciness = clampedBounciness,
                friction = 0f
            };
            RuntimeMaterialsByBounciness[key] = runtimeMaterial;
        }

        return runtimeMaterial;
    }
}

internal static class BallPhysicsUtility
{
    private const float MinSqrMagnitude = 0.0001f;

    public static float GetColliderBounciness(Collider2D collider, float fallback = 0f)
    {
        if (collider == null)
            return Mathf.Max(0f, fallback);

        PhysicsMaterial2D material = collider.sharedMaterial;
        if (material == null)
            return Mathf.Max(0f, fallback);

        return Mathf.Max(0f, material.bounciness);
    }

    public static float ResolveBounceRestitution(float ballBounciness, float surfaceBounciness)
    {
        float safeBallBounciness = Mathf.Max(0f, ballBounciness);
        float safeSurfaceBounciness = Mathf.Max(0f, surfaceBounciness);
        return Mathf.Clamp01(Mathf.Max(safeBallBounciness, safeSurfaceBounciness));
    }

    public static Vector2 CalculateBounceVelocity(
        Vector2 incomingVelocity,
        Vector2 surfaceNormal,
        float ballBounciness,
        float surfaceBounciness)
    {
        if (incomingVelocity.sqrMagnitude < MinSqrMagnitude)
            return Vector2.zero;

        if (surfaceNormal.sqrMagnitude < MinSqrMagnitude)
            return incomingVelocity;

        float restitution = ResolveBounceRestitution(ballBounciness, surfaceBounciness);
        Vector2 reflectedVelocity = Vector2.Reflect(incomingVelocity, surfaceNormal.normalized);
        return reflectedVelocity * restitution;
    }

    public static Vector2 GetBestContactNormal(Collision2D collision, Vector2 incomingVelocity)
    {
        if (collision == null || collision.contactCount == 0)
            return Vector2.zero;

        Vector2 bestNormal = collision.GetContact(0).normal;
        if (incomingVelocity.sqrMagnitude < MinSqrMagnitude)
            return bestNormal;

        Vector2 incomingDirection = incomingVelocity.normalized;
        float bestDot = Vector2.Dot(incomingDirection, bestNormal);

        for (int i = 1; i < collision.contactCount; i++)
        {
            Vector2 normal = collision.GetContact(i).normal;
            float dot = Vector2.Dot(incomingDirection, normal);
            if (dot < bestDot)
            {
                bestDot = dot;
                bestNormal = normal;
            }
        }

        return bestNormal;
    }
}
