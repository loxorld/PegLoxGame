using UnityEngine;

public abstract class ShotEffectBase : ScriptableObject, IShotEffect
{
    public virtual void OnShotStart(ShotContext ctx) { }
    public virtual void OnPegHit(ShotContext ctx, PegType pegType) { }
    public virtual void OnShotEnd(ShotContext ctx) { }
}
