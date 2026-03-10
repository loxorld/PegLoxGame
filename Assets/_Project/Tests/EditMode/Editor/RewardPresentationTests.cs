using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class RewardPresentationTests
{
    private GameObject rewardGameObject;
    private GameObject orbManagerGameObject;
    private GameObject relicManagerGameObject;
    private RewardManager rewardManager;
    private OrbManager orbManager;
    private RelicManager relicManager;
    private RunBalanceConfig balanceConfig;
    private OrbData testOrb;
    private TestRewardRelic ownedRelic;
    private TestRewardRelic offeredRelic;

    [TearDown]
    public void TearDown()
    {
        DestroyImmediateIfNeeded(rewardGameObject);
        DestroyImmediateIfNeeded(orbManagerGameObject);
        DestroyImmediateIfNeeded(relicManagerGameObject);
        DestroyImmediateIfNeeded(balanceConfig);
        DestroyImmediateIfNeeded(testOrb);
        DestroyImmediateIfNeeded(ownedRelic);
        DestroyImmediateIfNeeded(offeredRelic);
    }

    [Test]
    public void EncounterRewardPreview_ComputesCoinTotalsAndGuarantees()
    {
        var profile = new CombatEncounterProfile(
            CombatEncounterType.MiniBoss,
            "Miniboss",
            hpMultiplier: 1.8f,
            damageMultiplier: 1.35f,
            bonusCoins: 10,
            singleEnemy: true,
            guaranteesRelicChoice: true,
            guaranteesUpgradeChoice: true);

        var preview = new EncounterRewardPreview(profile, baseCoinsMin: 4, baseCoinsMax: 8, choiceCount: 3, grantedCoins: 15);

        Assert.AreEqual(14, preview.TotalCoinsMin);
        Assert.AreEqual(18, preview.TotalCoinsMax);
        Assert.IsTrue(preview.GuaranteesRelicChoice);
        Assert.IsTrue(preview.GuaranteesUpgradeChoice);
        Assert.AreEqual(15, preview.GrantedCoins);
    }

    [Test]
    public void BuildRewardPreviewText_ExplainsSpecialRewards()
    {
        var profile = new CombatEncounterProfile(
            CombatEncounterType.Elite,
            "Elite",
            hpMultiplier: 1.35f,
            damageMultiplier: 1.15f,
            bonusCoins: 6,
            singleEnemy: true,
            guaranteesRelicChoice: true,
            guaranteesUpgradeChoice: false);

        var preview = new EncounterRewardPreview(profile, baseCoinsMin: 4, baseCoinsMax: 8, choiceCount: 3, grantedCoins: 0);
        string text = RewardManager.BuildRewardPreviewText(preview);

        StringAssert.Contains("+10-14 oro", text);
        StringAssert.Contains("3 elecciones", text);
        StringAssert.Contains("relic asegurada", text);
    }

    [Test]
    public void RewardOption_UsesGuaranteedUpgradeMessaging()
    {
        var option = new RewardOption
        {
            kind = RewardKind.OrbUpgrade,
            offerOrigin = RewardOfferOrigin.GuaranteedUpgrade,
            encounterType = CombatEncounterType.Boss
        };

        Assert.AreEqual("MEJORA ASEGURADA", option.SourceTag);
        StringAssert.Contains("Boss", option.SourceDescription);
        Assert.IsTrue(option.IsGuaranteed);
    }

    [Test]
    public void RewardOption_HealDisplay_IsReadable()
    {
        var option = new RewardOption
        {
            kind = RewardKind.Heal,
            healAmount = 12,
            encounterType = CombatEncounterType.Regular,
            offerOrigin = RewardOfferOrigin.StandardRoll
        };

        Assert.IsTrue(option.IsValid);
        Assert.AreEqual("Curacion +12 HP", option.DisplayName);
        StringAssert.Contains("12", option.DisplayDescription);
    }

    [Test]
    public void GenerateMixedChoices_ConvertsOwnedOrbDuplicateIntoUpgrade()
    {
        SetupRewardManager();

        testOrb = ScriptableObject.CreateInstance<OrbData>();
        testOrb.orbName = "Orbe de Prueba";
        testOrb.description = "Descripcion de prueba";
        orbManager.AddOrb(testOrb);

        SetPrivateField(rewardManager, "orbPool", new[] { testOrb });
        SetPrivateField(rewardManager, "relicPool", new ShotEffectBase[0]);
        SetPrivateField(rewardManager, "chanceOrb", 1f);
        SetPrivateField(rewardManager, "chanceOrbUpgrade", 0f);
        SetPrivateField(rewardManager, "allowHealRewards", false);

        RewardOption[] choices = InvokeGenerateChoices(1, CombatEncounterProfile.CreateRegular());

        Assert.AreEqual(1, choices.Length);
        Assert.AreEqual(RewardKind.OrbUpgrade, choices[0].kind);
        Assert.AreEqual(RewardOfferOrigin.DuplicateOrbUpgrade, choices[0].offerOrigin);
        Assert.AreSame(orbManager.FindOwnedOrbInstance(testOrb), choices[0].orbInstance);
    }

    [Test]
    public void GenerateMixedChoices_SkipsOwnedRelicsWhenAnotherIsAvailable()
    {
        SetupRewardManager();

        ownedRelic = ScriptableObject.CreateInstance<TestRewardRelic>();
        ownedRelic.name = "Owned Relic";
        offeredRelic = ScriptableObject.CreateInstance<TestRewardRelic>();
        offeredRelic.name = "Offered Relic";
        relicManager.AddRelic(ownedRelic);

        SetPrivateField(rewardManager, "orbPool", new OrbData[0]);
        SetPrivateField(rewardManager, "relicPool", new ShotEffectBase[] { ownedRelic, offeredRelic });
        SetPrivateField(rewardManager, "chanceOrb", 0f);
        SetPrivateField(rewardManager, "chanceOrbUpgrade", 0f);
        SetPrivateField(rewardManager, "allowHealRewards", false);

        RewardOption[] choices = InvokeGenerateChoices(1, CombatEncounterProfile.CreateRegular());

        Assert.AreEqual(1, choices.Length);
        Assert.AreEqual(RewardKind.Relic, choices[0].kind);
        Assert.AreSame(offeredRelic, choices[0].relic);
    }

    private void SetupRewardManager()
    {
        rewardGameObject = new GameObject("RewardManagerTest");
        rewardManager = rewardGameObject.AddComponent<RewardManager>();

        orbManagerGameObject = new GameObject("OrbManagerTest");
        orbManager = orbManagerGameObject.AddComponent<OrbManager>();

        relicManagerGameObject = new GameObject("RelicManagerTest");
        relicManager = relicManagerGameObject.AddComponent<RelicManager>();

        balanceConfig = ScriptableObject.CreateInstance<RunBalanceConfig>();

        SetPrivateField(rewardManager, "orbs", orbManager);
        SetPrivateField(rewardManager, "relics", relicManager);
        SetPrivateField(rewardManager, "balanceConfig", balanceConfig);
        SetPrivateField(rewardManager, "avoidDuplicatesInSameRoll", true);
        SetPrivateField(rewardManager, "allowOrbUpgrade", true);
    }

    private RewardOption[] InvokeGenerateChoices(int count, CombatEncounterProfile encounterProfile)
    {
        MethodInfo method = typeof(RewardManager).GetMethod("GenerateMixedChoices", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "GenerateMixedChoices reflection lookup failed.");
        return (RewardOption[])method.Invoke(rewardManager, new object[] { count, encounterProfile });
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' was not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static void DestroyImmediateIfNeeded(Object obj)
    {
        if (obj != null)
            Object.DestroyImmediate(obj);
    }

    private sealed class TestRewardRelic : ShotEffectBase
    {
    }
}
