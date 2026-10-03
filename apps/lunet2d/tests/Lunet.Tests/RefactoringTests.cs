using Lunet.Compiler;
using Lunet.Editor;

namespace Lunet.Tests;

public class RefactoringTests
{
    private static CodeAnalyzer Analyzer(params (string Path, string Text)[] files)
    {
        var analyzer = new CodeAnalyzer(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        foreach (var (path, text) in files) analyzer.SetFile(path, text);
        return analyzer;
    }

    private const string Sample = """
        using System;
        using Lunet;

        namespace Demo
        {
            /// <summary>doc</summary>
            public class Player : Game
            {
                public int Score;
                public event Action Died;

                public Player() { }

                public int Compute(int lives)
                {
                    var bonus = lives * 2;
                    return bonus + Score;
                }

                public string Label { get; set; }

                // a
                // b
                // c
                protected override void Update(GameTime time)
                {
                    Score = Compute(3);
                    switch (Score)
                    {
                        case 1:
                            break;
                    }
                }
            }

            enum Mode { A, B }
        }
        """;

    [Fact]
    public void Outline_ListsNamespacesTypesAndMembersWithLines()
    {
        var outline = Analyzer(("A.cs", Sample)).GetOutline("A.cs");
        var ns = Assert.Single(outline);
        Assert.Equal("Demo", ns.Name);
        var player = ns.Children.First(c => c.Name == "Player");
        Assert.Equal("class", player.Kind);
        Assert.Contains(player.Children, c => c is { Name: "Score", Kind: "field" });
        Assert.Contains(player.Children, c => c is { Name: "Died", Kind: "event" });
        Assert.Contains(player.Children, c => c is { Name: "Compute", Kind: "method", Detail: "(int lives) : int" });
        Assert.Contains(player.Children, c => c is { Name: "Player", Kind: "constructor" });
        Assert.Equal(["A", "B"], ns.Children.First(c => c.Name == "Mode").Children.Select(c => c.Name));
        Assert.True(player.Line > 1);
    }

    [Fact]
    public void FoldRegions_CoverTypesMethodsCommentsAndUsings()
    {
        var regions = Analyzer(("A.cs", Sample)).GetFoldRegions("A.cs");
        Assert.Contains(regions, r => r.Kind == "namespace");
        Assert.Contains(regions, r => r.Kind == "type" && r.Placeholder == "{ … }");
        Assert.Contains(regions, r => r.Kind == "method");
        Assert.Contains(regions, r => r.Kind == "comment" && r.EndLine - r.StartLine == 2);
        Assert.Contains(regions, r => r.Kind == "usings");
        Assert.Contains(regions, r => r.Kind == "switch");
        Assert.All(regions, r => Assert.True(r.EndLine > r.StartLine));
    }

    [Fact]
    public void SymbolDetails_DescribeSourceAndFrameworkSymbols()
    {
        var analyzer = Analyzer(("A.cs", Sample));
        var at = Sample.IndexOf("Compute(3)", StringComparison.Ordinal);
        var compute = analyzer.GetSymbolDetails("A.cs", at)!;
        Assert.Equal("Method", compute.Kind);
        Assert.Equal("int", compute.Type);
        Assert.True(compute.IsFromSource);
        Assert.Equal("public", compute.Accessibility);

        var game = analyzer.GetSymbolDetails("A.cs", Sample.IndexOf(": Game", StringComparison.Ordinal) + 3)!;
        Assert.Equal("NamedType", game.Kind);
        Assert.False(game.IsFromSource);
        Assert.Contains("Update(GameTime time)", string.Join('|', game.Members));
        Assert.StartsWith("T:Lunet.Game", game.DocumentationId);
    }

    [Fact]
    public void Rename_UpdatesEveryUseAcrossFiles_AndRefusesInvalidOrFrameworkSymbols()
    {
        var other = "namespace Demo { class Use { int M(Player p) { return p.Score + p.Compute(1); } } }";
        var analyzer = Analyzer(("A.cs", Sample), ("B.cs", other));

        var result = analyzer.Rename("A.cs", Sample.IndexOf("Score;", StringComparison.Ordinal), "Points");
        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Edits.Count);
        var renamedA = TextChanges.Apply(Sample, result.Edits.First(e => e.Path == "A.cs").Changes);
        var renamedB = TextChanges.Apply(other, result.Edits.First(e => e.Path == "B.cs").Changes);
        Assert.DoesNotContain("Score", renamedA);
        Assert.Contains("p.Points", renamedB);
        Assert.Equal(5, result.Occurrences); // declaração + 3 usos em A + 1 em B

        Assert.False(analyzer.Rename("A.cs", Sample.IndexOf("Score;", StringComparison.Ordinal), "class").Success);
        Assert.False(analyzer.Rename("A.cs", Sample.IndexOf("Score;", StringComparison.Ordinal), "1abc").Success);
        var framework = analyzer.Rename("A.cs", Sample.IndexOf(": Game", StringComparison.Ordinal) + 3, "Jogo");
        Assert.False(framework.Success);
        Assert.Contains("framework", framework.Error);
    }

    [Fact]
    public void Rename_OfATypeAlsoRenamesItsConstructor_AndConflictsAreRefused()
    {
        var analyzer = Analyzer(("A.cs", Sample));
        var type = analyzer.Rename("A.cs", Sample.IndexOf("class Player", StringComparison.Ordinal) + 7, "Hero");
        Assert.True(type.Success, type.Error);
        var renamed = TextChanges.Apply(Sample, type.Edits.Single().Changes);
        Assert.Contains("public Hero()", renamed);

        var conflict = analyzer.Rename("A.cs", Sample.IndexOf("Score;", StringComparison.Ordinal), "Label");
        Assert.False(conflict.Success);
        Assert.Contains("conflito", conflict.Error);
    }

    [Fact]
    public void QuickFixes_AddMissingUsing_RemoveUnused_InsertSemicolon_AndSuggestNames()
    {
        var code = "using System;\nclass A\n{\n    void M()\n    {\n        var l = new List<int>();\n    }\n}\n";
        var analyzer = Analyzer(("A.cs", code));
        var fixes = analyzer.GetQuickFixes("A.cs", code.IndexOf("List", StringComparison.Ordinal) + 1);
        var add = fixes.First(f => f.Title == "using System.Collections.Generic;");
        var fixedCode = TextChanges.Apply(code, add.Changes);
        Assert.StartsWith("using System;\nusing System.Collections.Generic;\n", fixedCode);
        analyzer.SetFile("A.cs", fixedCode);
        Assert.DoesNotContain(analyzer.GetDiagnostics("A.cs"), d => d.Severity == DiagnosticSeverity.Error);

        var typo = "class A { void M() { int score = 1; scor = 2; } }";
        analyzer = Analyzer(("A.cs", typo));
        Assert.Contains(analyzer.GetQuickFixes("A.cs", typo.IndexOf("scor =", StringComparison.Ordinal) + 1), f => f.Title == "Você quis dizer \"score\"?");

        var semi = "class A { void M() { int x = 1 } }";
        analyzer = Analyzer(("A.cs", semi));
        var insert = analyzer.GetQuickFixes("A.cs", semi.IndexOf("1 }", StringComparison.Ordinal) + 1).First(f => f.Title == "Inserir \";\"");
        Assert.Contains("int x = 1; }", TextChanges.Apply(semi, insert.Changes));
    }

    [Fact]
    public void QuickFixes_SortUsings_AndExtensionMethodsSuggestLinq()
    {
        var code = "using Lunet;\nusing System.Text;\nusing System;\nclass A { void M(int[] v) { var l = v.ToList(); } }\n";
        var analyzer = Analyzer(("A.cs", code));
        var fixes = analyzer.GetQuickFixes("A.cs", code.IndexOf("ToList", StringComparison.Ordinal) + 1);
        Assert.Contains(fixes, f => f.Title == "using System.Linq;");
        var sort = fixes.First(f => f.Title == "Ordenar usings");
        Assert.StartsWith("using System;\nusing System.Text;\nusing Lunet;\n", TextChanges.Apply(code, sort.Changes));
    }
    [Theory]
    [InlineData("using System; class Player { public int Score = 7; } // keep")]
    [InlineData("using System; using System.Text; class Player { public int Score = 7; } // keep")]
    public void RemoveUnusedUsings_PreservesInlineGameCodeAndComments(string code)
    {
        var analyzer = Analyzer(("A.cs", code));
        var fixes = analyzer.GetQuickFixes("A.cs", 1);
        var fix = fixes.FirstOrDefault(f => f.Title.StartsWith("Remover todos"))
            ?? fixes.First(f => f.Title == "Remover using desnecessário");
        var result = TextChanges.Apply(code, fix.Changes);
        Assert.Contains("class Player { public int Score = 7; } // keep", result);
        analyzer.SetFile("A.cs", result);
        Assert.DoesNotContain(analyzer.GetDiagnostics("A.cs"), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void SortUsings_PreservesGlobalScopeCommentsAndCrLfAcrossFiles()
    {
        var code = "global using System.Text; // builder\r\nglobal using System; // action\r\nclass A { }\r\n";
        var other = "class Player { public StringBuilder Label = new(); public Action? Hit; }";
        var analyzer = Analyzer(("A.cs", code), ("Player.cs", other));
        var sort = analyzer.GetQuickFixes("A.cs", 0).First(f => f.Title == "Ordenar usings");
        var result = TextChanges.Apply(code, sort.Changes);
        Assert.Equal("global using System; // action\r\nglobal using System.Text; // builder\r\nclass A { }\r\n", result);
        analyzer.SetFile("A.cs", result);
        Assert.DoesNotContain(analyzer.GetDiagnostics("Player.cs"), d => d.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("using System.Text;\nusing System; class Player { }\n")]
    [InlineData("using System.Text;\nusing System\n;\nclass Player { }\n")]
    [InlineData("#if DEBUG\nusing System.Text;\n#endif\nusing System;\nclass Player { }\n")]
    [InlineData("using System.Text;\n// belongs to System\nusing System;\nclass Player { }\n")]
    public void SortUsings_DoesNotRewriteMixedCodeOrConditionalBlocks(string code)
    {
        Assert.DoesNotContain(Analyzer(("A.cs", code)).GetQuickFixes("A.cs", 0), f => f.Title == "Ordenar usings");
    }

    [Fact]
    public void SortUsings_PreservesMissingFinalNewline()
    {
        var code = "using System.Text;\nusing System;";
        var sort = Analyzer(("A.cs", code)).GetQuickFixes("A.cs", 0).First(f => f.Title == "Ordenar usings");
        Assert.Equal("using System;\nusing System.Text;", TextChanges.Apply(code, sort.Changes));
    }

}

public class IncrementalCompilerTests
{
    [Fact]
    public void Compile_ReusesUnchangedTreesAndCachesIdenticalSources()
    {
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        List<SourceFile> sources = [new("A.cs", "class A { }"), new("B.cs", "class B { int X => 1; }")];

        var first = compiler.Compile("G1", sources);
        Assert.True(first.Success);
        Assert.False(first.FromCache);
        Assert.Equal(0, first.ReusedFiles);

        var same = compiler.Compile("G2", sources);
        Assert.True(same.FromCache);
        Assert.Equal(first.Assembly, same.Assembly);

        var changed = compiler.Compile("G3", [new("A.cs", "class A { }"), new("B.cs", "class B { int X => 2; }")]);
        Assert.False(changed.FromCache);
        Assert.Equal(1, changed.ReusedFiles);
        Assert.True(changed.Success);

        var broken = compiler.Compile("G4", [new("A.cs", "class A { }"), new("B.cs", "class B { ")]);
        Assert.False(broken.Success);
        Assert.Equal(1, broken.ReusedFiles);
    }
}
