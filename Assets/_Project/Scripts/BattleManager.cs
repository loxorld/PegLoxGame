using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Enemy enemy;                 // el Enemy de la escena
    [SerializeField] private EnemyData[] enemiesPool;     // pool de datos


    public event System.Action EncounterCompleted;

    [SerializeField] private int enemiesToDefeat = 3;
    private int defeatedCount = 0;

    [Header("Flow")]
    [SerializeField] private float respawnDelay = 0.5f;

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
        SpawnRandomEnemy();
    }

    private void OnDestroy()
    {
        if (enemy != null)
            enemy.Defeated -= OnEnemyDefeated;
    }

    private void OnEnemyDefeated()
    {
        defeatedCount++;

        if (defeatedCount >= enemiesToDefeat)
        {
            Debug.Log("Encounter completed!");
            EncounterCompleted?.Invoke();

            // Reiniciar encounter automáticamente
            defeatedCount = 0;
            Invoke(nameof(SpawnRandomEnemy), respawnDelay);
            return;
        }

        Invoke(nameof(SpawnRandomEnemy), respawnDelay);
    }


    private void SpawnRandomEnemy()
    {
        if (enemiesPool == null || enemiesPool.Length == 0)
        {
            Debug.LogError("BattleManager: enemiesPool is empty.");
            return;
        }

        EnemyData chosen = enemiesPool[Random.Range(0, enemiesPool.Length)];
        enemy.SetDataAndReset(chosen);
    }
}
