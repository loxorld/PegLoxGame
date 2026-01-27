using System;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyData[] enemiesPool;

    [Header("Difficulty")]
    [SerializeField] private DifficultyConfig difficulty;

    public event Action EncounterStarted;
    public event Action EncounterCompleted;

    [Header("Encounter (fallback si no hay DifficultyConfig)")]
    [SerializeField, Min(1)] private int enemiesToDefeatFallback = 3;

    [Header("Flow")]
    [SerializeField, Min(0f)] private float respawnDelay = 0.5f;

    private int defeatedCount = 0;
    private bool waitingForRewards = false;

    private int encounterIndex = 0;         // 0,1,2...
    private int enemiesToDefeat = 3;        // se setea por stage actual
    private DifficultyStage stage;          // stage actual cacheado

    public Enemy CurrentEnemy => enemy;
    public bool WaitingForRewards => waitingForRewards;

    /// <summary>0-based (el HUD puede mostrar +1).</summary>
    public int EncounterIndex => encounterIndex;

    // --- Datos “stage actual” (encapsulados) ---
    public int EnemiesToDefeat => enemiesToDefeat;
    public float EnemyHpMultiplier => stage.enemyHpMultiplier;
    public float EnemyDamageMultiplier => stage.enemyDamageMultiplier;
    public int EnemyHpBonus => stage.enemyHpBonus;
    public int EnemyDamageBonus => stage.enemyDamageBonus;
    public bool HasDifficultyConfig => difficulty != null;

    public string StageName => string.IsNullOrWhiteSpace(stage.stageName) ? $"Stage {encounterIndex + 1}" : stage.stageName;


    private void Start()
    {
        if (enemy == null)
        {
            Debug.LogError("[Battle] Enemy reference missing.");
            return;
        }

        enemy.Defeated += OnEnemyDefeated;

        // primer encounter
        StartNewEncounter();
    }

    private void OnDestroy()
    {
        if (enemy != null)
            enemy.Defeated -= OnEnemyDefeated;
    }

    private void StartNewEncounter()
    {
        defeatedCount = 0;
        waitingForRewards = false;

        stage = (difficulty != null) ? difficulty.GetStage(encounterIndex) : DifficultyStage.Default;
        enemiesToDefeat = (difficulty != null) ? stage.enemiesToDefeat : enemiesToDefeatFallback;

        EncounterStarted?.Invoke();
        SpawnRandomEnemy();
    }

    private void OnEnemyDefeated()
    {
        if (waitingForRewards) return;

        defeatedCount++;

        if (defeatedCount >= enemiesToDefeat)
        {
            waitingForRewards = true;
            EncounterCompleted?.Invoke();
            return;
        }

        Invoke(nameof(SpawnRandomEnemy), respawnDelay);
    }

    public void ContinueAfterRewards()
    {
        if (!waitingForRewards) return;

        // Subimos dificultad para el próximo encounter
        encounterIndex++;

        Invoke(nameof(StartNewEncounter), respawnDelay);
    }

    private void SpawnRandomEnemy()
    {
        if (waitingForRewards) return;

        if (enemiesPool == null || enemiesPool.Length == 0)
        {
            Debug.LogError("[Battle] enemiesPool is empty.");
            return;
        }

        EnemyData chosen = enemiesPool[UnityEngine.Random.Range(0, enemiesPool.Length)];
        enemy.SetDataAndReset(chosen);

        // Aplicar dificultad (si hay config)
        if (difficulty != null)
        {
            int baseHp = chosen != null ? chosen.maxHP : 50;
            int baseDmg = chosen != null ? chosen.attackDamage : 5;

            int scaledHp = Mathf.RoundToInt(baseHp * stage.enemyHpMultiplier) + stage.enemyHpBonus;
            int scaledDmg = Mathf.RoundToInt(baseDmg * stage.enemyDamageMultiplier) + stage.enemyDamageBonus;

            enemy.ApplyDifficulty(scaledHp, scaledDmg);
        }
    }
}
