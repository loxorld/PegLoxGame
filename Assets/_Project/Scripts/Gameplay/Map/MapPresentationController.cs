using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapPresentationController : MonoBehaviour
{
    [SerializeField] private MonoBehaviour mapNodeModalView;

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

    public void ShowEvent(MapDomainService.EventOutcome eventOutcome, Action onContinue)
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

        modalView.ShowEvent(eventOutcome.Title, eventOutcome.Description, options);
    }

    public void ShowShopModal(MapDomainService.ShopOutcome shopOutcome, IReadOnlyList<ShopService.ShopOptionData> shopOptions)
    {
        IMapNodeModalView modalView = ResolveMapNodeModalView();
        if (modalView == null)
        {
            Debug.LogWarning("[MapPresentationController] No se encontró IMapNodeModalView en la escena.");
            return;
        }

        var options = new List<MapNodeModalOption>();
        for (int i = 0; i < shopOptions.Count; i++)
        {
            ShopService.ShopOptionData option = shopOptions[i];
            options.Add(new MapNodeModalOption(option.Label, option.OnSelect, option.IsEnabled));
        }

        modalView.ShowShop(shopOutcome.Title, shopOutcome.Description, options);
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

    private static bool IsMigratedMapSceneActive()
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string activeSceneName = SceneManager.GetActiveScene().name;
        return string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal);
    }

}