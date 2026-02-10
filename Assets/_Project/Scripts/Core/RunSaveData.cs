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

    public string SavedMapNodeId;
    public int EncounterIndex;
    public int EncounterInStageIndex;
    public int CurrentStageIndex;
    public string CurrentStageName;
    public int NodesVisited;
    public int Coins;
    public int PlayerMaxHP;
    public int SavedPlayerHP;
    public bool HasSavedPlayerHP;
    public int GameState;
    public string CurrentOrbId;
    public List<OrbSaveData> Orbs = new List<OrbSaveData>();
    public List<string> Relics = new List<string>();
}