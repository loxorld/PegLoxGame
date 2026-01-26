using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemy;                 // Enemy de la escena
    [SerializeField] private EnemyData[] enemiesPool;     // pool de datos

    public event System.Action EncounterCompleted;

    [Header("Encounter")]
    [SerializeField] private int enemiesToDefeat = 3;

    [Header("Flow")]
    [SerializeField] private float respawnDelay = 0.5f;

    private int defeatedCount = 0;
    private bool waitingForRewards = false;

    public Enemy CurrentEnemy => enemy;

    private void Start()
    {
        if (enemy == null)
        {
            Debug.LogError("BattleManager: Enemy reference missing.");
            return;
        }

        enemy.Defeated += OnEnemyDefeated;

        defeatedCount = 0;
        waitingForRewards = false;

        SpawnRandomEnemy();
    }

    private void OnDestroy()
    {
        if (enemy != null)
            enemy.Defeated -= OnEnemyDefeated;
    }

    private void OnEnemyDefeated()
    {
        if (waitingForRewards) return;

        defeatedCount++;

        if (defeatedCount >= enemiesToDefeat)
        {
            Debug.Log("Encounter completed!");
            waitingForRewards = true;

            EncounterCompleted?.Invoke();
            return; // IMPORTANTE: no spawnear acá
        }

        Invoke(nameof(SpawnRandomEnemy), respawnDelay);
    }

    // Llamado por RewardManager cuando termina la elección/recompensa
    public void ContinueAfterRewards()
    {
        if (!waitingForRewards) return;

        waitingForRewards = false;
        defeatedCount = 0;

        Invoke(nameof(SpawnRandomEnemy), respawnDelay);
    }

    private void SpawnRandomEnemy()
    {
        if (waitingForRewards) return;

        if (enemiesPool == null || enemiesPool.Length == 0)
        {
            Debug.LogError("BattleManager: enemiesPool is empty.");
            return;
        }

        EnemyData chosen = enemiesPool[Random.Range(0, enemiesPool.Length)];
        enemy.SetDataAndReset(chosen);
    }
}
