using System.Collections.Generic;
using UnityEngine;

public class RelicManager : MonoBehaviour
{
    [SerializeField] private List<ShotEffectBase> activeRelics = new List<ShotEffectBase>();

    public IReadOnlyList<ShotEffectBase> ActiveRelics => activeRelics;

    // MVP: se editan por Inspector. Más adelante: drop/tienda.
}
