using System;
using System.Collections.Generic;
using UnityEngine;

public interface IRunSaveGateway
{
    void Save(RunSaveData data);
    bool TryLoad(out RunSaveData data);
}

public sealed class RunSaveGateway : IRunSaveGateway
{
    private readonly RunSaveService runSaveService;

    public RunSaveGateway(RunSaveService runSaveService)
    {
        this.runSaveService = runSaveService;
    }

    public void Save(RunSaveData data) => runSaveService?.Save(data);

    public bool TryLoad(out RunSaveData data)
    {
        data = null;
        return runSaveService != null && runSaveService.TryLoad(out data);
    }
}

public class RunPersistenceService
{
    private readonly IRunSaveGateway saveGateway;

    public RunPersistenceService(IRunSaveGateway saveGateway)
    {
        this.saveGateway = saveGateway;
    }

    public void Save(RunState state, List<RunSaveData.OrbSaveData> orbs, string currentOrbId, List<string> relics)
    {
        if (state == null)
            return;

        saveGateway?.Save(BuildRunSnapshot(state, orbs, currentOrbId, relics));
    }

    public bool TryLoad(out RunSaveData data)
    {
        data = null;
        return saveGateway != null && saveGateway.TryLoad(out data);
    }

    public RunSaveData BuildRunSnapshot(RunState state, List<RunSaveData.OrbSaveData> orbs, string currentOrbId, List<string> relics)
    {
        RunSaveData data = new RunSaveData
        {
            SaveVersion = RunSaveData.CurrentVersion,
            SavedMapNodeId = BuildPersistentMapNodeId(state.SavedMapNode),
            EncounterIndex = Mathf.Max(0, state.EncounterIndex),
            EncounterInStageIndex = Mathf.Max(0, state.EncounterInStageIndex),
            CurrentStageIndex = Mathf.Max(0, state.CurrentStageIndex),
            NodesVisited = Mathf.Max(0, state.NodesVisited),
            Coins = Mathf.Max(0, state.Coins),
            PlayerMaxHP = Mathf.Max(1, state.PlayerMaxHP),
            SavedPlayerHP = Mathf.Max(0, state.SavedPlayerHP),
            HasSavedPlayerHP = state.HasSavedPlayerHP,
            GameState = (int)state.CurrentGameState,
            CurrentOrbId = currentOrbId,
            Orbs = orbs ?? new List<RunSaveData.OrbSaveData>(),
            Relics = relics ?? new List<string>(),
            ShopCatalogs = SerializeShopCatalogs(state.ShopCatalogsById),
            ResolvedEventNodeIds = new List<string>(state.ResolvedEventNodeIds),
            EventOptionCounters = SerializeEventOptionCounters(state.EventOptionCounters)
        };

        data.SavedPlayerHP = Mathf.Clamp(data.SavedPlayerHP, 0, data.PlayerMaxHP);
        return data;
    }

    public void ApplyRunSnapshot(RunState state, RunSaveData data, Func<string, bool, MapNodeData> mapNodeResolver)
    {
        if (state == null || data == null)
            return;

        state.SavedMapNode = mapNodeResolver?.Invoke(data.SavedMapNodeId, data.IsLegacySave());
        state.EncounterIndex = Mathf.Max(0, data.EncounterIndex);
        state.EncounterInStageIndex = Mathf.Max(0, data.EncounterInStageIndex);
        state.CurrentStageIndex = Mathf.Max(0, data.CurrentStageIndex);
        state.NodesVisited = Mathf.Max(0, data.NodesVisited);
        state.Coins = Mathf.Max(0, data.Coins);
        state.PlayerMaxHP = Mathf.Max(1, data.PlayerMaxHP);
        state.SavedPlayerHP = Mathf.Clamp(data.SavedPlayerHP, 0, state.PlayerMaxHP);
        state.HasSavedPlayerHP = data.HasSavedPlayerHP;

        DeserializeShopCatalogs(state.ShopCatalogsById, data.ShopCatalogs);
        DeserializeResolvedEventNodes(state.ResolvedEventNodeIds, data.ResolvedEventNodeIds);
        DeserializeEventOptionCounters(state.EventOptionCounters, data.EventOptionCounters);
    }

    public static GameState NormalizeLoadedState(GameState loadedState)
    {
        if (loadedState == GameState.Paused || loadedState == GameState.GameOver)
            return GameState.Combat;

        return loadedState;
    }

    public static string BuildPersistentMapNodeId(MapNodeData node)
    {
        if (node == null)
            return null;

        if (!string.IsNullOrWhiteSpace(node.PersistentId))
            return node.PersistentId.Trim();

        return string.IsNullOrWhiteSpace(node.name) ? null : node.name.Trim();
    }

    private static List<RunSaveData.ShopCatalogSaveData> SerializeShopCatalogs(Dictionary<string, List<GameFlowManager.ShopOfferRunData>> shopCatalogsById)
    {
        var serialized = new List<RunSaveData.ShopCatalogSaveData>();
        foreach (KeyValuePair<string, List<GameFlowManager.ShopOfferRunData>> entry in shopCatalogsById)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                continue;

            var catalogData = new RunSaveData.ShopCatalogSaveData { ShopId = entry.Key };
            for (int i = 0; i < entry.Value.Count; i++)
            {
                GameFlowManager.ShopOfferRunData offer = entry.Value[i];
                if (offer == null || string.IsNullOrWhiteSpace(offer.OfferId))
                    continue;

                catalogData.Offers.Add(new RunSaveData.ShopOfferSaveData
                {
                    OfferId = offer.OfferId,
                    OfferType = (int)offer.OfferType,
                    Cost = Mathf.Max(0, offer.Cost),
                    PrimaryValue = offer.PrimaryValue,
                    RemainingStock = Mathf.Max(0, offer.RemainingStock),
                    Rarity = (int)offer.Rarity,
                    RequiresMissingHp = offer.RequiresMissingHp,
                    RequiresUpgradableOrb = offer.RequiresUpgradableOrb,
                    RequiresAnyOrb = offer.RequiresAnyOrb
                });
            }

            serialized.Add(catalogData);
        }

        return serialized;
    }

    private static List<RunSaveData.EventOptionCounterSaveData> SerializeEventOptionCounters(Dictionary<string, int> eventOptionCounters)
    {
        var serialized = new List<RunSaveData.EventOptionCounterSaveData>(eventOptionCounters.Count);
        foreach (KeyValuePair<string, int> entry in eventOptionCounters)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
                continue;

            serialized.Add(new RunSaveData.EventOptionCounterSaveData
            {
                CounterKey = entry.Key,
                Count = Mathf.Max(0, entry.Value)
            });
        }

        return serialized;
    }

    private static void DeserializeShopCatalogs(Dictionary<string, List<GameFlowManager.ShopOfferRunData>> shopCatalogsById, List<RunSaveData.ShopCatalogSaveData> serialized)
    {
        shopCatalogsById.Clear();
        if (serialized == null)
            return;

        for (int i = 0; i < serialized.Count; i++)
        {
            RunSaveData.ShopCatalogSaveData catalogData = serialized[i];
            if (catalogData == null || string.IsNullOrWhiteSpace(catalogData.ShopId))
                continue;

            var offers = new List<GameFlowManager.ShopOfferRunData>();
            List<RunSaveData.ShopOfferSaveData> serializedOffers = catalogData.Offers;
            if (serializedOffers != null)
            {
                for (int j = 0; j < serializedOffers.Count; j++)
                {
                    RunSaveData.ShopOfferSaveData offer = serializedOffers[j];
                    if (offer == null || string.IsNullOrWhiteSpace(offer.OfferId))
                        continue;

                    offers.Add(new GameFlowManager.ShopOfferRunData
                    {
                        OfferId = offer.OfferId,
                        OfferType = (ShopService.ShopOfferType)Mathf.Max(0, offer.OfferType),
                        Cost = Mathf.Max(0, offer.Cost),
                        PrimaryValue = offer.PrimaryValue,
                        RemainingStock = Mathf.Max(0, offer.RemainingStock),
                        Rarity = (ShopService.ShopOfferRarity)Mathf.Clamp(offer.Rarity, 0, (int)ShopService.ShopOfferRarity.Legendary),
                        RequiresMissingHp = offer.RequiresMissingHp,
                        RequiresUpgradableOrb = offer.RequiresUpgradableOrb,
                        RequiresAnyOrb = offer.RequiresAnyOrb
                    });
                }
            }

            shopCatalogsById[catalogData.ShopId] = offers;
        }
    }

    private static void DeserializeResolvedEventNodes(HashSet<string> resolvedEventNodeIds, List<string> serializedNodeIds)
    {
        resolvedEventNodeIds.Clear();
        if (serializedNodeIds == null)
            return;

        for (int i = 0; i < serializedNodeIds.Count; i++)
        {
            string nodeId = serializedNodeIds[i];
            if (!string.IsNullOrWhiteSpace(nodeId))
                resolvedEventNodeIds.Add(nodeId);
        }
    }

    private static void DeserializeEventOptionCounters(Dictionary<string, int> eventOptionCounters, List<RunSaveData.EventOptionCounterSaveData> serializedCounters)
    {
        eventOptionCounters.Clear();
        if (serializedCounters == null)
            return;

        for (int i = 0; i < serializedCounters.Count; i++)
        {
            RunSaveData.EventOptionCounterSaveData counterData = serializedCounters[i];
            if (counterData == null || string.IsNullOrWhiteSpace(counterData.CounterKey))
                continue;

            eventOptionCounters[counterData.CounterKey] = Mathf.Max(0, counterData.Count);
        }
    }
}
