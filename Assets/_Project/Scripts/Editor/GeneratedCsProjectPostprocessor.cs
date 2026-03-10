using System;
using System.IO;
using System.Text;
using UnityEditor;

public sealed class GeneratedCsProjectPostprocessor : AssetPostprocessor
{
    private static readonly string[] AssemblyCSharpCompileIncludes =
    {
        @"Assets\_Project\Scripts\Gameplay\Map\MapDomainService.Events.cs",
        @"Assets\_Project\Scripts\Gameplay\Map\MapDomainService.Progression.cs",
        @"Assets\_Project\Scripts\UI\Map\MapGraphConnectionsGraphic.cs",
        @"Assets\_Project\Scripts\UI\Map\MapGraphLayoutService.cs",
        @"Assets\_Project\Scripts\Combat\EnemyEncounterSelector.cs",
        @"Assets\_Project\Scripts\UI\UIArtDirectives.cs",
        @"Assets\_Project\Scripts\UI\UIArtUtility.cs"
    };

    private static readonly string[] RuntimeCompileIncludes =
    {
        @"Assets\Plugins\Demigiant\DOTween\Modules\DOTweenModuleUI.cs",
        @"Assets\_Project\Scripts\Board\BoardManager.Generation.cs",
        @"Assets\_Project\Scripts\Board\BoardManager.Placement.cs",
        @"Assets\_Project\Scripts\Core\FlowSceneCoordinator.cs",
        @"Assets\_Project\Scripts\Core\GameFlowManager.Persistence.cs",
        @"Assets\_Project\Scripts\Core\RunState.cs",
        @"Assets\_Project\Scripts\Core\Save\RunPersistenceService.cs",
        @"Assets\_Project\Scripts\Gameplay\Launcher.Input.cs",
        @"Assets\_Project\Scripts\Gameplay\Launcher.Trajectory.cs",
        @"Assets\_Project\Scripts\Gameplay\Map\MapDomainService.Events.cs",
        @"Assets\_Project\Scripts\Gameplay\Map\MapManager.Dependencies.cs",
        @"Assets\_Project\Scripts\Gameplay\Map\MapManager.NodeFlow.cs",
        @"Assets\_Project\Scripts\Gameplay\Map\MapDomainService.Progression.cs",
        @"Assets\_Project\Scripts\UI\Map\MapGraphConnectionsGraphic.cs",
        @"Assets\_Project\Scripts\UI\Map\MapGraphLayoutService.cs",
        @"Assets\_Project\Scripts\UI\Map\ShopScene.RuntimeUi.cs",
        @"Assets\_Project\Scripts\Combat\EnemyEncounterSelector.cs",
        @"Assets\_Project\Scripts\UI\OverlayVisualStyler.cs",
        @"Assets\_Project\Scripts\UI\UIButtonMotion.cs",
        @"Assets\_Project\Scripts\UI\UIArtDirectives.cs",
        @"Assets\_Project\Scripts\UI\UIArtUtility.cs"
    };

    private static readonly string[] EditModeTestsCompileIncludes =
    {
        @"Assets\_Project\Tests\EditMode\Editor\FlowSceneCoordinatorTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\MapDomainServiceTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\MapGraphLayoutServiceTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\MapManagerDependencyTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\RunPersistenceServiceTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\ShopDomainServiceTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\EnemyEncounterSelectorTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\EnemyCombatBehaviorTests.cs",
        @"Assets\_Project\Tests\EditMode\Editor\RewardPresentationTests.cs"
    };

    public static string OnGeneratedCSProject(string path, string content)
    {
        string fileName = Path.GetFileName(path);
        if (string.Equals(fileName, "Assembly-CSharp.csproj", StringComparison.OrdinalIgnoreCase))
            return EnsureCompileIncludes(content, AssemblyCSharpCompileIncludes);

        if (string.Equals(fileName, "PegLox.Runtime.csproj", StringComparison.OrdinalIgnoreCase))
            return EnsureCompileIncludes(content, RuntimeCompileIncludes);

        if (string.Equals(fileName, "PegLox.Tests.EditMode.csproj", StringComparison.OrdinalIgnoreCase))
            return EnsureCompileIncludes(content, EditModeTestsCompileIncludes);

        return content;
    }

    private static string EnsureCompileIncludes(string content, string[] compileIncludes)
    {
        if (string.IsNullOrEmpty(content) || compileIncludes == null || compileIncludes.Length == 0)
            return content;

        const string compileItemGroupStart = "  <ItemGroup>\r\n    <Compile Include=";
        int itemGroupStartIndex = content.IndexOf(compileItemGroupStart, StringComparison.Ordinal);
        if (itemGroupStartIndex < 0)
            return content;

        const string itemGroupEnd = "  </ItemGroup>";
        int itemGroupEndIndex = content.IndexOf(itemGroupEnd, itemGroupStartIndex, StringComparison.Ordinal);
        if (itemGroupEndIndex < 0)
            return content;

        var builder = new StringBuilder(content.Length + (compileIncludes.Length * 96));
        builder.Append(content, 0, itemGroupEndIndex);

        for (int i = 0; i < compileIncludes.Length; i++)
        {
            string compileInclude = compileIncludes[i];
            string compileTag = $"<Compile Include=\"{compileInclude}\" />";
            if (content.IndexOf(compileTag, StringComparison.Ordinal) >= 0)
                continue;

            builder.Append("    ");
            builder.Append(compileTag);
            builder.Append("\r\n");
        }

        builder.Append(content, itemGroupEndIndex, content.Length - itemGroupEndIndex);
        return builder.ToString();
    }
}
