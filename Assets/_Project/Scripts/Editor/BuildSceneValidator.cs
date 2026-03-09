
using System;
using System.Collections;
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
    private enum SceneValidationRole
    {
        Boot,
        MainMenu,
        Map,
        Combat,
        Shop,
        Other
    }

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
        switch (ResolveSceneRole(scene))
        {
            case SceneValidationRole.Boot:
                ValidateBootScene(scene, scenePath, report);
                break;
            case SceneValidationRole.MainMenu:
                ValidateMainMenuScene(scene, scenePath, report);
                break;
            case SceneValidationRole.Map:
                ValidateMapScene(scene, scenePath, report);
                break;
            case SceneValidationRole.Combat:
                ValidateCombatScene(scene, scenePath, report);
                break;
            case SceneValidationRole.Shop:
                ValidateShopScene(scene, scenePath, report);
                break;
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

    private static SceneValidationRole ResolveSceneRole(Scene scene)
    {
        SceneCatalog catalog = SceneCatalog.Load();
        string sceneName = scene.name;

        if (string.Equals(sceneName, catalog.BootScene, StringComparison.Ordinal))
            return SceneValidationRole.Boot;

        if (string.Equals(sceneName, catalog.MainMenuScene, StringComparison.Ordinal))
            return SceneValidationRole.MainMenu;

        if (string.Equals(sceneName, catalog.MapScene, StringComparison.Ordinal))
            return SceneValidationRole.Map;

        if (string.Equals(sceneName, catalog.CombatScene, StringComparison.Ordinal))
            return SceneValidationRole.Combat;

        if (string.Equals(sceneName, "ShopScene", StringComparison.Ordinal))
            return SceneValidationRole.Shop;

        return SceneValidationRole.Other;
    }

    private static void ValidateBootScene(Scene scene, string scenePath, SceneValidationReport report)
    {
        BootSceneController[] controllers = FindInScene<BootSceneController>(scene);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(BootSceneController), controllers.Length);

        for (int i = 0; i < controllers.Length; i++)
            ValidateCollectionFieldHasEntries(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, controllers[i], "persistentPrefabs");
    }

    private static void ValidateMainMenuScene(Scene scene, string scenePath, SceneValidationReport report)
    {
        MainMenuController[] controllers = FindInScene<MainMenuController>(scene);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(MainMenuController), controllers.Length);

        for (int i = 0; i < controllers.Length; i++)
        {
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, controllers[i], "menuPanel");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, controllers[i], "optionsPanel");
        }
    }

    private static void ValidateMapScene(Scene scene, string scenePath, SceneValidationReport report)
    {
        MapManager[] mapManagers = FindInScene<MapManager>(scene);
        MapNavigationUI[] mapUis = FindInScene<MapNavigationUI>(scene);
        MonoBehaviour[] modalViews = FindModalViews(scene);

        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(MapManager), mapManagers.Length);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(MapNavigationUI), mapUis.Length);

        bool hasModalFallbackPrefab = Resources.Load<GameObject>("MapNodeModalUI") != null;
        if (modalViews.Length == 0 && !hasModalFallbackPrefab)
        {
            report.Issues.Add(new SceneValidationIssue(
                SceneValidationSettings.SerializedReferenceSeverity,
                scenePath,
                "<SceneRoot>",
                "IMapNodeModalView",
                "<presence>",
                "No hay modal view en escena ni prefab fallback en Resources/MapNodeModalUI."));
        }

        for (int i = 0; i < mapManagers.Length; i++)
        {
            ValidateMapStageConfiguration(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapManagers[i]);
            ValidateStageConsistency(report, SceneValidationSettings.MapStageConsistencySeverity, scenePath, mapManagers[i]);
        }

        for (int i = 0; i < mapUis.Length; i++)
        {
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapUis[i], "nodeContainer");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, mapUis[i], "nodePrefab");
        }

        ValidateModalViews(report, scenePath, modalViews);
    }

    private static void ValidateCombatScene(Scene scene, string scenePath, SceneValidationReport report)
    {
        BattleManager[] battles = FindInScene<BattleManager>(scene);
        RewardManager[] rewards = FindInScene<RewardManager>(scene);

        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(BattleManager), battles.Length);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(RewardManager), rewards.Length);

        for (int i = 0; i < battles.Length; i++)
        {
            ValidateAtLeastOneObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, battles[i], "fallbackEnemy", "enemySpawnPoint");
            ValidateCollectionFieldHasEntries(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, battles[i], "enemiesPool");
        }
    }

    private static void ValidateShopScene(Scene scene, string scenePath, SceneValidationReport report)
    {
        ShopScene[] shopScenes = FindInScene<ShopScene>(scene);
        AddPresenceIssueIfMissing(report, SceneValidationSettings.CoreComponentSeverity, scenePath, nameof(ShopScene), shopScenes.Length);

        for (int i = 0; i < shopScenes.Length; i++)
        {
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "titleLabel");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "coinLabel");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "stockLabel");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "itemSlotsRoot");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "itemButtonTemplate");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "detailLabel");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "priceLabel");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "rarityLabel");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "buyButton");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "refreshButton");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, shopScenes[i], "exitButton");
        }
    }

    private static void ValidateModalViews(SceneValidationReport report, string scenePath, MonoBehaviour[] modalViews)
    {
        for (int i = 0; i < modalViews.Length; i++)
        {
            if (modalViews[i] is not MapNodeModalUI modal)
                continue;

            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "titleText");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "bodyText");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "buttonsContainer");
            ValidateRequiredObjectField(report, SceneValidationSettings.SerializedReferenceSeverity, scenePath, modal, "buttonTemplate");
        }
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

    private static void ValidateCollectionFieldHasEntries(
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

        if (field.GetValue(component) is ICollection collection && collection.Count > 0)
            return;

        report.Issues.Add(new SceneValidationIssue(
            severity,
            scenePath,
            GetGameObjectPath(component.gameObject),
            component.GetType().Name,
            fieldName,
            "Colección serializada crítica está vacía o no asignada."));
    }

    private static void ValidateAtLeastOneObjectField(
        SceneValidationReport report,
        ValidationSeverity severity,
        string scenePath,
        Component component,
        params string[] fieldNames)
    {
        if (component == null || fieldNames == null || fieldNames.Length == 0)
            return;

        for (int i = 0; i < fieldNames.Length; i++)
        {
            FieldInfo field = component.GetType().GetField(fieldNames[i], BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                continue;

            if (field.GetValue(component) as UnityEngine.Object != null)
                return;
        }

        report.Issues.Add(new SceneValidationIssue(
            severity,
            scenePath,
            GetGameObjectPath(component.gameObject),
            component.GetType().Name,
            string.Join(" | ", fieldNames),
            "Se requiere al menos una referencia serializada válida."));
    }

    private static void ValidateMapStageConfiguration(
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
        bool hasSequenceEntries = sequence != null && sequence.Length > 0;

        if (current == null && !hasSequenceEntries)
        {
            report.Issues.Add(new SceneValidationIssue(
                severity,
                scenePath,
                GetGameObjectPath(mapManager.gameObject),
                nameof(MapManager),
                "currentMapStage | stageSequence",
                "MapManager necesita currentMapStage o stageSequence con al menos un stage."));
            return;
        }

        if (current != null)
            ValidateMapStageAsset(report, severity, scenePath, mapManager, current, "currentMapStage");

        if (!hasSequenceEntries)
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
        if (current == null || sequence == null || sequence.Length == 0)
            return;

        int resolvedIndex = -1;
        for (int i = 0; i < sequence.Length; i++)
        {
            if (sequence[i] == current)
            {
                resolvedIndex = i;
                break;
            }
        }

        if (resolvedIndex >= 0)
            return;

        report.Issues.Add(new SceneValidationIssue(
            severity,
            scenePath,
            GetGameObjectPath(mapManager.gameObject),
            nameof(MapManager),
            "currentMapStage",
            $"currentMapStage '{current.name}' no aparece dentro de stageSequence."));
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
