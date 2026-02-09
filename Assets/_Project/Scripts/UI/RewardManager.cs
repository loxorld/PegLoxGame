using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum RewardKind
{
    Orb,
    OrbUpgrade,
    Relic
}

[Serializable]
public struct RewardOption
{
    public RewardKind kind;
    public OrbData orb;
    public OrbInstance orbInstance;
    public ShotEffectBase relic;

    public bool IsValid =>
        (kind == RewardKind.Orb && orb != null) ||
        (kind == RewardKind.OrbUpgrade && orbInstance != null && orbInstance.CanLevelUp) ||
        (kind == RewardKind.Relic && relic != null);

    public string DisplayName =>
        kind == RewardKind.Orb ? (orb != null ? orb.orbName : "-") :
        kind == RewardKind.OrbUpgrade ? (orbInstance != null ? orbInstance.OrbName : "-") :
        (relic != null ? relic.DisplayName : "-");

    public Sprite DisplayIcon =>
        kind == RewardKind.Orb ? (orb != null ? orb.icon : null) :
        kind == RewardKind.OrbUpgrade ? (orbInstance != null ? orbInstance.Icon : null) :
        (relic != null ? relic.Icon : null);

    public string DisplayDescription =>
        kind == RewardKind.Orb ? (orb != null ? orb.description : "") :
        kind == RewardKind.OrbUpgrade ? BuildOrbUpgradeDescription(orbInstance) :
        (relic != null ? relic.Description : "");
    private static string BuildOrbUpgradeDescription(OrbInstance orbInstance)
    {
        if (orbInstance == null)
            return "";

        string baseDescription = orbInstance.Description;
        int currentDamage = orbInstance.DamagePerHit;
        int nextLevel = orbInstance.Level + 1;
        int nextDamage = currentDamage;

        if (orbInstance.BaseData != null)
            nextDamage = orbInstance.BaseData.damagePerHit + Mathf.Max(0, nextLevel - 1);

        int damageDelta = Mathf.Max(0, nextDamage - currentDamage);
        string upgradeText = damageDelta > 0
            ? $"Mejora: +{damageDelta} daño (Lv+1)"
            : "Mejora: Lv+1";

        if (string.IsNullOrWhiteSpace(baseDescription))
            return upgradeText;

        return $"{baseDescription}\n{upgradeText}";
    }
}

public class RewardManager : MonoBehaviour
{
    [SerializeField] private BattleManager battle;
    [SerializeField] private RunBalanceConfig balanceConfig;

    [Header("Reward Targets")]
    [SerializeField] private RelicManager relics;
    [SerializeField] private OrbManager orbs;

    [Header("Reward Pools")]
    [SerializeField] private ShotEffectBase[] relicPool;
    [SerializeField] private OrbData[] orbPool;

    [Header("Rules")]
    [SerializeField, Range(0f, 1f)] private float chanceOrb = 0.5f; // probabilidad por slot
    [SerializeField] private bool allowOrbUpgrade = true;
    [SerializeField, Range(0f, 1f)] private float chanceOrbUpgrade = 0.2f;
    [SerializeField] private bool avoidDuplicatesInSameRoll = true;

    [Header("Coins Reward")]
    [SerializeField, Min(0)] private int encounterCoinsMin = 4;
    [SerializeField, Min(0)] private int encounterCoinsMax = 8;

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
        GrantEncounterCoins();
        GameFlowManager.Instance?.SaveRun();
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
        else if (chosen.kind == RewardKind.OrbUpgrade)
        {
            if (orbs == null)
            {
                Debug.LogError("[Reward] OrbManager reference missing.");
                ResolveRewardAndContinue();
                return;
            }

            List<OrbInstance> upgradeableOrbs = GetUpgradeableOrbs();
            if (upgradeableOrbs.Count == 0)
            {
                Debug.LogWarning("[Reward] No upgradeable orbs available.");
                ResolveRewardAndContinue();
                return;
            }

            OrbInstance target = null;
            if (chosen.orbInstance != null && chosen.orbInstance.CanLevelUp && upgradeableOrbs.Contains(chosen.orbInstance))
                target = chosen.orbInstance;
            else
                target = upgradeableOrbs[UnityEngine.Random.Range(0, upgradeableOrbs.Count)];

            int previousLevel = target.Level;
            target.LevelUp();
            Debug.Log($"[Reward] Orb upgraded: {target.OrbName} ({previousLevel} -> {target.Level})");
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

        GameFlowManager.Instance?.SaveRun();
        ResolveRewardAndContinue();
    }


    private void GrantEncounterCoins()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null)
            return;

        RunBalanceConfig balance = ResolveBalanceConfig();
        int stageIndex = GetStageIndexForBalance(flow);
        int min = balance != null ? balance.GetEncounterCoinsMin(stageIndex, encounterCoinsMin) : encounterCoinsMin;
        int max = balance != null ? balance.GetEncounterCoinsMax(stageIndex, encounterCoinsMax) : encounterCoinsMax;
        min = Mathf.Max(0, min);
        max = Mathf.Max(min, max);
        if (max <= 0)
            return;

        int reward = UnityEngine.Random.Range(min, max + 1);
        if (reward > 0)
            flow.AddCoins(reward);
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

    private RunBalanceConfig ResolveBalanceConfig()
    {
        if (balanceConfig == null)
            balanceConfig = RunBalanceConfig.LoadDefault();

        return balanceConfig;
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

        SceneManager.LoadScene(SceneCatalog.Load().MapScene, LoadSceneMode.Single);
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

    private List<OrbInstance> GetUpgradeableOrbs()
    {
        var upgradeable = new List<OrbInstance>();

        if (orbs == null || orbs.OwnedOrbInstances == null)
            return upgradeable;

        IReadOnlyList<OrbInstance> owned = orbs.OwnedOrbInstances;
        for (int i = 0; i < owned.Count; i++)
        {
            OrbInstance orb = owned[i];
            if (orb != null && orb.CanLevelUp)
                upgradeable.Add(orb);
        }

        return upgradeable;
    }

    private RewardOption[] GenerateMixedChoices(int count)
    {
        count = Mathf.Clamp(count, 1, 3);

        var result = new List<RewardOption>(count);
        GameFlowManager flow = GameFlowManager.Instance;
        int stageIndex = GetStageIndexForBalance(flow);
        RunBalanceConfig balance = ResolveBalanceConfig();
        float stageChanceOrb = balance != null ? balance.GetChanceOrb(stageIndex, chanceOrb) : chanceOrb;
        float stageChanceUpgrade = balance != null ? balance.GetChanceOrbUpgrade(stageIndex, chanceOrbUpgrade) : chanceOrbUpgrade;

        // Para evitar duplicados dentro del mismo roll
        var usedOrbs = new HashSet<OrbData>();
        var usedRelics = new HashSet<ShotEffectBase>();
        var usedUpgradeOrbs = new HashSet<OrbInstance>();

        List<OrbInstance> upgradeableOrbs = allowOrbUpgrade ? GetUpgradeableOrbs() : new List<OrbInstance>();
        bool hasUpgradeableOrbs = upgradeableOrbs.Count > 0;
        int guard = 0;
        while (result.Count < count && guard < 200)
        {
            guard++;

            bool wantUpgrade = allowOrbUpgrade && hasUpgradeableOrbs && UnityEngine.Random.value < stageChanceUpgrade;

            if (wantUpgrade)
            {
                List<OrbInstance> availableUpgrades = upgradeableOrbs;
                if (avoidDuplicatesInSameRoll && usedUpgradeOrbs.Count > 0)
                {
                    availableUpgrades = new List<OrbInstance>();
                    for (int i = 0; i < upgradeableOrbs.Count; i++)
                    {
                        OrbInstance orb = upgradeableOrbs[i];
                        if (orb != null && !usedUpgradeOrbs.Contains(orb))
                            availableUpgrades.Add(orb);
                    }
                }

                if (availableUpgrades.Count > 0)
                {
                    OrbInstance target = availableUpgrades[UnityEngine.Random.Range(0, availableUpgrades.Count)];
                    usedUpgradeOrbs.Add(target);
                    result.Add(new RewardOption
                    {
                        kind = RewardKind.OrbUpgrade,
                        orb = null,
                        orbInstance = target,
                        relic = null
                    });
                    continue;
                }

                wantUpgrade = false;
            }

            bool wantOrb = UnityEngine.Random.value < stageChanceOrb;


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
                    orbInstance = null,
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
                    orbInstance = null,
                    relic = r
                });
            }
        }

        // Filtrar inválidos por si algo raro pasó
        result.RemoveAll(x => !x.IsValid);

        return result.ToArray();
    }
    private int GetStageIndexForBalance(GameFlowManager flow)
    {
        if (flow != null)
            return Mathf.Max(0, flow.EncounterIndex);

        if (battle != null)
            return Mathf.Max(0, battle.EncounterIndex);

        return 0;
    }
}