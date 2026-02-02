using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

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

    private void ResolveFlow()
    {
        if (flow == null)
            flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
    }

    private void Awake()
    {
        ResolveFlow();

        if (root != null) root.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenu); // Nuevo listener
    }

    private void OnEnable()
    {
        ResolveFlow();
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
        ResolveFlow();
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
        flow?.Resume();
    }

    private void OnRestart()
    {
        // Reiniciar la escena actual (MVP)
        flow?.ResetRunState();
        flow?.SetState(GameState.Combat);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // NUEVO: volver al menu principal
    private void OnMenu()
    {


        // Reanudar por si estaba en estado Paused
        flow?.Resume();

        // Cargar la escena del menu principal en modo Single
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }
}
