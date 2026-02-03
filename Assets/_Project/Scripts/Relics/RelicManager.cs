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

    // MVP: se editan por Inspector. Más adelante: drop/tienda.
}