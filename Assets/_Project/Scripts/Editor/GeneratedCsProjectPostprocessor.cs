using System;
using System.IO;
using System.Text;
using UnityEditor;

public sealed class GeneratedCsProjectPostprocessor : AssetPostprocessor
{
    private static readonly string[] AssemblyCSharpCompileIncludes =
    {
        @"Assets\_Project\Scripts\Gameplay\Map\MapDomainService.Events.cs",
        @"Assets\_Project\Scripts\Gameplay\Map\MapDomainService.Progression.cs"
    };

    public static string OnGeneratedCSProject(string path, string content)
    {
        if (!string.Equals(Path.GetFileName(path), "Assembly-CSharp.csproj", StringComparison.OrdinalIgnoreCase))
            return content;

        return EnsureCompileIncludes(content, AssemblyCSharpCompileIncludes);
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
