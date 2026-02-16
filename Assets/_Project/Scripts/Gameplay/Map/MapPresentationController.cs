using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapPresentationController : MonoBehaviour
{
    [SerializeField] private MonoBehaviour mapNodeModalView;
    [SerializeField] private MonoBehaviour mapShopView;
    [SerializeField] private bool loadShopSceneAdditively = true;
    [SerializeField] private string shopSceneName = "ShopScene";

    private readonly MapDomainService domainService = new MapDomainService();

    public void InjectModalView(IMapNodeModalView injectedMapNodeModalView)
    {
        if (injectedMapNodeModalView != null)
            mapNodeModalView = injectedMapNodeModalView as MonoBehaviour;
    }

    public void OpenNode(MapNodeData node)
    {
        if (node == null)
        {
            Debug.LogWarning("[MapPresentationController] OpenNode llamado con MapNodeData nulo.");
            return;
        }

        if (MapNavigationUI.Instance == null)
        {
            Debug.LogWarning("[MapPresentationController] MapNavigationUI.Instance es nulo. Reintentando...");
            StartCoroutine(WaitForMapUIAndShow(node));
            return;
        }

        MapNavigationUI.Instance.ShowNode(node);
    }

    public void ShowEvent(
         MapDomainService.EventScenarioOutcome eventOutcome,
         MapDomainService.EventOptionContext optionContext,
         Action<MapDomainService.EventOptionOutcome> onSelectOption,
         Action onContinue)
    {
        IMapNodeModalView modalView = ResolveMapNodeModalView();
        if (modalView == null)
        {
            Debug.LogWarning("[MapPresentationController] No se encontró IMapNodeModalView en la escena.");
            return;
        }

        var options = new List<MapNodeModalOption>();
        IReadOnlyList<MapDomainService.EventOptionOutcome> eventOptions = eventOutcome.Options;
        if (eventOptions != null)
        {
            for (int i = 0; i < eventOptions.Count; i++)
            {
                MapDomainService.EventOptionOutcome option = eventOptions[i];
                if (string.IsNullOrWhiteSpace(option.OptionLabel))
                    continue;

                string label = option.OptionLabel;
                if (option.Probability.HasValue)
                    label = $"{label} ({Mathf.RoundToInt(option.Probability.Value * 100f)}%)";

                MapDomainService.EventOptionAvailability availability = domainService.EvaluateEventOptionAvailability(option, optionContext);
                if (!availability.IsAvailable)
                    label = $"{label}\n<color=#FF8A8A>{availability.MissingRequirementText}</color>";

                options.Add(new MapNodeModalOption(label, () => onSelectOption?.Invoke(option), availability.IsAvailable));
            }
        }

        if (options.Count == 0)
            options.Add(new MapNodeModalOption("Continuar", onContinue, true));

        modalView.ShowEvent(eventOutcome.Title, eventOutcome.Description, options);
    }

    public void ShowShop(ShopScene.OpenParams openParams)
    {
        StartCoroutine(ShowShopRoutine(openParams));
    }

    private IEnumerator ShowShopRoutine(ShopScene.OpenParams openParams)
    {
        if (loadShopSceneAdditively && !IsShopSceneLoaded())
            yield return EnsureShopSceneLoaded();

        IMapShopView shopView = ResolveMapShopView();

        if (shopView == null)
        {
            Debug.LogWarning("[MapPresentationController] No se encontró IMapShopView en la escena.");
            yield break;

        }

        if (openParams != null && loadShopSceneAdditively && IsShopSceneLoaded())
            openParams = WrapOpenParamsWithShopSceneUnload(openParams);

        shopView.ShowShop(openParams);
    }

    public void ShowGenericResult(string title, string description, Action onContinue)
    {
        IMapNodeModalView modalView = ResolveMapNodeModalView();
        if (modalView == null)
        {
            Debug.LogWarning("[MapPresentationController] No se encontró IMapNodeModalView en la escena.");
            return;
        }

        var options = new List<MapNodeModalOption>
        {
            new MapNodeModalOption("Continuar", onContinue, true)
        };

        modalView.ShowGeneric(title, description, options);
    }

    private IEnumerator WaitForMapUIAndShow(MapNodeData node)
    {
        while (MapNavigationUI.Instance == null)
            yield return null;

        MapNavigationUI.Instance.ShowNode(node);
    }

    private IMapNodeModalView ResolveMapNodeModalView()
    {
        bool isMigratedMapScene = IsMigratedMapSceneActive();

        if (mapNodeModalView != null && mapNodeModalView is IMapNodeModalView view)
            return view;

        IMapNodeModalView registryView = ServiceRegistry.Resolve<IMapNodeModalView>();
        if (registryView != null)
        {
            mapNodeModalView = registryView as MonoBehaviour;
            return registryView;
        }

        ServiceRegistry.LogFallback(nameof(MapPresentationController), nameof(mapNodeModalView), "missing-injected-reference");

        if (isMigratedMapScene)
            ServiceRegistry.LogFallbackMetric(nameof(MapPresentationController), nameof(mapNodeModalView), "strict-missing-reference");

        MonoBehaviour[] behaviours = ServiceRegistry.LegacyFindAll<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMapNodeModalView candidate)
            {
                mapNodeModalView = behaviours[i];
                ServiceRegistry.Register(candidate);
                string source = isMigratedMapScene ? "strict-recovered-findobjectsoftype" : "findobjectsoftype";
                ServiceRegistry.LogFallbackMetric(nameof(MapPresentationController), nameof(mapNodeModalView), source);
                return candidate;
            }
        }

        MapNodeModalUI modalUI = MapNodeModalUI.GetOrCreate();
        if (modalUI != null)
        {
            mapNodeModalView = modalUI;
            ServiceRegistry.Register<IMapNodeModalView>(modalUI);
            string source = isMigratedMapScene ? "strict-recovered-mapnodemodalui-getorcreate" : "mapnodemodalui-getorcreate";
            ServiceRegistry.LogFallbackMetric(nameof(MapPresentationController), nameof(mapNodeModalView), source);
            return modalUI;
        }

        if (isMigratedMapScene)
            Debug.LogError("[MapPresentationController] DI estricto: falta IMapNodeModalView en escena migrada. Revisa el cableado de dependencias.");

        return null;
    }

    private IMapShopView ResolveMapShopView()
    {
        if (mapShopView != null && mapShopView is IMapShopView view)
            return view;

        IMapShopView registryView = ServiceRegistry.Resolve<IMapShopView>();
        if (registryView != null)
        {
            mapShopView = registryView as MonoBehaviour;
            return registryView;
        }

        MonoBehaviour[] behaviours = ServiceRegistry.LegacyFindAll<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMapShopView candidate)
            {
                mapShopView = behaviours[i];
                ServiceRegistry.Register(candidate);
                ServiceRegistry.LogFallbackMetric(nameof(MapPresentationController), nameof(mapShopView), "findobjectsoftype");
                return candidate;
            }
        }

        if (loadShopSceneAdditively && !IsShopSceneLoaded())
            return null;

        ShopScene scene = ShopScene.GetOrCreate();
        if (scene != null)
        {
            mapShopView = scene;
            ServiceRegistry.Register<IMapShopView>(scene);
            ServiceRegistry.LogFallbackMetric(nameof(MapPresentationController), nameof(mapShopView), "shopscene-getorcreate");
            return scene;
        }

        ServiceRegistry.LogFallback(nameof(MapPresentationController), nameof(mapShopView), "missing-injected-reference");
        return null;
    }

    private IEnumerator EnsureShopSceneLoaded()
    {
        if (IsShopSceneLoaded())
            yield break;

        if (string.IsNullOrWhiteSpace(shopSceneName))
            yield break;

        AsyncOperation loadOp = SceneManager.LoadSceneAsync(shopSceneName, LoadSceneMode.Additive);
        if (loadOp == null)
        {
            Debug.LogWarning($"[MapPresentationController] No se pudo cargar la escena de shop '{shopSceneName}' en modo aditivo.");
            yield break;
        }

        while (!loadOp.isDone)
            yield return null;
    }

    private ShopScene.OpenParams WrapOpenParamsWithShopSceneUnload(ShopScene.OpenParams openParams)
    {
        Action originalExit = openParams.OnExit;

        return new ShopScene.OpenParams
        {
            ShopOutcome = openParams.ShopOutcome,
            Config = openParams.Config,
            Service = openParams.Service,
            Flow = openParams.Flow,
            OrbManager = openParams.OrbManager,
            Balance = openParams.Balance,
            StageIndex = openParams.StageIndex,
            ShopId = openParams.ShopId,
            OnShopMessage = openParams.OnShopMessage,
            OnRequestReopen = openParams.OnRequestReopen,
            OnExit = () => StartCoroutine(CloseShopSceneAndContinue(originalExit))
        };
    }

    private IEnumerator CloseShopSceneAndContinue(Action onExit)
    {
        if (IsShopSceneLoaded())
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(shopSceneName);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone)
                    yield return null;
            }
        }

        mapShopView = null;
        onExit?.Invoke();
    }

    private bool IsShopSceneLoaded()
    {
        if (string.IsNullOrWhiteSpace(shopSceneName))
            return false;

        Scene scene = SceneManager.GetSceneByName(shopSceneName);
        return scene.IsValid() && scene.isLoaded;
    }

    private static bool IsMigratedMapSceneActive()
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal);
    }

}