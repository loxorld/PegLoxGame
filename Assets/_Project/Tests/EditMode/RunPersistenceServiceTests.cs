using System.Collections.Generic;
using NUnit.Framework;

public class RunPersistenceServiceTests
{
    [Test]
    public void BuildRunSnapshot_ClampsAndSerializesRunState()
    {
        var state = new RunState
        {
            CurrentGameState = GameState.MapNavigation,
            EncounterIndex = -5,
            EncounterInStageIndex = 2,
            CurrentStageIndex = 3,
            NodesVisited = 4,
            Coins = -10,
            PlayerMaxHP = 0,
            SavedPlayerHP = 500,
            HasSavedPlayerHP = true
        };

        state.EventOptionCounters["k1"] = -2;
        state.ResolvedEventNodeIds.Add("n1");
        state.ShopCatalogsById["shop"] = new List<GameFlowManager.ShopOfferRunData>
        {
            new GameFlowManager.ShopOfferRunData { OfferId = "o1", Cost = 5, RemainingStock = 1 }
        };

        RunPersistenceService service = new RunPersistenceService(new FakeGateway());

        RunSaveData data = service.BuildRunSnapshot(state, null, null, null);

        Assert.AreEqual(0, data.EncounterIndex);
        Assert.AreEqual(0, data.Coins);
        Assert.AreEqual(1, data.PlayerMaxHP);
        Assert.AreEqual(1, data.SavedPlayerHP);
        Assert.AreEqual(1, data.ShopCatalogs.Count);
        Assert.AreEqual(1, data.ResolvedEventNodeIds.Count);
    }

    [Test]
    public void ApplyRunSnapshot_UpdatesRunStateFromSave()
    {
        RunPersistenceService service = new RunPersistenceService(new FakeGateway());
        RunState state = new RunState();
        RunSaveData data = new RunSaveData
        {
            EncounterIndex = 6,
            EncounterInStageIndex = 2,
            CurrentStageIndex = 1,
            NodesVisited = 9,
            Coins = 40,
            PlayerMaxHP = 120,
            SavedPlayerHP = 80,
            HasSavedPlayerHP = true,
            ShopCatalogs = new List<RunSaveData.ShopCatalogSaveData>
            {
                new RunSaveData.ShopCatalogSaveData
                {
                    ShopId = "shop_1",
                    Offers = new List<RunSaveData.ShopOfferSaveData> { new RunSaveData.ShopOfferSaveData { OfferId = "offer" } }
                }
            }
        };

        service.ApplyRunSnapshot(state, data, (_, __) => null);

        Assert.AreEqual(6, state.EncounterIndex);
        Assert.AreEqual(40, state.Coins);
        Assert.AreEqual(120, state.PlayerMaxHP);
        Assert.AreEqual(80, state.SavedPlayerHP);
        Assert.IsTrue(state.ShopCatalogsById.ContainsKey("shop_1"));
    }

    private sealed class FakeGateway : IRunSaveGateway
    {
        public void Save(RunSaveData data) { }
        public bool TryLoad(out RunSaveData data)
        {
            data = null;
            return false;
        }
    }
}
