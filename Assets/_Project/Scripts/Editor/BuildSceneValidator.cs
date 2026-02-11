
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ValidationSeverity
{
    Warning,
    Error
}

public readonly struct SceneValidationIssue
{
    public readonly ValidationSeverity Severity;
    public readonly string ScenePath;
    public readonly string GameObjectPath;
    public readonly string ComponentName;
    public readonly string FieldName;
    public readonly string Message;

    public SceneValidationIssue(
        ValidationSeverity severity,
        string scenePath,
        string gameObjectPath,
        string componentName,
        string fieldName,
        string message)
    {
        Severity = severity;
        ScenePath = scenePath;
        GameObjectPath = gameObjectPath;
        ComponentName = componentName;
        FieldName = fieldName;
        Message = message;
    }

    public string ToConsoleLine()
    {
        return $"[{Severity}] Scene='{ScenePath}' GO='{GameObjectPath}' Component='{ComponentName}' Field='{FieldName}' -> {Message}";
    }
}

public sealed class SceneValidationReport
{
    public readonly List<SceneValidationIssue> Issues = new();

    public int ErrorCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Severity == ValidationSeverity.Error)
                    count++;
            }

            return count;
        }
    }

    public int WarningCount => Issues.Count - ErrorCount;

    public bool HasErrors => ErrorCount > 0;

    public string BuildSummaryLine()
    {
        return $"Validation finished. Issues={Issues.Count} Errors={ErrorCount} Warnings={WarningCount}";
    }

    public void LogToConsole(string prefix)
    {
        for (int i = 0; i < Issues.Count; i++)
        {
            SceneValidationIssue issue = Issues[i];
            string line = $"{prefix} {issue.ToConsoleLine()}";
            if (issue.Severity == ValidationSeverity.Error)
                Debug.LogError(line);
            else
                Debug.LogWarning(line);
        }

        Debug.Log($"{prefix} {BuildSummaryLine()}");
    }
}

public static class BuildSceneValidator
{
    private const string Prefix = "[SceneValidation]";

    public static SceneValidationReport ValidateBuildScenes()
    {
        SceneValidationReport report = new SceneValidationReport();
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        if (scenes == null || scenes.Length == 0)
        {
            report.Issues.Add(new SceneValidationIssue(
                ValidationSeverity.Warning,
                "<BuildSettings>",
                "<none>",
                "<none>",
                "EditorBuildSettings.scenes",
                "No hay escenas configuradas en Build Settings."));
            return report;
        }

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        string activeScenePath = SceneManager.GetActiveScene().path;

        try
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene buildScene = scenes[i];
                if (!buildScene.enabled)
                    continue;

                if (string.IsNullOrWhiteSpace(buildScene.path))
                {
                    report.Issues.Add(new SceneValidationIssue(
                        ValidationSeverity.Warning,
                        "<BuildSettings>",
                        "<none>",
                        "<none>",
                        "path",
                        "Entrada de escena habilitada con path vacío."));
                    continue;
                }

                Scene opened = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                ValidateOpenedScene(opened, report);
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            else if (!string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }

        return report;
    }

    [MenuItem("Tools/Validation/Run Scene Validation")]
    public static void RunManualValidationFromMenu()
    {
        SceneValidationReport report = ValidateBuildScenes();
        report.LogToConsole(Prefix);

        if (report.HasErrors)
            EditorUtility.DisplayDialog("Scene Validation", report.BuildSummaryLine(), "OK");
    }

    private static void ValidateOpenedScene(Scene scene, SceneValidationReport report)
    {
        string scenePath = scene.path;

        GameFlowManager[] gameFlows = FindInScene<GameFlowManager>(scene);
        MapManager[] mapManagers = FindInScene<MapManager>(scene);
        MapNavigationUI[] mapUis = FindInScene<MapNavigationUI>(scene);
        MonoBehaviour[] modalViews = FindModalViews(scene);

        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(GameFlowManager), gameFlows.Length);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(MapManager), mapManagers.Length);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(MapNavigationUI), mapUis.Length);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, "IMapNodeModalView", modalViews.Length);

        for (int i = 0; i < gameFlows.Length; i++)
        {
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, gameFlows[i], "mapManager");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, gameFlows[i], "orbManager");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, gameFlows[i], "relicManager");
        }

        for (int i = 0; i < mapManagers.Length; i++)
        {
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapManagers[i], "currentMapStage");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapManagers[i], "gameFlowManager");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapManagers[i], "presentationController");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapManagers[i], "mapNodeModalView");

            ValidateStageConsistency(report, SceneValidationSettings.MapStageConsistencySeverity, scenePath, mapManagers[i]);
        }

        for (int i = 0; i < mapUis.Length; i++)
        {
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapUis[i], "mapManager");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapUis[i], "nodeContainer");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapUis[i], "nodePrefab");
        }

        for (int i = 0; i < modalViews.Length; i++)
        {
            if (modalViews[i] is MapNodeModalUI modal)
            {
                ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "titleText");
                ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "bodyText");
                ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "buttonsContainer");
                ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "buttonTemplate");
            }
        }
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> found = new();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            found.AddRange(roots[i].GetComponentsInChildren<T>(true));

        return found.ToArray();
    }

    private static MonoBehaviour[] FindModalViews(Scene scene)
    {
        List<MonoBehaviour> found = new();
        MonoBehaviour[] allBehaviours = FindInScene<MonoBehaviour>(scene);
        for (int i = 0; i < allBehaviours.Length; i++)
        {
            if (allBehaviours[i] is IMapNodeModalView)
                found.Add(allBehaviours[i]);
        }

        return found.ToArray();
    }

    private static void AddPresenceIssueIfMissing(
        SceneValidationReport report,
        ValidationSeverity severity,
        string scenePath,
        string componentName,
        int count)
    {
        if (count > 0)
            return;

        report.Issues.Add(new SceneValidationIssue(
            severity,
            scenePath,
            "<SceneRoot>",
            componentName,
            "<presence>",
            "Componente requerido no encontrado en la escena."));
    }

    private static void ValidateRequiredObjectField(
        SceneValidationReport report,
        ValidationSeverity severity,
        string scenePath,
        Component component,
        string fieldName)
    {
        if (component == null)
            return;

        FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            return;

        if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            return;

        UnityEngine.Object fieldValue = field.GetValue(component) as UnityEngine.Object;
        if (fieldValue != null)
            return;

        report.Issues.Add(new SceneValidationIssue(
            severity,
            scenePath,
            GetGameObjectPath(component.gameObject),
            component.GetType().Name,
            fieldName,
            "Referencia serializada crítica es nula."));
    }

    private static void ValidateStageConsistency(
        SceneValidationReport report,
        ValidationSeverity severity,
        string scenePath,
        MapManager mapManager)
    {
        FieldInfo currentField = typeof(MapManager).GetField("currentMapStage", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo sequenceField = typeof(MapManager).GetField("stageSequence", BindingFlags.Instance | BindingFlags.NonPublic);

        if (currentField == null || sequenceField == null)
            return;

        MapStage current = currentField.GetValue(mapManager) as MapStage;
        MapStage[] sequence = sequenceField.GetValue(mapManager) as MapStage[];

        if (current != null)
            ValidateMapStageAsset(report, severity, scenePath, mapManager, current, "currentMapStage");

        if (sequence == null)
            return;

        for (int i = 0; i < sequence.Length; i++)
        {
            MapStage stage = sequence[i];
            if (stage == null)
            {
                report.Issues.Add(new SceneValidationIssue(
                    severity,
                    scenePath,
                    GetGameObjectPath(mapManager.gameObject),
                    nameof(MapManager),
                    $"stageSequence[{i}]",
                    "MapStage es nulo."));
                continue;
            }

            ValidateMapStageAsset(report, severity, scenePath, mapManager, stage, $"stageSequence[{i}]");
        }
    }

    private static void ValidateMapStageAsset(
        SceneValidationReport report,
        ValidationSeverity severity,
        string scenePath,
        MapManager owner,
        MapStage stage,
        string sourceField)
    {
        if (stage.startingNode == null)
        {
            report.Issues.Add(new SceneValidationIssue(
                severity,
                scenePath,
                GetGameObjectPath(owner.gameObject),
                nameof(MapStage),
                $"{sourceField}.startingNode",
                $"MapStage '{stage.name}' no tiene startingNode."));
            return;
        }

        if (stage.bossNode == null)
        {
            report.Issues.Add(new SceneValidationIssue(
                severity,
                scenePath,
                GetGameObjectPath(owner.gameObject),
                nameof(MapStage),
                $"{sourceField}.bossNode",
                $"MapStage '{stage.name}' no tiene bossNode."));
            return;
        }

        if (stage.startingNode == stage.bossNode)
        {
            report.Issues.Add(new SceneValidationIssue(
                severity,
                scenePath,
                GetGameObjectPath(owner.gameObject),
                nameof(MapStage),
                sourceField,
                $"MapStage '{stage.name}' tiene startingNode y bossNode apuntando al mismo asset."));
        }
    }

    private static string GetGameObjectPath(GameObject gameObject)
    {
        if (gameObject == null)
            return "<none>";

        string path = gameObject.name;
        Transform current = gameObject.transform.parent;
        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    public static bool ValidateAndLogWithBuildPolicy()
    {
        SceneValidationReport report = ValidateBuildScenes();
        report.LogToConsole(Prefix);

        if (!report.HasErrors)
            return true;

        return !SceneValidationSettings.BlockBuildOnErrors;
    }
}

public sealed class BuildSceneValidationPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        bool isValid = BuildSceneValidator.ValidateAndLogWithBuildPolicy();
        if (!isValid)
            throw new BuildFailedException("Build bloqueada por errores de Scene Validation. Revisa la consola.");
    }
}

[InitializeOnLoad]
public static class SceneValidationPlayModeHook
{
    static SceneValidationPlayModeHook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        if (!SceneValidationSettings.RunBeforePlayMode)
            return;

        SceneValidationReport report = BuildSceneValidator.ValidateBuildScenes();
        report.LogToConsole("[PlayModeValidation]");
    }
}