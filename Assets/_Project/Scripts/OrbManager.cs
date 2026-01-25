using System.Collections.Generic;
using UnityEngine;

public class OrbManager : MonoBehaviour
{
    [Header("Active Orb (current)")]
    [SerializeField] private OrbData currentOrb;

    [Header("Owned Orbs (optional, for future UI)")]
    [SerializeField] private List<OrbData> ownedOrbs = new List<OrbData>();

    public OrbData CurrentOrb => currentOrb;

    private void Awake()
    {
        // Si currentOrb está seteado, aseguramos que exista en ownedOrbs
        if (currentOrb != null && !ownedOrbs.Contains(currentOrb))
            ownedOrbs.Add(currentOrb);
    }

    public void SetCurrentOrb(OrbData orb)
    {
        if (orb == null) return;

        currentOrb = orb;
        if (!ownedOrbs.Contains(orb))
            ownedOrbs.Add(orb);

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


}
