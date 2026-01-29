using System;
using UnityEngine;

public class ShotManager : MonoBehaviour
{

    public event Action<ShotSummary> ShotStatsChanged;   // durante el tiro
    public event Action<ShotSummary> ShotResolved;       // al terminar y aplicar daño

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

    private void Update()
    {
        // Mantener IsGameOver sincronizado con el estado global 
        if (GameFlowManager.Instance != null)
            IsGameOver = GameFlowManager.Instance.State == GameState.GameOver;
    }

    private bool CanProcessCombat()
    {
        // Si no hay GameFlowManager, permitimos 
        if (GameFlowManager.Instance == null) return true;
        return GameFlowManager.Instance.State == GameState.Combat;
    }

    private void PublishStats(bool resolved = false)
    {
        if (currentShot == null) return;

        int mult = currentShot.Multiplier;
        // Si todavía no hay set de multiplier, usa el helper consistente
        if (mult <= 0) mult = 1 + currentShot.CriticalHits;

        var summary = new ShotSummary(
            orbName: currentShot.Orb != null ? currentShot.Orb.orbName : "None",
            normalHits: currentShot.NormalHits,
            criticalHits: currentShot.CriticalHits,
            damagePerHit: currentShot.DamagePerHit,
            multiplier: mult
        );

        if (resolved) ShotResolved?.Invoke(summary);
        else ShotStatsChanged?.Invoke(summary);
    }


    public void OnShotStarted(OrbData orb)
    {
        // Solo se puede iniciar tiro en combate
        if (!CanProcessCombat()) return;

        ShotInProgress = true;
        currentShot = new ShotContext(orb);

        pipeline.Clear();

        // Orden de efectos: Orbe -> Reliquia
        if (orb != null && orb.orbEffects != null)
            pipeline.AddOrbEffects(orb.orbEffects);

        if (relics != null)
            pipeline.AddRelicEffects(relics.ActiveRelics);

        pipeline.OnShotStart(currentShot);
        PublishStats();
    }

    public void RegisterPegHit(PegType pegType)
    {
        // No contamos hits fuera de combate o fuera de un tiro activo
        if (!ShotInProgress || currentShot == null) return;
        if (!CanProcessCombat()) return;

        currentShot.RegisterHit(pegType);
        // Orden consistente: OrbEffects -> RelicEffects (PegBehaviors se ejecutan en Peg antes de llamar acá).
        pipeline.OnPegHit(currentShot, pegType);
        PublishStats();
    }

    public void OnShotEnded()
    {
        // si ya terminó, no hagas nada
        if (!ShotInProgress) return;
        ShotInProgress = false;

        // Si ya no estamos en combate, cerramos el tiro sin aplicar nada
        if (!CanProcessCombat())
        {
            currentShot = null;
            return;
        }

        if (battle == null || player == null || currentShot == null)
        {
            currentShot = null;
            return;
        }

        // Multiplicador base
        currentShot.Multiplier = 1 + currentShot.CriticalHits;

        pipeline.OnShotEnd(currentShot);

        PublishStats();

        Enemy enemy = battle.CurrentEnemy;
        if (enemy == null || !enemy.gameObject.activeSelf)
        {
            currentShot = null;
            return;
        }

        int damage = currentShot.TotalHits * currentShot.DamagePerHit * currentShot.Multiplier;

        ShotResolved?.Invoke(new ShotSummary(
            currentShot.Orb != null ? currentShot.Orb.orbName : "None",
            currentShot.NormalHits,
            currentShot.CriticalHits,
            currentShot.DamagePerHit,
            currentShot.Multiplier
        ));

        enemy.TakeDamage(damage);

        // Si el enemigo murió, no contraataca
        if (!enemy.gameObject.activeSelf)
        {
            currentShot = null;
            return;
        }

        // Contraataque
        player.TakeDamage(enemy.AttackDamage);

        // GameOver lo dispara PlayerStats.Died -> GameOverMenuUI -> GameFlowManager.SetState(GameOver)
        // ShotManager NO decide GameOver, solo corta.
        currentShot = null;
    }

    // HUD helpers
    public int HudNormalHits => currentShot?.NormalHits ?? 0;
    public int HudCriticalHits => currentShot?.CriticalHits ?? 0;
    public int HudTotalHits => currentShot != null ? currentShot.TotalHits : 0;
    public int HudMultiplier => currentShot != null ? (1 + currentShot.CriticalHits) : 1;
    public string HudOrbName => currentShot?.Orb != null ? currentShot.Orb.orbName : "None";
}