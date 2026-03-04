using UnityEngine;

[CreateAssetMenu(menuName = "PeglinLike/Board/Board Generation Profile", fileName = "BoardGenerationProfile_")]
public class BoardGenerationProfile : ScriptableObject
{
    public enum SymmetryRule
    {
        None = 0,
        MirrorHorizontal = 1,
        MirrorVertical = 2,
        MirrorBoth = 3,
        Rotational180 = 4
    }

    [System.Serializable]
    public class LayoutWeight
    {
        public BoardLayout layout;
        [Min(0f)] public float weight = 1f;
    }

    [System.Serializable]
    public class SpecialLimit
    {
        public PegDefinition definition;
        [Min(0)] public int maxPerBoard = 0;
    }

    [System.Serializable]
    public class EncounterProfile
    {
        public string profileId = "default";
        [Tooltip("-1 = cualquier stage")]
        public int stageIndex = -1;
        [Tooltip("-1 = cualquier encounter global")]
        public int encounterIndex = -1;
        [Tooltip("-1 = cualquier encounter dentro del stage")]
        public int encounterInStage = -1;

        [Header("Layout")]
        public LayoutWeight[] allowedLayouts;
        [Range(0f, 1f)] public float targetDensity = 1f;
        [Range(0f, 0.45f)] public float jitter = 0.15f;
        public SymmetryRule symmetryRule = SymmetryRule.None;

        [Header("Peg Probabilities")]
        [Range(0f, 1f)] public float criticalChance = 0.15f;
        [Range(0f, 1f)] public float specialChance = 0.10f;

        [Header("Special Peg Limits")]
        public SpecialLimit[] specialLimits;

        public bool Matches(int currentStageIndex, int currentEncounterIndex, int currentEncounterInStage)
        {
            if (stageIndex >= 0 && stageIndex != currentStageIndex) return false;
            if (encounterIndex >= 0 && encounterIndex != currentEncounterIndex) return false;
            if (encounterInStage >= 0 && encounterInStage != currentEncounterInStage) return false;
            return true;
        }
    }

    [Header("Ordered Profiles")]
    [Tooltip("Se evalúan en orden, se usa el primer match por contexto.")]
    public EncounterProfile[] profiles;

    public bool TryGetProfile(int stageIndex, int encounterIndex, int encounterInStage, out EncounterProfile selected)
    {
        selected = null;
        if (profiles == null || profiles.Length == 0)
            return false;

        for (int i = 0; i < profiles.Length; i++)
        {
            EncounterProfile candidate = profiles[i];
            if (candidate == null)
                continue;

            if (!candidate.Matches(stageIndex, encounterIndex, encounterInStage))
                continue;

            selected = candidate;
            return true;
        }

        return false;
    }
}