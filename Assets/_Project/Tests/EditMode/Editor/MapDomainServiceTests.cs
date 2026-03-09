using NUnit.Framework;
using UnityEngine;

public class MapDomainServiceTests
{
    [TearDown]
    public void TearDown()
    {
        DestroyImmediateIfNeeded(testStage);
        DestroyImmediateIfNeeded(startNode);
        DestroyImmediateIfNeeded(midNode);
        DestroyImmediateIfNeeded(leafNode);
        DestroyImmediateIfNeeded(detachedNode);
        DestroyImmediateIfNeeded(shopNode);
        DestroyImmediateIfNeeded(eventNode);
        DestroyImmediateIfNeeded(directEventDefinition);
        DestroyImmediateIfNeeded(pooledEventDefinition);
    }

    [Test]
    public void ResolveCurrentNode_ReturnsReachableLeafNode_WhenSavedNodeHasNoOutgoingConnections()
    {
        var service = new MapDomainService();
        CreateLinearStage();

        MapDomainService.NodeResolution resolution = service.ResolveCurrentNode(testStage, leafNode);

        Assert.AreSame(leafNode, resolution.Node);
        Assert.IsFalse(resolution.ShouldClearSavedNode);
    }

    [Test]
    public void ResolveCurrentNode_FallsBackToStartingNode_WhenSavedNodeIsDetached()
    {
        var service = new MapDomainService();
        CreateLinearStage();
        detachedNode = ScriptableObject.CreateInstance<MapNodeData>();

        MapDomainService.NodeResolution resolution = service.ResolveCurrentNode(testStage, detachedNode);

        Assert.AreSame(startNode, resolution.Node);
        Assert.IsTrue(resolution.ShouldClearSavedNode);
    }

    [Test]
    public void ShouldForceBossNode_ReturnsBossNode_WhenThresholdReached()
    {
        var service = new MapDomainService();
        CreateLinearStage();

        bool shouldForceBoss = service.ShouldForceBossNode(testStage, nodesVisited: 3, bossAfterNodes: 3, out MapNodeData resolvedBossNode);

        Assert.IsTrue(shouldForceBoss);
        Assert.AreSame(leafNode, resolvedBossNode);
    }

    [Test]
    public void BuildShopOutcome_UsesExtraMessageWithoutLeadingLineBreak()
    {
        var service = new MapDomainService();
        shopNode = ScriptableObject.CreateInstance<MapNodeData>();
        shopNode.title = "Mercader";
        shopNode.description = string.Empty;

        MapDomainService.ShopOutcome outcome = service.BuildShopOutcome(
            shopNode,
            null,
            stageIndex: 0,
            coins: 22,
            fallbackHealCost: 10,
            fallbackHealAmount: 7,
            fallbackOrbUpgradeCost: 15,
            extraMessage: "Oferta especial");

        Assert.AreEqual("Oferta especial", outcome.Description);
        Assert.AreEqual(22, outcome.CurrentCoins);
        Assert.AreEqual(10, outcome.HealCost);
        Assert.AreEqual(7, outcome.HealAmount);
        Assert.AreEqual(15, outcome.OrbUpgradeCost);
    }

    [Test]
    public void EvaluateEventOptionAvailability_ReportsMissingCoinsHpAndRelic()
    {
        var service = new MapDomainService();
        var option = new MapDomainService.EventOptionOutcome(
            "Abrir santuario",
            coinDelta: 0,
            hpDelta: 0,
            resultDescription: "Nada cambia.",
            probability: null,
            requirement: new MapDomainService.EventOptionRequirement(minCoins: 20, minHp: 8, requiredRelicId: "GoldenIdol"));

        MapDomainService.EventOptionAvailability availability = service.EvaluateEventOptionAvailability(
            option,
            new MapDomainService.EventOptionContext(coins: 5, currentHp: 4, relicIds: new[] { "RustyKey" }));

        Assert.IsFalse(availability.IsAvailable);
        Assert.AreEqual("Requiere: 20 monedas, 8 HP, Reliquia 'GoldenIdol'", availability.MissingRequirementText);
    }

    [Test]
    public void BuildEventOutcome_UsesDefinitionPoolForMatchingStage()
    {
        var service = new MapDomainService();
        eventNode = ScriptableObject.CreateInstance<MapNodeData>();
        eventNode.title = "Ruinas";
        eventNode.description = "Descripcion fallback";
        directEventDefinition = CreateEventDefinition("Evento temprano", "Solo primer stage", stageIndex: 0, optionLabel: "Inspeccionar");
        pooledEventDefinition = CreateEventDefinition("Evento avanzado", "Disponible mas adelante", stageIndex: 2, optionLabel: "Explorar");

        eventNode.eventDefinition = directEventDefinition;
        eventNode.eventDefinitionPool = new[] { pooledEventDefinition };

        MapDomainService.EventScenarioOutcome outcome = service.BuildEventOutcome(
            eventNode,
            null,
            stageIndex: 2,
            eventCoinsRewardMin: 1,
            eventCoinsRewardMax: 1,
            eventCoinsPenaltyMin: 1,
            eventCoinsPenaltyMax: 1,
            eventHealMin: 1,
            eventHealMax: 1,
            eventDamageMin: 1,
            eventDamageMax: 1);

        Assert.AreEqual("Evento avanzado", outcome.Title);
        Assert.AreEqual("Disponible mas adelante", outcome.Description);
        Assert.AreEqual(1, outcome.Options.Count);
        Assert.AreEqual("Explorar", outcome.Options[0].OptionLabel);
    }

    [Test]
    public void ResolveEventOptionOutcome_ReturnsFailure_WhenRollExceedsProbability()
    {
        var service = new MapDomainService();
        var option = new MapDomainService.EventOptionOutcome(
            "Tomar riesgo",
            probability: 0.35f,
            successOutcome: new MapDomainService.EventResolutionOutcome(coinDelta: 12, hpDelta: 0, resultDescription: "Sale bien."),
            failureOutcome: new MapDomainService.EventResolutionOutcome(coinDelta: 0, hpDelta: -4, resultDescription: "Sale mal."));

        MapDomainService.EventResolutionOutcome outcome = service.ResolveEventOptionOutcome(option, roll: 0.8f);

        Assert.AreEqual(0, outcome.CoinDelta);
        Assert.AreEqual(-4, outcome.HpDelta);
        Assert.AreEqual("Sale mal.", outcome.ResultDescription);
    }

    private MapStage testStage;
    private MapNodeData startNode;
    private MapNodeData midNode;
    private MapNodeData leafNode;
    private MapNodeData detachedNode;
    private MapNodeData shopNode;
    private MapNodeData eventNode;
    private EventDefinition directEventDefinition;
    private EventDefinition pooledEventDefinition;

    private void CreateLinearStage()
    {
        startNode = ScriptableObject.CreateInstance<MapNodeData>();
        midNode = ScriptableObject.CreateInstance<MapNodeData>();
        leafNode = ScriptableObject.CreateInstance<MapNodeData>();

        startNode.nextNodes = new[]
        {
            new MapNodeConnection { targetNode = midNode }
        };
        midNode.nextNodes = new[]
        {
            new MapNodeConnection { targetNode = leafNode }
        };
        leafNode.nextNodes = null;

        testStage = ScriptableObject.CreateInstance<MapStage>();
        testStage.startingNode = startNode;
        testStage.bossNode = leafNode;
    }

    private static EventDefinition CreateEventDefinition(string title, string description, int stageIndex, string optionLabel)
    {
        var definition = ScriptableObject.CreateInstance<EventDefinition>();
        definition.title = title;
        definition.description = description;
        definition.conditions = new EventDefinition.EventCondition
        {
            useStageRange = true,
            minStageIndex = stageIndex,
            maxStageIndex = stageIndex
        };
        definition.options = new[]
        {
            new EventDefinition.EventOptionDefinition
            {
                optionLabel = optionLabel,
                useSuccessProbability = false,
                successOutcome = new EventDefinition.EventOutcomeDefinition
                {
                    coinDelta = 3,
                    hpDelta = 0,
                    resultDescription = "Resultado"
                },
                failureOutcome = new EventDefinition.EventOutcomeDefinition
                {
                    coinDelta = 0,
                    hpDelta = 0,
                    resultDescription = string.Empty
                }
            }
        };
        return definition;
    }

    private static void DestroyImmediateIfNeeded(Object obj)
    {
        if (obj != null)
            Object.DestroyImmediate(obj);
    }
}
