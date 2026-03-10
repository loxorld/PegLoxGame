using System.Collections.Generic;
using UnityEngine;

public class OrbManager : MonoBehaviour
{
    public static OrbManager Instance { get; private set; }

    [Header("Active Orb (current)")]
    [SerializeField] private OrbData currentOrb;

    [Header("Owned Orbs (optional, for future UI)")]
    [SerializeField] private List<OrbData> ownedOrbs = new List<OrbData>();

    private readonly List<OrbInstance> ownedOrbInstances = new List<OrbInstance>();
    private OrbInstance currentOrbInstance;
    private OrbData defaultOrb;
    private readonly Dictionary<string, OrbData> orbByPersistentId = new Dictionary<string, OrbData>();
    private readonly Dictionary<string, OrbData> orbByLegacyName = new Dictionary<string, OrbData>();

    public OrbInstance CurrentOrb => currentOrbInstance;
    public IReadOnlyList<OrbInstance> OwnedOrbInstances => ownedOrbInstances;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        defaultOrb = currentOrb;
        BuildOrbResolutionCache();

        // Si currentOrb está seteado, aseguro que exista en ownedOrbs
        if (currentOrb != null && !ownedOrbs.Contains(currentOrb))
            ownedOrbs.Add(currentOrb);

        BuildInstancesFromOwnedOrbs();
    }

    public void SetCurrentOrb(OrbData orb)
    {
        if (orb == null) return;

        CacheOrbCandidate(orb);

        // Si no estaba en la lista, lo agrego
        if (!ownedOrbs.Contains(orb))
            ownedOrbs.Add(orb);

        OrbInstance instance = GetOrCreateInstance(orb);
        SetCurrentOrb(instance);
    }

    public void SetCurrentOrb(OrbInstance orb)
    {
        if (orb == null) return;

        currentOrbInstance = orb;
        currentOrb = orb.BaseData;

        // Mantener índice alineado al orbe actual
        currentIndex = ownedOrbInstances.IndexOf(currentOrbInstance);
        if (currentIndex < 0) currentIndex = 0;

        Debug.Log($"[OrbManager] Current orb set to: {orb.OrbName}");
    }

    public void AddOrb(OrbData orb)
    {
        if (orb == null) return;

        CacheOrbCandidate(orb);

        if (!ownedOrbs.Contains(orb))
            ownedOrbs.Add(orb);

        OrbInstance instance = GetOrCreateInstance(orb);

        // MVP automático: cuando ganás un orbe, lo equipás
        SetCurrentOrb(instance);

        Debug.Log($"[OrbManager] Orb gained: {orb.orbName}");
    }

    public bool HasOrb(OrbData orb)
    {
        return FindOwnedOrbInstance(orb) != null;
    }

    public OrbInstance FindOwnedOrbInstance(OrbData orb)
    {
        return FindInstanceForOrb(orb);
    }

    private int currentIndex = 0;

    private void Start()
    {
        // Asegura índice consistente si ya hay currentOrb seteado
        if (currentOrb != null)
            SyncCurrentIndexFromInstance();
    }

    public void NextOrb()
    {
        if (ownedOrbInstances == null || ownedOrbInstances.Count == 0) return;

        currentIndex = (currentIndex + 1) % ownedOrbInstances.Count;
        SetCurrentOrb(ownedOrbInstances[currentIndex]);

        Debug.Log($"[OrbManager] Switched to next orb: {currentOrbInstance.OrbName}");
    }

    public void PrevOrb()
    {
        if (ownedOrbInstances == null || ownedOrbInstances.Count == 0) return;

        currentIndex = (currentIndex - 1 + ownedOrbInstances.Count) % ownedOrbInstances.Count;
        SetCurrentOrb(ownedOrbInstances[currentIndex]);

        Debug.Log($"[OrbManager] Switched to prev orb: {currentOrbInstance.OrbName}");
    }

    public void ResetToDefaults()
    {
        ownedOrbs.Clear();
        ownedOrbInstances.Clear();
        currentOrb = defaultOrb;
        currentOrbInstance = null;

        if (defaultOrb != null)
            ownedOrbs.Add(defaultOrb);

        BuildInstancesFromOwnedOrbs();
    }

    private void BuildInstancesFromOwnedOrbs()
    {
        ownedOrbInstances.Clear();

        if (ownedOrbs != null)
        {
            for (int i = 0; i < ownedOrbs.Count; i++)
            {
                OrbData orb = ownedOrbs[i];
                if (orb == null) continue;
                ownedOrbInstances.Add(new OrbInstance(orb));
            }
        }

        if (currentOrb != null)
            currentOrbInstance = FindInstanceForOrb(currentOrb);

        if (currentOrbInstance == null && ownedOrbInstances.Count > 0)
            currentOrbInstance = ownedOrbInstances[0];

        if (currentOrbInstance != null)
            currentOrb = currentOrbInstance.BaseData;

        SyncCurrentIndexFromInstance();
    }

    public List<RunSaveData.OrbSaveData> SerializeOrbs()
    {
        var result = new List<RunSaveData.OrbSaveData>();

        if (ownedOrbInstances == null || ownedOrbInstances.Count == 0)
            return result;

        for (int i = 0; i < ownedOrbInstances.Count; i++)
        {
            OrbInstance instance = ownedOrbInstances[i];
            if (instance == null || instance.BaseData == null) continue;

            result.Add(new RunSaveData.OrbSaveData
            {
                OrbId = BuildPersistentOrbId(instance.BaseData),
                Level = instance.Level
            });
        }

        return result;
    }

    public string GetCurrentOrbId()
    {
        if (currentOrbInstance != null && currentOrbInstance.BaseData != null)
            return BuildPersistentOrbId(currentOrbInstance.BaseData);

        if (currentOrb != null)
            return BuildPersistentOrbId(currentOrb);

        return null;
    }

    public void DeserializeOrbs(List<RunSaveData.OrbSaveData> savedOrbs, string currentOrbId)
    {
        BuildOrbResolutionCache();

        if (savedOrbs == null || savedOrbs.Count == 0)
        {
            ResetToDefaults();
            return;
        }

        ownedOrbs.Clear();
        ownedOrbInstances.Clear();

        var seen = new HashSet<string>();
        for (int i = 0; i < savedOrbs.Count; i++)
        {
            RunSaveData.OrbSaveData saved = savedOrbs[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.OrbId)) continue;
            if (!seen.Add(saved.OrbId)) continue;

            OrbData orb = ResolveOrbById(saved.OrbId);
            if (orb == null) continue;

            ownedOrbs.Add(orb);
            ownedOrbInstances.Add(new OrbInstance(orb, Mathf.Max(1, saved.Level)));
        }

        if (ownedOrbInstances.Count == 0)
        {
            ResetToDefaults();
            return;
        }

        OrbInstance desired = null;
        if (!string.IsNullOrWhiteSpace(currentOrbId))
            desired = FindInstanceForOrbId(currentOrbId);

        if (desired == null)
            desired = ownedOrbInstances[0];

        SetCurrentOrb(desired);
    }


    private OrbInstance GetOrCreateInstance(OrbData orb)
    {
        if (orb == null) return null;

        CacheOrbCandidate(orb);

        for (int i = 0; i < ownedOrbInstances.Count; i++)
        {
            OrbInstance instance = ownedOrbInstances[i];
            if (instance != null && instance.BaseData == orb)
                return instance;
        }

        OrbInstance newInstance = new OrbInstance(orb);
        ownedOrbInstances.Add(newInstance);
        return newInstance;
    }

    private OrbInstance FindInstanceForOrb(OrbData orb)
    {
        if (orb == null) return null;

        for (int i = 0; i < ownedOrbInstances.Count; i++)
        {
            OrbInstance instance = ownedOrbInstances[i];
            if (instance != null && instance.BaseData == orb)
                return instance;
        }

        return null;
    }

    private void SyncCurrentIndexFromInstance()
    {
        if (currentOrbInstance == null)
        {
            currentIndex = 0;
            return;
        }

        currentIndex = ownedOrbInstances.IndexOf(currentOrbInstance);
        if (currentIndex < 0) currentIndex = 0;
    }

    private OrbInstance FindInstanceForOrbId(string orbId)
    {
        if (string.IsNullOrWhiteSpace(orbId)) return null;

        for (int i = 0; i < ownedOrbInstances.Count; i++)
        {
            OrbInstance instance = ownedOrbInstances[i];
            if (instance == null || instance.BaseData == null)
                continue;

            if (string.Equals(BuildPersistentOrbId(instance.BaseData), orbId, System.StringComparison.Ordinal))
                return instance;
        }

        return null;
    }

    private void BuildOrbResolutionCache()
    {
        orbByPersistentId.Clear();
        orbByLegacyName.Clear();

        OrbData[] candidates = Resources.LoadAll<OrbData>(string.Empty);
        for (int i = 0; i < candidates.Length; i++)
            CacheOrbCandidate(candidates[i]);

        OrbData[] loadedCandidates = Resources.FindObjectsOfTypeAll<OrbData>();
        for (int i = 0; i < loadedCandidates.Length; i++)
            CacheOrbCandidate(loadedCandidates[i]);

        CacheOrbCandidate(currentOrb);
        CacheOrbCandidate(defaultOrb);

        if (ownedOrbs == null)
            return;

        for (int i = 0; i < ownedOrbs.Count; i++)
            CacheOrbCandidate(ownedOrbs[i]);
    }

    private void CacheOrbCandidate(OrbData orb)
    {
        if (orb == null)
            return;

        string persistentId = BuildPersistentOrbId(orb);
        if (!string.IsNullOrWhiteSpace(persistentId) && !orbByPersistentId.ContainsKey(persistentId))
            orbByPersistentId[persistentId] = orb;

        string legacyName = string.IsNullOrWhiteSpace(orb.name) ? null : orb.name.Trim();
        if (!string.IsNullOrWhiteSpace(legacyName) && !orbByLegacyName.ContainsKey(legacyName))
            orbByLegacyName[legacyName] = orb;
    }

    private OrbData ResolveOrbById(string orbId)
    {
        if (string.IsNullOrWhiteSpace(orbId))
            return null;

        if (orbByPersistentId.TryGetValue(orbId, out OrbData orbById) && orbById != null)
            return orbById;

        if (orbByLegacyName.TryGetValue(orbId, out OrbData orbByName) && orbByName != null)
        {
            Debug.LogWarning($"[OrbManager] Migración save legacy: OrbData '{orbId}' resuelto por name. Reguardar para persistir persistentId.");
            return orbByName;
        }

        return null;
    }

    private static string BuildPersistentOrbId(OrbData orb)
    {
        if (orb == null)
            return null;

        if (!string.IsNullOrWhiteSpace(orb.PersistentId))
            return orb.PersistentId.Trim();

        return string.IsNullOrWhiteSpace(orb.name) ? null : orb.name.Trim();
    }
}

