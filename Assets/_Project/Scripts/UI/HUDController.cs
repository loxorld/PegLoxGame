using TMPro;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats player;
    [SerializeField] private BattleManager battle;
    [SerializeField] private OrbManager orbs;
    [SerializeField] private GameFlowManager flow;

    [Header("UI Text (optional)")]
    [SerializeField] private TMP_Text orbNameText;
    [SerializeField] private TMP_Text stateText; // opcional

    [Header("Encounter / Difficulty (optional)")]
    [SerializeField] private TMP_Text encounterText;   // "ENCOUNTER: 1"
    [SerializeField] private TMP_Text difficultyText;  // "HP x1.2 (+0) | DMG x1.1 (+0) | N=3"
    [SerializeField] private TMP_Text coinsText;

    [Header("Bars")]
    [SerializeField] private HealthBarUI playerBar;
    [SerializeField] private HealthBarUI enemyBar;

    [Header("Update")]
    [SerializeField, Range(0.05f, 1f)] private float refreshInterval = 0.15f;

    private float timer;



    private void ResolveReferences()
    {
        if (player == null)
            player = FindObjectOfType<PlayerStats>(true);

        if (battle == null)
            battle = FindObjectOfType<BattleManager>(true);

        if (orbs == null)
            orbs = OrbManager.Instance ?? FindObjectOfType<OrbManager>(true);

        if (flow == null)
            flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (flow == null || orbs == null || player == null || battle == null)
            ResolveReferences();

        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;

        Refresh();
    }

    private void Refresh()
    {
        // Player bar
        if (playerBar != null && player != null)
            playerBar.Set(player.CurrentHP, player.MaxHP);

        // Enemy bar
        Enemy e = (battle != null) ? battle.CurrentEnemy : null;
        bool enemyVisible = e != null && e.gameObject.activeSelf;

        if (enemyBar != null)
        {
            if (!enemyVisible)
                enemyBar.Set(0, 1);
            else
                enemyBar.Set(e.CurrentHP, e.MaxHP);
        }

        // Orb text
        if (orbNameText != null)
        {
            OrbData orb = (orbs != null) ? orbs.CurrentOrb : null;
            orbNameText.text = orb != null ? $"Orb: {orb.orbName}" : "Orb: -";
        }

        // State text
        if (stateText != null)
        {
            GameState s = (flow != null) ? flow.State : GameState.Combat;
            stateText.text = s switch
            {
                GameState.Combat => "",
                GameState.RewardChoice => "REWARD",
                GameState.Paused => "PAUSED",
                GameState.GameOver => "GAME OVER",
                _ => s.ToString()
            };
        }

        // Coins text
        if (coinsText != null)
        {
            int coins = flow != null ? flow.Coins : 0;
            coinsText.text = $"Monedas: {coins}";
        }

        // Encounter text
        if (encounterText != null)
        {
            if (battle == null) encounterText.text = "";
            else encounterText.text = $"ENCOUNTER: {battle.EncounterIndex + 1}";
        }

        // Difficulty text
        if (difficultyText != null)
        {
            if (battle == null || !battle.HasDifficultyConfig)
                difficultyText.text = "";
            else
                difficultyText.text = $"STAGE: {battle.StageName}";
        }
    }
}
