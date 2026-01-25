using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Peg : MonoBehaviour
{
    [SerializeField] private PegType type = PegType.Normal;

    private bool hit;
    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        ApplyTypeVisual();
    }

    private void Start()
    {
        // fallback por si en OnEnable todavía no existía PegManager.Instance
        PegManager.Instance?.RegisterPeg(this);
    }


    private void ApplyTypeVisual()
    {
        sr.color = (type == PegType.Critical) ? Color.yellow : Color.cyan;
    }

    public void ResetPeg()
    {
        hit = false;
        ApplyTypeVisual();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hit) return;

        if (collision.gameObject.CompareTag("Ball"))
        {
            hit = true;
            OnHit();
        }
    }

    private void OnHit()
    {
        sr.color = Color.gray;
        ShotManager.Instance?.RegisterPegHit(type);
    }

    private void OnEnable()
    {
        PegManager.Instance?.RegisterPeg(this);
    }

    private void OnDisable()
    {
        PegManager.Instance?.UnregisterPeg(this);
    }
}
