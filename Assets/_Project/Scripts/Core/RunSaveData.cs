using System;
using System.Collections.Generic;

[Serializable]
public class RunSaveData
{
    [Serializable]
    public class OrbSaveData
    {
        public string OrbId;
        public int Level;
    }

    [Serializable]
    public class ShopOfferSaveData
    {
        public string OfferId;
        public int OfferType;
        public int Cost;
        public int PrimaryValue;
        public int RemainingStock;
        public int Rarity;
        public bool RequiresMissingHp;
        public bool RequiresUpgradableOrb;
        public bool RequiresAnyOrb;
    }

    [Serializable]
    public class ShopCatalogSaveData
    {
        public string ShopId;
        public List<ShopOfferSaveData> Offers = new List<ShopOfferSaveData>();
    }

    [Serializable]
    public class EventOptionCounterSaveData
    {
        public string CounterKey;
        public int Count;
    }

    public string SavedMapNodeId;
    public int EncounterIndex;
    public int EncounterInStageIndex;
    public int CurrentStageIndex;
    public int NodesVisited;
    public int Coins;
    public int PlayerMaxHP;
    public int SavedPlayerHP;
    public bool HasSavedPlayerHP;
    public int GameState;
    public string CurrentOrbId;
    public List<OrbSaveData> Orbs = new List<OrbSaveData>();
    public List<string> Relics = new List<string>();
    public List<ShopCatalogSaveData> ShopCatalogs = new List<ShopCatalogSaveData>();
    public List<string> ResolvedEventNodeIds = new List<string>();
    public List<EventOptionCounterSaveData> EventOptionCounters = new List<EventOptionCounterSaveData>();
}