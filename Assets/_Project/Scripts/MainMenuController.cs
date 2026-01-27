using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject optionsPanel;

    private void Awake()
    {
        // Estado inicial consistente
        if (menuPanel != null) menuPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    public void OnPlayButton()
    {
        SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }

    public void OnOptionsButton()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void OnCloseOptionsButton()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    public void OnQuitButton()
    {
        Application.Quit();
    }
}
