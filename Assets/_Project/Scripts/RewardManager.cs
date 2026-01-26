using System;
using System.Collections.Generic;
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

    // UI/event consumers
    public event Action<OrbData[]> OrbChoicesPresented;
    public event Action RewardResolved;

    public bool IsAwaitingChoice => awaitingChoice;
    public IReadOnlyList<OrbData> CurrentChoices => pendingOrbs;

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
        // Fallback para Editor/PC (podés desactivarlo cuando UI esté OK)
        if (!awaitingChoice) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) ChooseOrb(1);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) ChooseOrb(2);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) ChooseOrb(3);
    }

    private void OnEncounterCompleted()
    {
        bool giveOrb = UnityEngine.Random.value < chanceOrb;

        if (giveOrb && orbs != null && orbPool != null && orbPool.Length > 0)
        {
            pendingOrbs = GetRandomUniqueOrbs(3);
            awaitingChoice = true;

            GameFlowManager.Instance?.SetState(GameState.RewardChoice);

            Debug.Log("[Reward] Choose an orb: 1/2/3");
            for (int i = 0; i < pendingOrbs.Length; i++)
                Debug.Log($"  [{i + 1}] {pendingOrbs[i].orbName}");

            // Avisar a UI
            OrbChoicesPresented?.Invoke(pendingOrbs);

            return;
        }

        // Si no hay orbe, damos reliquia (si hay pool)
        if (relics != null && relicPool != null && relicPool.Length > 0)
        {
            ShotEffectBase chosen = relicPool[UnityEngine.Random.Range(0, relicPool.Length)];
            relics.AddRelic(chosen);
            Debug.Log($"[Reward] Relic reward: {chosen.name}");
        }

        ResolveRewardAndContinue();
    }

    public void ChooseOrb(int choiceIndex)
    {
        if (!awaitingChoice || pendingOrbs == null) return;

        int i = choiceIndex - 1;
        if (i < 0 || i >= pendingOrbs.Length) return;

        OrbData chosen = pendingOrbs[i];

        awaitingChoice = false;
        pendingOrbs = null;

        if (orbs == null)
        {
            Debug.LogError("[Reward] OrbManager reference missing.");
            ResolveRewardAndContinue();
            return;
        }

        orbs.AddOrb(chosen);
        Debug.Log($"[Reward] Chosen orb: {chosen.orbName}");

        ResolveRewardAndContinue();
    }

    private void ResolveRewardAndContinue()
    {
        RewardResolved?.Invoke();

        GameFlowManager.Instance?.SetState(GameState.Combat);

        if (battle != null)
            battle.ContinueAfterRewards();
    }

    private OrbData[] GetRandomUniqueOrbs(int count)
    {
        count = Mathf.Clamp(count, 1, orbPool.Length);

        var used = new System.Collections.Generic.HashSet<OrbData>();

        int guard = 0;
        while (used.Count < count && guard < 200)
        {
            guard++;
            OrbData candidate = orbPool[UnityEngine.Random.Range(0, orbPool.Length)];
            if (candidate != null) used.Add(candidate);
        }

        OrbData[] result = new OrbData[used.Count];
        int idx = 0;
        foreach (var o in used)
            result[idx++] = o;

        return result;
    }
}
