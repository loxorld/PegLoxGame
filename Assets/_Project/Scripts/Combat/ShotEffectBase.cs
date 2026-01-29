using UnityEngine;

public abstract class ShotEffectBase : ScriptableObject, IShotEffect
{
    [Header("UI")]
    [Tooltip("Nombre visible en UI. Si está vacío, se usa el nombre del asset.")]
    [SerializeField] private string displayName;

    [Tooltip("Icono para UI (rewards, inventario, etc.)")]
    [SerializeField] private Sprite icon;

    [Tooltip("Descripción corta para UI.")]
    [TextArea(2, 4)]
    [SerializeField] private string description;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string Description => description;

    public virtual void OnShotStart(ShotContext ctx) { }
    public virtual void OnPegHit(ShotContext ctx, PegType pegType) { }
    public virtual void OnShotEnd(ShotContext ctx) { }
}
