using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PegManager : MonoBehaviour
{
    public static PegManager Instance { get; private set; }

    private readonly List<Peg> pegs = new List<Peg>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }



    public void RegisterPeg(Peg peg)
    {
        if (peg != null && !pegs.Contains(peg))
            pegs.Add(peg);
    }

    public void UnregisterPeg(Peg peg)
    {
        pegs.Remove(peg);
    }

    public void ResetAllPegs()
    {
        for (int i = 0; i < pegs.Count; i++)
            pegs[i].ResetPeg();
        Debug.Log($"ResetAllPegs -> pegs registrados: {pegs.Count}");
    }
}

