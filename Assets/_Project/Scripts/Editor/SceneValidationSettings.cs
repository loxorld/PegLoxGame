using UnityEditor;

public static class SceneValidationSettings
{
    private const string KeyPrefix = "PegLox.SceneValidation";
    private const string CoreSeverityKey = KeyPrefix + ".CoreSeverity";
    private const string SerializedSeverityKey = KeyPrefix + ".SerializedSeverity";
    private const string StageSeverityKey = KeyPrefix + ".StageSeverity";
    private const string BlockBuildKey = KeyPrefix + ".BlockBuildOnErrors";
    private const string PlayModeHookKey = KeyPrefix + ".RunBeforePlayMode";

    public static ValidationSeverity CoreComponentSeverity
    {
        get => GetSeverity(CoreSeverityKey, ValidationSeverity.Error);
        set => SetSeverity(CoreSeverityKey, value);
    }

    public static ValidationSeverity SerializedReferenceSeverity
    {
        get => GetSeverity(SerializedSeverityKey, ValidationSeverity.Error);
        set => SetSeverity(SerializedSeverityKey, value);
    }

    public static ValidationSeverity MapStageConsistencySeverity
    {
        get => GetSeverity(StageSeverityKey, ValidationSeverity.Warning);
        set => SetSeverity(StageSeverityKey, value);
    }

    public static bool BlockBuildOnErrors
    {
        get => EditorPrefs.GetBool(BlockBuildKey, true);
        set => EditorPrefs.SetBool(BlockBuildKey, value);
    }

    public static bool RunBeforePlayMode
    {
        get => EditorPrefs.GetBool(PlayModeHookKey, true);
        set => EditorPrefs.SetBool(PlayModeHookKey, value);
    }

    private static ValidationSeverity GetSeverity(string key, ValidationSeverity fallback)
    {
        int value = EditorPrefs.GetInt(key, (int)fallback);
        if (value != (int)ValidationSeverity.Warning && value != (int)ValidationSeverity.Error)
            return fallback;

        return (ValidationSeverity)value;
    }

    private static void SetSeverity(string key, ValidationSeverity value)
    {
        EditorPrefs.SetInt(key, (int)value);
    }
}

public static class SceneValidationSettingsMenu
{
    [MenuItem("Tools/Validation/Settings/Block Build On Errors")]
    private static void ToggleBuildBlock()
    {
        SceneValidationSettings.BlockBuildOnErrors = !SceneValidationSettings.BlockBuildOnErrors;
    }

    [MenuItem("Tools/Validation/Settings/Block Build On Errors", true)]
    private static bool ToggleBuildBlockValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/Block Build On Errors", SceneValidationSettings.BlockBuildOnErrors);
        return true;
    }

    [MenuItem("Tools/Validation/Settings/Run Before Play Mode")]
    private static void TogglePlayModeValidation()
    {
        SceneValidationSettings.RunBeforePlayMode = !SceneValidationSettings.RunBeforePlayMode;
    }

    [MenuItem("Tools/Validation/Settings/Run Before Play Mode", true)]
    private static bool TogglePlayModeValidationValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/Run Before Play Mode", SceneValidationSettings.RunBeforePlayMode);
        return true;
    }

    [MenuItem("Tools/Validation/Settings/Core Components Severity/Error")]
    private static void SetCoreSeverityError() => SceneValidationSettings.CoreComponentSeverity = ValidationSeverity.Error;

    [MenuItem("Tools/Validation/Settings/Core Components Severity/Error", true)]
    private static bool SetCoreSeverityErrorValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/Core Components Severity/Error", SceneValidationSettings.CoreComponentSeverity == ValidationSeverity.Error);
        return true;
    }

    [MenuItem("Tools/Validation/Settings/Core Components Severity/Warning")]
    private static void SetCoreSeverityWarning() => SceneValidationSettings.CoreComponentSeverity = ValidationSeverity.Warning;

    [MenuItem("Tools/Validation/Settings/Core Components Severity/Warning", true)]
    private static bool SetCoreSeverityWarningValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/Core Components Severity/Warning", SceneValidationSettings.CoreComponentSeverity == ValidationSeverity.Warning);
        return true;
    }

    [MenuItem("Tools/Validation/Settings/Serialized Refs Severity/Error")]
    private static void SetSerializedSeverityError() => SceneValidationSettings.SerializedReferenceSeverity = ValidationSeverity.Error;

    [MenuItem("Tools/Validation/Settings/Serialized Refs Severity/Error", true)]
    private static bool SetSerializedSeverityErrorValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/Serialized Refs Severity/Error", SceneValidationSettings.SerializedReferenceSeverity == ValidationSeverity.Error);
        return true;
    }

    [MenuItem("Tools/Validation/Settings/Serialized Refs Severity/Warning")]
    private static void SetSerializedSeverityWarning() => SceneValidationSettings.SerializedReferenceSeverity = ValidationSeverity.Warning;

    [MenuItem("Tools/Validation/Settings/Serialized Refs Severity/Warning", true)]
    private static bool SetSerializedSeverityWarningValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/Serialized Refs Severity/Warning", SceneValidationSettings.SerializedReferenceSeverity == ValidationSeverity.Warning);
        return true;
    }

    [MenuItem("Tools/Validation/Settings/MapStage Consistency Severity/Error")]
    private static void SetStageSeverityError() => SceneValidationSettings.MapStageConsistencySeverity = ValidationSeverity.Error;

    [MenuItem("Tools/Validation/Settings/MapStage Consistency Severity/Error", true)]
    private static bool SetStageSeverityErrorValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/MapStage Consistency Severity/Error", SceneValidationSettings.MapStageConsistencySeverity == ValidationSeverity.Error);
        return true;
    }

    [MenuItem("Tools/Validation/Settings/MapStage Consistency Severity/Warning")]
    private static void SetStageSeverityWarning() => SceneValidationSettings.MapStageConsistencySeverity = ValidationSeverity.Warning;

    [MenuItem("Tools/Validation/Settings/MapStage Consistency Severity/Warning", true)]
    private static bool SetStageSeverityWarningValidate()
    {
        Menu.SetChecked("Tools/Validation/Settings/MapStage Consistency Severity/Warning", SceneValidationSettings.MapStageConsistencySeverity == ValidationSeverity.Warning);
        return true;
    }
}