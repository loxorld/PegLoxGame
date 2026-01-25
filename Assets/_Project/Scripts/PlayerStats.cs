using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [SerializeField] private int maxHP = 30;
    public int MaxHP => maxHP;

    public int CurrentHP { get; private set; }

    private void Awake()
    {
        CurrentHP = maxHP;
        Debug.Log($"Player HP: {CurrentHP}/{maxHP}");
    }

    public void TakeDamage(int damage)
    {
        CurrentHP -= damage;
        CurrentHP = Mathf.Max(CurrentHP, 0);

        Debug.Log($"Player took {damage} damage. HP: {CurrentHP}/{maxHP}");
    }

    public bool IsDead => CurrentHP <= 0;
}
