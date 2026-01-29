using System.Collections.Generic;

public class EffectPipeline
{
    private readonly List<IShotEffect> orbEffects = new List<IShotEffect>();
    private readonly List<IShotEffect> relicEffects = new List<IShotEffect>();

    public void Clear()
    {
        orbEffects.Clear();
        relicEffects.Clear();
    }

    public void AddOrbEffect(IShotEffect effect)
    {
        if (effect != null) orbEffects.Add(effect);
    }

    public void AddOrbEffects(IEnumerable<IShotEffect> toAdd)
    {
        if (toAdd == null) return;
        foreach (var e in toAdd) AddOrbEffect(e);
    }

    public void AddRelicEffect(IShotEffect effect)
    {
        if (effect != null) relicEffects.Add(effect);
    }

    public void AddRelicEffects(IEnumerable<IShotEffect> toAdd)
    {
        if (toAdd == null) return;
        foreach (var e in toAdd) AddRelicEffect(e);
    }

    public void OnShotStart(ShotContext ctx)
    {
        // Orden consistente: Orbe -> Reliquia
        for (int i = 0; i < orbEffects.Count; i++) orbEffects[i].OnShotStart(ctx);
        for (int i = 0; i < relicEffects.Count; i++) relicEffects[i].OnShotStart(ctx);
    }

    public void OnPegHit(ShotContext ctx, PegType pegType)
    {
        // Orden consistente: Orbe -> Reliquia
        for (int i = 0; i < orbEffects.Count; i++) orbEffects[i].OnPegHit(ctx, pegType);
        for (int i = 0; i < relicEffects.Count; i++) relicEffects[i].OnPegHit(ctx, pegType);
    }

    public void OnShotEnd(ShotContext ctx)
    {
        // Orden consistente: Orbe -> Reliquia
        for (int i = 0; i < orbEffects.Count; i++) orbEffects[i].OnShotEnd(ctx);
        for (int i = 0; i < relicEffects.Count; i++) relicEffects[i].OnShotEnd(ctx);
    }
}