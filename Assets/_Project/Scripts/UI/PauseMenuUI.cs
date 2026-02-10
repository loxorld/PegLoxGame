using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameFlowManager flow;

    [Header("UI")]
    [SerializeField] private GameObject root;   // Panel overlay completo
    [SerializeField] private OverlayAnimator overlayAnim; // OverlayAnimator en el root
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton; // Nuevo boton para volver al menu

    private void RefreshFlowSubscription()
    {
        GameFlowManager current = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);

        if (current == flow) return;

        if (flow != null)
            flow.OnStateChanged -= OnStateChanged;

        flow = current;

        if (flow != null)
            flow.OnStateChanged += OnStateChanged;
    }

    private void Awake()
    {
        if (root != null) root.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenu); // Nuevo listener
    }

    private void OnEnable()
    {
        RefreshFlowSubscription();
        Sync();
    }

    private void OnDisable()
    {
        if (flow != null)
            flow.OnStateChanged -= OnStateChanged;
    }

    private void Sync()
    {
        RefreshFlowSubscription();
        if (flow == null) return;

        if (flow.State == GameState.Paused)
            ShowOverlay();
        else
            HideOverlayImmediate();
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Paused)
            ShowOverlay();
        else
            HideOverlay();
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

    private void OnResume()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiClick);
        RefreshFlowSubscription();
        flow?.Resume();
    }

    private void OnRestart()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiClick);
        RefreshFlowSubscription();
        flow?.RestartCombatScene();
    }

    // NUEVO: volver al menu principal
    private void OnMenu()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiClick);

        // Reanudar por si estaba en estado Paused
        RefreshFlowSubscription();
        if (flow != null)
        {
            flow.SaveRun();
            flow.Resume();
        }
        flow?.Resume();

        // Cargar la escena del menu principal en modo Single
        SceneManager.LoadScene(SceneCatalog.Load().MainMenuScene, LoadSceneMode.Single);
    }
}