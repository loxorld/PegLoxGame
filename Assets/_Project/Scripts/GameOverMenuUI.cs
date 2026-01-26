using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private PlayerStats player;

    [Header("UI")]
    [SerializeField] private GameObject root;   // overlay completo
    [SerializeField] private Button restartButton;

    private void Awake()
    {
        if (flow == null) flow = GameFlowManager.Instance;

        if (root != null) root.SetActive(false);

        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);
    }

    private void OnEnable()
    {
        if (player != null)
            player.Died += OnPlayerDied;

        if (flow != null)
            flow.OnStateChanged += OnStateChanged;

        Sync();
    }

    private void OnDisable()
    {
        if (player != null)
            player.Died -= OnPlayerDied;

        if (flow != null)
            flow.OnStateChanged -= OnStateChanged;
    }

    private void OnPlayerDied()
    {
        flow?.SetState(GameState.GameOver);
    }

    private void OnStateChanged(GameState state)
    {
        Sync();
    }

    private void Sync()
    {
        if (flow == null || root == null) return;
        root.SetActive(flow.State == GameState.GameOver);
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
