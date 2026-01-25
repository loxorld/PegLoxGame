using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orb Data", fileName = "OrbData")]
public class OrbData : ScriptableObject
{
    public string orbName = "Basic Orb";

    [Header("Damage")]
    public int damagePerHit = 2;

    [Header("Physics")]
    [Range(0f, 1f)] public float bounciness = 0.9f;
    [Range(0f, 5f)] public float linearDrag = 0.0f;

    [Header("Visual (MVP)")]
    public Color color = Color.white;

    [Header("Orb Effects (optional)")]
    public List<ShotEffectBase> orbEffects = new List<ShotEffectBase>();
}
