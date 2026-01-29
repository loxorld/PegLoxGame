using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OrbSwitchUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OrbManager orbManager;
    [SerializeField] private GameFlowManager gameFlowManager;
    [SerializeField] private ShotManager shotManager;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text orbNameLabel;

    private void Start()
    {
        prevButton.onClick.AddListener(OnPrevOrb);
        nextButton.onClick.AddListener(OnNextOrb);
        UpdateUI();
    }

    private void Update()
    {
        bool canChange = gameFlowManager.State == GameState.Combat && !shotManager.ShotInProgress;
        prevButton.interactable = canChange;
        nextButton.interactable = canChange;
    }

    private void OnPrevOrb()
    {
        orbManager.PrevOrb();
        UpdateUI();
    }

    private void OnNextOrb()
    {
        orbManager.NextOrb();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (orbNameLabel != null)
        {
            orbNameLabel.text = orbManager.CurrentOrb?.name ?? "Orb";
        }
    }
}
