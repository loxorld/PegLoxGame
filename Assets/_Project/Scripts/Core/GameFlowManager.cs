using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;



public class GameFlowManager : MonoBehaviour
{
    public sealed class ShopOfferRunData
    {
        public string OfferId;
        public ShopService.ShopOfferType OfferType;
        public int Cost;
        public int PrimaryValue;
        public int RemainingStock;
        public ShopService.ShopOfferRarity Rarity;
        public bool RequiresMissingHp;
        public bool RequiresUpgradableOrb;
        public bool RequiresAnyOrb;
    }

    public static GameFlowManager Instance { get; private set; }

    private readonly RunState runState = new RunState();

    public GameState State
    {
        get => runState.CurrentGameState;
        private set => runState.CurrentGameState = value;
    }
    public event Action<GameState> OnStateChanged;
    public MapNodeData SavedMapNode { get => runState.SavedMapNode; private set => runState.SavedMapNode = value; }
    public int NodesVisited { get => runState.NodesVisited; private set => runState.NodesVisited = value; }
    public int EncounterIndex { get => runState.EncounterIndex; private set => runState.EncounterIndex = value; }
    public int EncounterInStageIndex { get => runState.EncounterInStageIndex; private set => runState.EncounterInStageIndex = value; }
    public int CurrentStageIndex { get => runState.CurrentStageIndex; private set => runState.CurrentStageIndex = value; }

    public bool HasSavedPlayerHP { get => runState.HasSavedPlayerHP; private set => runState.HasSavedPlayerHP = value; }
    public int SavedPlayerHP { get => runState.SavedPlayerHP; private set => runState.SavedPlayerHP = value; }
    public int Coins { get => runState.Coins; private set => runState.Coins = value; }
    public int PlayerMaxHP { get => runState.PlayerMaxHP; private set => runState.PlayerMaxHP = value; }

    public bool HasBossEncounter => bossEncounterActive;
    public EnemyData BossEnemy => bossEnemy;
    public float BossHpMultiplier => bossHpMultiplier;
    public float BossDamageMultiplier => bossDamageMultiplier;
    public int BossHpBonus => bossHpBonus;
    public int BossDamageBonus => bossDamageBonus;

    //  solo en Combat se juega
    public bool CanShoot => State == GameState.Combat;

    [Header("Run Defaults")]
    [SerializeField, Min(0)] private int startingCoins = 0;
    [SerializeField, Min(1)] private int startingPlayerMaxHP = 100;
    private bool bossEncounterActive;
    private EnemyData bossEnemy;
    private float bossHpMultiplier = 2f;
    private float bossDamageMultiplier = 1.5f;
    private int bossHpBonus = 0;
    private int bossDamageBonus = 0;
    private bool hasLoadedRun;
    private GameState pendingLoadedState;
    private bool pendingStateApply;
    private RunSaveData pendingRunData;
    private string pendingMapNodeId;
    private bool pendingOrbApply;
    private bool pendingRelicApply;
    private GameState stateBeforePause = GameState.Combat;
    private Dictionary<string, List<ShopOfferRunData>> shopCatalogsById => runState.ShopCatalogsById;
    private HashSet<string> resolvedEventNodeIds => runState.ResolvedEventNodeIds;
    private Dictionary<string, int> eventOptionCounters => runState.EventOptionCounters;
    private readonly Dictionary<string, MapNodeData> mapNodesByPersistentId = new Dictionary<string, MapNodeData>(StringComparer.Ordinal);
    private readonly Dictionary<string, MapNodeData> mapNodesByLegacyName = new Dictionary<string, MapNodeData>(StringComparer.Ordinal);

    [Header("Scene References (DI)")]
    [SerializeField] private MapManager mapManager;
    [SerializeField] private OrbManager orbManager;
    [SerializeField] private RelicManager relicManager;

    private RunSaveService runSaveService;
    private RunPersistenceService runPersistenceService;
    private FlowSceneCoordinator flowSceneCoordinator;

    public void InjectDependencies(MapManager injectedMapManager, OrbManager injectedOrbManager, RelicManager injectedRelicManager)
    {
        if (injectedMapManager != null)
            mapManager = injectedMapManager;

        if (injectedOrbManager != null)
            orbManager = injectedOrbManager;

        if (injectedRelicManager != null)
            relicManager = injectedRelicManager;
    }

    public void InjectRunSaveService(RunSaveService injectedRunSaveService)
    {
        if (injectedRunSaveService != null)
        {
            runSaveService = injectedRunSaveService;
            runPersistenceService = null;
        }
    }

    public void InjectRunPersistenceService(RunPersistenceService injectedRunPersistenceService)
    {
        if (injectedRunPersistenceService != null)
            runPersistenceService = injectedRunPersistenceService;
    }

    public void InjectFlowSceneCoordinator(FlowSceneCoordinator injectedFlowSceneCoordinator)
    {
        if (injectedFlowSceneCoordinator != null)
            flowSceneCoordinator = injectedFlowSceneCoordinator;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceRegistry.Register(this);
        DontDestroyOnLoad(gameObject);

        PlayerMaxHP = Mathf.Max(1, startingPlayerMaxHP);
        Coins = startingCoins;

        ResolveRunSaveService();
        ResolveRunPersistenceService();
        ResolveFlowSceneCoordinator();
        BuildMapNodeResolutionCache();
        LoadRun();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start()
    {
        if (pendingStateApply)
            StartCoroutine(ApplyLoadedStateNextFrame());
        else if (hasLoadedRun)
            ApplyManagersFromRunData();

        TryInitializeMapForCurrentState();
    }


    public void SetState(GameState newState)
    {
        if (State == newState) return;

        State = newState;

        if (State == GameState.Paused || State == GameState.GameOver)
            Time.timeScale = 0f;
        else
            Time.timeScale = 1f;

        OnStateChanged?.Invoke(State);
        Debug.Log($"[GameFlow] State -> {State}");

        TryInitializeMapForCurrentState();
    }


    public void SaveMapNode(MapNodeData node)
    {
        SavedMapNode = node;
    }

    public void IncrementNodesVisited()
    {
        NodesVisited++;
    }

    public void ResetNodesVisited()
    {
        NodesVisited = 0;
    }

    public void AdvanceEncounter()
    {
        EncounterIndex++;
        EncounterInStageIndex++;
        ValidateEncounterState("AdvanceEncounter");
    }

    public void ResetEncounterProgressInStage()
    {
        EncounterInStageIndex = 0;
        ValidateEncounterState("ResetEncounterProgressInStage");
    }

    public void SavePlayerHP(int currentHP)
    {
        SavedPlayerHP = Mathf.Max(currentHP, 0);
        HasSavedPlayerHP = true;
    }

    public void SavePlayerMaxHP(int maxHP)
    {
        PlayerMaxHP = Mathf.Max(1, maxHP);
        if (HasSavedPlayerHP)
            SavedPlayerHP = Mathf.Clamp(SavedPlayerHP, 0, PlayerMaxHP);
    }

    public void ModifySavedHP(int delta)
    {
        int baseHp = HasSavedPlayerHP ? SavedPlayerHP : PlayerMaxHP;
        int nextHp = Mathf.Clamp(baseHp + delta, 0, PlayerMaxHP);
        SavedPlayerHP = nextHp;
        HasSavedPlayerHP = true;
    }


    public void ClearSavedPlayerHP()
    {
        SavedPlayerHP = 0;
        HasSavedPlayerHP = false;
    }

    public void AddCoins(int amount)
    {
        Coins = Mathf.Max(0, Coins + amount);
    }

    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (Coins < amount) return false;
        Coins -= amount;
        return true;
    }

    public void ResetRunState()
    {
        SavedMapNode = null;
        NodesVisited = 0;
        EncounterIndex = 0;
        EncounterInStageIndex = 0;
        CurrentStageIndex = 0;
        Coins = startingCoins;
        PlayerMaxHP = Mathf.Max(1, startingPlayerMaxHP);
        ClearBossEncounter();
        SavedPlayerHP = PlayerMaxHP;
        HasSavedPlayerHP = true;
        shopCatalogsById.Clear();
        resolvedEventNodeIds.Clear();
        eventOptionCounters.Clear();
        ValidateEncounterState("ResetRunState");
    }

    public bool IsEventNodeResolved(MapNodeData node)
    {
        if (!TryBuildMapNodeId(node, out string nodeId))
            return false;

        return resolvedEventNodeIds.Contains(nodeId);
    }

    public void MarkEventNodeResolved(MapNodeData node)
    {
        if (!TryBuildMapNodeId(node, out string nodeId))
            return;

        resolvedEventNodeIds.Add(nodeId);
    }

    public int IncrementEventOptionCounter(MapStage stage, MapNodeData node, string optionLabel, MapDomainService.EventResolutionOutcome appliedOutcome)
    {
        string counterKey = BuildEventOptionCounterKey(stage, node, optionLabel, appliedOutcome);
        if (string.IsNullOrWhiteSpace(counterKey))
            return 0;

        eventOptionCounters.TryGetValue(counterKey, out int currentCount);
        int nextCount = currentCount + 1;
        eventOptionCounters[counterKey] = nextCount;
        return nextCount;
    }

    public List<ShopOfferRunData> GetShopCatalog(string shopId)
    {
        if (string.IsNullOrWhiteSpace(shopId))
            return null;

        if (!shopCatalogsById.TryGetValue(shopId, out List<ShopOfferRunData> catalog) || catalog == null)
            return null;

        return CloneCatalog(catalog);
    }

    public void SaveShopCatalog(string shopId, IReadOnlyList<ShopService.ShopOfferData> offers)
    {
        if (string.IsNullOrWhiteSpace(shopId) || offers == null)
            return;

        var snapshot = new List<ShopOfferRunData>(offers.Count);
        for (int i = 0; i < offers.Count; i++)
        {
            ShopService.ShopOfferData offer = offers[i];
            if (offer == null || string.IsNullOrWhiteSpace(offer.OfferId))
                continue;

            snapshot.Add(new ShopOfferRunData
            {
                OfferId = offer.OfferId,
                OfferType = offer.Type,
                Cost = Mathf.Max(0, offer.Cost),
                PrimaryValue = offer.PrimaryValue,
                RemainingStock = Mathf.Max(0, offer.Stock),
                Rarity = offer.Rarity,
                RequiresMissingHp = offer.RequiresMissingHp,
                RequiresUpgradableOrb = offer.RequiresUpgradableOrb,
                RequiresAnyOrb = offer.RequiresAnyOrb
            });
        }

        shopCatalogsById[shopId] = snapshot;
    }

    public bool TryConsumeShopOffer(string shopId, string offerId)
    {
        if (string.IsNullOrWhiteSpace(shopId) || string.IsNullOrWhiteSpace(offerId))
            return false;

        if (!shopCatalogsById.TryGetValue(shopId, out List<ShopOfferRunData> catalog) || catalog == null)
            return false;

        for (int i = 0; i < catalog.Count; i++)
        {
            ShopOfferRunData offer = catalog[i];
            if (offer == null || !string.Equals(offer.OfferId, offerId, StringComparison.Ordinal))
                continue;

            if (offer.RemainingStock <= 0)
                return false;

            offer.RemainingStock--;
            return true;
        }

        return false;
    }

    public bool TryRestoreShopOffer(string shopId, string offerId)
    {
        if (string.IsNullOrWhiteSpace(shopId) || string.IsNullOrWhiteSpace(offerId))
            return false;

        if (!shopCatalogsById.TryGetValue(shopId, out List<ShopOfferRunData> catalog) || catalog == null)
            return false;

        for (int i = 0; i < catalog.Count; i++)
        {
            ShopOfferRunData offer = catalog[i];
            if (offer == null || !string.Equals(offer.OfferId, offerId, StringComparison.Ordinal))
                continue;

            offer.RemainingStock++;
            return true;
        }

        return false;
    }

    public void SaveRun()
    {
        OrbManager orbManagerInstance = ResolveOrbManager();
        RelicManager relicManagerInstance = ResolveRelicManager();
        ResolveRunPersistenceService().Save(
            runState,
            orbManagerInstance != null ? orbManagerInstance.SerializeOrbs() : null,
            orbManagerInstance != null ? orbManagerInstance.GetCurrentOrbId() : null,
            relicManagerInstance != null ? relicManagerInstance.SerializeRelics() : null);
    }

    public bool LoadRun()
    {
        RunPersistenceService persistenceService = ResolveRunPersistenceService();
        if (persistenceService == null)
            return false;

        if (!persistenceService.TryLoad(out RunSaveData data))
            return false;

        StageLoadedRunSnapshot(data);
        return true;
    }


    private void StageLoadedRunSnapshot(RunSaveData data)
    {
        ApplyRunSnapshot(data);

        pendingRunData = data;
        pendingOrbApply = true;
        pendingRelicApply = true;

        hasLoadedRun = true;
        GameState loadedState = (GameState)Mathf.Clamp(data.GameState, 0, (int)GameState.GameOver);
        pendingLoadedState = NormalizeLoadedState(loadedState);
        pendingStateApply = true;
    }

    private static GameState NormalizeLoadedState(GameState loadedState)
    {
        return RunPersistenceService.NormalizeLoadedState(loadedState);
    }

    private void ApplyRunSnapshot(RunSaveData data)
    {
        pendingMapNodeId = data.SavedMapNodeId;
        ResolveRunPersistenceService().ApplyRunSnapshot(runState, data, ResolveMapNodeById);
        ValidateEncounterState("ApplyRunData");
    }

    private void ApplyManagersFromRunData()
    {
        if (pendingRunData == null)
            return;

        OrbManager orbManagerInstance = ResolveOrbManager();
        if (pendingOrbApply && orbManagerInstance != null)
        {
            orbManagerInstance.DeserializeOrbs(pendingRunData.Orbs, pendingRunData.CurrentOrbId);
            pendingOrbApply = false;
        }

        RelicManager relicManagerInstance = ResolveRelicManager();
        if (pendingRelicApply && relicManagerInstance != null)
        {
            relicManagerInstance.DeserializeRelics(pendingRunData.Relics);
            pendingRelicApply = false;
        }

        if (!pendingOrbApply && !pendingRelicApply)
            pendingRunData = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasLoadedRun)
            return;

        BuildMapNodeResolutionCache();

        if (SavedMapNode == null && !string.IsNullOrWhiteSpace(pendingMapNodeId))
            SavedMapNode = ResolveMapNodeById(pendingMapNodeId, pendingRunData != null && pendingRunData.IsLegacySave());

        if (pendingStateApply)
            StartCoroutine(ApplyLoadedStateNextFrame());
        else
            ApplyManagersFromRunData();

        TryInitializeMapForCurrentState();
    }

    private System.Collections.IEnumerator ApplyLoadedStateNextFrame()
    {
        yield return null;
        BuildMapNodeResolutionCache();

        if (SavedMapNode == null && !string.IsNullOrWhiteSpace(pendingMapNodeId))
            SavedMapNode = ResolveMapNodeById(pendingMapNodeId, pendingRunData != null && pendingRunData.IsLegacySave());
        ApplyManagersFromRunData();

        pendingStateApply = false;
        SetState(pendingLoadedState);
    }

    private void BuildMapNodeResolutionCache()
    {
        mapNodesByPersistentId.Clear();
        mapNodesByLegacyName.Clear();

        MapNodeData[] candidates = Resources.LoadAll<MapNodeData>(string.Empty);
        for (int i = 0; i < candidates.Length; i++)
        {
            MapNodeData node = candidates[i];
            if (node == null)
                continue;

            string persistentId = BuildPersistentMapNodeId(node);
            if (!string.IsNullOrWhiteSpace(persistentId) && !mapNodesByPersistentId.ContainsKey(persistentId))
                mapNodesByPersistentId[persistentId] = node;

            string legacyName = string.IsNullOrWhiteSpace(node.name) ? null : node.name.Trim();
            if (!string.IsNullOrWhiteSpace(legacyName) && !mapNodesByLegacyName.ContainsKey(legacyName))
                mapNodesByLegacyName[legacyName] = node;
        }
    }

    private MapNodeData ResolveMapNodeById(string mapNodeId, bool allowLegacyNameMigration)
    {
        if (string.IsNullOrWhiteSpace(mapNodeId))
            return null;

        if (mapNodesByPersistentId.TryGetValue(mapNodeId, out MapNodeData nodeByPersistentId) && nodeByPersistentId != null)
            return nodeByPersistentId;

        if (allowLegacyNameMigration && mapNodesByLegacyName.TryGetValue(mapNodeId, out MapNodeData nodeByLegacyName) && nodeByLegacyName != null)
        {
            Debug.LogWarning($"[GameFlow] Migración save legacy: MapNodeData '{mapNodeId}' resuelto por name. Reguardar para persistir persistentId.");
            return nodeByLegacyName;
        }

        return null;
    }

    private static List<ShopOfferRunData> CloneCatalog(List<ShopOfferRunData> source)
    {
        var clone = new List<ShopOfferRunData>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            ShopOfferRunData offer = source[i];
            if (offer == null)
                continue;

            clone.Add(new ShopOfferRunData
            {
                OfferId = offer.OfferId,
                OfferType = offer.OfferType,
                Cost = offer.Cost,
                PrimaryValue = offer.PrimaryValue,
                RemainingStock = offer.RemainingStock,
                Rarity = offer.Rarity,
                RequiresMissingHp = offer.RequiresMissingHp,
                RequiresUpgradableOrb = offer.RequiresUpgradableOrb,
                RequiresAnyOrb = offer.RequiresAnyOrb
            });
        }

        return clone;
    }

    private static string BuildEventOptionCounterKey(MapStage stage, MapNodeData node, string optionLabel, MapDomainService.EventResolutionOutcome appliedOutcome)
    {
        string stageId = stage != null && !string.IsNullOrWhiteSpace(stage.name) ? stage.name.Trim() : "unknown-stage";
        string nodeId = node != null && !string.IsNullOrWhiteSpace(node.name) ? node.name.Trim() : "unknown-node";
        string optionId = string.IsNullOrWhiteSpace(optionLabel) ? "unknown-option" : optionLabel.Trim();
        string outcomeId = BuildOutcomeId(appliedOutcome);
        return $"{stageId}|{nodeId}|{optionId}|{outcomeId}";
    }

    private static string BuildOutcomeId(MapDomainService.EventResolutionOutcome outcome)
    {
        string description = string.IsNullOrWhiteSpace(outcome.ResultDescription)
            ? "no-description"
            : outcome.ResultDescription.Trim();
        return $"c{outcome.CoinDelta}_h{outcome.HpDelta}_{description}";
    }

    private static bool TryBuildMapNodeId(MapNodeData node, out string nodeId)
    {
        nodeId = BuildPersistentMapNodeId(node);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            Debug.LogWarning("[GameFlow] No se pudo generar un id estable para MapNodeData (persistentId/name vacío).");
            return false;
        }

        return true;
    }

    private static string BuildPersistentMapNodeId(MapNodeData node)
    {
        return RunPersistenceService.BuildPersistentMapNodeId(node);
    }

    public void RestartCombatScene()
    {
        RestartRunFromMenu();
    }

    public void RestartRunFromMenu()
    {
        ResolveFlowSceneCoordinator().RestartRunFromMenu(ResetRunState, ResetPersistentManagers, SetState);
    }

    public void SetCurrentStageIndex(int stageIndex)
    {
        int nextStageIndex = Mathf.Max(0, stageIndex);
        if (CurrentStageIndex != nextStageIndex)
            ResetEncounterProgressInStage();

        CurrentStageIndex = nextStageIndex;
        ValidateEncounterState("SetCurrentStageIndex");
    }

    public void AdvanceStage()
    {
        CurrentStageIndex = Mathf.Max(0, CurrentStageIndex + 1);
        ResetEncounterProgressInStage();
        ValidateEncounterState("AdvanceStage");
    }

    private void ValidateEncounterState(string source)
    {
        if (CurrentStageIndex < 0 || EncounterIndex < 0 || EncounterInStageIndex < 0)
        {
            Debug.LogError($"[GameFlow] Invalid run state after {source}. Stage={CurrentStageIndex}, EncounterInStage={EncounterInStageIndex}, EncounterGlobal={EncounterIndex}");
            return;
        }

        if (EncounterInStageIndex > EncounterIndex)
        {
            Debug.LogWarning($"[GameFlow] Unexpected state after {source}. EncounterInStage ({EncounterInStageIndex}) is greater than EncounterGlobal ({EncounterIndex}).");
        }
    }

    private void TryInitializeMapForCurrentState()
    {
        ResolveFlowSceneCoordinator().TryInitializeMapForCurrentState(State);
    }

    public bool ContinueRunFromMenu()
    {
        return ResolveFlowSceneCoordinator().ContinueRunFromMenu(SavedMapNode);
    }

    private void ResetPersistentManagers()
    {
        ResolveOrbManager()?.ResetToDefaults();
        ResolveRelicManager()?.ResetToDefaults();
    }

    private MapManager ResolveMapManager()
    {
        if (mapManager != null)
            return mapManager;

        if (ServiceRegistry.TryResolve(out mapManager))
            return mapManager;

        ServiceRegistry.LogFallback(nameof(GameFlowManager), nameof(mapManager), "missing-injected-reference");

        if (IsMigratedMapSceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(mapManager), "strict-missing-reference");
            Debug.LogError("[GameFlow] DI estricto: falta MapManager en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        mapManager = ServiceRegistry.ResolveWithFallback(nameof(GameFlowManager), nameof(mapManager), () => ServiceRegistry.LegacyFind<MapManager>(true));
        if (mapManager != null)
        {
            ServiceRegistry.Register(mapManager);
            ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(mapManager), "findobjectoftype");
        }

        return mapManager;
    }

    private OrbManager ResolveOrbManager()
    {
        if (orbManager != null)
            return orbManager;

        if (ServiceRegistry.TryResolve(out orbManager))
            return orbManager;

        ServiceRegistry.LogFallback(nameof(GameFlowManager), nameof(orbManager), "missing-injected-reference");

        if (IsMigratedGameplaySceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(orbManager), "strict-missing-reference");
            Debug.LogError("[GameFlow] DI estricto: falta OrbManager en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        orbManager = ServiceRegistry.ResolveWithFallback(nameof(GameFlowManager), nameof(orbManager), () => OrbManager.Instance);
        if (orbManager != null)
        {
            ServiceRegistry.Register(orbManager);
            ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(orbManager), "legacy-resolver");
        }

        return orbManager;
    }

    private RelicManager ResolveRelicManager()
    {
        if (relicManager != null)
            return relicManager;

        if (ServiceRegistry.TryResolve(out relicManager))
            return relicManager;

        ServiceRegistry.LogFallback(nameof(GameFlowManager), nameof(relicManager), "missing-injected-reference");

        if (IsMigratedGameplaySceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(relicManager), "strict-missing-reference");
            Debug.LogError("[GameFlow] DI estricto: falta RelicManager en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        relicManager = ServiceRegistry.ResolveWithFallback(nameof(GameFlowManager), nameof(relicManager), () => RelicManager.Instance);
        if (relicManager != null)
        {
            ServiceRegistry.Register(relicManager);
            ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(relicManager), "legacy-resolver");
        }

        return relicManager;
    }

    private RunSaveService ResolveRunSaveService()
    {
        if (runSaveService != null)
            return runSaveService;

        if (ServiceRegistry.TryResolve(out runSaveService))
            return runSaveService;

        ServiceRegistry.LogFallback(nameof(GameFlowManager), nameof(runSaveService), "missing-injected-reference");

        if (IsMigratedGameplaySceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(runSaveService), "strict-missing-reference");
            Debug.LogError("[GameFlow] DI estricto: falta RunSaveService en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        runSaveService = new RunSaveService();
        ServiceRegistry.Register(runSaveService);
        ServiceRegistry.LogFallbackMetric(nameof(GameFlowManager), nameof(runSaveService), "in-process-default");
        return runSaveService;
    }

    private RunPersistenceService ResolveRunPersistenceService()
    {
        if (runPersistenceService != null)
            return runPersistenceService;

        RunSaveService saveService = ResolveRunSaveService();
        if (saveService == null)
            return null;

        runPersistenceService = new RunPersistenceService(new RunSaveGateway(saveService));
        return runPersistenceService;
    }

    private FlowSceneCoordinator ResolveFlowSceneCoordinator()
    {
        if (flowSceneCoordinator != null)
            return flowSceneCoordinator;

        flowSceneCoordinator = new FlowSceneCoordinator(
            () =>
            {
                MapManager map = ResolveMapManager();
                if (map == null)
                    return false;

                map.StartStageForCurrentRun();
                return true;
            },
            () => SceneManager.GetActiveScene().name,
            SceneManager.LoadScene,
            SceneCatalog.Load,
            value => Time.timeScale = value);
        return flowSceneCoordinator;
    }

    private static bool IsMigratedMapSceneActive()
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal);
    }

    private static bool IsMigratedGameplaySceneActive()
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal)
            || string.Equals(activeSceneName, catalog.CombatScene, StringComparison.Ordinal);
    }

    public void SetBossEncounter(EnemyData enemy, float hpMultiplier, float damageMultiplier, int hpBonus, int damageBonus)
    {
        bossEncounterActive = true;
        bossEnemy = enemy;
        bossHpMultiplier = Mathf.Max(1f, hpMultiplier);
        bossDamageMultiplier = Mathf.Max(1f, damageMultiplier);
        bossHpBonus = Mathf.Max(0, hpBonus);
        bossDamageBonus = Mathf.Max(0, damageBonus);
    }

    public void ClearBossEncounter()
    {
        bossEncounterActive = false;
        bossEnemy = null;
        bossHpMultiplier = 2f;
        bossDamageMultiplier = 1.5f;
        bossHpBonus = 0;
        bossDamageBonus = 0;
    }


    public void Pause()
    {
        // No pausamos si ya está game over o en rewards
        if (State == GameState.GameOver) return;
        if (State == GameState.RewardChoice) return;
        if (State == GameState.Paused) return;

        stateBeforePause = State;
        SaveRun();
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;

        GameState resumeState = stateBeforePause == GameState.Paused
            ? GameState.Combat
            : stateBeforePause;

        SetState(resumeState);
    }

    public void TogglePause()
    {
        if (State == GameState.Paused) Resume();
        else Pause();
    }

    private void OnApplicationQuit()
    {
        SaveRun();
    }
}
