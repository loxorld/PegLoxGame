using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameBootstrap : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private GameFlowManager gameFlowManager;
    [SerializeField] private MapManager mapManager;
    [SerializeField] private OrbManager orbManager;
    [SerializeField] private RelicManager relicManager;

    [Header("Gameplay Services")]
    [SerializeField] private MonoBehaviour mapNodeModalView;
    [SerializeField] private bool autoCreateShopService = true;

    private ShopService shopService;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        shopService = autoCreateShopService ? new ShopService() : null;
        RegisterAndInject();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RegisterAndInject();
    }

    private void RegisterAndInject()
    {
        RegisterCoreServices();
        InjectCoreDependencies();
    }

    private void RegisterCoreServices()
    {
        gameFlowManager = ResolveSceneComponent(gameFlowManager, () => GameFlowManager.Instance ?? ServiceRegistry.LegacyFind<GameFlowManager>(true));
        mapManager = ResolveSceneComponent(mapManager, () => ServiceRegistry.LegacyFind<MapManager>(true));
        orbManager = ResolveSceneComponent(orbManager, () => OrbManager.Instance ?? ServiceRegistry.LegacyFind<OrbManager>(true));
        relicManager = ResolveSceneComponent(relicManager, () => RelicManager.Instance ?? ServiceRegistry.LegacyFind<RelicManager>(true));
        mapNodeModalView = ResolveMapNodeModalView();

        ServiceRegistry.Register(gameFlowManager);
        ServiceRegistry.Register(mapManager);
        ServiceRegistry.Register(orbManager);
        ServiceRegistry.Register(relicManager);

        if (shopService != null)
            ServiceRegistry.Register(shopService);

        if (mapNodeModalView is IMapNodeModalView modalView)
            ServiceRegistry.Register<IMapNodeModalView>(modalView);
    }

    private void InjectCoreDependencies()
    {
        if (gameFlowManager != null)
            gameFlowManager.InjectDependencies(mapManager, orbManager, relicManager);

        if (mapManager != null)
            mapManager.InjectDependencies(gameFlowManager, shopService, mapNodeModalView as IMapNodeModalView);
    }

    private static T ResolveSceneComponent<T>(T current, Func<T> resolver) where T : Component
    {
        if (IsSceneObject(current))
            return current;

        T resolved = resolver != null ? resolver.Invoke() : null;
        return IsSceneObject(resolved) ? resolved : null;
    }

    private MonoBehaviour ResolveMapNodeModalView()
    {
        if (mapNodeModalView != null && IsSceneObject(mapNodeModalView) && mapNodeModalView is IMapNodeModalView)
            return mapNodeModalView;

        if (ServiceRegistry.TryResolve(out IMapNodeModalView registeredModalView) && registeredModalView is MonoBehaviour registeredMono && IsSceneObject(registeredMono))
            return registeredMono;

        MonoBehaviour[] candidates = ServiceRegistry.LegacyFindAll<MonoBehaviour>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            MonoBehaviour candidate = candidates[i];
            if (!IsSceneObject(candidate))
                continue;

            if (candidate is IMapNodeModalView)
                return candidate;
        }

        return null;
    }

    private static bool IsSceneObject(Component component)
    {
        return component != null && component.gameObject.scene.IsValid();
    }
}