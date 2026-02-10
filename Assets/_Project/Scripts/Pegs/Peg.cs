using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class Peg : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private PegDefinition definition;

    private bool consumed;
    private SpriteRenderer sr;
    private Collider2D col;

    // Para Durable (o futuras mecánicas)
    private int hitPointsRemaining = 1;

    public PegDefinition Definition => definition;
    public PegType Type => definition != null ? definition.type : PegType.Normal;
    public bool IsConsumed => consumed;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        ApplyIdleVisual();
    }

    private void OnEnable()
    {
        PegManager.Instance?.RegisterPeg(this);
        ResetForNewEncounter();
    }

    private void OnDisable()
    {
        PegManager.Instance?.UnregisterPeg(this);
    }

    public void SetDefinition(PegDefinition def)
    {
        definition = def;
        ResetForNewEncounter();
    }

    /// <summary>Reset TOTAL: solo cuando empieza un nuevo encounter.</summary>
    public void ResetForNewEncounter()
    {
        consumed = false;
        hitPointsRemaining = 1;

        if (col != null) col.enabled = true;
        if (sr != null) sr.enabled = true;

        ApplyIdleVisual();

        // Behaviors pueden inicializar estado (ej: Durable setea HP=2)
        if (definition != null && definition.behaviors != null)
        {
            for (int i = 0; i < definition.behaviors.Length; i++)
            {
                var b = definition.behaviors[i];
                if (b != null) b.OnResetForEncounter(this);
            }
        }
    }

    /// <summary>Revive dentro del mismo encounter (para Refresh): no toca definition.</summary>
    public void RestoreForSameEncounter()
    {
        consumed = false;
        if (col != null) col.enabled = true;
        if (sr != null) sr.enabled = true;

        
        ApplyIdleVisual();

        // Behaviors pueden querer resetear su estado por encounter 
        if (definition != null && definition.behaviors != null)
        {
            for (int i = 0; i < definition.behaviors.Length; i++)
            {
                var b = definition.behaviors[i];
                if (b != null) b.OnResetForEncounter(this);
            }
        }
    }

    private void ApplyIdleVisual()
    {
        if (sr == null) return;
        sr.color = (definition != null) ? definition.idleColor : Color.cyan;
    }

    private void ApplyHitVisual()
    {
        if (sr == null) return;
        sr.color = (definition != null) ? definition.hitColor : Color.gray;
    }

    private void Consume()
    {
        consumed = true;

        // visual
        if (sr != null) sr.enabled = false;

        // colisión
        if (col != null) col.enabled = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (consumed) return;
        if (!collision.gameObject.CompareTag("Ball")) return;

        

        // 2) Behaviors deciden si se consume o no (durable: no en primer hit)
        bool consumeNow = true;

        if (definition != null && definition.behaviors != null && definition.behaviors.Length > 0)
        {
            // Si hay behaviours, el consume por default lo define el resultado:
            // si cualquiera dice "no consumir todavía", se mantiene.
            // (Y si alguno quiere consumir sí o sí, igual devolvemos true.)
            consumeNow = true;
            for (int i = 0; i < definition.behaviors.Length; i++)
            {
                var b = definition.behaviors[i];
                if (b == null) continue;

                bool wantsConsume = b.OnBallHit(this, collision);
                if (!wantsConsume) consumeNow = false;
            }
        }

        ShotManager.Instance?.RegisterPegHit(Type);
        AudioManager.Instance?.PlaySfx(AudioEventId.PegHit);

        if (consumeNow)
        {
            Consume();
        }
        else
        {
            // feedback visual opcional para hit estándar 
            // ApplyHitVisual();
        }
    }

    // -------- API para behaviors (composición) --------

    public void SetHitPoints(int hp)
    {
        hitPointsRemaining = Mathf.Max(1, hp);
    }

    public int ConsumeOneHitPoint()
    {
        hitPointsRemaining = Mathf.Max(0, hitPointsRemaining - 1);
        return hitPointsRemaining;
    }

    public void SetColor(Color c)
    {
        if (sr != null) sr.color = c;
    }

    public void SetColorToIdle()
    {
        ApplyIdleVisual();
    }

    /// <summary>
    /// Consumir el peg desde afuera (Bomb, etc.). No hace RegisterPegHit (eso lo decide PegManager).
    /// </summary>
    public void ForceConsumeNoHitCount()
    {
        if (consumed) return;
        Consume();
    }

    

}
