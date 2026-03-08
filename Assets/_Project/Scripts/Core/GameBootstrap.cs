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
/// - mapManager: referencia obligatoria en escenas de mapa/combat.
/// - orbManager: referencia obligatoria en escenas de mapa/combat.
/// - relicManager: referencia obligatoria en escenas de mapa/combat.
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
    [SerializeField] private string sceneWiringDocumentation =
        "Required refs: gameFlowManager, mapManager, orbManager, relicManager. Optional: mapNodeModalView (IMapNodeModalView). New scenes should keep allowLegacyFallback=false.";

    private ShopService shopService;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        shopService = autoCreateShopService ? new ShopService() : null;
        ServiceRegistry.ConfigureLegacyFallback(allowLegacyFallback);

        RegisterAndInject();
        ValidateCriticalServicesOrFail();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnValidate()
    {
        if (mapNodeModalView != null && !(mapNodeModalView is IMapNodeModalView))
            Debug.LogError("[GameBootstrap] mapNodeModalView debe implementar IMapNodeModalView.", this);

        ValidateRequiredReferences(false);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
        bool valid = true;
        valid &= ValidateReference(gameFlowManager, nameof(gameFlowManager), logErrors);
        valid &= ValidateReference(mapManager, nameof(mapManager), logErrors);
        valid &= ValidateReference(orbManager, nameof(orbManager), logErrors);
        valid &= ValidateReference(relicManager, nameof(relicManager), logErrors);
        return valid;
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
