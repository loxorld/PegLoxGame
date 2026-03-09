using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class MapManager
{
    private void EnsurePresentationController()
    {
        if (presentationController == null)
            presentationController = GetComponent<MapPresentationController>();

        if (presentationController == null)
            presentationController = gameObject.AddComponent<MapPresentationController>();

        if (mapNodeModalView is IMapNodeModalView modalView)
            presentationController.InjectModalView(modalView);
    }

    private GameFlowManager ResolveGameFlowManager()
    {
        if (gameFlowManager != null)
            return gameFlowManager;
        if (ServiceRegistry.TryResolve(out gameFlowManager))
            return gameFlowManager;

        ServiceRegistry.LogFallback(nameof(MapManager), nameof(gameFlowManager), "missing-injected-reference");

        gameFlowManager = GameFlowManager.Instance;
        if (gameFlowManager != null)
        {
            ServiceRegistry.Register(gameFlowManager);
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(gameFlowManager), "gameflow-instance");
            return gameFlowManager;
        }

        if (IsMigratedMapSceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(gameFlowManager), "strict-missing-reference");
            Debug.LogError("[MapManager] DI estricto: falta GameFlowManager en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        gameFlowManager = GameFlowManager.Instance;
        if (gameFlowManager != null)
        {
            ServiceRegistry.Register(gameFlowManager);
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(gameFlowManager), "gameflow-instance");
            return gameFlowManager;
        }

        Debug.LogError("[MapManager] Falta GameFlowManager. Configura la referencia en GameBootstrap.");
        return null;
    }

    private ShopService ResolveShopService()
    {
        if (shopService != null)
            return shopService;

        if (ServiceRegistry.TryResolve(out shopService))
            return shopService;

        ServiceRegistry.LogFallback(nameof(MapManager), nameof(shopService), "missing-injected-reference");

        if (IsMigratedMapSceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(shopService), "strict-missing-reference");
            Debug.LogError("[MapManager] DI estricto: falta ShopService en escena migrada. Revisa el cableado de dependencias.");
            return null;
        }

        shopService = new ShopService();
        ServiceRegistry.Register(shopService);
        ServiceRegistry.LogFallbackMetric(nameof(MapManager), nameof(shopService), "in-process-default");
        return shopService;
    }

    private OrbManager ResolveOrbManagerForShop()
    {
        if (orbManager != null)
            return orbManager;

        if (ServiceRegistry.TryResolve(out OrbManager registeredOrbManager))
            return registeredOrbManager;

        ServiceRegistry.LogFallback(nameof(MapManager), "OrbManagerForShop", "missing-injected-reference");

        if (IsMigratedMapSceneActive())
        {
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), "OrbManagerForShop", "strict-missing-reference");
            Debug.LogError("[MapManager] DI estricto: falta OrbManager en escena migrada para tienda. Revisa el cableado de dependencias.");
            return null;
        }

        orbManager = OrbManager.Instance;
        if (orbManager != null)
        {
            ServiceRegistry.Register(orbManager);
            ServiceRegistry.LogFallbackMetric(nameof(MapManager), "OrbManagerForShop", "orbmanager-instance");
            return orbManager;
        }

        Debug.LogError("[MapManager] Falta OrbManager para tienda. Configura la referencia en GameBootstrap.");
        return null;
    }

    private RelicManager ResolveRelicManager()
    {
        if (relicManager != null)
            return relicManager;

        if (ServiceRegistry.TryResolve(out relicManager))
            return relicManager;

        relicManager = RelicManager.Instance;
        if (relicManager != null)
            ServiceRegistry.Register(relicManager);

        return relicManager;
    }

    private static bool IsMigratedMapSceneActive()
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal);
    }

    private MapStage ResolveStageByIndex(int stageIndex)
    {
        if (stageSequence != null && stageSequence.Length > 0)
        {
            int clamped = Mathf.Clamp(stageIndex, 0, stageSequence.Length - 1);
            MapStage selected = stageSequence[clamped];
            if (selected != null)
                return selected;
        }

        return currentMapStage;
    }

    private int GetStageIndexForBalance(GameFlowManager flow)
    {
        return flow != null ? Mathf.Max(0, flow.CurrentStageIndex) : 0;
    }

    private void ValidateStageConsistency(int expectedStageIndex, MapStage stage, GameFlowManager flow)
    {
        if (flow == null || stage == null)
            return;

        if (domainService.HasStageConsistencyIssue(stageSequence, stage, expectedStageIndex))
        {
            int resolvedIndex = domainService.GetStageIndex(stageSequence, stage);
            Debug.LogWarning($"[MapManager] Stage mismatch detected. FlowStage={expectedStageIndex}, MapStageIndex={resolvedIndex}, MapStage='{stage.stageName}'.");
        }
    }

    private RunBalanceConfig ResolveBalanceConfig()
    {
        if (balanceConfig == null)
            balanceConfig = RunBalanceConfig.LoadDefault();

        return balanceConfig;
    }
}
