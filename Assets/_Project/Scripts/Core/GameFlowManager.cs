using System;
using UnityEngine;
using UnityEngine.SceneManagement;



public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Combat;
    public event Action<GameState> OnStateChanged;
    public MapNodeData SavedMapNode { get; private set; }
    public int NodesVisited { get; private set; }
    public int EncounterIndex { get; private set; }
    public int EncounterInStageIndex { get; private set; }
    public int CurrentStageIndex { get; private set; }

    public bool HasSavedPlayerHP { get; private set; }
    public int SavedPlayerHP { get; private set; }
    public int Coins { get; private set; }
    public int PlayerMaxHP { get; private set; }

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

    [Header("Scene References (DI)")]
    [SerializeField] private MapManager mapManager;
    [SerializeField] private OrbManager orbManager;
    [SerializeField] private RelicManager relicManager;

    private RunSaveService runSaveService;

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
            runSaveService = injectedRunSaveService;
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
        ValidateEncounterState("ResetRunState");
    }

    public void SaveRun()
    {
        RunSaveData data = BuildRunSnapshot();
        ResolveRunSaveService()?.Save(data);
    }

    public bool LoadRun()
    {
        RunSaveService saveService = ResolveRunSaveService();
        if (saveService == null)
            return false;

        if (!saveService.TryLoad(out RunSaveData data))
            return false;

        StageLoadedRunSnapshot(data);
        return true;
    }

    private RunSaveData BuildRunSnapshot()
    {
        RunSaveData data = new RunSaveData
        {
            SavedMapNodeId = SavedMapNode != null ? SavedMapNode.name : null,
            EncounterIndex = EncounterIndex,
            EncounterInStageIndex = EncounterInStageIndex,
            CurrentStageIndex = CurrentStageIndex,
            NodesVisited = NodesVisited,
            Coins = Coins,
            PlayerMaxHP = PlayerMaxHP,
            SavedPlayerHP = SavedPlayerHP,
            HasSavedPlayerHP = HasSavedPlayerHP,
            GameState = (int)State
        };

        OrbManager orbManagerInstance = ResolveOrbManager();
        if (orbManagerInstance != null)
        {
            data.Orbs = orbManagerInstance.SerializeOrbs();
            data.CurrentOrbId = orbManagerInstance.GetCurrentOrbId();
        }

        RelicManager relicManagerInstance = ResolveRelicManager();
        if (relicManagerInstance != null)
            data.Relics = relicManagerInstance.SerializeRelics();

        return data;
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
        // Nunca restaurar una corrida directamente en estados que congelan el tiempo.
        // Si se guardó estando en pausa o game over, retomamos en combate.
        if (loadedState == GameState.Paused || loadedState == GameState.GameOver)
            return GameState.Combat;

        return loadedState;
    }

    private void ApplyRunSnapshot(RunSaveData data)
    {
        pendingMapNodeId = data.SavedMapNodeId;
        SavedMapNode = ResolveMapNodeById(pendingMapNodeId);
        EncounterIndex = Mathf.Max(0, data.EncounterIndex);
        EncounterInStageIndex = Mathf.Max(0, data.EncounterInStageIndex);
        CurrentStageIndex = Mathf.Max(0, data.CurrentStageIndex);
        NodesVisited = Mathf.Max(0, data.NodesVisited);
        Coins = Mathf.Max(0, data.Coins);
        PlayerMaxHP = Mathf.Max(1, data.PlayerMaxHP);
        SavedPlayerHP = Mathf.Clamp(data.SavedPlayerHP, 0, PlayerMaxHP);
        HasSavedPlayerHP = data.HasSavedPlayerHP;

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

        if (SavedMapNode == null && !string.IsNullOrWhiteSpace(pendingMapNodeId))
            SavedMapNode = ResolveMapNodeById(pendingMapNodeId);

        if (pendingStateApply)
            StartCoroutine(ApplyLoadedStateNextFrame());
        else
            ApplyManagersFromRunData();

        TryInitializeMapForCurrentState();
    }

    private System.Collections.IEnumerator ApplyLoadedStateNextFrame()
    {
        yield return null;
        if (SavedMapNode == null && !string.IsNullOrWhiteSpace(pendingMapNodeId))
            SavedMapNode = ResolveMapNodeById(pendingMapNodeId);
        ApplyManagersFromRunData();

        pendingStateApply = false;
        SetState(pendingLoadedState);
    }

    private static MapNodeData ResolveMapNodeById(string mapNodeId)
    {
        if (string.IsNullOrWhiteSpace(mapNodeId))
            return null;

        MapNodeData[] candidates = Resources.FindObjectsOfTypeAll<MapNodeData>();
        for (int i = 0; i < candidates.Length; i++)
        {
            MapNodeData node = candidates[i];
            if (node != null && node.name == mapNodeId)
                return node;
        }

        return null;
    }

    public void RestartCombatScene()
    {
        RestartRunFromMenu();
    }

    public void RestartRunFromMenu()
    {
        ResetRunState();
        ResetPersistentManagers();
        SetState(GameState.Combat);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneCatalog.Load().MapScene, LoadSceneMode.Single);
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
        if (State != GameState.MapNavigation)
            return;

        MapManager resolvedMapManager = ResolveMapManager();
        if (resolvedMapManager != null)
        {
            resolvedMapManager.StartStageForCurrentRun();
            return;
        }

        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal))
            Debug.LogError("[GameFlow] No se encontr MapManager en la escena de mapa.");
    }

    public bool ContinueRunFromMenu()
    {
        if (SavedMapNode == null)
            return false;

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneCatalog.Load().MapScene, LoadSceneMode.Single);
        return true;
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
