using System;
using System.Collections.Generic;

[Serializable]
public class RunState
{
    public GameState CurrentGameState = GameState.Combat;
    public MapNodeData SavedMapNode;
    public int NodesVisited;
    public int EncounterIndex;
    public int EncounterInStageIndex;
    public int CurrentStageIndex;
    public bool HasSavedPlayerHP;
    public int SavedPlayerHP;
    public int Coins;
    public int PlayerMaxHP = 100;
    public readonly Dictionary<string, List<GameFlowManager.ShopOfferRunData>> ShopCatalogsById = new Dictionary<string, List<GameFlowManager.ShopOfferRunData>>();
    public readonly HashSet<string> ResolvedEventNodeIds = new HashSet<string>(StringComparer.Ordinal);
    public readonly Dictionary<string, int> EventOptionCounters = new Dictionary<string, int>(StringComparer.Ordinal);
}
