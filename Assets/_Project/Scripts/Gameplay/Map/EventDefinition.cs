using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EventDefinition", menuName = "Map/Event Definition")]
public class EventDefinition : ScriptableObject
{
    public string title;

    [TextArea]
    public string description;

    public EventCondition conditions;

    public EventOptionDefinition[] options;

    [Serializable]
    public struct EventCondition
    {
        public bool useStageRange;
        public int minStageIndex;
        public int maxStageIndex;

        public bool Matches(int stageIndex)
        {
            if (!useStageRange)
                return true;

            if (minStageIndex >= 0 && stageIndex < minStageIndex)
                return false;

            if (maxStageIndex >= 0 && stageIndex > maxStageIndex)
                return false;

            return true;
        }
    }

    [Serializable]
    public struct EventOptionDefinition
    {
        public string optionLabel;
        [Range(0f, 1f)] public float successProbability;
        public bool useSuccessProbability;
        public EventOutcomeDefinition successOutcome;
        public EventOutcomeDefinition failureOutcome;
    }

    [Serializable]
    public struct EventOutcomeDefinition
    {
        public int coinDelta;
        public int hpDelta;

        [TextArea]
        public string resultDescription;
    }
}