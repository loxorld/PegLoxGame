using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ServiceRegistry
{
    private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();
    private static readonly HashSet<string> ReportedFallbackKeys = new HashSet<string>();

    public static void Register<T>(T service) where T : class
    {
        if (!IsValidService(service))
            return;

        Services[typeof(T)] = service;
    }

    public static bool TryResolve<T>(out T service) where T : class
    {
        if (Services.TryGetValue(typeof(T), out object candidate) && candidate is T typed)
        {
            if (!IsValidService(typed))
            {
                Services.Remove(typeof(T));
                service = null;
                return false;
            }

            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    public static T Resolve<T>() where T : class
    {
        TryResolve(out T service);
        return service;
    }

    public static T ResolveWithFallback<T>(string owner, string dependency, Func<T> fallbackResolver) where T : class
    {
        if (TryResolve(out T registered))
            return registered;

        LogFallback(owner, dependency, "registry-miss");

        T fallback = fallbackResolver != null ? fallbackResolver.Invoke() : null;
        if (fallback == null)
            return null;

        Register(fallback);
        LogFallbackMetric(owner, dependency, "legacy-resolver");
        return fallback;
    }

    public static void LogFallback(string owner, string dependency, string reason)
    {
        string key = $"{owner}:{dependency}:{reason}";
        if (ReportedFallbackKeys.Contains(key))
            return;

        ReportedFallbackKeys.Add(key);
        Debug.LogWarning($"[DI][Fallback] owner={owner} dependency={dependency} reason={reason}");
    }

    public static void LogFallbackMetric(string owner, string dependency, string source)
    {
        string key = $"metric:{owner}:{dependency}:{source}";
        if (ReportedFallbackKeys.Contains(key))
            return;

        ReportedFallbackKeys.Add(key);
        Debug.Log($"[DI][Metric] owner={owner} dependency={dependency} source={source}");
    }


    public static T LegacyFind<T>(bool includeInactive = false) where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
    }

    public static T[] LegacyFindAll<T>(bool includeInactive = false) where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
    }

    public static void Clear()
    {
        Services.Clear();
        ReportedFallbackKeys.Clear();
    }
    private static bool IsValidService(object service)
    {
        if (service == null)
            return false;

        if (!(service is UnityEngine.Object))
            return true;

        UnityEngine.Object unityObject = (UnityEngine.Object)service;
        if (unityObject == null)
            return false;

        if (unityObject is Component component)
            return IsRuntimeSceneObject(component.gameObject.scene);

        if (unityObject is GameObject gameObject)
            return IsRuntimeSceneObject(gameObject.scene);

        return true;
    }

    private static bool IsRuntimeSceneObject(Scene scene)
    {
        return scene.IsValid();
    }
}