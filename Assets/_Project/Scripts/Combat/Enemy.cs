using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    public event Action Defeated;

    [SerializeField] private EnemyData data;
    [SerializeField] private EnemyFeedbackController feedback;

    private int currentHP;
    private int currentMaxHP;          // <- CLAVE: max runtime (escalado)
    private int currentAttackDamage;

    private SpriteRenderer sr;
    private bool initializedExternally;

    public int CurrentHP => currentHP;
    public int MaxHP => currentMaxHP > 0 ? currentMaxHP : (data != null ? data.maxHP : 50);
    public int AttackDamage => currentAttackDamage > 0 ? currentAttackDamage : (data != null ? data.attackDamage : 5);
    public bool IsDead => currentHP <= 0;
    public string EnemyName => data != null ? data.enemyName : "Enemy";

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // Fallback solo si no lo inicializó BattleManager
        if (!initializedExternally && data != null)
            ResetFromData();
    }

    public void SetDataAndReset(EnemyData newData)
    {
        initializedExternally = true;

        data = newData;
        ResetFromData();
        gameObject.SetActive(true);
    }

    private void ResetFromData()
    {
        currentMaxHP = data != null ? data.maxHP : 50;
        currentHP = currentMaxHP;

        currentAttackDamage = data != null ? data.attackDamage : 5;

        UpdateVisual();

        Debug.Log($"Enemy spawned: {EnemyName} HP {currentHP}/{currentMaxHP}");
    }

    public void ApplyDifficulty(int maxHp, int attackDamage)
    {
        // Seteo max/dmg escalados
        currentMaxHP = Mathf.Max(1, maxHp);
        currentAttackDamage = Mathf.Max(1, attackDamage);

        // Para flujo actual (cada spawn arranca full):
        currentHP = currentMaxHP;

        UpdateVisual();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        // Restar vida y clavarla a cero como mínimo
        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0);

        // Actualizar la apariencia (tinte según vida) antes del flash
        UpdateVisual();

        // Reproducir el feedback visual si existe
        feedback?.Flash();

        // Si la vida llegó a cero, ejecutar la muerte
        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void UpdateVisual()
    {
        int max = MaxHP;
        float hpPercent = max > 0 ? (float)currentHP / max : 0f;

        sr.color = Color.Lerp(Color.black, Color.red, hpPercent);
    }

    private void Die()
    {
        gameObject.SetActive(false);
        Defeated?.Invoke();
    }
}
