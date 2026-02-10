using System;
using System.IO;
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        PlayerMaxHP = Mathf.Max(1, startingPlayerMaxHP);
        Coins = startingCoins;

        LoadRun();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (pendingStateApply)
            StartCoroutine(ApplyLoadedStateNextFrame());
        else if (hasLoadedRun)
            ApplyManagersFromRunData();
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

        // Inicializa el mapa si estamos entrando en navegacin
        if (State == GameState.MapNavigation)
        {
            MapManager mapManager = FindObjectOfType<MapManager>();
            if (mapManager != null)
                mapManager.StartStageForCurrentRun();
            else
                Debug.LogError("[GameFlow] No se encontr MapManager.");
        }
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

        OrbManager orbManager = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);
        if (orbManager != null)
        {
            data.Orbs = orbManager.SerializeOrbs();
            data.CurrentOrbId = orbManager.GetCurrentOrbId();
        }

        RelicManager relicManager = RelicManager.Instance ?? FindObjectOfType<RelicManager>(true);
        if (relicManager != null)
            data.Relics = relicManager.SerializeRelics();

        string json = JsonUtility.ToJson(data, true);
        string path = GetRunSavePath();

        try
        {
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameFlow] Failed to save run: {ex.Message}");
        }
    }

    public bool LoadRun()
    {
        string path = GetRunSavePath();
        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            RunSaveData data = JsonUtility.FromJson<RunSaveData>(json);
            if (data == null)
                return false;

            ApplyRunData(data);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameFlow] Failed to load run: {ex.Message}");
            return false;
        }
    }

    private void ApplyRunData(RunSaveData data)
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

        hasLoadedRun = true;
        pendingLoadedState = (GameState)Mathf.Clamp(data.GameState, 0, (int)GameState.GameOver);
        pendingStateApply = true;
        pendingRunData = data;
        pendingOrbApply = true;
        pendingRelicApply = true;

        ValidateEncounterState("ApplyRunData");

        ApplyManagersFromRunData();
    }

    private void ApplyManagersFromRunData()
    {
        if (pendingRunData == null)
            return;

        OrbManager orbManager = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);
        if (pendingOrbApply && orbManager != null)
        {
            orbManager.DeserializeOrbs(pendingRunData.Orbs, pendingRunData.CurrentOrbId);
            pendingOrbApply = false;
        }

        RelicManager relicManager = RelicManager.Instance ?? FindObjectOfType<RelicManager>(true);
        if (pendingRelicApply && relicManager != null)
        {
            relicManager.DeserializeRelics(pendingRunData.Relics);
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

    private static string GetRunSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "run_save.json");
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
        OrbManager.Instance?.ResetToDefaults();
        RelicManager.Instance?.ResetToDefaults();
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
        // No pausamos si ya est game over o en rewards
        if (State == GameState.GameOver) return;
        if (State == GameState.RewardChoice) return;

        SaveRun();
        SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        SetState(GameState.Combat);
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