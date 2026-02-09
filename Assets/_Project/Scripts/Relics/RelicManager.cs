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
        if (!activeRelics.Contains(relic))
            activeRelics.Add(relic);
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
            result.Add(relic.name);
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
                activeRelics.Add(relic);
        }
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
