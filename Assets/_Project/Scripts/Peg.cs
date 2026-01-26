using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Peg : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private PegDefinition definition;

    private bool hit;
    private SpriteRenderer sr;

    public PegType Type => definition != null ? definition.type : PegType.Normal;

    

    public void SetDefinition(PegDefinition def)
    {
        definition = def;
        hit = false;
        ApplyIdleVisual();
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ApplyIdleVisual();
    }

    private void OnEnable()
    {
        PegManager.Instance?.RegisterPeg(this);
       
        ApplyIdleVisual();
        hit = false;
    }

    private void OnDisable()
    {
        PegManager.Instance?.UnregisterPeg(this);
    }

    public void ResetPeg()
    {
        hit = false;
        ApplyIdleVisual();
    }

    private void ApplyIdleVisual()
    {
        if (sr == null) return;
        if (definition == null)
        {
            sr.color = Color.cyan; // fallback
            return;
        }
        sr.color = definition.idleColor;
    }

    private void ApplyHitVisual()
    {
        if (sr == null) return;
        if (definition == null)
        {
            sr.color = Color.gray; // fallback
            return;
        }
        sr.color = definition.hitColor;
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
