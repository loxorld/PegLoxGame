using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Maneja las interacciones del menú principal: iniciar/continuar partida,
/// mostrar/ocultar el panel de opciones, y cerrar la aplicación.
/// Además inicializa el texto de versión y asegura que solo los paneles
/// correctos estén activos al cargar la escena.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("Panel con los botones principales (Continue, New Run, Options, Quit).")]
    [SerializeField] private GameObject menuPanel;

    [Tooltip("Panel con los controles de opciones. Debe estar desactivado por defecto.")]
    [SerializeField] private GameObject optionsPanel;

    [Header("UI Text")]
    [Tooltip("Elemento de texto para mostrar la versión de la aplicación.")]
    [SerializeField] private TMP_Text versionLabel;

    private void Awake()
    {
        // Asegura que los paneles correctos estén activos al iniciar.
        if (menuPanel != null) menuPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        // Muestra la versión actual si se asignó la etiqueta.
        if (versionLabel != null)
        {
            versionLabel.text = $"v{Application.version}";
        }
    }

    /// <summary>
    /// Carga la escena de juego. El modo Single garantiza que se descarte
    /// el menú y solo quede la escena de combate.
    /// </summary>
    public void OnPlayButton()
    {
        GameFlowManager flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
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
        GameFlowManager flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
        if (flow != null && flow.SavedMapNode != null)
        {
            flow.SetState(GameState.Combat);
            SceneManager.LoadScene(SceneCatalog.Load().CombatScene, LoadSceneMode.Single);
            return;
        }

        SceneManager.LoadScene(SceneCatalog.Load().MapScene, LoadSceneMode.Single);
    }

    /// <summary>
    /// Llamado por el botón New Run. Por ahora hace lo mismo que OnPlayButton().
    /// </summary>
    public void OnNewRunButton() => OnPlayButton();

    /// <summary>
    /// Muestra el panel de opciones y oculta el panel principal.
    /// </summary>
    public void OnOptionsButton()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    /// <summary>
    /// Oculta el panel de opciones y vuelve a mostrar el panel principal.
    /// </summary>
    public void OnCloseOptionsButton()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    /// <summary>
    /// Cierra la aplicación. En móvil cierra la app; en el editor no hace nada.
    /// </summary>
    public void OnQuitButton()
    {
        Application.Quit();
    }
}
