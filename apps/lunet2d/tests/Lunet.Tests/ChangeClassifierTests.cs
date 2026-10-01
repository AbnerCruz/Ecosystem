using Lunet.Compiler;

namespace Lunet.Tests;

public class ChangeClassifierTests
{
    private const string Base = """
        using System;
        namespace G
        {
            public class Player : Lunet.Game
            {
                public int Score = 0;
                public float Speed { get; set; } = 1;
                public int Double => Score * 2;

                public Player() { }

                protected override void Update(Lunet.GameTime time)
                {
                    Score += 1; // ponto
                }

                private int Helper(int a) { return a + 1; }
            }
            enum Mode { A, B }
        }
        """;

    private static ChangeReport Classify(string after, string before = Base) =>
        ChangeClassifier.Classify([new("Game.cs", before)], [new("Game.cs", after)]);

    [Fact]
    public void SameCode_CommentsAndFormatting_AreNoChange()
    {
        Assert.Equal(ChangeKind.None, Classify(Base).Kind);
        var reformatted = Base.Replace("Score += 1; // ponto", "Score   +=   1;\n        // outro comentário");
        Assert.Equal(ChangeKind.None, Classify(reformatted).Kind);
    }

    [Fact]
    public void OnlyBodies_AreHotReloadPossible_AndNameTheMembers()
    {
        var report = Classify(Base.Replace("Score += 1;", "Score += 2;").Replace("a + 1", "a + 2").Replace("Score * 2", "Score * 3"));
        Assert.Equal(ChangeKind.HotReloadPossible, report.Kind);
        Assert.Contains(report.Reasons, r => r == "corpo alterado: método G.Player.Update(Lunet.GameTime)");
        Assert.Contains(report.Reasons, r => r.Contains("Helper"));
        Assert.Contains(report.Reasons, r => r.Contains("propriedade G.Player.Double"));
    }

    [Fact]
    public void Structure_SignaturesAndInitializers_RequireRestart()
    {
        Assert.Equal(ChangeKind.RestartRequired, Classify(Base.Replace("private int Helper(int a)", "private int Helper(int a, int b)")).Kind);
        Assert.Equal(ChangeKind.RestartRequired, Classify(Base.Replace("public int Score = 0;", "public int Score = 5;")).Kind);
        Assert.Equal(ChangeKind.RestartRequired, Classify(Base.Replace("public int Score = 0;", "public int Score = 0;\n public int Lives;")).Kind);
        Assert.Equal(ChangeKind.RestartRequired, Classify(Base.Replace("enum Mode { A, B }", "enum Mode { A, B, C }")).Kind);
        Assert.Equal(ChangeKind.RestartRequired, Classify(Base.Replace("private int Helper", "public int Helper")).Kind);
        Assert.Equal(ChangeKind.RestartRequired, Classify(Base.Replace(": Lunet.Game", ": Lunet.Game, IDisposable")).Kind);

        var added = Classify(Base + "\nclass Extra { }");
        Assert.Equal(ChangeKind.RestartRequired, added.Kind);
        Assert.Contains("adicionado: tipo Extra", added.Reasons);
    }

    [Fact]
    public void FilesAddedOrRemoved_AreStructuralChanges()
    {
        var withFile = ChangeClassifier.Classify([new("Game.cs", Base)], [new("Game.cs", Base), new("Enemy.cs", "class Enemy { void Hit() { } }")]);
        Assert.Equal(ChangeKind.RestartRequired, withFile.Kind);
        Assert.Contains(withFile.Reasons, r => r.Contains("Enemy"));
        var removed = ChangeClassifier.Classify([new("Game.cs", Base), new("Enemy.cs", "class Enemy { }")], [new("Game.cs", Base)]);
        Assert.Equal(ChangeKind.RestartRequired, removed.Kind);
    }

    [Fact]
    public void Usings_AreNotStructural()
    {
        var report = Classify(Base.Replace("using System;", "using System;\nusing System.Linq;"));
        Assert.Equal(ChangeKind.HotReloadPossible, report.Kind);
    }
}
