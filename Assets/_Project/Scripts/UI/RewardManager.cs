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
    Relic,
    Heal
}

public enum RewardOfferOrigin
{
    StandardRoll,
    GuaranteedRelic,
    GuaranteedUpgrade,
    UpgradeFallbackOrb,
    RelicFallbackChoice,
    DuplicateOrbUpgrade,
    HealFallbackChoice
}

[Serializable]
public struct RewardOption
{
    public RewardKind kind;
    public OrbData orb;
    public OrbInstance orbInstance;
    public ShotEffectBase relic;
    public int healAmount;
    public RewardOfferOrigin offerOrigin;
    public CombatEncounterType encounterType;

    public bool IsValid =>
        (kind == RewardKind.Orb && orb != null) ||
        (kind == RewardKind.OrbUpgrade && orbInstance != null && orbInstance.CanLevelUp) ||
        (kind == RewardKind.Relic && relic != null) ||
        (kind == RewardKind.Heal && healAmount > 0);

    public bool IsGuaranteed =>
        offerOrigin == RewardOfferOrigin.GuaranteedRelic ||
        offerOrigin == RewardOfferOrigin.GuaranteedUpgrade;

    public string DisplayName =>
        kind == RewardKind.Orb ? (orb != null ? orb.orbName : "-") :
        kind == RewardKind.OrbUpgrade ? (orbInstance != null ? orbInstance.OrbName : "-") :
        kind == RewardKind.Relic ? (relic != null ? relic.DisplayName : "-") :
        $"Curacion +{healAmount} HP";

    public Sprite DisplayIcon =>
        kind == RewardKind.Orb ? (orb != null ? orb.icon : null) :
        kind == RewardKind.OrbUpgrade ? (orbInstance != null ? orbInstance.Icon : null) :
        kind == RewardKind.Relic ? (relic != null ? relic.Icon : null) :
        null;

    public string KindLabel =>
        kind == RewardKind.OrbUpgrade ? "UPGRADE" :
        kind == RewardKind.Orb ? "ORB" :
        kind == RewardKind.Relic ? "RELIC" :
        "HEAL";

    public string SourceTag
    {
        get
        {
            return offerOrigin switch
            {
                RewardOfferOrigin.GuaranteedRelic => "RELIC ASEGURADA",
                RewardOfferOrigin.GuaranteedUpgrade => "MEJORA ASEGURADA",
                RewardOfferOrigin.UpgradeFallbackOrb => "REEMPLAZO DE MEJORA",
                RewardOfferOrigin.RelicFallbackChoice => "REEMPLAZO DE RELIC",
                RewardOfferOrigin.DuplicateOrbUpgrade => "DUPLICADO -> MEJORA",
                RewardOfferOrigin.HealFallbackChoice => "REEMPLAZO DE DUPLICADO",
                _ => encounterType switch
                {
                    CombatEncounterType.Elite => "BOTIN ELITE",
                    CombatEncounterType.MiniBoss => "BOTIN MINIBOSS",
                    CombatEncounterType.Boss => "BOTIN DE BOSS",
                    _ => "BOTIN"
                }
            };
        }
    }

    public string SourceDescription
    {
        get
        {
            string encounterLabel = EncounterLabel(encounterType);
            return offerOrigin switch
            {
                RewardOfferOrigin.GuaranteedRelic => $"Esta reliquia estaba asegurada por {encounterLabel}.",
                RewardOfferOrigin.GuaranteedUpgrade => $"Esta mejora estaba asegurada por {encounterLabel}.",
                RewardOfferOrigin.UpgradeFallbackOrb => $"Este orbe reemplaza una mejora no disponible en {encounterLabel}.",
                RewardOfferOrigin.RelicFallbackChoice => $"Esta opcion reemplaza una reliquia no disponible en {encounterLabel}.",
                RewardOfferOrigin.DuplicateOrbUpgrade => $"Ya tenias este orbe, asi que se convierte en una mejora por {encounterLabel}.",
                RewardOfferOrigin.HealFallbackChoice => $"Esta curacion reemplaza una recompensa duplicada o no disponible en {encounterLabel}.",
                _ => encounterType switch
                {
                    CombatEncounterType.Elite => "Botin extra por superar un Elite.",
                    CombatEncounterType.MiniBoss => "Botin premium por derrotar al Miniboss.",
                    CombatEncounterType.Boss => "Botin mayor por vencer al Boss.",
                    _ => "Elige una recompensa para seguir la run."
                }
            };
        }
    }

    public string DisplayDescription =>
        kind == RewardKind.Orb ? (orb != null ? orb.description : string.Empty) :
        kind == RewardKind.OrbUpgrade ? BuildOrbUpgradeDescription(orbInstance, offerOrigin) :
        kind == RewardKind.Relic ? (relic != null ? relic.Description : string.Empty) :
        BuildHealDescription(healAmount);

    private static string BuildOrbUpgradeDescription(OrbInstance orbInstance, RewardOfferOrigin offerOrigin)
    {
        if (orbInstance == null)
            return string.Empty;

        string baseDescription = orbInstance.Description;
        int currentDamage = orbInstance.DamagePerHit;
        int nextLevel = orbInstance.Level + 1;
        int nextDamage = currentDamage;

        if (orbInstance.BaseData != null)
            nextDamage = orbInstance.BaseData.damagePerHit + Mathf.Max(0, nextLevel - 1);

        int damageDelta = Mathf.Max(0, nextDamage - currentDamage);
        string upgradeText = damageDelta > 0
            ? $"Mejora: +{damageDelta} dano (Lv+1)"
            : "Mejora: Lv+1";

        string duplicateText = offerOrigin == RewardOfferOrigin.DuplicateOrbUpgrade
            ? "Ya tienes este orbe. Esta copia mejora su nivel."
            : string.Empty;

        if (string.IsNullOrWhiteSpace(baseDescription))
            return string.IsNullOrWhiteSpace(duplicateText)
                ? upgradeText
                : $"{duplicateText}\n{upgradeText}";

        if (string.IsNullOrWhiteSpace(duplicateText))
            return $"{baseDescription}\n{upgradeText}";

        return $"{baseDescription}\n{duplicateText}\n{upgradeText}";
    }

    private static string BuildHealDescription(int healAmount)
    {
        return healAmount > 0
            ? $"Recupera {healAmount} de vida antes del siguiente nodo."
            : string.Empty;
    }

    private static string EncounterLabel(CombatEncounterType encounterType)
    {
        return encounterType switch
        {
            CombatEncounterType.Elite => "el Elite",
            CombatEncounterType.MiniBoss => "el Miniboss",
            CombatEncounterType.Boss => "el Boss",
            _ => "el combate"
        };
    }
}

public readonly struct EncounterRewardPreview
{
    public EncounterRewardPreview(
        CombatEncounterProfile encounterProfile,
        int baseCoinsMin,
        int baseCoinsMax,
        int choiceCount,
        int grantedCoins)
    {
        EncounterProfile = encounterProfile;
        BaseCoinsMin = Mathf.Max(0, baseCoinsMin);
        BaseCoinsMax = Mathf.Max(BaseCoinsMin, baseCoinsMax);
        ChoiceCount = Mathf.Max(1, choiceCount);
        GrantedCoins = Mathf.Max(0, grantedCoins);
    }

    public CombatEncounterProfile EncounterProfile { get; }
    public int BaseCoinsMin { get; }
    public int BaseCoinsMax { get; }
    public int ChoiceCount { get; }
    public int GrantedCoins { get; }
    public int BonusCoins => EncounterProfile.BonusCoins;
    public int TotalCoinsMin => BaseCoinsMin + BonusCoins;
    public int TotalCoinsMax => BaseCoinsMax + BonusCoins;
    public bool GuaranteesRelicChoice => EncounterProfile.GuaranteesRelicChoice;
    public bool GuaranteesUpgradeChoice => EncounterProfile.GuaranteesUpgradeChoice;
    public string EncounterLabel => string.IsNullOrWhiteSpace(EncounterProfile.Label) ? "Combate" : EncounterProfile.Label;
    public bool HasCoinReward => GrantedCoins > 0 || TotalCoinsMax > 0;

    public EncounterRewardPreview WithGrantedCoins(int grantedCoins)
    {
        return new EncounterRewardPreview(EncounterProfile, BaseCoinsMin, BaseCoinsMax, ChoiceCount, grantedCoins);
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
    [SerializeField, Range(0f, 1f)] private float chanceOrb = 0.5f;
    [SerializeField] private bool allowOrbUpgrade = true;
    [SerializeField, Range(0f, 1f)] private float chanceOrbUpgrade = 0.2f;
    [SerializeField] private bool allowHealRewards = true;
    [SerializeField, Range(0f, 1f)] private float chanceHealReward = 0.18f;
    [SerializeField] private bool avoidDuplicatesInSameRoll = true;
    [SerializeField, Min(1)] private int rewardChoiceCount = 3;
    [SerializeField, Min(1)] private int healRewardMin = 8;
    [SerializeField, Min(1)] private int healRewardMax = 14;
    [SerializeField, Min(0)] private int healRewardPerStageBonus = 2;

    [Header("Coins Reward")]
    [SerializeField, Min(0)] private int encounterCoinsMin = 4;
    [SerializeField, Min(0)] private int encounterCoinsMax = 8;

    [Header("Debug / PC Fallback")]
    [SerializeField] private bool enableKeyboardFallback = true;

    public event Action<RewardOption[]> RewardChoicesPresented;
    public event Action<EncounterRewardPreview, RewardOption[]> RewardChoicesDetailedPresented;
    public event Action RewardResolved;

    public bool IsAwaitingChoice => awaitingChoice;
    public IReadOnlyList<RewardOption> CurrentChoices => pendingChoices;
    public EncounterRewardPreview LastRewardPreview => lastRewardPreview;
    public EncounterRewardPreview CurrentEncounterPreview => BuildCurrentEncounterPreview();

    private RewardOption[] pendingChoices;
    private bool awaitingChoice;
    private bool selectionLocked;
    private bool battleSubscribed;
    private EncounterRewardPreview lastRewardPreview;

    private void Awake()
    {
        ResolveReferences();
        lastRewardPreview = BuildCurrentEncounterPreview();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeBattle();
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeBattle();
        lastRewardPreview = BuildCurrentEncounterPreview();
    }

    private void OnDisable()
    {
        UnsubscribeBattle();
    }

    private void OnDestroy()
    {
        UnsubscribeBattle();
    }

    private void Update()
    {
        if (!battleSubscribed)
        {
            ResolveReferences();
            SubscribeBattle();
        }

        if (!enableKeyboardFallback || !awaitingChoice || Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) Choose(1);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) Choose(2);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) Choose(3);
    }

    public static string BuildRewardPreviewText(EncounterRewardPreview preview)
    {
        var parts = new List<string>(4);
        if (preview.TotalCoinsMax > 0)
        {
            string coinsText = preview.TotalCoinsMin == preview.TotalCoinsMax
                ? $"+{preview.TotalCoinsMax} oro"
                : $"+{preview.TotalCoinsMin}-{preview.TotalCoinsMax} oro";
            parts.Add(coinsText);
        }

        parts.Add($"{preview.ChoiceCount} elecciones");

        if (preview.GuaranteesRelicChoice)
            parts.Add("relic asegurada");

        if (preview.GuaranteesUpgradeChoice)
            parts.Add("mejora asegurada");

        return parts.Count == 0
            ? "Botin estandar"
            : $"Botin: {string.Join(" | ", parts)}";
    }

    public static string BuildRewardOverlayTitle(EncounterRewardPreview preview)
    {
        return preview.EncounterProfile.Type switch
        {
            CombatEncounterType.Elite => "BOTIN ELITE",
            CombatEncounterType.MiniBoss => "BOTIN DEL MINIBOSS",
            CombatEncounterType.Boss => "BOTIN DEL BOSS",
            _ => "BOTIN DEL COMBATE"
        };
    }

    public static string BuildRewardOverlaySubtitle(EncounterRewardPreview preview)
    {
        string coinsText = preview.GrantedCoins > 0
            ? $"Ganaste {preview.GrantedCoins} de oro en esta pelea."
            : preview.TotalCoinsMax > 0
                ? preview.TotalCoinsMin == preview.TotalCoinsMax
                    ? $"Este combate entrega {preview.TotalCoinsMax} de oro."
                    : $"Este combate entrega entre {preview.TotalCoinsMin} y {preview.TotalCoinsMax} de oro."
                : "Este combate no agrega oro directo.";

        string choiceText = $"Elige 1 de {preview.ChoiceCount} recompensas para seguir la run.";
        string contractText = BuildRewardContractText(preview);

        return string.IsNullOrEmpty(contractText)
            ? $"{coinsText}\n{choiceText}"
            : $"{coinsText}\n{choiceText} {contractText}";
    }

    private void OnEncounterCompleted()
    {
        ResolveReferences();

        CombatEncounterProfile encounterProfile = battle != null
            ? battle.CurrentEncounterProfile
            : CombatEncounterProfile.CreateRegular();

        int grantedCoins = GrantEncounterCoins();
        lastRewardPreview = BuildEncounterRewardPreview(encounterProfile).WithGrantedCoins(grantedCoins);

        GameFlowManager.Instance?.SaveRun();
        pendingChoices = GenerateMixedChoices(lastRewardPreview.ChoiceCount, encounterProfile);

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
        RewardChoicesDetailedPresented?.Invoke(lastRewardPreview, pendingChoices);
    }

    public void Choose(int choiceIndex)
    {
        if (!awaitingChoice || selectionLocked)
            return;

        if (pendingChoices == null || pendingChoices.Length == 0)
            return;

        int index = choiceIndex - 1;
        if (index < 0 || index >= pendingChoices.Length)
            return;

        ResolveReferences();

        RewardOption chosen = pendingChoices[index];
        if (!chosen.IsValid)
            return;

        selectionLocked = true;
        awaitingChoice = false;
        pendingChoices = null;

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
        else if (chosen.kind == RewardKind.Heal)
        {
            int restored = ApplyHealingReward(chosen.healAmount);
            Debug.Log($"[Reward] Chosen HEAL: +{restored} HP");
        }
        else
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

    private int GrantEncounterCoins()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null)
            return 0;

        RunBalanceConfig balance = ResolveBalanceConfig();
        int stageIndex = GetStageIndexForBalance(flow);
        int min = balance != null ? balance.GetEncounterCoinsMin(stageIndex, encounterCoinsMin) : encounterCoinsMin;
        int max = balance != null ? balance.GetEncounterCoinsMax(stageIndex, encounterCoinsMax) : encounterCoinsMax;
        min = Mathf.Max(0, min);
        max = Mathf.Max(min, max);
        int bonusCoins = battle != null ? Mathf.Max(0, battle.CurrentEncounterBonusCoins) : 0;
        if (max <= 0 && bonusCoins <= 0)
            return 0;

        int reward = max > 0 ? UnityEngine.Random.Range(min, max + 1) : 0;
        reward += bonusCoins;
        if (reward > 0)
            flow.AddCoins(reward);

        return reward;
    }

    private void ResolveReferences()
    {
        if (battle == null)
            battle = ServiceRegistry.ResolveWithFallback(nameof(RewardManager), nameof(battle), () => ServiceRegistry.LegacyFind<BattleManager>());

        if (orbs == null)
            orbs = ServiceRegistry.ResolveWithFallback(nameof(RewardManager), nameof(orbs), () => OrbManager.Instance ?? ServiceRegistry.LegacyFind<OrbManager>(true));

        if (relics == null)
            relics = ServiceRegistry.ResolveWithFallback(nameof(RewardManager), nameof(relics), () => RelicManager.Instance ?? ServiceRegistry.LegacyFind<RelicManager>(true));
    }

    private void SubscribeBattle()
    {
        if (battle == null || battleSubscribed)
            return;

        battle.EncounterCompleted += OnEncounterCompleted;
        battleSubscribed = true;
    }

    private void UnsubscribeBattle()
    {
        if (!battleSubscribed)
            return;

        if (battle != null)
            battle.EncounterCompleted -= OnEncounterCompleted;

        battleSubscribed = false;
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
            bool shouldAdvanceEncounter = battle == null || !battle.LastEncounterWasBoss;
            if (shouldAdvanceEncounter)
                flow.AdvanceEncounter();
            flow.StartCoroutine(WaitForMapManagerAndSetState(flow));
        }

        SceneManager.LoadScene(SceneCatalog.Load().MapScene, LoadSceneMode.Single);
    }

    private EncounterRewardPreview BuildCurrentEncounterPreview()
    {
        ResolveReferences();

        CombatEncounterProfile encounterProfile = battle != null
            ? battle.CurrentEncounterProfile
            : CombatEncounterProfile.CreateRegular();

        return BuildEncounterRewardPreview(encounterProfile);
    }

    private EncounterRewardPreview BuildEncounterRewardPreview(CombatEncounterProfile encounterProfile)
    {
        GameFlowManager flow = GameFlowManager.Instance;
        RunBalanceConfig balance = ResolveBalanceConfig();
        int stageIndex = GetStageIndexForBalance(flow);
        int min = balance != null ? balance.GetEncounterCoinsMin(stageIndex, encounterCoinsMin) : encounterCoinsMin;
        int max = balance != null ? balance.GetEncounterCoinsMax(stageIndex, encounterCoinsMax) : encounterCoinsMax;
        return new EncounterRewardPreview(encounterProfile, min, max, rewardChoiceCount, 0);
    }

    private static string BuildRewardContractText(EncounterRewardPreview preview)
    {
        if (preview.GuaranteesRelicChoice && preview.GuaranteesUpgradeChoice)
            return "Incluye una relic asegurada y una mejora asegurada.";

        if (preview.GuaranteesRelicChoice)
            return "Incluye una relic asegurada.";

        if (preview.GuaranteesUpgradeChoice)
            return "Incluye una mejora asegurada.";

        return string.Empty;
    }

    private static IEnumerator WaitForMapManagerAndSetState(GameFlowManager flow)
    {
        if (flow == null)
            yield break;

        MapManager mapManager = null;
        while (mapManager == null)
        {
            mapManager = ServiceRegistry.ResolveWithFallback(nameof(RewardManager), "MapManagerDuringSceneTransition", () => ServiceRegistry.LegacyFind<MapManager>());
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

    private RewardOption[] GenerateMixedChoices(int count, CombatEncounterProfile encounterProfile)
    {
        count = Mathf.Clamp(count, 1, 3);

        var result = new List<RewardOption>(count);
        GameFlowManager flow = GameFlowManager.Instance;
        int stageIndex = GetStageIndexForBalance(flow);
        RunBalanceConfig balance = ResolveBalanceConfig();
        float stageChanceOrb = balance != null ? balance.GetChanceOrb(stageIndex, chanceOrb) : chanceOrb;
        float stageChanceUpgrade = balance != null ? balance.GetChanceOrbUpgrade(stageIndex, chanceOrbUpgrade) : chanceOrbUpgrade;
        if (encounterProfile.Type == CombatEncounterType.Elite)
        {
            stageChanceOrb = Mathf.Clamp01(stageChanceOrb - 0.08f);
            stageChanceUpgrade = Mathf.Clamp01(stageChanceUpgrade + 0.18f);
        }
        else if (encounterProfile.Type == CombatEncounterType.MiniBoss || encounterProfile.Type == CombatEncounterType.Boss)
        {
            stageChanceOrb = Mathf.Clamp01(stageChanceOrb - 0.12f);
            stageChanceUpgrade = Mathf.Clamp01(stageChanceUpgrade + 0.24f);
        }
        float stageChanceHeal = ResolveHealRewardChance(encounterProfile, flow);

        var usedOrbs = new HashSet<OrbData>();
        var usedRelics = new HashSet<ShotEffectBase>();
        var usedUpgradeOrbs = new HashSet<OrbInstance>();

        List<OrbInstance> upgradeableOrbs = allowOrbUpgrade ? GetUpgradeableOrbs() : new List<OrbInstance>();
        if (encounterProfile.GuaranteesRelicChoice)
        {
            bool addedRelic = TryAddRelicChoice(result, usedRelics, encounterProfile, RewardOfferOrigin.GuaranteedRelic);
            if (!addedRelic)
            {
                bool addedFallbackUpgrade = TryAddUpgradeChoice(result, upgradeableOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.RelicFallbackChoice);
                if (!addedFallbackUpgrade)
                {
                    bool addedFallbackOrb = TryAddOrbChoice(result, usedOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.RelicFallbackChoice);
                    if (!addedFallbackOrb)
                        TryAddHealChoice(result, encounterProfile, RewardOfferOrigin.HealFallbackChoice, stageIndex);
                }
            }
        }

        if (result.Count < count && encounterProfile.GuaranteesUpgradeChoice)
        {
            bool addedUpgrade = TryAddUpgradeChoice(result, upgradeableOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.GuaranteedUpgrade);
            if (!addedUpgrade)
            {
                bool addedOrbFallback = TryAddOrbChoice(result, usedOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.UpgradeFallbackOrb);
                if (!addedOrbFallback)
                {
                    bool addedRelicFallback = TryAddRelicChoice(result, usedRelics, encounterProfile, RewardOfferOrigin.UpgradeFallbackOrb);
                    if (!addedRelicFallback)
                        TryAddHealChoice(result, encounterProfile, RewardOfferOrigin.HealFallbackChoice, stageIndex);
                }
            }
        }

        int guard = 0;
        while (result.Count < count && guard < 200)
        {
            guard++;

            bool hasOrbChoices = HasAvailableOrbChoice(usedOrbs, usedUpgradeOrbs);
            bool hasRelicChoices = HasAvailableRelicChoice(usedRelics);
            bool hasUpgradeableOrbs = HasAvailableUpgradeChoice(upgradeableOrbs, usedUpgradeOrbs);
            bool hasHealChoices = CanOfferHealChoice(result);

            if (!hasOrbChoices && !hasRelicChoices && !hasUpgradeableOrbs && !hasHealChoices)
                break;

            if (hasHealChoices && UnityEngine.Random.value < stageChanceHeal)
            {
                if (TryAddHealChoice(result, encounterProfile, RewardOfferOrigin.StandardRoll, stageIndex))
                    continue;
            }

            bool wantUpgrade = allowOrbUpgrade && hasUpgradeableOrbs && UnityEngine.Random.value < stageChanceUpgrade;

            if (wantUpgrade)
            {
                if (TryAddUpgradeChoice(result, upgradeableOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.StandardRoll))
                    continue;
            }

            bool wantOrb = hasOrbChoices && UnityEngine.Random.value < stageChanceOrb;

            if (!wantOrb && !hasRelicChoices && hasOrbChoices)
                wantOrb = true;

            if (wantOrb)
            {
                if (TryAddOrbChoice(result, usedOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.StandardRoll))
                    continue;
            }

            if (TryAddRelicChoice(result, usedRelics, encounterProfile, RewardOfferOrigin.StandardRoll))
                continue;

            if (TryAddOrbChoice(result, usedOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.StandardRoll))
                continue;

            if (TryAddUpgradeChoice(result, upgradeableOrbs, usedUpgradeOrbs, encounterProfile, RewardOfferOrigin.StandardRoll))
                continue;

            if (TryAddHealChoice(result, encounterProfile, RewardOfferOrigin.StandardRoll, stageIndex))
                continue;

            break;
        }

        result.RemoveAll(option => !option.IsValid);
        return result.ToArray();
    }

    private float ResolveHealRewardChance(CombatEncounterProfile encounterProfile, GameFlowManager flow)
    {
        if (!allowHealRewards)
            return 0f;

        int maxHp = GetPlayerMaxHp(flow);
        if (maxHp <= 0)
            return 0f;

        int missingHp = GetMissingPlayerHp(flow);
        if (missingHp <= 0)
            return 0f;

        float chance = chanceHealReward;
        float missingRatio = Mathf.Clamp01((float)missingHp / maxHp);
        chance += missingRatio * 0.18f;

        if (encounterProfile.Type == CombatEncounterType.Elite)
            chance += 0.03f;
        else if (encounterProfile.Type == CombatEncounterType.MiniBoss || encounterProfile.Type == CombatEncounterType.Boss)
            chance += 0.05f;

        return Mathf.Clamp01(chance);
    }

    private int ApplyHealingReward(int requestedAmount)
    {
        if (requestedAmount <= 0)
            return 0;

        GameFlowManager flow = GameFlowManager.Instance;
        int missingHp = GetMissingPlayerHp(flow);
        if (missingHp <= 0)
            return 0;

        int clampedAmount = Mathf.Min(requestedAmount, missingHp);
        PlayerStats livePlayer = ServiceRegistry.LegacyFind<PlayerStats>();
        if (livePlayer != null)
            return livePlayer.Heal(clampedAmount);

        if (flow == null)
            return 0;

        int previousHp = flow.HasSavedPlayerHP ? flow.SavedPlayerHP : flow.PlayerMaxHP;
        flow.ModifySavedHP(clampedAmount);
        int nextHp = flow.HasSavedPlayerHP ? flow.SavedPlayerHP : flow.PlayerMaxHP;
        return Mathf.Max(0, nextHp - previousHp);
    }

    private int GetMissingPlayerHp(GameFlowManager flow)
    {
        int maxHp = GetPlayerMaxHp(flow);
        int currentHp = GetPlayerCurrentHp(flow);
        return Mathf.Max(0, maxHp - currentHp);
    }

    private int GetPlayerCurrentHp(GameFlowManager flow)
    {
        if (flow == null)
            return 0;

        return flow.HasSavedPlayerHP
            ? Mathf.Clamp(flow.SavedPlayerHP, 0, flow.PlayerMaxHP)
            : flow.PlayerMaxHP;
    }

    private int GetPlayerMaxHp(GameFlowManager flow)
    {
        return flow != null ? Mathf.Max(1, flow.PlayerMaxHP) : 0;
    }

    private bool CanOfferHealChoice(List<RewardOption> currentChoices)
    {
        if (!allowHealRewards)
            return false;

        GameFlowManager flow = GameFlowManager.Instance;
        if (GetMissingPlayerHp(flow) <= 0)
            return false;

        if (!avoidDuplicatesInSameRoll || currentChoices == null)
            return true;

        for (int i = 0; i < currentChoices.Count; i++)
        {
            if (currentChoices[i].kind == RewardKind.Heal)
                return false;
        }

        return true;
    }

    private bool HasAvailableRelicChoice(HashSet<ShotEffectBase> usedRelics)
    {
        if (relicPool == null || relicPool.Length == 0)
            return false;

        for (int i = 0; i < relicPool.Length; i++)
        {
            ShotEffectBase relic = relicPool[i];
            if (relic == null)
                continue;

            if (relics != null && relics.HasRelic(relic))
                continue;

            if (avoidDuplicatesInSameRoll && usedRelics != null && usedRelics.Contains(relic))
                continue;

            return true;
        }

        return false;
    }

    private bool HasAvailableOrbChoice(HashSet<OrbData> usedOrbs, HashSet<OrbInstance> usedUpgradeOrbs)
    {
        if (orbPool == null || orbPool.Length == 0)
            return false;

        for (int i = 0; i < orbPool.Length; i++)
        {
            if (TryBuildOrbRewardOption(orbPool[i], usedOrbs, usedUpgradeOrbs, CombatEncounterType.Regular, RewardOfferOrigin.StandardRoll, out _))
                return true;
        }

        return false;
    }

    private static bool HasAvailableUpgradeChoice(List<OrbInstance> upgradeableOrbs, HashSet<OrbInstance> usedUpgradeOrbs)
    {
        if (upgradeableOrbs == null || upgradeableOrbs.Count == 0)
            return false;

        for (int i = 0; i < upgradeableOrbs.Count; i++)
        {
            OrbInstance orb = upgradeableOrbs[i];
            if (orb == null || !orb.CanLevelUp)
                continue;

            if (usedUpgradeOrbs != null && usedUpgradeOrbs.Contains(orb))
                continue;

            return true;
        }

        return false;
    }

    private bool TryAddRelicChoice(
        List<RewardOption> result,
        HashSet<ShotEffectBase> usedRelics,
        CombatEncounterProfile encounterProfile,
        RewardOfferOrigin offerOrigin)
    {
        if (result == null || relicPool == null || relicPool.Length == 0)
            return false;

        var candidates = new List<ShotEffectBase>(relicPool.Length);
        for (int i = 0; i < relicPool.Length; i++)
        {
            ShotEffectBase rewardRelic = relicPool[i];
            if (rewardRelic == null)
                continue;

            if (relics != null && relics.HasRelic(rewardRelic))
                continue;

            if (avoidDuplicatesInSameRoll && usedRelics.Contains(rewardRelic))
                continue;

            candidates.Add(rewardRelic);
        }

        if (candidates.Count == 0)
            return false;

        ShotEffectBase selectedRelic = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        usedRelics.Add(selectedRelic);
        result.Add(new RewardOption
        {
            kind = RewardKind.Relic,
            orb = null,
            orbInstance = null,
            relic = selectedRelic,
            healAmount = 0,
            offerOrigin = offerOrigin,
            encounterType = encounterProfile.Type
        });
        return true;
    }

    private bool TryBuildOrbRewardOption(
        OrbData rewardOrb,
        HashSet<OrbData> usedOrbs,
        HashSet<OrbInstance> usedUpgradeOrbs,
        CombatEncounterType encounterType,
        RewardOfferOrigin offerOrigin,
        out RewardOption option)
    {
        option = default;

        if (rewardOrb == null)
            return false;

        OrbInstance ownedInstance = orbs != null ? orbs.FindOwnedOrbInstance(rewardOrb) : null;
        if (ownedInstance != null)
        {
            if (!ownedInstance.CanLevelUp)
                return false;

            if (avoidDuplicatesInSameRoll && usedUpgradeOrbs != null && usedUpgradeOrbs.Contains(ownedInstance))
                return false;

            option = new RewardOption
            {
                kind = RewardKind.OrbUpgrade,
                orb = null,
                orbInstance = ownedInstance,
                relic = null,
                healAmount = 0,
                offerOrigin = RewardOfferOrigin.DuplicateOrbUpgrade,
                encounterType = encounterType
            };
            return true;
        }

        if (avoidDuplicatesInSameRoll && usedOrbs != null && usedOrbs.Contains(rewardOrb))
            return false;

        option = new RewardOption
        {
            kind = RewardKind.Orb,
            orb = rewardOrb,
            orbInstance = null,
            relic = null,
            healAmount = 0,
            offerOrigin = offerOrigin,
            encounterType = encounterType
        };
        return true;
    }

    private static void RegisterUsedReward(RewardOption option, HashSet<OrbData> usedOrbs, HashSet<OrbInstance> usedUpgradeOrbs)
    {
        if (option.kind == RewardKind.Orb && option.orb != null)
            usedOrbs?.Add(option.orb);
        else if (option.kind == RewardKind.OrbUpgrade && option.orbInstance != null)
            usedUpgradeOrbs?.Add(option.orbInstance);
    }

    private bool TryAddOrbChoice(
        List<RewardOption> result,
        HashSet<OrbData> usedOrbs,
        HashSet<OrbInstance> usedUpgradeOrbs,
        CombatEncounterProfile encounterProfile,
        RewardOfferOrigin offerOrigin)
    {
        if (result == null || orbPool == null || orbPool.Length == 0)
            return false;

        var candidates = new List<RewardOption>(orbPool.Length);
        for (int i = 0; i < orbPool.Length; i++)
        {
            if (TryBuildOrbRewardOption(
                orbPool[i],
                usedOrbs,
                usedUpgradeOrbs,
                encounterProfile.Type,
                offerOrigin,
                out RewardOption option))
            {
                candidates.Add(option);
            }
        }

        if (candidates.Count == 0)
            return false;

        RewardOption selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        RegisterUsedReward(selected, usedOrbs, usedUpgradeOrbs);
        result.Add(selected);
        return true;
    }

    private bool TryAddHealChoice(
        List<RewardOption> result,
        CombatEncounterProfile encounterProfile,
        RewardOfferOrigin offerOrigin,
        int stageIndex)
    {
        if (result == null || !CanOfferHealChoice(result))
            return false;

        int healAmount = RollHealRewardAmount(stageIndex);
        if (healAmount <= 0)
            return false;

        result.Add(new RewardOption
        {
            kind = RewardKind.Heal,
            orb = null,
            orbInstance = null,
            relic = null,
            healAmount = healAmount,
            offerOrigin = offerOrigin,
            encounterType = encounterProfile.Type
        });
        return true;
    }

    private bool TryAddUpgradeChoice(
        List<RewardOption> result,
        List<OrbInstance> upgradeableOrbs,
        HashSet<OrbInstance> usedUpgradeOrbs,
        CombatEncounterProfile encounterProfile,
        RewardOfferOrigin offerOrigin)
    {
        if (result == null || upgradeableOrbs == null || upgradeableOrbs.Count == 0)
            return false;

        for (int attempt = 0; attempt < upgradeableOrbs.Count * 2; attempt++)
        {
            OrbInstance rewardOrb = upgradeableOrbs[UnityEngine.Random.Range(0, upgradeableOrbs.Count)];
            if (rewardOrb == null || !rewardOrb.CanLevelUp)
                continue;

            if (avoidDuplicatesInSameRoll && usedUpgradeOrbs.Contains(rewardOrb))
                continue;

            usedUpgradeOrbs.Add(rewardOrb);
            result.Add(new RewardOption
            {
                kind = RewardKind.OrbUpgrade,
                orb = null,
                orbInstance = rewardOrb,
                relic = null,
                offerOrigin = offerOrigin,
                encounterType = encounterProfile.Type
            });
            return true;
        }

        return false;
    }

    private int RollHealRewardAmount(int stageIndex)
    {
        GameFlowManager flow = GameFlowManager.Instance;
        int missingHp = GetMissingPlayerHp(flow);
        if (missingHp <= 0)
            return 0;

        int minHeal = Mathf.Max(1, healRewardMin + Mathf.Max(0, stageIndex) * healRewardPerStageBonus);
        int maxHeal = Mathf.Max(minHeal, healRewardMax + Mathf.Max(0, stageIndex) * healRewardPerStageBonus);
        int rolledHeal = UnityEngine.Random.Range(minHeal, maxHeal + 1);
        return Mathf.Clamp(rolledHeal, 1, missingHp);
    }

    private int GetStageIndexForBalance(GameFlowManager flow)
    {
        if (flow != null)
            return Mathf.Max(0, flow.CurrentStageIndex);

        if (battle != null)
            return Mathf.Max(0, battle.CurrentStageIndex);

        return 0;
    }
}
