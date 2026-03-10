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
            orbManager = ServiceRegistry.ResolveWithFallback(nameof(OrbSwitchUI), nameof(orbManager), () => OrbManager.Instance ?? ServiceRegistry.LegacyFind<OrbManager>(true));

        if (gameFlowManager == null)
            gameFlowManager = ServiceRegistry.ResolveWithFallback(nameof(OrbSwitchUI), nameof(gameFlowManager), () => GameFlowManager.Instance ?? ServiceRegistry.LegacyFind<GameFlowManager>(true));

        if (shotManager == null)
            shotManager = ServiceRegistry.ResolveWithFallback(nameof(OrbSwitchUI), nameof(shotManager), () => ServiceRegistry.LegacyFind<ShotManager>(true));
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        if (prevButton != null)
        {
            prevButton.onClick.AddListener(OnPrevOrb);
            UIButtonMotion.Attach(prevButton.transform as RectTransform, 1.03f, 0.965f, 0.12f);
        }
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextOrb);
            UIButtonMotion.Attach(nextButton.transform as RectTransform, 1.03f, 0.965f, 0.12f);
        }
        UpdateUI();
    }

    private void Update()
    {
        if (orbManager == null || gameFlowManager == null || shotManager == null)
            ResolveReferences();

        if (orbManager == null || gameFlowManager == null || shotManager == null) return;

        bool canChange = gameFlowManager.State == GameState.Combat && !shotManager.ShotInProgress;
        if (prevButton != null)
            prevButton.interactable = canChange;
        if (nextButton != null)
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
            OrbInstance orb = orbManager != null ? orbManager.CurrentOrb : null;
            if (orb != null)
            {
                string orbHex = ColorUtility.ToHtmlStringRGB(Color.Lerp(orb.Color, Color.white, 0.18f));
                string plainText = $"{orb.OrbName}\nLv {orb.Level}";
                string richText = $"<color=#{orbHex}><b>{orb.OrbName}</b></color>\n<size=72%><color=#F0DEC1CC>Lv {orb.Level}</color></size>";
                orbNameLabel.text = UIArtUtility.ResolveDynamicText(orbNameLabel, plainText, richText);
            }
            else
            {
                orbNameLabel.text = UIArtUtility.ResolveDynamicText(orbNameLabel, "Sin orbe", "<color=#F0DEC1CC>Sin orbe</color>");
            }
        }
    }
}
