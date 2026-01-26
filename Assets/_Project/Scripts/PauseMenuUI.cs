using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameFlowManager flow;

    [Header("UI")]
    [SerializeField] private GameObject root;   // el panel overlay completo
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;

    private void Awake()
    {
        if (flow == null) flow = GameFlowManager.Instance;

        if (root != null) root.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
    }

    private void OnEnable()
    {
        if (flow != null)
            flow.OnStateChanged += OnStateChanged;

        Sync();
    }

    private void OnDisable()
    {
        if (flow != null)
            flow.OnStateChanged -= OnStateChanged;
    }

    private void Sync()
    {
        if (flow == null || root == null) return;
        root.SetActive(flow.State == GameState.Paused);
    }

    private void OnStateChanged(GameState state)
    {
        if (root == null) return;
        root.SetActive(state == GameState.Paused);
    }

    private void OnResume()
    {
        flow?.Resume();
    }

    private void OnRestart()
    {
        // Reiniciar escena actual (MVP)
        Time.timeScale = 1f; // por si estaba pausado
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
