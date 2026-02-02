using System.Collections.Generic;
using UnityEngine;

public class OrbManager : MonoBehaviour
{
    public static OrbManager Instance { get; private set; }

    [Header("Active Orb (current)")]
    [SerializeField] private OrbData currentOrb;

    [Header("Owned Orbs (optional, for future UI)")]
    [SerializeField] private List<OrbData> ownedOrbs = new List<OrbData>();


    public OrbData CurrentOrb => currentOrb;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Si currentOrb está seteado, aseguro que exista en ownedOrbs
        if (currentOrb != null && !ownedOrbs.Contains(currentOrb))
            ownedOrbs.Add(currentOrb);
    }

    public void SetCurrentOrb(OrbData orb)
    {
        if (orb == null) return;

        // Si no estaba en la lista, lo agrego
        if (!ownedOrbs.Contains(orb))
            ownedOrbs.Add(orb);

        currentOrb = orb;

        // Mantener índice alineado al orbe actual
        currentIndex = ownedOrbs.IndexOf(currentOrb);
        if (currentIndex < 0) currentIndex = 0;

        Debug.Log($"[OrbManager] Current orb set to: {orb.orbName}");
    }

    public void AddOrb(OrbData orb)
    {
        if (orb == null) return;

        if (!ownedOrbs.Contains(orb))
            ownedOrbs.Add(orb);

        // MVP automático: cuando ganás un orbe, lo equipás
        SetCurrentOrb(orb);

        Debug.Log($"[OrbManager] Orb gained: {orb.orbName}");
    }

    private int currentIndex = 0;

    private void Start()
    {
        // Asegura índice consistente si ya hay currentOrb seteado
        if (currentOrb != null)
        {
            currentIndex = ownedOrbs.IndexOf(currentOrb);
            if (currentIndex < 0) currentIndex = 0;
        }
    }

    public void NextOrb()
    {
        if (ownedOrbs == null || ownedOrbs.Count == 0) return;

        currentIndex = (currentIndex + 1) % ownedOrbs.Count;
        SetCurrentOrb(ownedOrbs[currentIndex]);

        Debug.Log($"[OrbManager] Switched to next orb: {currentOrb.orbName}");
    }

    public void PrevOrb()
    {
        if (ownedOrbs == null || ownedOrbs.Count == 0) return;

        currentIndex = (currentIndex - 1 + ownedOrbs.Count) % ownedOrbs.Count;
        SetCurrentOrb(ownedOrbs[currentIndex]);

        Debug.Log($"[OrbManager] Switched to prev orb: {currentOrb.orbName}");
    }



}
