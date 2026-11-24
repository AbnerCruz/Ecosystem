namespace Lunet.Core;

/// <summary>Presets de fonte C# visível. Nenhum import é sintetizado pelo compilador.</summary>
public static class ProjectImports
{
    public const string Path = "Project/Imports.cs";
    public const int ExplicitSdkVersion = 1;
    private static readonly string[] Essential =
    [
        "System", "System.Numerics", "System.Collections.Generic",
        "Lunet", "Lunet.Graphics", "Lunet.Input", "Lunet.Content", "Lunet.Audio"
    ];
    private static readonly string[] Broad =
    [
        "System", "System.Numerics", "System.Collections.Generic", "System.Linq",
        "System.Text", "System.Text.Json", "System.IO", "System.Threading", "System.Threading.Tasks",
        "System.Diagnostics", "System.Reflection", "System.Collections.Concurrent", "System.Text.RegularExpressions",
        "Lunet", "Lunet.Graphics", "Lunet.Input", "Lunet.Content", "Lunet.Audio", "Lunet.Physics",
        "Lunet.Scenes", "Lunet.UI", "Lunet.Pathfinding", "Lunet.Storage", "Lunet.Networking"
    ];

    public static string Source(bool broad = false) =>
        string.Join("\n", (broad ? Broad : Essential).Select(n => "global using " + n + ";")) + "\n";

    // Only called for the template text being materialized, never on existing user files.
    public static string RemoveCoveredFromGenerated(string source, bool broad = false)
    {
        var covered = new HashSet<string>((broad ? Broad : Essential).Select(n => "using " + n + ";"), StringComparer.Ordinal);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int i = 0;
        var prefix = new List<string>();
        for (; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) { prefix.Add(lines[i]); continue; }
            if (!line.StartsWith("using ", StringComparison.Ordinal)) break;
            if (!covered.Contains(line)) prefix.Add(lines[i]);
        }
        while (prefix.Count > 0 && string.IsNullOrWhiteSpace(prefix[0])) prefix.RemoveAt(0);
        return string.Join("\n", prefix.Concat(lines.Skip(i)));
    }
}
