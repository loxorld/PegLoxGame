using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BallController : MonoBehaviour
{
    public OrbData Orb { get; private set; }

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    public void Init(OrbData orb)
    {
        Orb = orb;

        if (Orb == null)
            return;

        // Visual MVP
        sr.color = Orb.color;

        // Physics
        rb.linearDamping = Orb.linearDrag;

        // Bounciness via PhysicsMaterial2D
        var mat = new PhysicsMaterial2D("BallMat_Runtime")
        {
            bounciness = Orb.bounciness,
            friction = 0f
        };
        col.sharedMaterial = mat;
    }
}
