using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Peg : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private PegDefinition definition;

    private bool hit;
    private SpriteRenderer sr;

    public PegDefinition Definition => definition;
    public PegType Type => definition != null ? definition.type : PegType.Normal;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ApplyIdleVisual();
    }

    private void OnEnable()
    {
        PegManager.Instance?.RegisterPeg(this);
        // Estado limpio al habilitarse (por si se reusa / reactiva)
        hit = false;
        ApplyIdleVisual();
    }

    private void OnDisable()
    {
        PegManager.Instance?.UnregisterPeg(this);
    }

    /// <summary>
    /// Setea definición (data-driven) y resetea estado visual/hit.
    /// Lo llama el BoardManager al spawnear.
    /// </summary>
    public void SetDefinition(PegDefinition def)
    {
        definition = def;
        ResetPeg();
    }

    public void ResetPeg()
    {
        hit = false;
        ApplyIdleVisual();
    }

    private void ApplyIdleVisual()
    {
        if (sr == null) return;

        sr.color = (definition != null)
            ? definition.idleColor
            : Color.cyan; // fallback razonable
    }

    private void ApplyHitVisual()
    {
        if (sr == null) return;

        sr.color = (definition != null)
            ? definition.hitColor
            : Color.gray; // fallback razonable
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hit) return;
        if (!collision.gameObject.CompareTag("Ball")) return;

        hit = true;
        ApplyHitVisual();

        ShotManager.Instance?.RegisterPegHit(Type);
    }
}
