using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MapManagerDependencyTests
{
    [SetUp]
    public void SetUp()
    {
        CleanupSceneObjects();
        ServiceRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        CleanupSceneObjects();
        ServiceRegistry.Clear();
    }

    [Test]
    public void ResolveOrbManagerForShop_ReturnsInjectedOrbManager_WhenAvailable()
    {
        MapManager mapManager = new GameObject("MapManagerTest").AddComponent<MapManager>();
        OrbManager injectedOrbManager = new GameObject("InjectedOrbManager").AddComponent<OrbManager>();

        mapManager.InjectDependencies(null, new ShopService(), null, injectedOrbManager, null);

        OrbManager resolvedOrbManager = InvokeResolveOrbManagerForShop(mapManager);

        Assert.AreSame(injectedOrbManager, resolvedOrbManager);
    }

    [Test]
    public void ResolveOrbManagerForShop_UsesRegisteredOrbManager_WhenNoInjectionExists()
    {
        MapManager mapManager = new GameObject("MapManagerTest").AddComponent<MapManager>();
        OrbManager registeredOrbManager = new GameObject("RegisteredOrbManager").AddComponent<OrbManager>();
        ServiceRegistry.Register(registeredOrbManager);

        OrbManager resolvedOrbManager = InvokeResolveOrbManagerForShop(mapManager);

        Assert.AreSame(registeredOrbManager, resolvedOrbManager);
    }

    private static OrbManager InvokeResolveOrbManagerForShop(MapManager mapManager)
    {
        MethodInfo resolveMethod = typeof(MapManager).GetMethod(
            "ResolveOrbManagerForShop",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(resolveMethod);
        return resolveMethod.Invoke(mapManager, null) as OrbManager;
    }

    private static void CleanupSceneObjects()
    {
        DestroyAll<MapManager>();
        DestroyAll<MapPresentationController>();

        if (GameFlowManager.Instance != null)
            Object.DestroyImmediate(GameFlowManager.Instance.gameObject);

        if (OrbManager.Instance != null)
            Object.DestroyImmediate(OrbManager.Instance.gameObject);

        if (RelicManager.Instance != null)
            Object.DestroyImmediate(RelicManager.Instance.gameObject);
    }

    private static void DestroyAll<T>() where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                Object.DestroyImmediate(found[i].gameObject);
        }
    }
}
