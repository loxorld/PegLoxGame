public interface IShotEffect
{
    void OnShotStart(ShotContext ctx);
    void OnPegHit(ShotContext ctx, PegType pegType);
    void OnShotEnd(ShotContext ctx);
}
