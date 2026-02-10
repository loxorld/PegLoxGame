using System;
using UnityEngine;
using System.Collections;
using System.Globalization;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy fallbackEnemy;
    [SerializeField] private Transform enemySpawnPoint;
    [SerializeField] private EnemyData[] enemiesPool;

    [Header("Balance")]
    [SerializeField] private RunBalanceConfig balanceConfig;

    public event Action EncounterStarted;
    public event Action EncounterCompleted;

    [Header("Encounter Fallback")]
    [SerializeField, Min(1)] private int enemiesToDefeatFallback = 3;

    [Header("Flow")]
    [SerializeField, Min(0f)] private float respawnDelay = 0.5f;
    [SerializeField] private bool autoStartOnLoad = true;

    private int defeatedCount = 0;
    private bool waitingForRewards = false;
    private bool hasStartedEncounter = false;

    private int encounterIndex = 0;
    private int encounterInStage = 0;
    private int currentStageIndex = 0;
    private int enemiesToDefeat = 3;
    private float currentEnemyHpMultiplier = 1f;
    private float currentEnemyDamageMultiplier = 1f;
    private bool isBossEncounter;
    private bool lastEncounterWasBoss;

    private Enemy currentEnemy;

    public Enemy CurrentEnemy => currentEnemy;
    public bool WaitingForRewards => waitingForRewards;
    public bool LastEncounterWasBoss => lastEncounterWasBoss;
    public int EncounterIndex => encounterIndex;
    public int EnemiesToDefeat => enemiesToDefeat;
    public int CurrentStageIndex => currentStageIndex;
    public float EnemyHpMultiplier => currentEnemyHpMultiplier;
    public float EnemyDamageMultiplier => currentEnemyDamageMultiplier;
    public int EnemyHpBonus => 0;
    public int EnemyDamageBonus => 0;
    public bool HasDifficultyConfig => balanceConfig != null;

    public string StageName => $"Stage {currentStageIndex + 1}";

    public string DifficultyHudText
    {
        get
        {
            string hpMultiplier = currentEnemyHpMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
            string dmgMultiplier = currentEnemyDamageMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
            return $"{StageName} | HP x{hpMultiplier} | DMG x{dmgMultiplier} | N={enemiesToDefeat}";
        }
    }

    private void Start()
    {
        if (enemySpawnPoint == null && fallbackEnemy != null)
            enemySpawnPoint = fallbackEnemy.transform;

        if (fallbackEnemy == null && enemySpawnPoint == null)
        {
            Debug.LogError("[Battle] Enemy spawn point missing.");
            return;
        }

        currentEnemy = fallbackEnemy;
        if (currentEnemy != null)
            currentEnemy.Defeated += OnEnemyDefeated;

        if (autoStartOnLoad && !hasStartedEncounter)
            StartEncounterFromMap();
    }


    private void OnDestroy()
    {
        if (currentEnemy != null)
            currentEnemy.Defeated -= OnEnemyDefeated;
    }

    private void StartNewEncounter()
    {
        hasStartedEncounter = true;
        defeatedCount = 0;
        waitingForRewards = false;
        lastEncounterWasBoss = false;

        SyncEncounterIndexFromFlow();
        ResolveBalanceConfig();

        currentEnemyHpMultiplier = balanceConfig != null
           ? balanceConfig.GetEnemyHpMultiplier(currentStageIndex, encounterInStage, 1f)
            : 1f;
        currentEnemyDamageMultiplier = balanceConfig != null
            ? balanceConfig.GetEnemyDamageMultiplier(currentStageIndex, encounterInStage, 1f)
            : 1f;
        enemiesToDefeat = balanceConfig != null
            ? balanceConfig.GetEnemiesToDefeat(currentStageIndex, encounterInStage, enemiesToDefeatFallback)
            : enemiesToDefeatFallback;

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
            lastEncounterWasBoss = isBossEncounter;
            if (isBossEncounter)
            {
                GameFlowManager flow = GameFlowManager.Instance;
                if (flow != null)
                {
                    flow.AdvanceStage();
                    flow.ResetNodesVisited();
                }
                GameFlowManager.Instance?.ClearBossEncounter();
            }
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

        Enemy prefab = chosen != null ? chosen.enemyPrefab : null;
        if (prefab == null)
            prefab = fallbackEnemy;

        ReplaceEnemy(prefab);
        if (currentEnemy == null)
            return;

        currentEnemy.SetDataAndReset(chosen);

        bool hasScaling = balanceConfig != null;
        if (hasScaling)
        {
            int baseHp = chosen != null ? chosen.maxHP : 50;
            int baseDmg = chosen != null ? chosen.attackDamage : 5;

            int scaledHp = Mathf.RoundToInt(baseHp * currentEnemyHpMultiplier);
            int scaledDmg = Mathf.RoundToInt(baseDmg * currentEnemyDamageMultiplier);

            if (isBossEncounter)
            {
                GameFlowManager flow = GameFlowManager.Instance;
                if (flow != null)
                {
                    scaledHp = Mathf.RoundToInt(scaledHp * flow.BossHpMultiplier) + flow.BossHpBonus;
                    scaledDmg = Mathf.RoundToInt(scaledDmg * flow.BossDamageMultiplier) + flow.BossDamageBonus;
                }
            }
            currentEnemy.ApplyDifficulty(scaledHp, scaledDmg);
        }
        else if (isBossEncounter)
        {
            GameFlowManager flow = GameFlowManager.Instance;
            if (flow != null && chosen != null)
            {
                int scaledHp = Mathf.RoundToInt(chosen.maxHP * flow.BossHpMultiplier) + flow.BossHpBonus;
                int scaledDmg = Mathf.RoundToInt(chosen.attackDamage * flow.BossDamageMultiplier) + flow.BossDamageBonus;
                currentEnemy.ApplyDifficulty(scaledHp, scaledDmg);
            }
        }
    }

    public void StartEncounterFromMap()
    {
        if (hasStartedEncounter)
            return;

        Debug.Log("[BattleManager] StartEncounterFromMap llamado");
        StartCoroutine(DelayedStartEncounter());
    }

    private IEnumerator DelayedStartEncounter()
    {
        yield return null;
        StartNewEncounter();
    }

    private void SyncEncounterIndexFromFlow()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null)
        {
            // Ejes explcitos de escalado: bioma real + encounter dentro de ese bioma.
            int flowStageIndex = Mathf.Max(0, flow.CurrentStageIndex);
            int flowEncounterInStage = Mathf.Max(0, flow.EncounterInStageIndex);
            int flowEncounterIndex = Mathf.Max(0, flow.EncounterIndex);

            if (hasStartedEncounter && flowStageIndex != currentStageIndex)
                Debug.LogWarning($"[Battle] Stage mismatch detected before scaling. Local={currentStageIndex}, Flow={flowStageIndex}.");

            currentStageIndex = flowStageIndex;
            encounterInStage = flowEncounterInStage;

            // EncounterIndex global slo para telemetra/progreso total de la run.
            encounterIndex = flowEncounterIndex;

            if (encounterInStage > encounterIndex)
                Debug.LogWarning($"[Battle] Invalid encounter state. EncounterInStage={encounterInStage} > EncounterGlobal={encounterIndex}.");
        }
    }

    private void ResolveBalanceConfig()
    {
        if (balanceConfig == null)
            balanceConfig = RunBalanceConfig.LoadDefault();
    }

    private void ReplaceEnemy(Enemy prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[Battle] Enemy prefab missing.");
            return;
        }

        bool prefabIsSceneObject = prefab.gameObject.scene.IsValid();
        Enemy newEnemy;
        if (prefabIsSceneObject)
        {
            newEnemy = prefab;
        }
        else
        {
            Vector3 position = enemySpawnPoint != null ? enemySpawnPoint.position : Vector3.zero;
            Quaternion rotation = enemySpawnPoint != null ? enemySpawnPoint.rotation : Quaternion.identity;
            newEnemy = Instantiate(prefab, position, rotation);
        }

        if (currentEnemy != null && currentEnemy != newEnemy)
        {
            currentEnemy.Defeated -= OnEnemyDefeated;
            Destroy(currentEnemy.gameObject);
        }

        currentEnemy = newEnemy;
        if (enemySpawnPoint != null)
            currentEnemy.transform.SetPositionAndRotation(enemySpawnPoint.position, enemySpawnPoint.rotation);

        currentEnemy.gameObject.SetActive(true);
        currentEnemy.Defeated += OnEnemyDefeated;
    }

}