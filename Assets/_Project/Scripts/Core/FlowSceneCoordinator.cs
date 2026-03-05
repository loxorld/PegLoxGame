using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FlowSceneCoordinator
{
    private readonly Func<bool> tryStartMapAction;
    private readonly Func<string> activeSceneNameProvider;
    private readonly Action<string, LoadSceneMode> sceneLoader;
    private readonly Func<SceneCatalog> sceneCatalogProvider;
    private readonly Action<float> timeScaleSetter;

    public FlowSceneCoordinator(
        Func<bool> tryStartMapAction,
        Func<string> activeSceneNameProvider,
        Action<string, LoadSceneMode> sceneLoader,
        Func<SceneCatalog> sceneCatalogProvider,
        Action<float> timeScaleSetter)
    {
        this.tryStartMapAction = tryStartMapAction;
        this.activeSceneNameProvider = activeSceneNameProvider;
        this.sceneLoader = sceneLoader;
        this.sceneCatalogProvider = sceneCatalogProvider;
        this.timeScaleSetter = timeScaleSetter;
    }

    public void TryInitializeMapForCurrentState(GameState state)
    {
        if (state != GameState.MapNavigation)
            return;

        if (tryStartMapAction != null && tryStartMapAction.Invoke())
            return;

        SceneCatalog catalog = sceneCatalogProvider?.Invoke();
        string activeSceneName = activeSceneNameProvider?.Invoke();
        if (catalog != null && string.Equals(activeSceneName, catalog.MapScene, StringComparison.Ordinal))
            Debug.LogError("[GameFlow] No se encontró MapManager en la escena de mapa.");
    }

    public bool ContinueRunFromMenu(MapNodeData savedMapNode)
    {
        if (savedMapNode == null)
            return false;

        timeScaleSetter?.Invoke(1f);
        SceneCatalog catalog = sceneCatalogProvider?.Invoke();
        if (catalog == null)
            return false;

        sceneLoader?.Invoke(catalog.MapScene, LoadSceneMode.Single);
        return true;
    }

    public void RestartRunFromMenu(Action resetRunStateAction, Action resetPersistentManagersAction, Action<GameState> setStateAction)
    {
        resetRunStateAction?.Invoke();
        resetPersistentManagersAction?.Invoke();
        setStateAction?.Invoke(GameState.Combat);
        timeScaleSetter?.Invoke(1f);

        SceneCatalog catalog = sceneCatalogProvider?.Invoke();
        if (catalog != null)
            sceneLoader?.Invoke(catalog.MapScene, LoadSceneMode.Single);
    }
}
