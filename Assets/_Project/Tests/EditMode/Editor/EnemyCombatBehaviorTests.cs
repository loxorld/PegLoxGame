using NUnit.Framework;
using UnityEngine;

public class EnemyCombatBehaviorTests
{
    [TearDown]
    public void TearDown()
    {
        if (enemyGameObject != null)
            Object.DestroyImmediate(enemyGameObject);

        if (enemyData != null)
            Object.DestroyImmediate(enemyData);
    }

    [Test]
    public void ResolveIncomingDamage_AppliesFlatReduction()
    {
        CreateEnemy(flatDamageReduction: 3);
        Enemy.CombatAlert? capturedAlert = null;
        enemy.CombatAlertRaised += alert => capturedAlert = alert;

        int appliedDamage = enemy.ResolveIncomingDamage(10);

        Assert.AreEqual(7, appliedDamage);
        Assert.IsTrue(capturedAlert.HasValue);
        Assert.AreEqual(Enemy.CombatAlertType.Block, capturedAlert.Value.Type);
        Assert.AreEqual(3, capturedAlert.Value.Amount);
    }

    [Test]
    public void OnShotResolved_AddsRageAndHeal_WhenEnemySurvives()
    {
        CreateEnemy(healOnSurvive: 4, rageDamageOnSurvive: 2);
        int healAlerts = 0;
        int rageAlerts = 0;
        enemy.CombatAlertRaised += alert =>
        {
            if (alert.Type == Enemy.CombatAlertType.Heal)
                healAlerts++;
            else if (alert.Type == Enemy.CombatAlertType.Rage)
                rageAlerts++;
        };
        enemy.TakeDamage(8);

        enemy.OnShotResolved(rawDamage: 8, appliedDamage: 8, totalHits: 3, criticalHits: 1);

        Assert.AreEqual(16, enemy.CurrentHP);
        Assert.AreEqual(7, enemy.AttackDamage);
        Assert.AreEqual(1, healAlerts);
        Assert.AreEqual(1, rageAlerts);
    }

    [Test]
    public void OnShotResolved_TriggersDesperationOnlyOnce()
    {
        CreateEnemy(desperationThreshold: 0.5f, desperationAttackBonus: 4, desperationHeal: 3);
        int phaseAlerts = 0;
        enemy.CombatAlertRaised += alert =>
        {
            if (alert.Type == Enemy.CombatAlertType.Phase)
                phaseAlerts++;
        };
        enemy.TakeDamage(12);

        enemy.OnShotResolved(rawDamage: 12, appliedDamage: 12, totalHits: 4, criticalHits: 2);
        int firstAttackDamage = enemy.AttackDamage;
        int firstHp = enemy.CurrentHP;

        enemy.OnShotResolved(rawDamage: 0, appliedDamage: 0, totalHits: 0, criticalHits: 0);

        Assert.AreEqual(firstAttackDamage, enemy.AttackDamage);
        Assert.AreEqual(firstHp, enemy.CurrentHP);
        Assert.AreEqual(1, phaseAlerts);
    }

    private GameObject enemyGameObject;
    private Enemy enemy;
    private EnemyData enemyData;

    private void CreateEnemy(
        int flatDamageReduction = 0,
        int rageDamageOnSurvive = 0,
        int healOnSurvive = 0,
        float desperationThreshold = 0f,
        int desperationAttackBonus = 0,
        int desperationHeal = 0)
    {
        enemyData = ScriptableObject.CreateInstance<EnemyData>();
        enemyData.enemyName = "Test Enemy";
        enemyData.maxHP = 20;
        enemyData.attackDamage = 5;

        var type = typeof(EnemyData);
        type.GetField("flatDamageReduction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemyData, flatDamageReduction);
        type.GetField("rageDamageOnSurvive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemyData, rageDamageOnSurvive);
        type.GetField("healOnSurvive", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemyData, healOnSurvive);
        type.GetField("desperationThreshold", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemyData, desperationThreshold);
        type.GetField("desperationAttackBonus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemyData, desperationAttackBonus);
        type.GetField("desperationHeal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemyData, desperationHeal);

        enemyGameObject = new GameObject("EnemyTest");
        enemyGameObject.AddComponent<SpriteRenderer>();
        enemy = enemyGameObject.AddComponent<Enemy>();
        enemy.SetDataAndReset(enemyData);
        enemy.ApplyDifficulty(enemyData.maxHP, enemyData.attackDamage);
    }
}
