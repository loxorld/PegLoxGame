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

    private void ResolveReferences()
    {
        if (orbManager == null)
            orbManager = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);

        if (gameFlowManager == null)
            gameFlowManager = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);

        if (shotManager == null)
            shotManager = FindObjectOfType<ShotManager>(true);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        prevButton.onClick.AddListener(OnPrevOrb);
        nextButton.onClick.AddListener(OnNextOrb);
        UpdateUI();
    }

    private void Update()
    {
        if (orbManager == null || gameFlowManager == null || shotManager == null)
            ResolveReferences();

        if (orbManager == null || gameFlowManager == null || shotManager == null) return;

        bool canChange = gameFlowManager.State == GameState.Combat && !shotManager.ShotInProgress;
        prevButton.interactable = canChange;
        nextButton.interactable = canChange;
    }

    private void OnPrevOrb()
    {
        if (orbManager == null) return;
        orbManager.PrevOrb();
        UpdateUI();
    }

    private void OnNextOrb()
    {
        if (orbManager == null) return;
        orbManager.NextOrb();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (orbNameLabel != null)
        {
            orbNameLabel.text = orbManager != null ? (orbManager.CurrentOrb?.OrbName ?? "Orb") : "Orb";
        }
    }
}
