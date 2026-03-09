using System.Collections.Generic;
using UnityEngine;

public partial class MapDomainService
{
    public readonly struct EventOptionRequirement
    {
        public EventOptionRequirement(int minCoins, int minHp, string requiredRelicId)
        {
            MinCoins = Mathf.Max(0, minCoins);
            MinHp = Mathf.Max(0, minHp);
            RequiredRelicId = requiredRelicId ?? string.Empty;
        }

        public int MinCoins { get; }
        public int MinHp { get; }
        public string RequiredRelicId { get; }
        public bool HasRequirements => MinCoins > 0 || MinHp > 0 || !string.IsNullOrWhiteSpace(RequiredRelicId);
    }

    public readonly struct EventOptionContext
    {
        public EventOptionContext(int coins, int currentHp, IReadOnlyCollection<string> relicIds)
        {
            Coins = Mathf.Max(0, coins);
            CurrentHp = Mathf.Max(0, currentHp);
            RelicIds = relicIds;
        }

        public int Coins { get; }
        public int CurrentHp { get; }
        public IReadOnlyCollection<string> RelicIds { get; }
    }

    public readonly struct EventOptionAvailability
    {
        public EventOptionAvailability(bool isAvailable, string missingRequirementText)
        {
            IsAvailable = isAvailable;
            MissingRequirementText = missingRequirementText ?? string.Empty;
        }

        public bool IsAvailable { get; }
        public string MissingRequirementText { get; }
    }

    public readonly struct NodeResolution
    {
        public NodeResolution(MapNodeData node, bool shouldClearSavedNode)
        {
            Node = node;
            ShouldClearSavedNode = shouldClearSavedNode;
        }

        public MapNodeData Node { get; }
        public bool ShouldClearSavedNode { get; }
    }

    public readonly struct EventOptionOutcome
    {
        public EventOptionOutcome(string optionLabel, int coinDelta, int hpDelta, string resultDescription, float? probability = null, EventOptionRequirement? requirement = null)
        {
            OptionLabel = optionLabel;
            Probability = probability;
            SuccessOutcome = new EventResolutionOutcome(coinDelta, hpDelta, resultDescription);
            FailureOutcome = SuccessOutcome;
            Requirement = requirement ?? default;
        }

        public EventOptionOutcome(string optionLabel, float probability, EventResolutionOutcome successOutcome, EventResolutionOutcome failureOutcome, EventOptionRequirement? requirement = null)
        {
            OptionLabel = optionLabel;
            Probability = Mathf.Clamp01(probability);
            SuccessOutcome = successOutcome;
            FailureOutcome = failureOutcome;
            Requirement = requirement ?? default;
        }

        public string OptionLabel { get; }
        public float? Probability { get; }
        public EventResolutionOutcome SuccessOutcome { get; }
        public EventResolutionOutcome FailureOutcome { get; }
        public EventOptionRequirement Requirement { get; }
    }

    public readonly struct EventResolutionOutcome
    {
        public EventResolutionOutcome(int coinDelta, int hpDelta, string resultDescription)
        {
            CoinDelta = coinDelta;
            HpDelta = hpDelta;
            ResultDescription = resultDescription;
        }

        public int CoinDelta { get; }
        public int HpDelta { get; }
        public string ResultDescription { get; }
    }

    public readonly struct EventScenarioOutcome
    {
        public EventScenarioOutcome(string title, string description, IReadOnlyList<EventOptionOutcome> options)
        {
            Title = title;
            Description = description;
            Options = options;
        }

        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<EventOptionOutcome> Options { get; }
    }

    public readonly struct ShopOutcome
    {
        public ShopOutcome(string title, string description, int currentCoins, int healCost, int healAmount, int orbUpgradeCost)
        {
            Title = title;
            Description = description;
            CurrentCoins = currentCoins;
            HealCost = healCost;
            HealAmount = healAmount;
            OrbUpgradeCost = orbUpgradeCost;
        }

        public string Title { get; }
        public string Description { get; }
        public int CurrentCoins { get; }
        public int HealCost { get; }
        public int HealAmount { get; }
        public int OrbUpgradeCost { get; }
    }
}
