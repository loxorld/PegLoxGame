using System.Collections.Generic;
using UnityEngine;

public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }

    [SerializeField] private List<ShotEffectBase> activeRelics = new List<ShotEffectBase>();

    public IReadOnlyList<ShotEffectBase> ActiveRelics => activeRelics;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddRelic(ShotEffectBase relic)
    {
        if (relic == null) return;
        if (HasRelic(relic))
            return;

        activeRelics.Add(relic);
    }

    public bool HasRelic(ShotEffectBase relic)
    {
        return HasRelicId(BuildRelicId(relic));
    }

    public bool HasRelicId(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId) || activeRelics == null)
            return false;

        for (int i = 0; i < activeRelics.Count; i++)
        {
            ShotEffectBase activeRelic = activeRelics[i];
            if (string.Equals(BuildRelicId(activeRelic), relicId, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public void ResetToDefaults()
    {
        activeRelics.Clear();
    }

    public List<string> SerializeRelics()
    {
        var result = new List<string>();

        if (activeRelics == null)
            return result;

        for (int i = 0; i < activeRelics.Count; i++)
        {
            ShotEffectBase relic = activeRelics[i];
            if (relic == null) continue;
            string relicId = BuildRelicId(relic);
            if (!string.IsNullOrWhiteSpace(relicId))
                result.Add(relicId);
        }

        return result;
    }

    public void DeserializeRelics(List<string> relicIds)
    {
        activeRelics.Clear();

        if (relicIds == null || relicIds.Count == 0)
            return;

        for (int i = 0; i < relicIds.Count; i++)
        {
            string relicId = relicIds[i];
            if (string.IsNullOrWhiteSpace(relicId)) continue;

            ShotEffectBase relic = ResolveRelicById(relicId);
            if (relic != null)
                AddRelic(relic);
        }
    }

    public static string BuildRelicId(ShotEffectBase relic)
    {
        if (relic == null || string.IsNullOrWhiteSpace(relic.name))
            return null;

        return relic.name.Trim();
    }

    private static ShotEffectBase ResolveRelicById(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return null;

        ShotEffectBase[] candidates = Resources.FindObjectsOfTypeAll<ShotEffectBase>();
        for (int i = 0; i < candidates.Length; i++)
        {
            ShotEffectBase relic = candidates[i];
            if (relic != null && relic.name == relicId)
                return relic;
        }

        return null;
    }
}
