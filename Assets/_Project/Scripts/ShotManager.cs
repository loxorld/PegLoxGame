using UnityEngine;

public class ShotManager : MonoBehaviour
{
    public static ShotManager Instance { get; private set; }

    public bool ShotInProgress { get; private set; }
    public bool IsGameOver { get; private set; }

    [Header("Battle References")]
    [SerializeField] private BattleManager battle;
    [SerializeField] private PlayerStats player;

    [Header("Effects Sources")]
    [SerializeField] private RelicManager relics;

    private ShotContext currentShot;
    private readonly EffectPipeline pipeline = new EffectPipeline();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ShotInProgress = false;
        IsGameOver = false;
    }

    public void OnShotStarted(OrbData orb)
    {
        if (IsGameOver) return;

        ShotInProgress = true;
        currentShot = new ShotContext(orb);

        pipeline.Clear();

        // 1) Reliquias persistentes
        if (relics != null)
            pipeline.AddRange(relics.ActiveRelics);

        // 2) Efectos del orbe
        if (orb != null && orb.orbEffects != null)
            pipeline.AddRange(orb.orbEffects);

        pipeline.OnShotStart(currentShot);
    }

    public void RegisterPegHit(PegType pegType)
    {
        if (!ShotInProgress || currentShot == null) return;

        currentShot.RegisterHit(pegType);
        pipeline.OnPegHit(currentShot, pegType);
    }

    public void OnShotEnded()
    {
        // EXACTLY-ONCE: si ya terminó, no hagas nada
        if (!ShotInProgress) return;
        ShotInProgress = false;

        if (IsGameOver) { currentShot = null; return; }
        if (battle == null || player == null || currentShot == null) return;

        // Multiplicador base Peglin-ish (B)
        currentShot.Multiplier = 1 + currentShot.CriticalHits;

        pipeline.OnShotEnd(currentShot);

        Enemy enemy = battle.CurrentEnemy;
        if (enemy == null || !enemy.gameObject.activeSelf) { currentShot = null; return; }

        int damage = currentShot.TotalHits * currentShot.DamagePerHit * currentShot.Multiplier;

        enemy.TakeDamage(damage);

        if (!enemy.gameObject.activeSelf) { currentShot = null; return; }

        player.TakeDamage(enemy.AttackDamage);

        if (player.IsDead)
        {
            IsGameOver = true;
            Debug.Log("GAME OVER");
            currentShot = null;
            return;
        }

        currentShot = null;
    }

    // HUD helpers
    public int HudNormalHits => currentShot?.NormalHits ?? 0;
    public int HudCriticalHits => currentShot?.CriticalHits ?? 0;
    public int HudTotalHits => currentShot != null ? currentShot.TotalHits : 0;
    public int HudMultiplier => currentShot != null ? (1 + currentShot.CriticalHits) : 1;
    public string HudOrbName => currentShot?.Orb != null ? currentShot.Orb.orbName : "None";
}
