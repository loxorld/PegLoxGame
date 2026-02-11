using UnityEngine;
using UnityEngine.SceneManagement;

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
        gameFlowManager = gameFlowManager != null ? gameFlowManager : GameFlowManager.Instance;
        mapManager = mapManager != null ? mapManager : ServiceRegistry.ResolveWithFallback(nameof(GameBootstrap), nameof(mapManager), () => ServiceRegistry.LegacyFind<MapManager>(true));
        orbManager = orbManager != null ? orbManager : OrbManager.Instance;
        relicManager = relicManager != null ? relicManager : RelicManager.Instance;

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
}