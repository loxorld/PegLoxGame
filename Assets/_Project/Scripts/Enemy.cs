using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    public event Action Defeated;

    [SerializeField] private EnemyData data;

    private int currentHP;
    private SpriteRenderer sr;

    private bool initializedExternally; // <- clave

    public int AttackDamage => data != null ? data.attackDamage : 5;
    public bool IsDead => currentHP <= 0;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        // NO inicializamos acá
    }

    private void Start()
    {
        // Si no lo inicializó BattleManager y hay data asignada en Inspector, inicializamos una vez.
        if (!initializedExternally && data != null)
        {
            ResetFromData();
        }
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
        int maxHP = data != null ? data.maxHP : 50;
        currentHP = maxHP;

        UpdateVisual();

        Debug.Log($"Enemy spawned: {(data != null ? data.enemyName : "Default")} HP {currentHP}/{maxHP}");
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        currentHP = Mathf.Max(currentHP, 0);

        UpdateVisual();

        if (currentHP <= 0)
            Die();
    }

    private void UpdateVisual()
    {
        int maxHP = data != null ? data.maxHP : 50;
        float hpPercent = maxHP > 0 ? (float)currentHP / maxHP : 0f;

        sr.color = Color.Lerp(Color.black, Color.red, hpPercent);
    }

    private void Die()
    {
        gameObject.SetActive(false);
        Defeated?.Invoke();
    }
}
