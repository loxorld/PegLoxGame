using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Orb Data", fileName = "OrbData")]
public class OrbData : ScriptableObject
{
    [Header("UI")]
    public string orbName = "Basic Orb";

    [Tooltip("Icono para UI (rewards, selector, etc.)")]
    public Sprite icon;

    [TextArea(2, 4)]
    [Tooltip("Descripción corta para UI.")]
    public string description;

    [Header("Damage")]
    public int damagePerHit = 2;
    [Min(0)]
    [Tooltip("0 = sin tope de nivel.")]
    public int maxLevel = 0;

    [Header("Physics")]
    [Range(0f, 1f)] public float bounciness = 0.9f;
    [Range(0f, 5f)] public float linearDrag = 0.0f;

    [Header("Visual (MVP)")]
    public Color color = Color.white;

    [Header("Orb Effects (optional)")]
    public List<ShotEffectBase> orbEffects = new List<ShotEffectBase>();
}
