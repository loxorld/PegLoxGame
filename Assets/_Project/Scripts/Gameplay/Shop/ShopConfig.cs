using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Shop/Shop Config", fileName = "ShopConfig")]
public class ShopConfig : ScriptableObject
{
    [Serializable]
    public sealed class OfferTemplate
    {
        [SerializeField] private string offerId = "offer";
        [SerializeField] private ShopService.ShopOfferType type = ShopService.ShopOfferType.Heal;
        [SerializeField] private ShopService.ShopOfferRarity rarity = ShopService.ShopOfferRarity.Common;
        [SerializeField, Min(0)] private int baseCost = 10;
        [SerializeField, Min(1)] private int baseStock = 1;
        [SerializeField, Min(1)] private int primaryValue = 1;
        [SerializeField] private bool requiresMissingHp;
        [SerializeField] private bool requiresUpgradableOrb;
        [SerializeField] private bool requiresAnyOrb;

        public string OfferId => offerId;
        public ShopService.ShopOfferType Type => type;
        public ShopService.ShopOfferRarity Rarity => rarity;
        public int BaseCost => baseCost;
        public int BaseStock => baseStock;
        public int PrimaryValue => primaryValue;
        public bool RequiresMissingHp => requiresMissingHp;
        public bool RequiresUpgradableOrb => requiresUpgradableOrb;
        public bool RequiresAnyOrb => requiresAnyOrb;
    }

    [Serializable]
    public sealed class RarityWeight
    {
        [SerializeField] private ShopService.ShopOfferRarity rarity = ShopService.ShopOfferRarity.Common;
        [SerializeField, Min(0f)] private float weight = 1f;

        public ShopService.ShopOfferRarity Rarity => rarity;
        public float Weight => Mathf.Max(0f, weight);
    }

    [Header("Offers")]
    [SerializeField] private List<OfferTemplate> offerTable = new List<OfferTemplate>();

    [Header("Generation")]
    [SerializeField] private List<RarityWeight> rarityWeights = new List<RarityWeight>();
    [SerializeField, Min(1)] private int minOffersPerVisit = 3;
    [SerializeField, Min(1)] private int maxOffersPerVisit = 6;

    [Header("Pricing")]
    [SerializeField, Min(0.1f)] private float commonPriceMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float rarePriceMultiplier = 1.2f;
    [SerializeField, Min(0.1f)] private float epicPriceMultiplier = 1.35f;
    [SerializeField, Min(0.1f)] private float legendaryPriceMultiplier = 1.6f;

    [Header("Refresh")]
    [SerializeField] private bool allowManualRefresh = true;
    [SerializeField, Min(0)] private int refreshCost = 0;
    [SerializeField, Min(0)] private int maxRefreshesPerVisit = 2;

    public IReadOnlyList<OfferTemplate> OfferTable => offerTable;
    public IReadOnlyList<RarityWeight> RarityWeights => rarityWeights;
    public int MinOffersPerVisit => Mathf.Max(1, minOffersPerVisit);
    public int MaxOffersPerVisit => Mathf.Max(MinOffersPerVisit, maxOffersPerVisit);
    public bool AllowManualRefresh => allowManualRefresh;
    public int RefreshCost => Mathf.Max(0, refreshCost);
    public int MaxRefreshesPerVisit => Mathf.Max(0, maxRefreshesPerVisit);

    public float GetPriceMultiplier(ShopService.ShopOfferRarity rarity)
    {
        switch (rarity)
        {
            case ShopService.ShopOfferRarity.Rare:
                return rarePriceMultiplier;
            case ShopService.ShopOfferRarity.Epic:
                return epicPriceMultiplier;
            case ShopService.ShopOfferRarity.Legendary:
                return legendaryPriceMultiplier;
            default:
                return commonPriceMultiplier;
        }
    }
}