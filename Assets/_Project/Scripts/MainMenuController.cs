using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Maneja las interacciones del menÃº principal: iniciar/continuar partida,
/// mostrar/ocultar el panel de opciones, y cerrar la aplicaciÃ³n.
/// AdemÃ¡s inicializa el texto de versiÃ³n y asegura que solo los paneles
/// correctos estÃ©n activos al cargar la escena.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("Panel con los botones principales (Continue, New Run, Options, Quit).")]
    [SerializeField] private GameObject menuPanel;

    [Tooltip("Panel con los controles de opciones. Debe estar desactivado por defecto.")]
    [SerializeField] private GameObject optionsPanel;

    [Header("UI Text")]
    [Tooltip("Elemento de texto para mostrar la versiÃ³n de la aplicaciÃ³n.")]
    [SerializeField] private TMP_Text versionLabel;

    private void Awake()
    {
        // El menÃº siempre debe arrancar con tiempo real, aunque el Ãºltimo save se haya hecho en pausa.
        Time.timeScale = 1f;

        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null && (flow.State == GameState.Paused || flow.State == GameState.GameOver || flow.State == GameState.Inventory))
            flow.SetState(GameState.Combat);

        AudioManager.Instance?.PlayMenuMusic();

        if (optionsPanel != null) optionsPanel.SetActive(false);

        // Muestra la versiÃ³n actual si se asignÃ³ la etiqueta.
        if (versionLabel != null)
        {
            versionLabel.text = $"v{Application.version}";
        }
    }

    /// <summary>
    /// Carga la escena de juego. El modo Single garantiza que se descarte
    /// el menÃº y solo quede la escena de combate.
    /// </summary>
    public void OnPlayButton()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiClick);

        GameFlowManager flow = GameFlowManager.Instance ?? ServiceRegistry.ResolveWithFallback(nameof(MainMenuController), "GameFlowManagerPlayButton", () => ServiceRegistry.LegacyFind<GameFlowManager>(true));
        if (flow != null)
        {
            flow.RestartRunFromMenu();
            return;
        }

        SceneManager.LoadScene(SceneCatalog.Load().MapScene, LoadSceneMode.Single);
    }

    /// <summary>
    /// Llamado por el botn Continue. Intenta seguir la corrida activa si existe.
    /// </summary>
    public void OnContinueButton()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiClick);

        GameFlowManager flow = GameFlowManager.Instance ?? ServiceRegistry.ResolveWithFallback(nameof(MainMenuController), "GameFlowManagerContinueButton", () => ServiceRegistry.LegacyFind<GameFlowManager>(true));
        if (flow != null && flow.ContinueRunFromMenu())
        {
            return;
        }

        SceneManager.LoadScene(SceneCatalog.Load().MapScene, LoadSceneMode.Single);
    }

    /// <summary>
    /// Llamado por el botÃ³n New Run. Por ahora hace lo mismo que OnPlayButton().
    /// </summary>
    public void OnNewRunButton() => OnPlayButton();

    /// <summary>
    /// Muestra el panel de opciones y oculta el panel principal.
    /// </summary>
    public void OnOptionsButton()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiOpenPanel);

        if (menuPanel != null) menuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    /// <summary>
    /// Oculta el panel de opciones y vuelve a mostrar el panel principal.
    /// </summary>
    public void OnCloseOptionsButton()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiClosePanel);

        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    /// <summary>
    /// Cierra la aplicaciÃ³n. En mÃ³vil cierra la app; en el editor no hace nada.
    /// </summary>
    public void OnQuitButton()
    {
        AudioManager.Instance?.PlaySfx(AudioEventId.UiClick);
        Application.Quit();
    }
}
