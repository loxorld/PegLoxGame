using System;
using UnityEngine;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyData[] enemiesPool;

    [Header("Difficulty")]
    [SerializeField] private DifficultyConfig difficulty;
    [SerializeField] private RunBalanceConfig balanceConfig;

    public event Action EncounterStarted;
    public event Action EncounterCompleted;

    [Header("Encounter (fallback si no hay DifficultyConfig)")]
    [SerializeField, Min(1)] private int enemiesToDefeatFallback = 3;

    [Header("Flow")]
    [SerializeField, Min(0f)] private float respawnDelay = 0.5f;
    [SerializeField] private bool autoStartOnLoad = true;

    private int defeatedCount = 0;
    private bool waitingForRewards = false;
    private bool hasStartedEncounter = false;

    private int encounterIndex = 0;         // 0,1,2...
    private int enemiesToDefeat = 3;        // se setea por stage actual
    private DifficultyStage stage;          // stage actual cacheado
    private bool isBossEncounter;

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

    public string StageName => stage.GetDisplayName(encounterIndex);
    public string DifficultyHudText => stage.GetHudText(encounterIndex, enemiesToDefeat);


    private void Start()
    {
        if (enemy == null)
        {
            Debug.LogError("[Battle] Enemy reference missing.");
            return;
        }

        enemy.Defeated += OnEnemyDefeated;

        if (autoStartOnLoad && !hasStartedEncounter)
            StartEncounterFromMap();
    }


    private void OnDestroy()
    {
        if (enemy != null)
            enemy.Defeated -= OnEnemyDefeated;
    }

    private void StartNewEncounter()
    {
        hasStartedEncounter = true;
        defeatedCount = 0;
        waitingForRewards = false;

        SyncEncounterIndexFromFlow();
        ResolveBalanceConfig();

        stage = (difficulty != null) ? difficulty.GetStage(encounterIndex) : DifficultyStage.Default;
        enemiesToDefeat = (difficulty != null) ? stage.enemiesToDefeat : enemiesToDefeatFallback;

        GameFlowManager flow = GameFlowManager.Instance;
        isBossEncounter = flow != null && flow.HasBossEncounter;
        if (isBossEncounter)
            enemiesToDefeat = 1;


        EncounterStarted?.Invoke();
        Debug.Log("[BattleManager] Evento EncounterStarted disparado");

        SpawnRandomEnemy();
    }

    private void OnEnemyDefeated()
    {
        if (waitingForRewards) return;

        defeatedCount++;

        if (defeatedCount >= enemiesToDefeat)
        {
            waitingForRewards = true;
            if (isBossEncounter)
                GameFlowManager.Instance?.ClearBossEncounter();
            EncounterCompleted?.Invoke();
            return;
        }

        Invoke(nameof(SpawnRandomEnemy), respawnDelay);
    }

    public void ContinueAfterRewards()
    {
        if (!waitingForRewards) return;

        // Subimos dificultad para el próximo encounter
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null)
            flow.AdvanceEncounter();
        else
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
        if (isBossEncounter)
        {
            EnemyData bossEnemy = GameFlowManager.Instance?.BossEnemy;
            if (bossEnemy != null)
                chosen = bossEnemy;
        }

        enemy.SetDataAndReset(chosen);

        // Aplicar dificultad (si hay config)
        bool hasScaling = difficulty != null || balanceConfig != null;
        if (hasScaling)
        {
            int baseHp = chosen != null ? chosen.maxHP : 50;
            int baseDmg = chosen != null ? chosen.attackDamage : 5;

            float balanceHpMultiplier = balanceConfig != null ? balanceConfig.GetEnemyHpMultiplier(encounterIndex, 1f) : 1f;
            float balanceDmgMultiplier = balanceConfig != null ? balanceConfig.GetEnemyDamageMultiplier(encounterIndex, 1f) : 1f;

            int scaledHp = Mathf.RoundToInt(baseHp * stage.enemyHpMultiplier * balanceHpMultiplier) + stage.enemyHpBonus;
            int scaledDmg = Mathf.RoundToInt(baseDmg * stage.enemyDamageMultiplier * balanceDmgMultiplier) + stage.enemyDamageBonus;

            if (isBossEncounter)
            {
                GameFlowManager flow = GameFlowManager.Instance;
                if (flow != null)
                {
                    scaledHp = Mathf.RoundToInt(scaledHp * flow.BossHpMultiplier) + flow.BossHpBonus;
                    scaledDmg = Mathf.RoundToInt(scaledDmg * flow.BossDamageMultiplier) + flow.BossDamageBonus;
                }
            }

            enemy.ApplyDifficulty(scaledHp, scaledDmg);
        }
        else if (isBossEncounter)
        {
            GameFlowManager flow = GameFlowManager.Instance;
            if (flow != null && chosen != null)
            {
                int scaledHp = Mathf.RoundToInt(chosen.maxHP * flow.BossHpMultiplier) + flow.BossHpBonus;
                int scaledDmg = Mathf.RoundToInt(chosen.attackDamage * flow.BossDamageMultiplier) + flow.BossDamageBonus;
                enemy.ApplyDifficulty(scaledHp, scaledDmg);
            }
        }
    }

    public void StartEncounterFromMap()
    {
        if (hasStartedEncounter)
            return;

        Debug.Log("[BattleManager] StartEncounterFromMap llamado");
        // Esperar un frame para asegurar que otros componentes se hayan suscrito a los eventos
        StartCoroutine(DelayedStartEncounter());
    }

    private IEnumerator DelayedStartEncounter()
    {
        yield return null; // Espera un frame
        StartNewEncounter();
    }

    private void SyncEncounterIndexFromFlow()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null)
            encounterIndex = flow.EncounterIndex;
    }

    private void ResolveBalanceConfig()
    {
        if (balanceConfig == null)
            balanceConfig = RunBalanceConfig.LoadDefault();
    }

}