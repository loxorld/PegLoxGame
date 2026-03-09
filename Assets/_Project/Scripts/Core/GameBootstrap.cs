using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap centralizado del runtime.
///
/// Política DI única:
/// - Runtime: solo ServiceRegistry + inyección explícita.
/// - Sin descubrimiento implícito en escena para servicios críticos.
///
/// Wiring requerido por escena (ver también prefab GameBootstrap):
/// - gameFlowManager: referencia obligatoria.
/// - mapManager: referencia obligatoria solo en MapScene.
/// - orbManager: referencia obligatoria en MapScene/CombatScene.
/// - relicManager: referencia obligatoria en MapScene/CombatScene.
/// - mapNodeModalView: opcional, pero debe implementar IMapNodeModalView cuando se use modal de mapa.
/// </summary>
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

    [Header("Migration")]
    [SerializeField] private bool allowLegacyFallback;

    [Header("Scene Wiring Documentation")]
    [TextArea(4, 10)]
#pragma warning disable CS0414
    [SerializeField] private string sceneWiringDocumentation =
        "Required refs: gameFlowManager always. mapManager only in MapScene. orbManager/relicManager in MapScene and CombatScene. Optional: mapNodeModalView (IMapNodeModalView). New scenes should keep allowLegacyFallback=false.";
#pragma warning restore CS0414

    private ShopService shopService;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        shopService = autoCreateShopService ? new ShopService() : null;
        ServiceRegistry.ConfigureLegacyFallback(allowLegacyFallback);


        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void Start()
    {
        EnsureCoreReferencesForActiveScene(true);
        RegisterAndInject();
        ValidateCriticalServicesOrFail();
    }

    private void OnValidate()
    {
        if (mapNodeModalView != null && !(mapNodeModalView is IMapNodeModalView))
            Debug.LogError("[GameBootstrap] mapNodeModalView debe implementar IMapNodeModalView.", this);

        ValidateRequiredReferences(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureCoreReferencesForActiveScene(true);
        RegisterAndInject();
        ValidateCriticalServicesOrFail();
    }

    private void RegisterAndInject()
    {
        RegisterCoreServices();
        InjectCoreDependencies();
    }

    private void RegisterCoreServices()
    {
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
        gameFlowManager?.InjectDependencies(mapManager, orbManager, relicManager);

        if (mapManager != null)
            mapManager.InjectDependencies(gameFlowManager, shopService, mapNodeModalView as IMapNodeModalView, orbManager, relicManager);
    }

    private void ValidateCriticalServicesOrFail()
    {
        if (!ValidateRequiredReferences(true))
            throw new InvalidOperationException("[GameBootstrap] Faltan referencias críticas para DI. Corrige el wiring de la escena/prefab.");
    }

    private bool ValidateRequiredReferences(bool logErrors)
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        bool isMapScene = string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal);
        bool isCombatScene = string.Equals(activeSceneName, catalog.CombatScene, StringComparison.Ordinal);

        bool valid = true;
        valid &= ValidateReference(gameFlowManager, nameof(gameFlowManager), logErrors);

        if (isMapScene)
            valid &= ValidateReference(mapManager, nameof(mapManager), logErrors);

        if (isMapScene || isCombatScene)
        {
            valid &= ValidateReference(orbManager, nameof(orbManager), logErrors);
            valid &= ValidateReference(relicManager, nameof(relicManager), logErrors);
        }

        return valid;
    }

    private void EnsureCoreReferencesForActiveScene(bool logWarnings)
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        bool isMapScene = string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal);
        bool isCombatScene = string.Equals(activeSceneName, catalog.CombatScene, StringComparison.Ordinal);

        gameFlowManager ??= GameFlowManager.Instance;
        gameFlowManager ??= ServiceRegistry.Resolve<GameFlowManager>();
        gameFlowManager ??= FindAnyObjectByType<GameFlowManager>(FindObjectsInactive.Include);

        if (isMapScene)
            mapManager ??= FindAnyObjectByType<MapManager>(FindObjectsInactive.Include);

        if (isMapScene || isCombatScene)
        {
            orbManager ??= OrbManager.Instance;
            orbManager ??= FindAnyObjectByType<OrbManager>(FindObjectsInactive.Include);

            relicManager ??= RelicManager.Instance;
            relicManager ??= FindAnyObjectByType<RelicManager>(FindObjectsInactive.Include);
        }

        if (isMapScene && mapNodeModalView == null)
        {
            IMapNodeModalView registryView = ServiceRegistry.Resolve<IMapNodeModalView>();
            if (registryView is MonoBehaviour viewBehaviour)
                mapNodeModalView = viewBehaviour;

            mapNodeModalView ??= FindMapNodeModalView();

            if (mapNodeModalView == null)
                mapNodeModalView = MapNodeModalUI.GetOrCreate();

            if (mapNodeModalView == null && logWarnings)
                Debug.LogWarning("[GameBootstrap] No se encontró IMapNodeModalView en MapScene. Se intentará resolver desde MapPresentationController.", this);
        }
    }

    private static MonoBehaviour FindMapNodeModalView()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is IMapNodeModalView)
                return behaviour;
        }

        return null;
    }

    private bool ValidateReference(UnityEngine.Object reference, string fieldName, bool logError)
    {
        if (reference != null)
            return true;

        if (logError)
            Debug.LogError($"[GameBootstrap] Falta referencia crítica: {fieldName}.", this);

        return false;
    }
}
