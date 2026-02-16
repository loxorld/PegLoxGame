using System;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [SerializeField] private int maxHP = 100;
    public int MaxHP => maxHP;

    public int CurrentHP { get; private set; }

    public event Action Died;

    private bool isDead;

    private void Awake()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null)
        {
            
            maxHP = Mathf.Max(1, flow.PlayerMaxHP);

            if (flow.HasSavedPlayerHP)
                CurrentHP = Mathf.Clamp(flow.SavedPlayerHP, 0, maxHP);
            else
                CurrentHP = maxHP;

            flow.SavePlayerMaxHP(maxHP);
            flow.SavePlayerHP(CurrentHP);
        }
        else
        {
            maxHP = Mathf.Max(1, maxHP);
            CurrentHP = maxHP;
        }

        isDead = false;
        Debug.Log($"Player HP: {CurrentHP}/{maxHP}");
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;
        if (damage <= 0) return;

        CurrentHP -= damage;
        CurrentHP = Mathf.Max(CurrentHP, 0);
        GameFlowManager.Instance?.SavePlayerHP(CurrentHP);

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