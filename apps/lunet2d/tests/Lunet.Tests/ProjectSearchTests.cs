using Lunet.Core;

namespace Lunet.Tests;

public sealed class ProjectSearchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-search-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private LunetProject Make()
    {
        var project = new ProjectStore(_root).Create("Busca");
        project.WriteText("Game.cs", "class A { int score; int Score2; }\nvar SCORE = 1;\r\n");
        project.WriteText("Data/level.json", "{ \"score\": 3 }");
        File.WriteAllBytes(Path.Combine(project.Directory, "bin.dat"), [1, 0, 2, 115, 99, 111, 114, 101]);
        return project;
    }

    [Fact]
    public void Finds_AcrossFiles_WithLineAndColumn_IgnoringCaseByDefault()
    {
        var matches = ProjectSearch.Search(Make(), "score");
        Assert.Contains(matches, m => m.Path == "Game.cs" && m.Line == 1 && m.Column == 15);
        Assert.Contains(matches, m => m.Path == "Game.cs" && m.Line == 2);
        Assert.Contains(matches, m => m.Path == "Data/level.json");
        Assert.DoesNotContain(matches, m => m.Path == "bin.dat");
    }

    [Fact]
    public void Options_MatchCaseWholeWordAndRegex()
    {
        var project = Make();
        Assert.All(ProjectSearch.Search(project, "score", new ProjectSearchOptions(MatchCase: true)), m => Assert.DoesNotContain("SCORE", m.Preview.Substring(m.Column - 1, m.Length)));
        Assert.DoesNotContain(ProjectSearch.Search(project, "score", new ProjectSearchOptions(WholeWord: true)), m => m.Preview.Contains("Score2") && !m.Preview.Contains("score;"));
        Assert.Single(ProjectSearch.Search(project, @"Score\d", new ProjectSearchOptions(UseRegex: true)));
    }

    [Fact]
    public void InvalidRegexEmptyQueryAndLimit_AreSafe()
    {
        var project = Make();
        Assert.Empty(ProjectSearch.Search(project, "("  , new ProjectSearchOptions(UseRegex: true)));
        Assert.Empty(ProjectSearch.Search(project, ""));
        Assert.Single(ProjectSearch.Search(project, "score", new ProjectSearchOptions(MaxResults: 1)));
    }
}
