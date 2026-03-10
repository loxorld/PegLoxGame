using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopService
{
    public enum ShopOfferType
    {
        Heal,
        OrbUpgrade,
        FocusedUpgrade,
        OrbUpgradeDiscount,
        RecoveryPack,
        VitalityBoost
    }

    public enum ShopOfferRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public sealed class ShopOfferData
    {
        public string OfferId;
        public ShopOfferType Type;
        public int Cost;
        public int Stock;
        public ShopOfferRarity Rarity;
        public int PrimaryValue;
        public bool RequiresMissingHp;
        public bool RequiresUpgradableOrb;
        public bool RequiresAnyOrb;
    }


    public sealed class ShopOptionData
    {
        public ShopOptionData(string label, bool isEnabled, Action onSelect, ShopOfferRarity? rarity = null, bool isExitOption = false)
        {
            Label = label;
            IsEnabled = isEnabled;
            OnSelect = onSelect;
            Rarity = rarity;
            IsExitOption = isExitOption;
        }

        public string Label { get; }
        public bool IsEnabled { get; }
        public Action OnSelect { get; }
        public ShopOfferRarity? Rarity { get; }
        public bool IsExitOption { get; }
    }

    public sealed class PlayerShopState
    {
        public int Coins;
        public int OrbCount;
        public int UpgradableOrbCount;
        public bool HasMissingHp;
        public int CurrentHp;
        public int MaxHp;
        public bool HasCurrentOrb;
        public bool CurrentOrbCanUpgrade;
        public string CurrentOrbName;
    }

    public sealed class ShopOfferPresentation
    {
        public string Title;
        public string Subtitle;
        public string Detail;
        public string Badge;
        public string CostText;
        public string StatusText;
        public bool IsEnabled;
        public bool IsAffordable;
        public Color AccentColor;
        public Color CardColor;
        public Color BadgeColor;
    }

    private readonly ShopDomainService domainService = new ShopDomainService();

    public List<ShopOfferData> BuildOrLoadCatalog(
        GameFlowManager flow,
        OrbManager orbManager,
        ShopConfig config,
        RunBalanceConfig balance,
        int stageIndex,
        string shopId,
        int fallbackHealCost,
        int fallbackHealAmount,
        int fallbackUpgradeCost,
        bool forceRefresh = false)
    {
        if (!forceRefresh)
        {
            List<GameFlowManager.ShopOfferRunData> persisted = flow != null ? flow.GetShopCatalog(shopId) : null;
            if (persisted != null && persisted.Count > 0)
            {
                var restored = new List<ShopOfferData>(persisted.Count);
                for (int i = 0; i < persisted.Count; i++)
                {
                    GameFlowManager.ShopOfferRunData data = persisted[i];
                    restored.Add(new ShopOfferData
                    {
                        OfferId = data.OfferId,
                        Type = data.OfferType,
                        Cost = data.Cost,
                        Stock = data.RemainingStock,
                        Rarity = data.Rarity,
                        PrimaryValue = data.PrimaryValue > 0
                            ? data.PrimaryValue
                            : ResolveDefaultPrimaryValue(data.OfferType, fallbackHealAmount),
                        RequiresMissingHp = data.RequiresMissingHp,
                        RequiresUpgradableOrb = data.RequiresUpgradableOrb,
                        RequiresAnyOrb = data.RequiresAnyOrb
                    });
                }

                return restored;
            }
        }

        PlayerShopState playerState = BuildPlayerState(flow, orbManager);
        List<ShopOfferData> generated = domainService.GenerateOffers(config, balance, stageIndex, fallbackHealCost, fallbackHealAmount, fallbackUpgradeCost, playerState);
        if (generated == null || generated.Count == 0)
            return new List<ShopOfferData>();

        flow?.SaveShopCatalog(shopId, generated);
        return generated;
    }

    public PlayerShopState BuildPlayerState(GameFlowManager flow, OrbManager orbManager)
    {
        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        int orbCount = ownedOrbs != null ? ownedOrbs.Count : 0;
        int upgradable = GetUpgradableOrbs(ownedOrbs).Count;
        bool missingHp = flow != null && flow.HasSavedPlayerHP && flow.SavedPlayerHP < flow.PlayerMaxHP;
        OrbInstance currentOrb = orbManager != null ? orbManager.CurrentOrb : null;
        int maxHp = flow != null ? Mathf.Max(1, flow.PlayerMaxHP) : 0;
        int currentHp = flow != null
            ? (flow.HasSavedPlayerHP ? Mathf.Clamp(flow.SavedPlayerHP, 0, maxHp) : maxHp)
            : 0;

        return new PlayerShopState
        {
            Coins = flow != null ? flow.Coins : 0,
            OrbCount = orbCount,
            UpgradableOrbCount = upgradable,
            HasMissingHp = missingHp,
            CurrentHp = currentHp,
            MaxHp = maxHp,
            HasCurrentOrb = currentOrb != null,
            CurrentOrbCanUpgrade = currentOrb != null && currentOrb.CanLevelUp,
            CurrentOrbName = currentOrb != null ? currentOrb.OrbName : string.Empty
        };
    }

    public bool IsOfferEnabled(PlayerShopState state, ShopOfferData offer, out string reason)
    {
        return domainService.IsOfferEnabled(state, offer, out reason);
    }

    public bool TryPurchaseOffer(GameFlowManager flow, OrbManager orbManager, string shopId, ShopOfferData offer, out string message)
    {
        ShopDomainService.PurchaseResult result = domainService.TryPurchaseAtomic(flow, orbManager, shopId, offer);
        message = result.Message;
        return result.Success;
    }

    public ShopOfferPresentation BuildOfferPresentation(PlayerShopState state, ShopOfferData offer)
    {
        if (offer == null)
            return new ShopOfferPresentation
            {
                Title = "Oferta",
                Subtitle = string.Empty,
                Detail = "Oferta invalida.",
                Badge = string.Empty,
                CostText = "0g",
                StatusText = string.Empty,
                IsEnabled = false,
                IsAffordable = false,
                AccentColor = new Color(0.8f, 0.8f, 0.8f, 1f),
                CardColor = new Color(0.17f, 0.19f, 0.2f, 0.96f),
                BadgeColor = new Color(0.35f, 0.35f, 0.35f, 1f)
            };

        bool enabled = IsOfferEnabled(state, offer, out string reason);
        bool affordable = state != null && state.Coins >= offer.Cost;

        return new ShopOfferPresentation
        {
            Title = GetOfferDisplayTitle(offer),
            Subtitle = BuildOfferSubtitle(offer, state),
            Detail = BuildOfferDetail(offer, state),
            Badge = $"{GetRarityLabel(offer.Rarity)} · {GetOfferCategoryLabel(offer.Type)}",
            CostText = $"{offer.Cost}g",
            StatusText = enabled ? BuildAvailabilitySummary(offer, state) : reason ?? "No disponible",
            IsEnabled = enabled,
            IsAffordable = affordable,
            AccentColor = GetRarityAccentColor(offer.Rarity),
            CardColor = GetOfferCardColor(offer.Rarity, enabled),
            BadgeColor = GetOfferBadgeColor(offer.Rarity)
        };
    }

    public static string GetOfferDisplayTitle(ShopOfferData offer)
    {
        if (offer == null)
            return "Oferta";

        return offer.Type switch
        {
            ShopOfferType.Heal => offer.Rarity == ShopOfferRarity.Rare ? "Tonico mayor" : "Curacion de campamento",
            ShopOfferType.OrbUpgrade => "Yunque comun",
            ShopOfferType.FocusedUpgrade => "Forja precisa",
            ShopOfferType.OrbUpgradeDiscount => "Descuento del herrero",
            ShopOfferType.RecoveryPack => "Kit de recuperacion",
            ShopOfferType.VitalityBoost => "Corazon reforzado",
            _ => "Oferta"
        };
    }

    public static string GetOfferTypeLabel(ShopOfferType type)
    {
        return type switch
        {
            ShopOfferType.Heal => "Curacion",
            ShopOfferType.OrbUpgrade => "Mejora de orbe",
            ShopOfferType.FocusedUpgrade => "Mejora precisa",
            ShopOfferType.OrbUpgradeDiscount => "Mejora barata",
            ShopOfferType.RecoveryPack => "Recuperacion",
            ShopOfferType.VitalityBoost => "Vitalidad",
            _ => "Oferta"
        };
    }

    public static string GetOfferCategoryLabel(ShopOfferType type)
    {
        return type switch
        {
            ShopOfferType.Heal => "SUSTAIN",
            ShopOfferType.RecoveryPack => "SUSTAIN",
            ShopOfferType.VitalityBoost => "META",
            ShopOfferType.OrbUpgrade => "POWER",
            ShopOfferType.FocusedUpgrade => "POWER",
            ShopOfferType.OrbUpgradeDiscount => "POWER",
            _ => "SHOP"
        };
    }

    public static string GetRarityLabel(ShopOfferRarity rarity)
    {
        return rarity switch
        {
            ShopOfferRarity.Common => "COMUN",
            ShopOfferRarity.Rare => "RARA",
            ShopOfferRarity.Epic => "EPICA",
            ShopOfferRarity.Legendary => "LEGENDARIA",
            _ => "SHOP"
        };
    }

    public static Color GetRarityAccentColor(ShopOfferRarity rarity)
    {
        return rarity switch
        {
            ShopOfferRarity.Common => new Color(0.73f, 0.79f, 0.82f, 1f),
            ShopOfferRarity.Rare => new Color(0.46f, 0.8f, 0.98f, 1f),
            ShopOfferRarity.Epic => new Color(0.8f, 0.58f, 0.99f, 1f),
            ShopOfferRarity.Legendary => new Color(1f, 0.83f, 0.42f, 1f),
            _ => Color.white
        };
    }

    private static string BuildOfferSubtitle(ShopOfferData offer, PlayerShopState state)
    {
        if (offer == null)
            return string.Empty;

        return offer.Type switch
        {
            ShopOfferType.Heal => $"Recupera {offer.PrimaryValue} HP.",
            ShopOfferType.OrbUpgrade => "Mejora un orbe aleatorio que pueda subir.",
            ShopOfferType.FocusedUpgrade => state != null && state.HasCurrentOrb
                ? $"Mejora tu orbe actual: {state.CurrentOrbName}."
                : "Mejora el orbe equipado.",
            ShopOfferType.OrbUpgradeDiscount => "Mejora un orbe por menos monedas.",
            ShopOfferType.RecoveryPack => $"Recupera {offer.PrimaryValue} HP y suma +1 HP max.",
            ShopOfferType.VitalityBoost => $"Aumenta HP maximo en {offer.PrimaryValue}.",
            _ => "Oferta especial."
        };
    }

    private static string BuildOfferDetail(ShopOfferData offer, PlayerShopState state)
    {
        if (offer == null)
            return string.Empty;

        string detail = offer.Type switch
        {
            ShopOfferType.Heal => $"Sustain directo para la run. Cura hasta {offer.PrimaryValue} HP.",
            ShopOfferType.OrbUpgrade => "Compra segura de poder. Sube de nivel un orbe disponible y mejora su dano por impacto.",
            ShopOfferType.FocusedUpgrade => state != null && state.HasCurrentOrb
                ? $"Sube de nivel {state.CurrentOrbName} de forma garantizada."
                : "Sube de nivel el orbe que lleves equipado.",
            ShopOfferType.OrbUpgradeDiscount => "Un upgrade economico para no quedarte atras en dano.",
            ShopOfferType.RecoveryPack => "Recuperacion premium: cura vida ahora y empuja tu tanque para el resto de la run.",
            ShopOfferType.VitalityBoost => "Escalado defensivo permanente para encuentros largos y bosses.",
            _ => "Oferta especial de la tienda."
        };

        string stockText = $"Stock: {Mathf.Max(0, offer.Stock)}";
        return $"{detail}\n{stockText}";
    }

    private static string BuildAvailabilitySummary(ShopOfferData offer, PlayerShopState state)
    {
        if (offer == null)
            return string.Empty;

        if (state == null)
            return "Disponible";

        return offer.Type switch
        {
            ShopOfferType.Heal => state.HasMissingHp
                ? $"Te faltan {Mathf.Max(0, state.MaxHp - state.CurrentHp)} HP."
                : "Disponible, pero ya estas al maximo.",
            ShopOfferType.OrbUpgrade => state.UpgradableOrbCount > 0
                ? $"{state.UpgradableOrbCount} orbes listos para mejorar."
                : "Sin orbes mejorables.",
            ShopOfferType.FocusedUpgrade => state.CurrentOrbCanUpgrade
                ? $"{state.CurrentOrbName} puede subir de nivel."
                : "Tu orbe actual no puede mejorar mas.",
            ShopOfferType.OrbUpgradeDiscount => state.UpgradableOrbCount > 0
                ? "Buen momento para comprar power barato."
                : "Esperando un orbe mejorable.",
            ShopOfferType.RecoveryPack => "Compra flexible para aguantar mas nodos.",
            ShopOfferType.VitalityBoost => "Escalado permanente de vida.",
            _ => "Disponible"
        };
    }

    private static Color GetOfferCardColor(ShopOfferRarity rarity, bool isEnabled)
    {
        Color baseColor = rarity switch
        {
            ShopOfferRarity.Common => new Color(0.11f, 0.16f, 0.2f, 0.96f),
            ShopOfferRarity.Rare => new Color(0.08f, 0.18f, 0.24f, 0.96f),
            ShopOfferRarity.Epic => new Color(0.18f, 0.1f, 0.26f, 0.96f),
            ShopOfferRarity.Legendary => new Color(0.24f, 0.19f, 0.08f, 0.96f),
            _ => new Color(0.12f, 0.14f, 0.16f, 0.96f)
        };

        return isEnabled ? baseColor : Color.Lerp(baseColor, new Color(0.09f, 0.09f, 0.1f, 0.86f), 0.45f);
    }

    private static Color GetOfferBadgeColor(ShopOfferRarity rarity)
    {
        Color accent = GetRarityAccentColor(rarity);
        return new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f, 0.95f);
    }

    private static int ResolveDefaultPrimaryValue(ShopOfferType type, int fallbackHealAmount)
    {
        switch (type)
        {
            case ShopOfferType.Heal:
                return Mathf.Max(1, fallbackHealAmount);
            case ShopOfferType.RecoveryPack:
                return 8;
            case ShopOfferType.VitalityBoost:
                return 4;
            case ShopOfferType.FocusedUpgrade:
                return 1;
            default:
                return 1;
        }
    }

    private static List<OrbInstance> GetUpgradableOrbs(IReadOnlyList<OrbInstance> ownedOrbs)
    {
        var upgradableOrbs = new List<OrbInstance>();
        if (ownedOrbs == null)
            return upgradableOrbs;

        for (int i = 0; i < ownedOrbs.Count; i++)
        {
            OrbInstance orb = ownedOrbs[i];
            if (orb != null && orb.CanLevelUp)
                upgradableOrbs.Add(orb);
        }

        return upgradableOrbs;
    }
}
