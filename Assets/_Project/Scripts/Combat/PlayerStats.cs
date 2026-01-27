using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [SerializeField] private int maxHP = 30;
    public int MaxHP => maxHP;

    public int CurrentHP { get; private set; }

    public event Action Died;

    private bool isDead;

    private void Awake()
    {
        CurrentHP = maxHP;
        isDead = false;
        Debug.Log($"Player HP: {CurrentHP}/{maxHP}");
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (damage <= 0) return;

        CurrentHP -= damage;
        CurrentHP = Mathf.Max(CurrentHP, 0);

        Debug.Log($"Player took {damage} damage. HP: {CurrentHP}/{maxHP}");

        if (CurrentHP <= 0 && !isDead)
        {
            isDead = true;
            Debug.Log("[Player] Died");
            Died?.Invoke();
        }
    }

    public bool IsDead => isDead;
}
