using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [SerializeField] private BattleManager battle;
    [SerializeField] private RelicManager relics;

    [Header("Reward Pool")]
    [SerializeField] private ShotEffectBase[] relicPool;

    private void Start()
    {
        if (battle != null)
            battle.EncounterCompleted += OnEncounterCompleted;
    }

    private void OnDestroy()
    {
        if (battle != null)
            battle.EncounterCompleted -= OnEncounterCompleted;
    }

    private void OnEncounterCompleted()
    {
        if (relics == null || relicPool == null || relicPool.Length == 0) return;

        ShotEffectBase chosen = relicPool[Random.Range(0, relicPool.Length)];
        relics.AddRelic(chosen);

        Debug.Log($"Reward gained: {chosen.name}");
    }
}
