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
        if (!pegs.Contains(peg))
            pegs.Add(peg);
    }

    public void UnregisterPeg(Peg peg)
    {
        if (peg == null) return;
        pegs.Remove(peg);
    }

    public void ResetAllPegs()
    {
        // limpia referencias muertas por las dudas
        for (int i = pegs.Count - 1; i >= 0; i--)
        {
            if (pegs[i] == null) { pegs.RemoveAt(i); continue; }
            pegs[i].ResetPeg();
        }

        Debug.Log($"ResetAllPegs -> pegs registrados: {pegs.Count}");
    }
}
