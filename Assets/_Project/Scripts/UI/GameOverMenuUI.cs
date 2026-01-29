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
    [SerializeField] private OverlayAnimator overlayAnim; // OverlayAnimator en el root
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
        if (state == GameState.GameOver)
            ShowOverlay();
        else
            HideOverlay();
    }

    private void Sync()
    {
        if (flow == null) return;

        if (flow.State == GameState.GameOver)
            ShowOverlay();
        else
            HideOverlayImmediate();
    }

    private void ShowOverlay()
    {
        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (overlayAnim != null)
        {
            overlayAnim.Show();
            return;
        }
    }

    private void HideOverlay()
    {
        if (overlayAnim != null)
        {
            overlayAnim.Hide();
            return;
        }

        if (root != null)
            root.SetActive(false);
    }

    private void HideOverlayImmediate()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}