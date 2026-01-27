using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameFlowManager flow;

    [Header("UI")]
    [SerializeField] private GameObject root;   // Panel overlay completo
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton; // Nuevo botón para volver al menú

    private void Awake()
    {
        if (flow == null) flow = GameFlowManager.Instance;

        if (root != null) root.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenu); // Nuevo listener
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
        // Reiniciar la escena actual (MVP)
        Time.timeScale = 1f; // Por si estaba pausado
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // NUEVO: volver al menú principal
    private void OnMenu()
    {
        // Asegúrate de restaurar la escala de tiempo
        Time.timeScale = 1f;

        // Reanudar por si estaba en estado Paused
        flow?.Resume();

        // Cargar la escena del menú principal en modo Single
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }
}
