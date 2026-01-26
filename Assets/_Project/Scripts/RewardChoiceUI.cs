using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardChoiceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RewardManager rewards;
    [SerializeField] private GameFlowManager flow;

    [Header("UI")]
    [SerializeField] private GameObject root; // el panel entero
    [SerializeField] private Button choice1Button;
    [SerializeField] private Button choice2Button;
    [SerializeField] private Button choice3Button;

    [SerializeField] private TMP_Text choice1Label;
    [SerializeField] private TMP_Text choice2Label;
    [SerializeField] private TMP_Text choice3Label;

    private void Awake()
    {
        if (root != null) root.SetActive(false);

        if (choice1Button != null) choice1Button.onClick.AddListener(() => OnChoose(1));
        if (choice2Button != null) choice2Button.onClick.AddListener(() => OnChoose(2));
        if (choice3Button != null) choice3Button.onClick.AddListener(() => OnChoose(3));
    }

    private void Start()
    {
        SyncStateAndChoices();
    }

    private void OnEnable()
    {
        if (rewards != null)
        {
            rewards.OrbChoicesPresented += OnOrbChoicesPresented;
            rewards.RewardResolved += OnRewardResolved;
        }

        if (flow != null)
        {
            flow.OnStateChanged += OnStateChanged;
        }

        
        SyncStateAndChoices();
    }

    private void OnDisable()
    {
        if (rewards != null)
        {
            rewards.OrbChoicesPresented -= OnOrbChoicesPresented;
            rewards.RewardResolved -= OnRewardResolved;
        }

        if (flow != null)
        {
            flow.OnStateChanged -= OnStateChanged;
        }
    }

    private void SyncStateAndChoices()
    {
        // 1) Sincronizar visibilidad con el estado actual
        if (flow != null && root != null)
            root.SetActive(flow.State == GameState.RewardChoice);

        // 2) Si ya estamos esperando choice, pintar lo que tenga RewardManager ahora
        if (rewards != null && rewards.IsAwaitingChoice)
        {
            var choices = rewards.CurrentChoices;
            if (choices != null && choices.Count > 0)
            {
                // Convertimos a array para reutilizar el mismo método
                OrbData[] arr = new OrbData[choices.Count];
                for (int i = 0; i < choices.Count; i++) arr[i] = choices[i];
                OnOrbChoicesPresented(arr);
            }
        }
    }

    private void OnStateChanged(GameState state)
    {
        if (root != null)
            root.SetActive(state == GameState.RewardChoice);

        // Si entramos a RewardChoice, pintamos por si el evento ya pasó
        if (state == GameState.RewardChoice)
            SyncStateAndChoices();
    }

    private void OnOrbChoicesPresented(OrbData[] choices)
    {
        if (root != null) root.SetActive(true);

        SetChoice(choice1Label, choice1Button, choices, 0);
        SetChoice(choice2Label, choice2Button, choices, 1);
        SetChoice(choice3Label, choice3Button, choices, 2);
    }

    private void SetChoice(TMP_Text label, Button button, OrbData[] choices, int index)
    {
        bool valid = choices != null && index >= 0 && index < choices.Length && choices[index] != null;

        if (label != null)
            label.text = valid ? choices[index].orbName : "-";

        if (button != null)
            button.interactable = valid;
    }

    private void OnChoose(int idx)
    {
        if (rewards == null) return;
        rewards.ChooseOrb(idx);
    }

    private void OnRewardResolved()
    {
        if (root != null) root.SetActive(false);
    }
}
