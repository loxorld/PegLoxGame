using System.Collections.Generic;
using UnityEngine;

public class RelicManager : MonoBehaviour
{
    [SerializeField] private List<ShotEffectBase> activeRelics = new List<ShotEffectBase>();

    public IReadOnlyList<ShotEffectBase> ActiveRelics => activeRelics;

    public void AddRelic(ShotEffectBase relic)
    {
        if (relic == null) return;
        if (!activeRelics.Contains(relic))
            activeRelics.Add(relic);
    }

    // MVP: se editan por Inspector. Más adelante: drop/tienda.
}
    