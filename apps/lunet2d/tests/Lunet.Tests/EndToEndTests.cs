using System.Numerics;
using Lunet.Compiler;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Runtime;

namespace Lunet.Tests;

public class EndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static GameCompiler NewCompiler() =>
        new(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));

    [Fact]
    public void Compiler_ReportsErrorsWithFileLineAndColumn()
    {
        var result = NewCompiler().Compile("bad1", [new SourceFile("Game.cs", "using Lunet;\npublic class G : Game\n{\n    void M() { int x = \"a\"; }\n}\n")]);
        Assert.False(result.Success);
        Assert.Null(result.Assembly);
        var error = Assert.Single(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("CS0029", error.Id);
        Assert.Equal("Game.cs", error.FilePath);
        Assert.Equal(4, error.Line);
    }

    [Fact]
    public void Compiler_RejectsProjectsWithoutSources()
    {
        var result = NewCompiler().Compile("empty", []);
        Assert.False(result.Success);
        Assert.Equal("LUNET0001", result.Diagnostics[0].Id);
    }

    [Fact]
    public void Compiler_DoesNotExposeAndroidOrRoslynApisToGames()
    {
        var result = NewCompiler().Compile("iso", [new SourceFile("Game.cs",
            "public class X { object o = typeof(Microsoft.CodeAnalysis.SyntaxTree); }")]);
        Assert.False(result.Success);
    }

    [Fact]
    public void Template_CompilesLoadsRunsAndFollowsTouch()
    {
        var store = new ProjectStore(_root);
        var project = store.Create("My-Game");
        var sources = project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();

        var result = NewCompiler().Compile("game_template", sources);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);

        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend);
        var logs = new List<string>();
        loaded.Game.Log.Written += (_, m) => logs.Add(m);

        Assert.True(host.Start(360, 640));
        host.Tick(0.016);
        Assert.Contains(backend.Clears, c => c == Color.CornflowerBlue);
        Assert.Equal(new Vector2(180, 320), backend.LastQuadCenter());

        for (var i = 0; i < 120; i++)
        {
            host.SetSurfaceTouches([new TouchPoint(0, TouchPhase.Moved, new Vector2(60, 100))]);
            host.Tick(1.0 / 60);
        }
        var center = backend.LastQuadCenter();
        Assert.Equal(60, center.X, 1);
        Assert.Equal(100, center.Y, 1);
        Assert.False(host.IsFaulted);
        Assert.Contains(logs, l => l.Contains("Jogo iniciado"));
    }

    [Fact]
    public void Loader_ExplainsMissingOrAmbiguousGameClass()
    {
        var none = NewCompiler().Compile("none1", [new SourceFile("A.cs", "public class Nothing { }")]);
        var ex = Assert.Throws<GameLoadException>(() => GameLoader.Load(none.Assembly!));
        Assert.Contains("Lunet.Game", ex.Message);

        var two = NewCompiler().Compile("two1", [new SourceFile("A.cs",
            "using Lunet; public class A : Game { } public class B : Game { }")]);
        var ex2 = Assert.Throws<GameLoadException>(() => GameLoader.Load(two.Assembly!));
        Assert.Contains("Mais de uma", ex2.Message);
    }

    [Fact]
    public void Loader_ReloadsUpdatedGameWithSameAssemblyName()
    {
        string Source(string text) => "using Lunet; public class G : Game { public string Text = \"" + text + "\"; }";
        var compiler = NewCompiler();
        using var first = GameLoader.Load(compiler.Compile("same", [new SourceFile("G.cs", Source("um"))]).Assembly!);
        first.Dispose();
        using var second = GameLoader.Load(compiler.Compile("same", [new SourceFile("G.cs", Source("dois"))]).Assembly!);
        var field = second.Game.GetType().GetField("Text")!;
        Assert.Equal("dois", field.GetValue(second.Game));
    }

    [Fact]
    public void RuntimeExceptionInGameCode_IsReportedWithSourceLine()
    {
        var result = NewCompiler().Compile("throws1", [new SourceFile("Game.cs",
            "using Lunet;\npublic class G : Game\n{\n    protected override void Update(GameTime t)\n    {\n        throw new System.Exception(\"falhou\");\n    }\n}\n")]);
        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var logs = new List<string>();
        loaded.Game.Log.Written += (_, m) => logs.Add(m);
        var host = new GameHost(loaded.Game, new RecordingBackend());
        host.Start(100, 100);
        host.Tick(0.1);
        Assert.True(host.IsFaulted);
        Assert.Contains("Game.cs:line 6", logs.Single());
    }
}
