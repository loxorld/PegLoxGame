using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum RewardKind
{
    Orb,
    Relic
}

[Serializable]
public struct RewardOption
{
    public RewardKind kind;
    public OrbData orb;
    public ShotEffectBase relic;

    public bool IsValid =>
        (kind == RewardKind.Orb && orb != null) ||
        (kind == RewardKind.Relic && relic != null);

    public string DisplayName =>
        kind == RewardKind.Orb ? (orb != null ? orb.orbName : "-") :
        (relic != null ? relic.DisplayName : "-");

    public Sprite DisplayIcon =>
        kind == RewardKind.Orb ? (orb != null ? orb.icon : null) :
        (relic != null ? relic.Icon : null);

    public string DisplayDescription =>
        kind == RewardKind.Orb ? (orb != null ? orb.description : "") :
        (relic != null ? relic.Description : "");
}

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
    [SerializeField, Range(0f, 1f)] private float chanceOrb = 0.5f; // probabilidad por slot
    [SerializeField] private bool avoidDuplicatesInSameRoll = true;

    [Header("Debug / PC Fallback")]
    [SerializeField] private bool enableKeyboardFallback = true;

    // UI/event consumers
    public event Action<RewardOption[]> RewardChoicesPresented;
    public event Action RewardResolved;

    public bool IsAwaitingChoice => awaitingChoice;
    public IReadOnlyList<RewardOption> CurrentChoices => pendingChoices;

    private RewardOption[] pendingChoices;
    private bool awaitingChoice;
    private bool selectionLocked;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
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
        if (!enableKeyboardFallback) return;
        if (!awaitingChoice) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) Choose(1);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) Choose(2);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) Choose(3);
    }

    private void OnEncounterCompleted()
    {
        ResolveReferences();
        // Generar 3 opciones mixtas (Orb/Relic)
        pendingChoices = GenerateMixedChoices(3);

        // Si no pudimos generar nada válido, resolvemos y continuamos
        if (pendingChoices == null || pendingChoices.Length == 0)
        {
            Debug.LogWarning("[Reward] No valid rewards available. Continuing.");
            ResolveRewardAndContinue();
            return;
        }

        awaitingChoice = true;
        selectionLocked = false;

        GameFlowManager.Instance?.SetState(GameState.RewardChoice);

        Debug.Log("[Reward] Choose a reward: 1/2/3");
        for (int i = 0; i < pendingChoices.Length; i++)
            Debug.Log($"  [{i + 1}] {pendingChoices[i].kind} - {pendingChoices[i].DisplayName}");

        RewardChoicesPresented?.Invoke(pendingChoices);
    }

    public void Choose(int choiceIndex)
    {
        if (!awaitingChoice) return;
        if (selectionLocked) return;

        if (pendingChoices == null || pendingChoices.Length == 0) return;

        int i = choiceIndex - 1;
        if (i < 0 || i >= pendingChoices.Length) return;

        ResolveReferences();

        RewardOption chosen = pendingChoices[i];
        if (!chosen.IsValid) return;

        // lock EXACTLY-ONCE
        selectionLocked = true;
        awaitingChoice = false;

        // Limpiamos primero para evitar dobles aplicaciones por eventos
        pendingChoices = null;

        // Aplicar recompensa
        if (chosen.kind == RewardKind.Orb)
        {
            if (orbs == null)
            {
                Debug.LogError("[Reward] OrbManager reference missing.");
                ResolveRewardAndContinue();
                return;
            }

            orbs.AddOrb(chosen.orb);
            Debug.Log($"[Reward] Chosen ORB: {chosen.orb.orbName}");
        }
        else // Relic
        {
            if (relics == null)
            {
                Debug.LogError("[Reward] RelicManager reference missing.");
                ResolveRewardAndContinue();
                return;
            }

            relics.AddRelic(chosen.relic);
            Debug.Log($"[Reward] Chosen RELIC: {chosen.relic.name}");
        }

        ResolveRewardAndContinue();
    }

    private void ResolveReferences()
    {
        if (battle == null)
            battle = FindObjectOfType<BattleManager>();

        if (orbs == null)
            orbs = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);

        if (relics == null)
            relics = RelicManager.Instance ?? FindObjectOfType<RelicManager>(true);
    }

    private void ResolveRewardAndContinue()
    {
        RewardResolved?.Invoke();

        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null)
        {
            flow.AdvanceEncounter();
            flow.StartCoroutine(WaitForMapManagerAndSetState(flow));
        }

        SceneManager.LoadScene("MapScene", LoadSceneMode.Single);
    }

    private static IEnumerator WaitForMapManagerAndSetState(GameFlowManager flow)
    {
        if (flow == null)
            yield break;

        MapManager mapManager = null;
        while (mapManager == null)
        {
            mapManager = FindObjectOfType<MapManager>();
            yield return null;
        }

        flow.SetState(GameState.MapNavigation);
    }

    private RewardOption[] GenerateMixedChoices(int count)
    {
        count = Mathf.Clamp(count, 1, 3);

        var result = new List<RewardOption>(count);

        // Para evitar duplicados dentro del mismo roll
        var usedOrbs = new HashSet<OrbData>();
        var usedRelics = new HashSet<ShotEffectBase>();

        int guard = 0;
        while (result.Count < count && guard < 200)
        {
            guard++;

            bool wantOrb = UnityEngine.Random.value < chanceOrb;

            // Si el pool del tipo elegido está vacío, intentamos el otro
            if (wantOrb && (orbPool == null || orbPool.Length == 0)) wantOrb = false;
            if (!wantOrb && (relicPool == null || relicPool.Length == 0)) wantOrb = true;

            if (wantOrb)
            {
                OrbData o = orbPool[UnityEngine.Random.Range(0, orbPool.Length)];
                if (o == null) continue;

                if (avoidDuplicatesInSameRoll && usedOrbs.Contains(o)) continue;

                usedOrbs.Add(o);
                result.Add(new RewardOption
                {
                    kind = RewardKind.Orb,
                    orb = o,
                    relic = null
                });
            }
            else
            {
                ShotEffectBase r = relicPool[UnityEngine.Random.Range(0, relicPool.Length)];
                if (r == null) continue;

                if (avoidDuplicatesInSameRoll && usedRelics.Contains(r)) continue;

                usedRelics.Add(r);
                result.Add(new RewardOption
                {
                    kind = RewardKind.Relic,
                    orb = null,
                    relic = r
                });
            }
        }

        // Filtrar inválidos por si algo raro pasó
        result.RemoveAll(x => !x.IsValid);

        return result.ToArray();
    }
}
