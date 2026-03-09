using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class MapManager
{
    private void StartCombatEncounter()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontro GameFlowManager en la escena.");
            return;
        }

        flow.SetState(GameState.Combat);
        SceneManager.LoadScene(SceneCatalog.Load().CombatScene, LoadSceneMode.Single);
    }

    private void HandleEventNode()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontro GameFlowManager en la escena.");
            return;
        }

        int balanceStageIndex = GetStageIndexForBalance(flow);
        MapDomainService.EventScenarioOutcome eventOutcome = domainService.BuildEventOutcome(
            currentNode,
            ResolveBalanceConfig(),
            balanceStageIndex,
            eventCoinsRewardMin,
            eventCoinsRewardMax,
            eventCoinsPenaltyMin,
            eventCoinsPenaltyMax,
            eventHealMin,
            eventHealMax,
            eventDamageMin,
            eventDamageMax);

        EnsurePresentationController();
        presentationController?.ShowEvent(
            eventOutcome,
            BuildEventOptionContext(flow),
            option =>
            {
                MapDomainService.EventOptionAvailability availability = domainService.EvaluateEventOptionAvailability(option, BuildEventOptionContext(flow));
                if (!availability.IsAvailable)
                {
                    presentationController?.ShowGenericResult(
                        eventOutcome.Title,
                        availability.MissingRequirementText,
                        () => OpenNode(currentNode));
                    return;
                }

                float roll = option.Probability.HasValue ? ResolveEventRngService().Roll01() : 0f;
                MapDomainService.EventResolutionOutcome resolvedOutcome = domainService.ResolveEventOptionOutcome(option, roll);
                string appliedOutcome = ResolveAppliedOutcomeLabel(option, roll);
                int runCounter = flow.IncrementEventOptionCounter(currentMapStage, currentNode, option.OptionLabel, resolvedOutcome);

                flow.AddCoins(resolvedOutcome.CoinDelta);
                flow.ModifySavedHP(resolvedOutcome.HpDelta);
                flow.SaveRun();

                LogEventResolution(currentMapStage, currentNode, option, roll, appliedOutcome, resolvedOutcome, runCounter);

                presentationController?.ShowGenericResult(
                    eventOutcome.Title,
                    resolvedOutcome.ResultDescription,
                    () => OpenNode(currentNode));
            },
            () =>
            {
                flow.SaveRun();
                OpenNode(currentNode);
            });
    }

    private IEventRngService ResolveEventRngService()
    {
        eventRngService ??= new UnityEventRngService();
        return eventRngService;
    }

    private static string ResolveAppliedOutcomeLabel(MapDomainService.EventOptionOutcome option, float roll)
    {
        if (!option.Probability.HasValue)
            return "deterministic";

        return roll <= option.Probability.Value ? "success" : "failure";
    }

    private static void LogEventResolution(
        MapStage stage,
        MapNodeData node,
        MapDomainService.EventOptionOutcome option,
        float roll,
        string appliedOutcome,
        MapDomainService.EventResolutionOutcome resolvedOutcome,
        int runCounter)
    {
        var payload = new LogEventResolutionPayload
        {
            stage = stage != null ? stage.name : "unknown-stage",
            node = node != null ? node.name : "unknown-node",
            option = option.OptionLabel,
            probability = option.Probability.HasValue ? option.Probability.Value : 1f,
            roll = option.Probability.HasValue ? roll : -1f,
            appliedOutcome = appliedOutcome,
            coinDelta = resolvedOutcome.CoinDelta,
            hpDelta = resolvedOutcome.HpDelta,
            resultDescription = resolvedOutcome.ResultDescription,
            runCounter = runCounter
        };

        Debug.Log($"[MapEventResolution] {JsonUtility.ToJson(payload)}");
    }

    [Serializable]
    private sealed class LogEventResolutionPayload
    {
        public string stage;
        public string node;
        public string option;
        public float probability;
        public float roll;
        public string appliedOutcome;
        public int coinDelta;
        public int hpDelta;
        public string resultDescription;
        public int runCounter;
    }

    private MapDomainService.EventOptionContext BuildEventOptionContext(GameFlowManager flow)
    {
        int currentHp = flow != null
            ? (flow.HasSavedPlayerHP ? flow.SavedPlayerHP : flow.PlayerMaxHP)
            : 0;

        var relicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RelicManager activeRelicManager = ResolveRelicManager();
        if (activeRelicManager != null)
        {
            IReadOnlyList<ShotEffectBase> activeRelics = activeRelicManager.ActiveRelics;
            for (int i = 0; i < activeRelics.Count; i++)
            {
                ShotEffectBase relic = activeRelics[i];
                if (relic == null || string.IsNullOrWhiteSpace(relic.name))
                    continue;

                relicIds.Add(relic.name);
            }
        }

        int currentCoins = flow != null ? flow.Coins : 0;
        return new MapDomainService.EventOptionContext(currentCoins, currentHp, relicIds);
    }

    private void HandleShopNode()
    {
        ShowShopScreen();
    }

    private void ShowShopScreen()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontro GameFlowManager en la escena.");
            return;
        }

        ShopService resolvedShopService = ResolveShopService();
        if (resolvedShopService == null)
            return;

        OrbManager orbManager = ResolveOrbManagerForShop();
        if (orbManager == null)
            return;

        int balanceStageIndex = GetStageIndexForBalance(flow);

        MapDomainService.ShopOutcome shopOutcome = domainService.BuildShopOutcome(
            currentNode,
            ResolveBalanceConfig(),
            balanceStageIndex,
            flow.Coins,
            shopHealCost,
            shopHealAmount,
            shopOrbUpgradeCost,
            null);

        string shopId = BuildShopId(currentNode, flow, balanceStageIndex);

        EnsurePresentationController();
        presentationController?.ShowShop(new ShopScene.OpenParams
        {
            ShopOutcome = shopOutcome,
            Config = shopConfig,
            Service = resolvedShopService,
            Flow = flow,
            OrbManager = orbManager,
            Balance = ResolveBalanceConfig(),
            StageIndex = balanceStageIndex,
            ShopId = shopId,
            OnShopMessage = ShowNonDestructiveShopFeedback,
            OnExit = () => OpenNode(currentNode)
        });
    }

    private void ShowNonDestructiveShopFeedback(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Debug.Log($"[MapManager][Shop] {message}");

        if (MapNodeModalUI.Instance == null)
            return;

        MapNodeModalUI.Show("Tienda", message, new MapNodeModalUI.Option("Cerrar", null, true));
    }

    private static string BuildShopId(MapNodeData node, GameFlowManager flow, int stageIndex)
    {
        string nodeId = node != null && !string.IsNullOrWhiteSpace(node.name) ? node.name : "unknown-node";
        int nodeVisitIndex = flow != null ? flow.NodesVisited : 0;
        return $"shop_{stageIndex}_{nodeVisitIndex}_{nodeId}";
    }

    private void HandleBossNode()
    {
        GameFlowManager flow = ResolveGameFlowManager();
        if (flow == null)
        {
            Debug.LogWarning("[MapManager] No se encontro GameFlowManager en la escena.");
            return;
        }

        if (currentNode != null)
        {
            flow.SetBossEncounter(
                currentNode.bossEnemy,
                currentNode.bossHpMultiplier,
                currentNode.bossDamageMultiplier,
                currentNode.bossHpBonus,
                currentNode.bossDamageBonus);
        }

        flow.SaveMapNode(null);
        flow.SetState(GameState.Combat);
        SceneManager.LoadScene(SceneCatalog.Load().CombatScene, LoadSceneMode.Single);
    }
}
