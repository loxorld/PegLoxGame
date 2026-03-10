using NUnit.Framework;
using UnityEngine;

public class EnemyEncounterSelectorTests
{
    [TearDown]
    public void TearDown()
    {
        DestroyImmediateIfNeeded(forestEnemy);
        DestroyImmediateIfNeeded(swampEnemy);
        DestroyImmediateIfNeeded(castleEnemy);
        DestroyImmediateIfNeeded(bossOnlyEnemy);
        DestroyImmediateIfNeeded(eliteEnemy);
        DestroyImmediateIfNeeded(miniBossEnemy);
    }

    [Test]
    public void BuildRegularEncounterCandidates_FiltersEnemiesByStageAndEncounterFlags()
    {
        var selector = new EnemyEncounterSelector();
        forestEnemy = CreateEnemy("Forest", minStage: 0, maxStage: 0, weight: 2);
        swampEnemy = CreateEnemy("Swamp", minStage: 1, maxStage: 1, weight: 1);
        bossOnlyEnemy = CreateEnemy("Boss", minStage: 0, maxStage: 2, weight: 4, allowRegularEncounters: false);

        var candidates = selector.BuildRegularEncounterCandidates(
            new[] { forestEnemy, swampEnemy, bossOnlyEnemy },
            stageIndex: 0,
            encounterInStage: 0);

        Assert.AreEqual(1, candidates.Count);
        Assert.AreSame(forestEnemy, candidates[0]);
    }

    [Test]
    public void SelectRegularEncounterEnemy_UsesEncounterWeights()
    {
        var selector = new EnemyEncounterSelector();
        forestEnemy = CreateEnemy("Forest", minStage: 0, maxStage: 2, weight: 1);
        castleEnemy = CreateEnemy("Castle", minStage: 0, maxStage: 2, weight: 5);

        EnemyData selected = selector.SelectRegularEncounterEnemy(
            new[] { forestEnemy, castleEnemy },
            stageIndex: 1,
            encounterInStage: 0,
            (min, max) => max - 1);

        Assert.AreSame(castleEnemy, selected);
    }

    [Test]
    public void BuildRegularEncounterCandidates_FallsBackToAnyRegularEnemy_WhenStageHasNoMatches()
    {
        var selector = new EnemyEncounterSelector();
        swampEnemy = CreateEnemy("Swamp", minStage: 1, maxStage: 1, weight: 1);
        castleEnemy = CreateEnemy("Castle", minStage: 2, maxStage: 2, weight: 1);

        var candidates = selector.BuildRegularEncounterCandidates(
            new[] { swampEnemy, castleEnemy },
            stageIndex: 5,
            encounterInStage: 0);

        Assert.AreEqual(2, candidates.Count);
    }

    [Test]
    public void BuildEliteEncounterCandidates_FiltersToElitePool()
    {
        var selector = new EnemyEncounterSelector();
        eliteEnemy = CreateEnemy("Elite", minStage: 1, maxStage: 1, weight: 1, allowRegularEncounters: false, allowEliteEncounters: true);
        forestEnemy = CreateEnemy("Regular", minStage: 1, maxStage: 1, weight: 3);

        var candidates = selector.BuildEliteEncounterCandidates(
            new[] { forestEnemy, eliteEnemy },
            stageIndex: 1,
            encounterInStage: 1);

        Assert.AreEqual(1, candidates.Count);
        Assert.AreSame(eliteEnemy, candidates[0]);
    }

    [Test]
    public void SelectMiniBossEncounterEnemy_UsesMiniBossWeight()
    {
        var selector = new EnemyEncounterSelector();
        eliteEnemy = CreateEnemy("Mini A", minStage: 2, maxStage: 2, weight: 1, allowRegularEncounters: false, allowMiniBossEncounters: true, miniBossWeight: 1);
        miniBossEnemy = CreateEnemy("Mini B", minStage: 2, maxStage: 2, weight: 1, allowRegularEncounters: false, allowMiniBossEncounters: true, miniBossWeight: 5);

        EnemyData selected = selector.SelectMiniBossEncounterEnemy(
            new[] { eliteEnemy, miniBossEnemy },
            stageIndex: 2,
            encounterInStage: 3,
            (min, max) => max - 1);

        Assert.AreSame(miniBossEnemy, selected);
    }

    [Test]
    public void EncounterProfilePlanner_PrioritizesMiniBossOverElite()
    {
        var planner = new CombatEncounterProfilePlanner();
        RunBalanceConfig balance = ScriptableObject.CreateInstance<RunBalanceConfig>();

        CombatEncounterProfile profile = planner.Resolve(
            balance,
            stageIndex: 0,
            encounterInStage: 2,
            isBossEncounter: false,
            hasEliteCandidates: true,
            hasMiniBossCandidates: true);

        Assert.AreEqual(CombatEncounterType.MiniBoss, profile.Type);
        Object.DestroyImmediate(balance);
    }

    [Test]
    public void EncounterProfilePlanner_FallsBackToRegular_WhenNoSpecialCandidates()
    {
        var planner = new CombatEncounterProfilePlanner();

        CombatEncounterProfile profile = planner.Resolve(
            balance: null,
            stageIndex: 0,
            encounterInStage: 3,
            isBossEncounter: false,
            hasEliteCandidates: false,
            hasMiniBossCandidates: false);

        Assert.AreEqual(CombatEncounterType.Regular, profile.Type);
    }

    private EnemyData forestEnemy;
    private EnemyData swampEnemy;
    private EnemyData castleEnemy;
    private EnemyData bossOnlyEnemy;
    private EnemyData eliteEnemy;
    private EnemyData miniBossEnemy;

    private static EnemyData CreateEnemy(
        string enemyName,
        int minStage,
        int maxStage,
        int weight,
        bool allowRegularEncounters = true,
        bool allowEliteEncounters = false,
        int eliteWeight = 1,
        bool allowMiniBossEncounters = false,
        int miniBossWeight = 1)
    {
        var enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.enemyName = enemyName;

        var type = typeof(EnemyData);
        type.GetField("minStageIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, minStage);
        type.GetField("maxStageIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, maxStage);
        type.GetField("encounterWeight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, weight);
        type.GetField("allowRegularEncounters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, allowRegularEncounters);
        type.GetField("allowEliteEncounters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, allowEliteEncounters);
        type.GetField("eliteEncounterWeight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, eliteWeight);
        type.GetField("allowMiniBossEncounters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, allowMiniBossEncounters);
        type.GetField("miniBossEncounterWeight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(enemy, miniBossWeight);
        return enemy;
    }

    private static void DestroyImmediateIfNeeded(Object obj)
    {
        if (obj != null)
            Object.DestroyImmediate(obj);
    }
}
