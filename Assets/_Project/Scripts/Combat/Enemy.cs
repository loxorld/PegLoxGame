using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    public enum CombatAlertType
    {
        Block,
        Heal,
        Rage,
        Phase
    }

    public readonly struct CombatAlert
    {
        public CombatAlert(CombatAlertType type, string message, int amount = 0)
        {
            Type = type;
            Message = message ?? string.Empty;
            Amount = amount;
        }

        public CombatAlertType Type { get; }
        public string Message { get; }
        public int Amount { get; }
    }

    public event Action Defeated;
    public event Action<CombatAlert> CombatAlertRaised;

    [SerializeField] private EnemyData data;
    [SerializeField] private EnemyFeedbackController feedback;

    private int currentHP;
    private int currentMaxHP;          // <- CLAVE: max runtime (escalado)
    private int currentAttackDamage;
    private int bonusAttackDamage;
    private bool desperationTriggered;

    private SpriteRenderer sr;
    private Sprite defaultSprite;
    private Color originalColor;
    private bool initializedExternally;

    public int CurrentHP => currentHP;
    public int MaxHP => currentMaxHP > 0 ? currentMaxHP : (data != null ? data.maxHP : 50);
    public int AttackDamage => Mathf.Max(1, (currentAttackDamage > 0 ? currentAttackDamage : (data != null ? data.attackDamage : 5)) + bonusAttackDamage);
    public bool IsDead => currentHP <= 0;
    public string EnemyName => data != null ? data.DisplayName : "Enemy";
    public string CurrentIntentText => BuildIntentText();
    public bool DesperationTriggered => desperationTriggered;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        defaultSprite = sr != null ? sr.sprite : null;
        originalColor = sr != null ? sr.color : Color.white;
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
        bonusAttackDamage = 0;
        desperationTriggered = false;

        ApplyDataVisuals();
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
        bonusAttackDamage = 0;
        desperationTriggered = false;

        UpdateVisual();
    }

    public int ResolveIncomingDamage(int damage)
    {
        if (damage <= 0)
            return 0;

        int blocked = data != null ? data.FlatDamageReduction : 0;
        if (blocked <= 0)
            return damage;

        int appliedDamage = Mathf.Max(1, damage - blocked);
        int prevented = damage - appliedDamage;
        if (prevented > 0)
        {
            Debug.Log($"[Enemy] {EnemyName} bloquea {prevented} de dano.");
            RaiseCombatAlert(CombatAlertType.Block, $"Bloquea {prevented}", prevented);
        }

        return appliedDamage;
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
        AudioManager.Instance?.PlaySfx(AudioEventId.EnemyHit);

        // Si la vida llegó a cero, ejecutar la muerte
        if (currentHP <= 0)
        {
            Die();
        }
    }

    public void OnShotResolved(int rawDamage, int appliedDamage, int totalHits, int criticalHits)
    {
        if (IsDead || data == null)
            return;

        bool stateChanged = false;
        if (!desperationTriggered && data.DesperationThreshold > 0f)
        {
            int thresholdHp = Mathf.CeilToInt(MaxHP * data.DesperationThreshold);
            if (currentHP > 0 && currentHP <= thresholdHp)
            {
                desperationTriggered = true;

                if (data.DesperationAttackBonus > 0)
                {
                    bonusAttackDamage += data.DesperationAttackBonus;
                    stateChanged = true;
                }

                if (data.DesperationHeal > 0 && currentHP < MaxHP)
                {
                    int desperationHeal = Mathf.Min(data.DesperationHeal, MaxHP - currentHP);
                    currentHP += desperationHeal;
                    stateChanged = desperationHeal > 0 || stateChanged;
                }

                Debug.Log($"[Enemy] {EnemyName} entra en fase desesperada.");
                RaiseCombatAlert(CombatAlertType.Phase, "Fase de boss");
            }
        }

        if (data.HealOnSurvive > 0 && currentHP < MaxHP)
        {
            int healed = Mathf.Min(data.HealOnSurvive, MaxHP - currentHP);
            if (healed > 0)
            {
                currentHP += healed;
                stateChanged = true;
                Debug.Log($"[Enemy] {EnemyName} recupera {healed} HP tras aguantar la tirada.");
                RaiseCombatAlert(CombatAlertType.Heal, $"+{healed} HP", healed);
            }
        }

        if (data.RageDamageOnSurvive > 0)
        {
            bonusAttackDamage += data.RageDamageOnSurvive;
            stateChanged = true;
            Debug.Log($"[Enemy] {EnemyName} gana +{data.RageDamageOnSurvive} ATQ para el contraataque.");
            RaiseCombatAlert(CombatAlertType.Rage, $"+{data.RageDamageOnSurvive} ATQ", data.RageDamageOnSurvive);
        }

        if (stateChanged)
            UpdateVisual();
    }

    private void UpdateVisual()
    {
        int max = MaxHP;
        float hpPercent = max > 0 ? (float)currentHP / max : 0f;

        sr.color = Color.Lerp(Color.red, originalColor, hpPercent);
    }

    private void ApplyDataVisuals()
    {
        if (sr == null)
            return;

        Sprite visualSprite = data != null && data.WorldSpriteOverride != null
            ? data.WorldSpriteOverride
            : defaultSprite;
        if (visualSprite != null)
            sr.sprite = visualSprite;
    }

    private void Die()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.EnemyDefeated);
        gameObject.SetActive(false);
        Defeated?.Invoke();
    }

    private string BuildIntentText()
    {
        if (data == null)
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.combatHint))
            parts.Add(data.combatHint.Trim());

        if (desperationTriggered)
            parts.Add("Fase activa");

        if (parts.Count == 0)
            return string.Empty;

        return string.Join(" | ", parts);
    }

    private void RaiseCombatAlert(CombatAlertType type, string message, int amount = 0)
    {
        CombatAlertRaised?.Invoke(new CombatAlert(type, message, amount));
    }
}
