using System.Collections.Generic;

public class EffectPipeline
{
    private readonly List<IShotEffect> effects = new List<IShotEffect>();

    public void Clear() => effects.Clear();

    public void Add(IShotEffect effect)
    {
        if (effect != null) effects.Add(effect);
    }

    public void AddRange(IEnumerable<IShotEffect> toAdd)
    {
        if (toAdd == null) return;
        foreach (var e in toAdd) Add(e);
    }

    public void OnShotStart(ShotContext ctx)
    {
        for (int i = 0; i < effects.Count; i++) effects[i].OnShotStart(ctx);
    }

    public void OnPegHit(ShotContext ctx, PegType pegType)
    {
        for (int i = 0; i < effects.Count; i++) effects[i].OnPegHit(ctx, pegType);
    }

    public void OnShotEnd(ShotContext ctx)
    {
        for (int i = 0; i < effects.Count; i++) effects[i].OnShotEnd(ctx);
    }
}
