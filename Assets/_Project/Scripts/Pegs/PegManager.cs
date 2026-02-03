using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PegManager : MonoBehaviour
{
    public static PegManager Instance { get; private set; }

    private readonly List<Peg> pegs = new List<Peg>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void RegisterPeg(Peg peg)
    {
        if (peg == null) return;
        if (!pegs.Contains(peg)) pegs.Add(peg);
    }

    public void UnregisterPeg(Peg peg)
    {
        if (peg == null) return;
        pegs.Remove(peg);
    }

    public void ResetAllPegsForNewEncounter()
    {
        for (int i = pegs.Count - 1; i >= 0; i--)
        {
            if (pegs[i] == null) { pegs.RemoveAt(i); continue; }
            pegs[i].ResetForNewEncounter();
        }
    }

    /// <summary>
    /// Revive todos los consumidos en el encounter actual (excepto un peg opcional).
    /// (Este método queda igual para no romper nada)
    /// </summary>
    public void RefreshEncounterPegs(Peg exclude)
    {
        for (int i = pegs.Count - 1; i >= 0; i--)
        {
            Peg p = pegs[i];
            if (p == null) { pegs.RemoveAt(i); continue; }
            if (p == exclude) continue;

            if (p.IsConsumed)
                p.RestoreForSameEncounter();
        }
    }

    /// <summary>
    /// NUEVO: Revive consumidos en el encounter actual, excluyendo:
    /// - un peg puntual (exclude)
    /// - y opcionalmente todos los pegs con una PegDefinition específica (excludeDefinition)
    ///
    /// Usalo para: "Refresh no revive Refresh".
    /// </summary>
    public void RefreshEncounterPegs(Peg exclude, PegDefinition excludeDefinition)
    {
        for (int i = pegs.Count - 1; i >= 0; i--)
        {
            Peg p = pegs[i];
            if (p == null) { pegs.RemoveAt(i); continue; }

            if (p == exclude) continue;
            if (excludeDefinition != null && p.Definition == excludeDefinition) continue;

            if (p.IsConsumed)
                p.RestoreForSameEncounter();
        }
    }

    /// <summary>
    /// Consume pegs dentro de un radio. Opcional: contar como hits (normal/crit según cada peg).
    /// </summary>
    public void ConsumePegsInRadius(Vector2 center, float radius, LayerMask mask, Peg exclude, bool countHits)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, mask);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i];
            if (col == null) continue;

            Peg p = col.GetComponent<Peg>();
            if (p == null) continue;
            if (p == exclude) continue;
            if (p.IsConsumed) continue;

            if (countHits)
                ShotManager.Instance?.RegisterPegHit(p.Type);

            p.ForceConsumeNoHitCount();
        }
    }
    public Peg GetRandomActivePeg(Peg exclude = null)
    {
        if (pegs.Count == 0) return null;

        int startIndex = Random.Range(0, pegs.Count);
        for (int i = 0; i < pegs.Count; i++)
        {
            int index = (startIndex + i) % pegs.Count;
            Peg peg = pegs[index];
            if (peg == null) continue;
            if (peg == exclude) continue;
            if (peg.IsConsumed) continue;
            return peg;
        }

        return null;
    }

}
