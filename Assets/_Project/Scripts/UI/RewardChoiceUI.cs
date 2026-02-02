using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardChoiceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RewardManager rewards;
    [SerializeField] private GameFlowManager flow;

    [Header("Overlay Root")]
    [SerializeField] private GameObject root;              // RewardChoiceOverlay
    [SerializeField] private OverlayAnimator overlayAnim;  // OverlayAnimator en RewardChoiceOverlay

    [Header("Choice 1")]
    [SerializeField] private Button choice1Button;
    [SerializeField] private Image choice1Icon;
    [SerializeField] private TMP_Text choice1Title;
    [SerializeField] private TMP_Text choice1Desc;

    [Header("Choice 2")]
    [SerializeField] private Button choice2Button;
    [SerializeField] private Image choice2Icon;
    [SerializeField] private TMP_Text choice2Title;
    [SerializeField] private TMP_Text choice2Desc;

    [Header("Choice 3")]
    [SerializeField] private Button choice3Button;
    [SerializeField] private Image choice3Icon;
    [SerializeField] private TMP_Text choice3Title;
    [SerializeField] private TMP_Text choice3Desc;

    private bool flowSubscribed;

    private void ResolveReferences()
    {
        if (rewards == null)
            rewards = FindObjectOfType<RewardManager>(true);

        if (flow == null)
            flow = GameFlowManager.Instance ?? FindObjectOfType<GameFlowManager>(true);
    }

    private void Awake()
    {
        // Estado inicial: oculto
        if (root != null) root.SetActive(false);

        if (choice1Button != null) choice1Button.onClick.AddListener(() => OnChoose(1));
        if (choice2Button != null) choice2Button.onClick.AddListener(() => OnChoose(2));
        if (choice3Button != null) choice3Button.onClick.AddListener(() => OnChoose(3));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
    }
#endif
    private void Start()
    {
        ResolveReferences();
        TrySubscribeFlow();
        SyncStateAndChoices();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (rewards != null)
        {
            rewards.RewardChoicesPresented += OnRewardChoicesPresented;
            rewards.RewardResolved += OnRewardResolved;
        }

        if (flow != null)
            flow.OnStateChanged += OnStateChanged;
        TrySubscribeFlow();

        SyncStateAndChoices();
    }

    private void Update()
    {
        if (flow == null)
        {
            ResolveReferences();
            TrySubscribeFlow();
        }
    }


    private void OnDisable()
    {
        if (rewards != null)
        {
            rewards.RewardChoicesPresented -= OnRewardChoicesPresented;
            rewards.RewardResolved -= OnRewardResolved;
        }

        if (flow != null && flowSubscribed)
        {
            flow.OnStateChanged -= OnStateChanged;
            flowSubscribed = false;
        }
    }


    private void SyncStateAndChoices()
    {
        ResolveReferences();
        bool shouldBeVisible = (flow != null && flow.State == GameState.RewardChoice);

        if (shouldBeVisible)
            ShowOverlay();
        else
            HideOverlayImmediate();

        
        if (rewards != null && rewards.IsAwaitingChoice)
        {
            var choices = rewards.CurrentChoices;
            if (choices != null && choices.Count > 0)
            {
                RewardOption[] arr = new RewardOption[choices.Count];
                for (int i = 0; i < choices.Count; i++) arr[i] = choices[i];
                OnRewardChoicesPresented(arr);
            }
        }
    }

    private void OnStateChanged(GameState state)
    {
        if (state == GameState.RewardChoice)
            ShowOverlay();
        else
            HideOverlayImmediate(); 
    }

    private void OnRewardChoicesPresented(RewardOption[] choices)
    {
        ShowOverlay();

        SetChoice(choice1Button, choice1Icon, choice1Title, choice1Desc, choices, 0);
        SetChoice(choice2Button, choice2Icon, choice2Title, choice2Desc, choices, 1);
        SetChoice(choice3Button, choice3Icon, choice3Title, choice3Desc, choices, 2);
    }

    private void SetChoice(Button button, Image icon, TMP_Text title, TMP_Text desc, RewardOption[] choices, int index)
    {
        bool valid = choices != null && index >= 0 && index < choices.Length && choices[index].IsValid;

        if (button != null)
            button.interactable = valid;

        if (!valid)
        {
            if (title != null) title.text = "-";
            if (desc != null) desc.text = "";
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }
            return;
        }

        var opt = choices[index];
        string prefix = opt.kind == RewardKind.Orb ? "ORB" : "RELIC";

        if (title != null)
            title.text = $"{prefix}: {opt.DisplayName}";

        if (desc != null)
            desc.text = opt.DisplayDescription ?? "";

        if (icon != null)
        {
            icon.sprite = opt.DisplayIcon;
            icon.enabled = opt.DisplayIcon != null;
        }
    }

    private void OnChoose(int idx)
    {
        if (rewards == null) return;
        rewards.Choose(idx);
    }

    private void OnRewardResolved()
    {
        
        if (overlayAnim != null)
            overlayAnim.Hide();
        else if (root != null)
            root.SetActive(false);
    }

    private void ShowOverlay()
    {
        if (overlayAnim != null)
        {
            overlayAnim.Show();
            return;
        }

        if (root != null && !root.activeSelf)
            root.SetActive(true);
    }

    // Ocultado inmediato para sincronizar estado al cargar/si cambia estado afuera del flujo de rewards
    private void HideOverlayImmediate()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void TrySubscribeFlow()
    {
        if (flow != null && !flowSubscribed)
        {
            flow.OnStateChanged += OnStateChanged;
            flowSubscribed = true;
        }
    }

}
