using UnityEngine;
using UnityEngine.SceneManagement;

public partial class GameFlowManager
{
    public void SaveRun()
    {
        OrbManager orbManagerInstance = ResolveOrbManager();
        RelicManager relicManagerInstance = ResolveRelicManager();
        ResolveRunPersistenceService().Save(
            runState,
            orbManagerInstance != null ? orbManagerInstance.SerializeOrbs() : null,
            orbManagerInstance != null ? orbManagerInstance.GetCurrentOrbId() : null,
            relicManagerInstance != null ? relicManagerInstance.SerializeRelics() : null);
    }

    public bool LoadRun()
    {
        RunPersistenceService persistenceService = ResolveRunPersistenceService();
        if (persistenceService == null)
            return false;

        if (!persistenceService.TryLoad(out RunSaveData data))
            return false;

        StageLoadedRunSnapshot(data);
        return true;
    }

    private void StageLoadedRunSnapshot(RunSaveData data)
    {
        ApplyRunSnapshot(data);

        pendingRunData = data;
        pendingOrbApply = true;
        pendingRelicApply = true;

        hasLoadedRun = true;
        GameState loadedState = (GameState)Mathf.Clamp(data.GameState, 0, (int)GameState.GameOver);
        pendingLoadedState = NormalizeLoadedState(loadedState);
        pendingStateApply = true;
    }

    private static GameState NormalizeLoadedState(GameState loadedState)
    {
        return RunPersistenceService.NormalizeLoadedState(loadedState);
    }

    private void ApplyRunSnapshot(RunSaveData data)
    {
        pendingMapNodeId = data.SavedMapNodeId;
        ResolveRunPersistenceService().ApplyRunSnapshot(runState, data, ResolveMapNodeById);
        ValidateEncounterState("ApplyRunData");
    }

    private void ApplyManagersFromRunData()
    {
        if (pendingRunData == null)
            return;

        OrbManager orbManagerInstance = ResolveOrbManager();
        if (pendingOrbApply && orbManagerInstance != null)
        {
            orbManagerInstance.DeserializeOrbs(pendingRunData.Orbs, pendingRunData.CurrentOrbId);
            pendingOrbApply = false;
        }

        RelicManager relicManagerInstance = ResolveRelicManager();
        if (pendingRelicApply && relicManagerInstance != null)
        {
            relicManagerInstance.DeserializeRelics(pendingRunData.Relics);
            pendingRelicApply = false;
        }

        if (!pendingOrbApply && !pendingRelicApply)
            pendingRunData = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasLoadedRun)
            return;

        BuildMapNodeResolutionCache();
        TryRestorePendingSavedMapNode();

        if (pendingStateApply)
            StartCoroutine(ApplyLoadedStateNextFrame());
        else
            ApplyManagersFromRunData();

        TryInitializeMapForCurrentState();
    }

    private System.Collections.IEnumerator ApplyLoadedStateNextFrame()
    {
        yield return null;
        BuildMapNodeResolutionCache();
        TryRestorePendingSavedMapNode();
        ApplyManagersFromRunData();

        pendingStateApply = false;
        SetState(pendingLoadedState);
    }

    private void TryRestorePendingSavedMapNode()
    {
        if (SavedMapNode != null || string.IsNullOrWhiteSpace(pendingMapNodeId))
            return;

        bool allowLegacyNameMigration = pendingRunData != null && pendingRunData.IsLegacySave();
        SavedMapNode = ResolveMapNodeById(pendingMapNodeId, allowLegacyNameMigration);
    }

    private void BuildMapNodeResolutionCache()
    {
        mapNodesByPersistentId.Clear();
        mapNodesByLegacyName.Clear();

        MapNodeData[] candidates = Resources.LoadAll<MapNodeData>(string.Empty);
        for (int i = 0; i < candidates.Length; i++)
            CacheMapNodeCandidate(candidates[i]);

        MapNodeData[] loadedCandidates = Resources.FindObjectsOfTypeAll<MapNodeData>();
        for (int i = 0; i < loadedCandidates.Length; i++)
            CacheMapNodeCandidate(loadedCandidates[i]);
    }

    private void CacheMapNodeCandidate(MapNodeData node)
    {
        if (node == null)
            return;

        string persistentId = BuildPersistentMapNodeId(node);
        if (!string.IsNullOrWhiteSpace(persistentId) && !mapNodesByPersistentId.ContainsKey(persistentId))
            mapNodesByPersistentId[persistentId] = node;

        string legacyName = string.IsNullOrWhiteSpace(node.name) ? null : node.name.Trim();
        if (!string.IsNullOrWhiteSpace(legacyName) && !mapNodesByLegacyName.ContainsKey(legacyName))
            mapNodesByLegacyName[legacyName] = node;
    }

    private MapNodeData ResolveMapNodeById(string mapNodeId, bool allowLegacyNameMigration)
    {
        if (string.IsNullOrWhiteSpace(mapNodeId))
            return null;

        if (mapNodesByPersistentId.TryGetValue(mapNodeId, out MapNodeData nodeByPersistentId) && nodeByPersistentId != null)
            return nodeByPersistentId;

        if (mapNodesByLegacyName.TryGetValue(mapNodeId, out MapNodeData nodeByLegacyName) && nodeByLegacyName != null)
        {
            string reason = allowLegacyNameMigration ? "Migración save legacy" : "Fallback de compatibilidad";
            Debug.LogWarning($"[GameFlow] {reason}: MapNodeData '{mapNodeId}' resuelto por name. Reguardar para persistir persistentId.");
            return nodeByLegacyName;
        }

        return null;
    }

    private static string BuildEventOptionCounterKey(MapStage stage, MapNodeData node, string optionLabel, MapDomainService.EventResolutionOutcome appliedOutcome)
    {
        string stageId = stage != null && !string.IsNullOrWhiteSpace(stage.name) ? stage.name.Trim() : "unknown-stage";
        string nodeId = node != null && !string.IsNullOrWhiteSpace(node.name) ? node.name.Trim() : "unknown-node";
        string optionId = string.IsNullOrWhiteSpace(optionLabel) ? "unknown-option" : optionLabel.Trim();
        string outcomeId = BuildOutcomeId(appliedOutcome);
        return $"{stageId}|{nodeId}|{optionId}|{outcomeId}";
    }

    private static string BuildOutcomeId(MapDomainService.EventResolutionOutcome outcome)
    {
        string description = string.IsNullOrWhiteSpace(outcome.ResultDescription)
            ? "no-description"
            : outcome.ResultDescription.Trim();
        return $"c{outcome.CoinDelta}_h{outcome.HpDelta}_{description}";
    }

    private static bool TryBuildMapNodeId(MapNodeData node, out string nodeId)
    {
        nodeId = BuildPersistentMapNodeId(node);
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            Debug.LogWarning("[GameFlow] No se pudo generar un id estable para MapNodeData (persistentId/name vacío).");
            return false;
        }

        return true;
    }

    private static string BuildPersistentMapNodeId(MapNodeData node)
    {
        return RunPersistenceService.BuildPersistentMapNodeId(node);
    }
}
