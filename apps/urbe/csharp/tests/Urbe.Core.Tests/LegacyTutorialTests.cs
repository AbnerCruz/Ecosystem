using System.Text.RegularExpressions;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>tutorial.js: the version of the Tutorial content is build-tutorial.mjs's, and the mark is marcar's JSON.</summary>
public sealed class LegacyTutorialTests
{
    [Fact]
    public void TheVersionIsTheOneOfContentJs()
    {
        var root = Root().FullName;
        var dir = Path.Combine(root, "tutorial");
        // walk(src).sort(): the full paths in code-unit order
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => "Tutorial/" + Path.GetRelativePath(dir, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .Select(p => (p, Regex.Replace(File.ReadAllText(Path.Combine(dir, p["Tutorial/".Length..])), "\r\n?", "\n")))
            .ToList();
        var content = File.ReadAllText(Path.Combine(root, "src", "tutorial", "content.js"));
        var expected = Regex.Match(content, "\"version\": \"([0-9a-f]{12})\"").Groups[1].Value;
        Assert.Equal(expected, LegacyTutorial.Version(files));
    }

    [Fact]
    public void TheMarkAndTheComparisonAreTutorialJs()
    {
        Assert.Equal("{\"versao\":\"a1e944750d90\",\"em\":\"2026-10-12T20:40:05.123Z\"}",
            LegacyTutorial.Mark("a1e944750d90", new DateTimeOffset(2026, 10, 12, 17, 40, 5, 123, TimeSpan.FromHours(-3))));
        var (missing, changed) = LegacyTutorial.Compare([("Tutorial/A.md", "a"), ("Tutorial/B.md", "b"), ("Tutorial/C.md", "c")],
            p => p switch { "Tutorial/A.md" => "a", "Tutorial/B.md" => "mudei", _ => null });
        Assert.Equal(["Tutorial/C.md"], missing);
        Assert.Equal(["Tutorial/B.md"], changed);
        Assert.Equal(LegacyTutorial.Version([("a\u0001\"", "\ud800x")]), LegacyTutorial.Version([("a\u0001\"", "\ud800x")]));
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}

/// <summary>editor/context.js outline: the # headings with level, trimmed title, line and offset.</summary>
public sealed class LegacyEditorContextTests
{
    [Fact]
    public void TheOutlineIsContextJs()
    {
        var o = LegacyEditorContext.Outline("# Título ##\ntexto\n\n### Parte  \n#semtitulo\n####### sete\n## Fim");
        Assert.Equal(new[] { (1, "Título", 1, 0), (3, "Parte", 4, 19), (2, "Fim", 7, 55) },
            o.Select(h => (h.Level, h.Title, h.Line, h.Offset)).ToArray());
        Assert.Empty(LegacyEditorContext.Outline(null));
    }
}
