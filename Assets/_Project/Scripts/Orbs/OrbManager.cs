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

        // Si currentOrb está seteado, aseguro que exista en ownedOrbs
        if (currentOrb != null && !ownedOrbs.Contains(currentOrb))
            ownedOrbs.Add(currentOrb);

        BuildInstancesFromOwnedOrbs();
    }

    public void SetCurrentOrb(OrbData orb)
    {
        if (orb == null) return;

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

        if (!ownedOrbs.Contains(orb))
            ownedOrbs.Add(orb);

        OrbInstance instance = GetOrCreateInstance(orb);

        // MVP automático: cuando ganás un orbe, lo equipás
        SetCurrentOrb(instance);

        Debug.Log($"[OrbManager] Orb gained: {orb.orbName}");
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

    private OrbInstance GetOrCreateInstance(OrbData orb)
    {
        if (orb == null) return null;

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
}