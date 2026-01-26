using UnityEngine;
using UnityEngine.InputSystem;

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

    private OrbData[] pendingOrbs;
    private bool awaitingChoice;

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

    private void Update()
    {
        // MVP: elección por teclado en Editor/PC
        if (!awaitingChoice) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) ChooseOrb(1);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) ChooseOrb(2);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) ChooseOrb(3);
    }

    private void OnEncounterCompleted()
    {
        bool giveOrb = Random.value < chanceOrb;

        if (giveOrb && orbs != null && orbPool != null && orbPool.Length > 0)
        {
            pendingOrbs = GetRandomUniqueOrbs(3);
            awaitingChoice = true;

            Debug.Log("[Reward] Choose an orb: 1/2/3");
            for (int i = 0; i < pendingOrbs.Length; i++)
                Debug.Log($"  [{i + 1}] {pendingOrbs[i].orbName}");

            return;
        }

        if (relics != null && relicPool != null && relicPool.Length > 0)
        {
            ShotEffectBase chosen = relicPool[Random.Range(0, relicPool.Length)];
            relics.AddRelic(chosen);
            Debug.Log($"[Reward] Relic reward: {chosen.name}");
        }
    }

    public void ChooseOrb(int choiceIndex)
    {
        Debug.Log($"[Reward] ChooseOrb called with: {choiceIndex}");

        if (!awaitingChoice || pendingOrbs == null) return;

        int i = choiceIndex - 1;
        if (i < 0 || i >= pendingOrbs.Length) return;

        OrbData chosen = pendingOrbs[i];
        awaitingChoice = false;
        pendingOrbs = null;

        if (orbs == null)
        {
            Debug.LogError("[Reward] OrbManager reference missing.");
            return;
        }

        Debug.Log($"[Reward] Applying orb reward: {chosen.orbName}");
        orbs.AddOrb(chosen);

        Debug.Log($"[Reward] Chosen orb: {chosen.orbName}");
    }

    private OrbData[] GetRandomUniqueOrbs(int count)
    {
        count = Mathf.Clamp(count, 1, orbPool.Length);

        var used = new System.Collections.Generic.HashSet<OrbData>();

        int guard = 0;
        while (used.Count < count && guard < 200)
        {
            guard++;
            OrbData candidate = orbPool[Random.Range(0, orbPool.Length)];
            if (candidate != null) used.Add(candidate);
        }

        OrbData[] result = new OrbData[used.Count];
        int idx = 0;
        foreach (var o in used)
            result[idx++] = o;

        return result;
    }
}
