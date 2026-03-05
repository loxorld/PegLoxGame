using UnityEngine;
using NUnit.Framework;
using UnityEngine.SceneManagement;

public class FlowSceneCoordinatorTests
{
    [Test]
    public void ContinueRunFromMenu_ReturnsFalse_WhenNoSavedNode()
    {
        bool loaded = false;
        FlowSceneCoordinator coordinator = new FlowSceneCoordinator(
            () => false,
            () => "MainMenu",
            (_, __) => loaded = true,
            () => BuildCatalog("MapScene"),
            _ => { });

        bool result = coordinator.ContinueRunFromMenu(null);

        Assert.IsFalse(result);
        Assert.IsFalse(loaded);
    }

    [Test]
    public void RestartRunFromMenu_DelegatesResetAndLoadsMap()
    {
        bool resetRun = false;
        bool resetManagers = false;
        GameState? state = null;
        string loadedScene = null;

        FlowSceneCoordinator coordinator = new FlowSceneCoordinator(
            () => false,
            () => "MainMenu",
            (scene, _) => loadedScene = scene,
            () => BuildCatalog("MapScene"),
            _ => { });

        coordinator.RestartRunFromMenu(() => resetRun = true, () => resetManagers = true, s => state = s);

        Assert.IsTrue(resetRun);
        Assert.IsTrue(resetManagers);
        Assert.AreEqual(GameState.Combat, state);
        Assert.AreEqual("MapScene", loadedScene);
    }

    [Test]
    public void TryInitializeMapForCurrentState_StartsMapWhenInNavigation()
    {
        bool started = false;
        FlowSceneCoordinator coordinator = new FlowSceneCoordinator(
            () =>
            {
                started = true;
                return true;
            },
            () => "MapScene",
            (_, __) => { },
            () => BuildCatalog("MapScene"),
            _ => { });

        coordinator.TryInitializeMapForCurrentState(GameState.MapNavigation);

        Assert.IsTrue(started);
    }

    private static SceneCatalog BuildCatalog(string mapScene)
    {
        SceneCatalog catalog = ScriptableObject.CreateInstance<SceneCatalog>();
        var field = typeof(SceneCatalog).GetField("mapScene", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.SetValue(catalog, mapScene);
        return catalog;
    }
}
