using System;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Combat;
    public event Action<GameState> OnStateChanged;
    public MapNodeData SavedMapNode { get; private set; }

    //  solo en Combat se juega
    public bool CanShoot => State == GameState.Combat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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

        // Inicializa el mapa si estamos entrando en navegación
        if (State == GameState.MapNavigation)
        {
            MapManager mapManager = FindObjectOfType<MapManager>();
            if (mapManager != null)
                mapManager.StartStage(mapManager.CurrentMapStage);
            else
                Debug.LogError("[GameFlow] No se encontró MapManager.");
        }
    }


    public void SaveMapNode(MapNodeData node)
    {
        SavedMapNode = node;
    }


    public void Pause()
    {
        // No pausamos si ya está game over o en rewards
        if (State == GameState.GameOver) return;
        if (State == GameState.RewardChoice) return;

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
}