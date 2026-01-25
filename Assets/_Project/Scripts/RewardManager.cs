using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [SerializeField] private BattleManager battle;

    [Header("Reward Targets")]
    [SerializeField] private RelicManager relics;
    [SerializeField] private OrbManager orbs;

    [Header("Reward Pools")]
    [SerializeField] private ShotEffectBase[] relicPool;
    [SerializeField] private OrbData[] orbPool;

    [Header("Rules")]
    [SerializeField, Range(0f, 1f)] private float chanceOrb = 0.5f; // 50/50

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
        bool giveOrb = Random.value < chanceOrb;

        if (giveOrb && orbs != null && orbPool != null && orbPool.Length > 0)
        {
            OrbData chosen = orbPool[Random.Range(0, orbPool.Length)];
            orbs.AddOrb(chosen);
            Debug.Log($"[Reward] Orb reward: {chosen.orbName}");
            return;
        }

        if (relics != null && relicPool != null && relicPool.Length > 0)
        {
            ShotEffectBase chosen = relicPool[Random.Range(0, relicPool.Length)];
            relics.AddRelic(chosen);
            Debug.Log($"[Reward] Relic reward: {chosen.name}");
        }
    }
}
