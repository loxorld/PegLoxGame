using System.Collections.Generic;
using UnityEngine;

public class ShopService
{
    public sealed class ShopOptionData
    {
        public ShopOptionData(string label, bool isEnabled, System.Action onSelect)
        {
            Label = label;
            IsEnabled = isEnabled;
            OnSelect = onSelect;
        }

        public string Label { get; }
        public bool IsEnabled { get; }
        public System.Action OnSelect { get; }
    }

    public bool TryHeal(GameFlowManager flow, int healCost, int healAmount, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontr GameFlowManager en la escena.";
            return false;
        }

        if (!flow.SpendCoins(healCost))
        {
            message = "No alcanzan las monedas para curar.";
            return false;
        }

        flow.ModifySavedHP(healAmount);
        message = $"Te curaste +{healAmount} HP.";
        return true;
    }

    public bool TryUpgradeOrb(GameFlowManager flow, OrbManager orbManager, int upgradeCost, out string message)
    {
        message = null;
        if (flow == null)
        {
            message = "No se encontr GameFlowManager en la escena.";
            return false;
        }

        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        if (ownedOrbs == null || ownedOrbs.Count == 0)
        {
            message = "No hay orbes para mejorar.";
            return false;
        }

        var upgradableOrbs = GetUpgradableOrbs(ownedOrbs);
        if (upgradableOrbs.Count == 0)
        {
            message = "Todos los orbes ya estn al mximo.";
            return false;
        }

        if (!flow.SpendCoins(upgradeCost))
        {
            message = "No alcanzan las monedas para mejorar un orbe.";
            return false;
        }

        int randomIndex = Random.Range(0, upgradableOrbs.Count);
        OrbInstance chosenOrb = upgradableOrbs[randomIndex];
        int previousLevel = chosenOrb.Level;
        chosenOrb.LevelUp();

        message = chosenOrb.Level > previousLevel
            ? $"Mejoraste {chosenOrb.OrbName} a nivel {chosenOrb.Level}."
            : $"{chosenOrb.OrbName} ya est al mximo.";

        return true;
    }

    public List<ShopOptionData> GetShopOptions(
        GameFlowManager flow,
        OrbManager orbManager,
        int healCost,
        int healAmount,
        int upgradeCost,
        System.Action<string> refreshAction,
        System.Action exitAction)
    {
        var options = new List<ShopOptionData>();
        int missingHealCoins = Mathf.Max(0, healCost - (flow != null ? flow.Coins : 0));
        bool canAffordHeal = flow != null && flow.Coins >= healCost;
        if (canAffordHeal)
        {
            options.Add(new ShopOptionData(
                $"Curar +{healAmount} HP ({healCost} monedas)",
                true,
                () =>
                {
                    TryHeal(flow, healCost, healAmount, out string result);
                    refreshAction?.Invoke(result);
                }));
        }
        else
        {
            options.Add(new ShopOptionData(
                $"Curar +{healAmount} HP (faltan {missingHealCoins} monedas)",
                false,
                () => refreshAction?.Invoke("No alcanzan las monedas para curar.")));
        }

        IReadOnlyList<OrbInstance> ownedOrbs = orbManager != null ? orbManager.OwnedOrbInstances : null;
        bool hasOrbs = ownedOrbs != null && ownedOrbs.Count > 0;
        List<OrbInstance> upgradableOrbs = hasOrbs ? GetUpgradableOrbs(ownedOrbs) : new List<OrbInstance>();

        if (!hasOrbs)
        {
            options.Add(new ShopOptionData(
                "Mejora de Orbe (sin orbes disponibles)",
                false,
                () => refreshAction?.Invoke("No hay orbes para mejorar.")));
        }
        else if (upgradableOrbs.Count == 0)
        {
            options.Add(new ShopOptionData(
                "Mejora de Orbe (orbes al mximo)",
                false,
                () => refreshAction?.Invoke("Todos los orbes ya estn al mximo.")));
        }
        else
        {
            bool canAffordUpgrade = flow != null && flow.Coins >= upgradeCost;
            int missingUpgradeCoins = Mathf.Max(0, upgradeCost - (flow != null ? flow.Coins : 0));
            if (canAffordUpgrade)
            {
                options.Add(new ShopOptionData(
                    $"Mejora de Orbe (+1 nivel, {upgradeCost} monedas)",
                    true,
                    () =>
                    {
                        TryUpgradeOrb(flow, orbManager, upgradeCost, out string result);
                        refreshAction?.Invoke(result);
                    }));
            }
            else
            {
                options.Add(new ShopOptionData(
                    $"Mejora de Orbe (+1 nivel, faltan {missingUpgradeCoins} monedas)",
                    false,
                    () => refreshAction?.Invoke("No alcanzan las monedas para mejorar un orbe.")));
            }
        }

        options.Add(new ShopOptionData("Salir", true, exitAction));
        return options;
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