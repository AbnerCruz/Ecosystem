using System.Diagnostics;
using System.Numerics;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Docs;
using Lunet.Editor;
using Lunet.Git;
using Lunet.Runtime;
using Lunet.Runtime.Inspection;
using Lunet.Storage;

namespace Lunet.Tests;

/// <summary>
/// Versão automatizada do roteiro de teste da Fase 3 (docs/audits/fase-3-roteiro.md) para o que roda fora do aparelho:
/// cada teste executa os mesmos passos sobre um projeto real (modelo Coletor de moedas) usando a mesma lógica do app.
/// O que depende de tela, toque e teclado do Android (dobrar, minimapa, diálogos) fica de fora e continua no aparelho.
/// </summary>
public sealed class Fase3RoteiroTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-f3-" + Guid.NewGuid().ToString("N"));
    private readonly LunetProject _project;
    private static readonly GitSignature Me = new("Ana", "ana@example.com", DateTimeOffset.UtcNow);

    public Fase3RoteiroTests() => _project = new ProjectStore(_root).Create("TesteF3", ProjectTemplate.CoinCatcher);

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private static readonly LoadedAssembliesReferenceProvider References = new(typeof(Game).Assembly);

    private CodeAnalyzer Analyzer()
    {
        var analyzer = new CodeAnalyzer(References);
        foreach (var source in _project.LoadSources()) analyzer.SetFile(source.Path, source.Text);
        return analyzer;
    }

    private string GameText => _project.ReadText("Game.cs");

    private static string Git(string dir, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) start.ArgumentList.Add(a);
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        using var p = Process.Start(start)!;
        var o = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return o;
    }

    // ---------- A. Regressão ----------

    [Fact]
    public void A1_Run_CompilesAndRunsWithoutFault_ThroughThePreviewHost()
    {
        var result = new GameCompiler(References).Compile("a1", _project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList());
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var host = new GameHost(loaded.Game, new RecordingBackend(), new DirectoryContentSource(Path.Combine(_project.Directory, "Content")), null, new MemorySaveStore());
        Assert.True(host.Start(360, 640));
        for (var i = 0; i < 300; i++) host.Tick(1.0 / 60);
        Assert.False(host.IsFaulted);
    }

    [Fact]
    public void A4_A5_ExplorerOperationsAndZipExport()
    {
        _project.CreateFile("Notas/ideias.txt", "oi");
        _project.CreateDirectory("Assets");
        _project.Rename("Notas/ideias.txt", "Notas/plano.txt");
        Assert.Contains("Notas/plano.txt", _project.ListFiles());
        using var zip = new MemoryStream();
        _project.ExportZip(zip);
        Assert.True(zip.Length > 500);
        _project.Delete("Notas");
        Assert.DoesNotContain(_project.ListFiles(), f => f.StartsWith("Notas", StringComparison.Ordinal));
    }

    // ---------- B. Editor ----------

    [Fact]
    public void B1_MissingSemicolon_IsReportedWithTheLine()
    {
        var analyzer = Analyzer();
        var broken = GameText.Replace("spawnTimer = 0;", "spawnTimer = 0", StringComparison.Ordinal);
        if (broken == GameText) broken = GameText.Replace("lives = 3;", "lives = 3", StringComparison.Ordinal);
        analyzer.SetFile("Game.cs", broken);
        Assert.Contains(analyzer.GetDiagnostics("Game.cs"), d => d.Severity == DiagnosticSeverity.Error && d.Line > 0);
    }

    [Fact]
    public void B2_B3_B4_B5_CompletionsHoverDefinitionAndReferences()
    {
        var analyzer = Analyzer();
        var text = GameText;
        var at = text.IndexOf("batch = new SpriteBatch", StringComparison.Ordinal);
        var withPrefix = text.Insert(at, "Con");
        analyzer.SetFile("Game.cs", withPrefix);
        Assert.Contains(analyzer.GetCompletions("Game.cs", at + 3), c => c.Label == "Content");
        analyzer.SetFile("Game.cs", text);

        var hover = analyzer.GetHover("Game.cs", text.IndexOf("SpriteBatch(GraphicsDevice)", StringComparison.Ordinal) + 3);
        Assert.NotNull(hover);
        Assert.NotNull(hover!.DocumentationId);

        var spawn = text.IndexOf("TryCollect(gesture.Position)", StringComparison.Ordinal);
        var definition = analyzer.GetDefinition("Game.cs", spawn + 2);
        Assert.NotNull(definition);
        Assert.True(analyzer.FindReferences("Game.cs", spawn + 2).Count >= 2);
    }

    [Fact]
    public void B6_B7_B8_FindReplaceUndoAndAutoIndent()
    {
        var text = GameText;
        Assert.NotEmpty(FindReplace.FindAll(text, "score", new FindOptions(WholeWord: true)));
        var replaced = FindReplace.ReplaceAll(text, "score", "points", new FindOptions(WholeWord: true), out var count);
        Assert.True(count > 0);
        Assert.DoesNotContain("score", replaced.Replace("Score", ""));

        var history = new UndoHistory();
        history.Record(0, "", "abc", 0, 0);
        history.Record(3, "", "def", 3, 5000);
        Assert.Equal(new TextEdit(3, 3, "", 3), history.Undo()!.Value);
        Assert.NotNull(history.Redo());

        var edit = IndentationService.NewLine("void M() {", 10);
        Assert.Contains("\n    ", edit.Insert);
    }

    // ---------- C. Roslyn ----------

    [Fact]
    public void C1_Format_MessedUpTemplate_KeepsMeaningAndStillCompiles()
    {
        var messy = string.Join('\n', GameText.Split('\n').Select(l => l.TrimStart()));
        var formatted = CodeFormatter.Format(messy);
        Assert.Equal(formatted, CodeFormatter.Format(formatted));
        var result = new GameCompiler(References).Compile("c1", [new SourceFile("Game.cs", formatted)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }

    [Fact]
    public void C2_Rename_PrivateFieldEverywhere_StillCompiles_AndRefusals()
    {
        var analyzer = Analyzer();
        var text = GameText;
        var position = text.IndexOf("int score;", StringComparison.Ordinal) + 5;
        var result = analyzer.Rename("Game.cs", position, "points");
        Assert.True(result.Success, result.Error);
        var renamed = TextChanges.Apply(text, result.Edits.Single().Changes);
        var compiled = new GameCompiler(References).Compile("c2", [new SourceFile("Game.cs", renamed)]);
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
        Assert.DoesNotContain(" score", renamed.Replace("[Inspect, Tooltip(\"Pontos desta partida\")] int points", ""));

        Assert.False(analyzer.Rename("Game.cs", position, "best").Success); // conflito com outro campo
        var gameType = text.IndexOf(": Game", StringComparison.Ordinal) + 3;
        Assert.False(analyzer.Rename("Game.cs", gameType, "Jogo").Success);
    }

    [Fact]
    public void C3_QuickFixes_AddUsing_AndDidYouMean()
    {
        var analyzer = Analyzer();
        var code = "class A\n{\n    void M()\n    {\n        var l = new List<int>();\n        int score = 1;\n        scor = 2;\n    }\n}\n";
        analyzer.SetFile("A.cs", code);
        var listFix = analyzer.GetQuickFixes("A.cs", code.IndexOf("List", StringComparison.Ordinal) + 1);
        Assert.Contains(listFix, f => f.Title == "using System.Collections.Generic;");
        Assert.Contains(analyzer.GetQuickFixes("A.cs", code.IndexOf("scor =", StringComparison.Ordinal) + 1), f => f.Title.Contains("score", StringComparison.Ordinal));
    }

    [Fact]
    public void C4_C5_OutlineAndSymbolDetailsOnTheTemplate()
    {
        var analyzer = Analyzer();
        var outline = analyzer.GetOutline("Game.cs");
        Assert.Contains(outline.SelectMany(o => o.Children.Append(o)), o => o.Kind == "class");
        var text = GameText;
        var own = analyzer.GetSymbolDetails("Game.cs", text.IndexOf("SpawnCoin", StringComparison.Ordinal) + 2)
                  ?? analyzer.GetSymbolDetails("Game.cs", text.IndexOf("TryCollect", StringComparison.Ordinal) + 2);
        Assert.NotNull(own);
        Assert.True(own!.IsFromSource);
        var framework = analyzer.GetSymbolDetails("Game.cs", text.IndexOf("SpriteBatch(GraphicsDevice)", StringComparison.Ordinal) + 3);
        Assert.False(framework!.IsFromSource);
    }

    // ---------- D. Edição avançada (a parte de lógica) ----------

    [Fact]
    public void D1_D2_D3_D4_FoldRegionsMultiCursorAndLineCommands()
    {
        var analyzer = Analyzer();
        Assert.Contains(analyzer.GetFoldRegions("Game.cs"), r => r.Kind == "method");

        var text = GameText;
        var all = MultiCursor.SelectAllOccurrences(text, [new Selection(text.IndexOf("score", StringComparison.Ordinal), text.IndexOf("score", StringComparison.Ordinal))]);
        Assert.True(all.Count > 3);
        var edited = MultiCursor.Replace(text, all, "points");
        Assert.DoesNotContain("score", edited.Text.Replace("Score", ""));

        var duplicated = LineOperations.DuplicateLines("a\nb", 0, 0).Apply("a\nb");
        Assert.Equal("a\na\nb", duplicated);
        Assert.Equal("// a\nb", LineOperations.ToggleComment("a\nb", 0, 0).Apply("a\nb"));
    }

    // ---------- E. Busca, layout, configurações, logs ----------

    [Fact]
    public void E1_ProjectSearch_FindsUpdateWithFileAndLine()
    {
        var matches = ProjectSearch.Search(_project, "Update", new ProjectSearchOptions(WholeWord: true));
        Assert.Contains(matches, m => m.Path == "Game.cs" && m.Line > 1);
    }

    [Fact]
    public void E2_E3_E4_E5_E6_LayoutSettingsAndLogsSurviveARestart()
    {
        var layouts = new LayoutStore(Path.Combine(_root, "layouts.json"));
        layouts.Load();
        layouts.Current = new WorkspaceLayout { Dock = PanelDock.Right, PanelFraction = 0.4 };
        Assert.True(layouts.SaveAs("Meu"));
        layouts.Save();
        var again = new LayoutStore(Path.Combine(_root, "layouts.json"));
        again.Load();
        Assert.Equal(PanelDock.Right, again.Current.Dock);
        Assert.True(again.Apply("Foco no código"));
        Assert.False(again.Current.ShowPanel);
        Assert.Equal(PanelDock.Right, new WorkspaceLayout { Dock = PanelDock.Auto }.EffectiveDock(landscape: true));

        var settings = new SettingsStore(Path.Combine(_root, "settings.json"));
        settings.Save(new EditorSettings { FontSize = 18, ShowMinimap = false, IsolatedPreview = true });
        var loaded = settings.Load();
        Assert.Equal((18, false, true), (loaded.FontSize, loaded.ShowMinimap, loaded.IsolatedPreview));

        var log = LogExport.Build("TesteF3", "dev", "aparelho", DateTimeOffset.Now, ["[erro] x"], [new LogProblem("error", "CS1002", "; expected", "Game.cs", 3, 4)]);
        Assert.Contains("CS1002", log);
    }

    // ---------- F. Documentação ----------

    [Fact]
    public void F1_to_F5_OfflineDocumentation_SearchExampleGuideAndExplainSymbol()
    {
        var docs = DocsTests.Generate();
        var guides = Directory.GetFiles(FindRepo("docs/guides"), "*.md").ToDictionary(f => Path.GetFileNameWithoutExtension(f)!, File.ReadAllText);
        var browser = new DocumentationBrowser(docs, guides);
        Assert.Contains(browser.Navigate("home").Blocks, b => b.Target == "guide:primeiros-passos");
        Assert.Contains(browser.Navigate("search:SpriteBatch").Blocks, b => b.Target?.StartsWith("id:", StringComparison.Ordinal) == true);
        var page = browser.Navigate(browser.TargetForSymbol("T:Lunet.Graphics.SpriteBatch", "SpriteBatch"));
        Assert.Contains(page.Blocks, b => b.Kind == DocBlockKind.Heading && b.Text.StartsWith("Exemplo", StringComparison.Ordinal));
        Assert.Contains(browser.Resolve("guide:primeiros-passos").Blocks, b => b.Kind == DocBlockKind.Code);

        // "Explicar na Documentação": o id do Roslyn precisa levar a uma página real.
        var hover = Analyzer().GetHover("Game.cs", GameText.IndexOf("SpriteBatch(GraphicsDevice)", StringComparison.Ordinal) + 3);
        Assert.StartsWith("id:", browser.TargetForSymbol(hover!.DocumentationId, "SpriteBatch"));
    }

    private static string FindRepo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, relative))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    // ---------- G. Inspector, atributos e classificação ----------

    [Fact]
    public void G1_G2_InspectorShowsAndEditsTheRunningTemplateGame()
    {
        var result = new GameCompiler(References).Compile("g1", _project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList());
        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var host = new GameHost(loaded.Game, new RecordingBackend(), new DirectoryContentSource(Path.Combine(_project.Directory, "Content")), null, new MemorySaveStore());
        Assert.True(host.Start(360, 640));

        var items = ObjectInspector.Build(loaded.Game, ObjectInspector.FindCustomInspectors(loaded.Game.GetType().Assembly));
        var lives = items.First(i => i.Label == "lives");
        Assert.Equal(3, lives.Getter!());
        Assert.Equal((0f, 5f), (lives.Min, lives.Max));
        Assert.Contains(items, i => i.Label == "score");

        // Edição feita como o app faz: pela fila do jogo, aplicada no próximo quadro.
        loaded.Game.Dispatcher.Post(() => lives.Setter!(1));
        host.Tick(1.0 / 60);
        Assert.Equal(1, lives.Getter!());
        loaded.Game.Dispatcher.Post(() => lives.Setter!(99));
        host.Tick(1.0 / 60);
        Assert.Equal(5, lives.Getter!());
    }

    [Fact]
    public void G4_ChangeStatusAfterRun_BodyOnlyVersusRestart()
    {
        var before = _project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
        var bodyOnly = before.Select(s => s.Path == "Game.cs" ? s with { Text = s.Text.Replace("CoinRadius = 20f", "CoinRadius = 20f").Replace("spawnTimer = 0f", "spawnTimer = 0.0f") } : s).ToList();
        var edited = before.Select(s => s.Path == "Game.cs" ? s with { Text = s.Text.Replace("lives--;", "lives -= 1;") } : s).ToList();
        Assert.Equal(ChangeKind.HotReloadPossible, ChangeClassifier.Classify(before, edited).Kind);
        var structural = before.Select(s => s.Path == "Game.cs" ? s with { Text = s.Text.Replace("float spawnTimer;", "float spawnTimer;\n            int extra;") } : s).ToList();
        Assert.Equal(ChangeKind.RestartRequired, ChangeClassifier.Classify(before, structural).Kind);
        _ = bodyOnly;
    }

    // ---------- H. Git ----------

    [Fact]
    public void H1_to_H6_GitFlowOnARealProject_IgnoringInternalFolders_AndInteroperableWithRealGit()
    {
        var dir = _project.Directory;
        var repo = GitRepository.Init(dir);
        File.WriteAllText(Path.Combine(dir, ".gitignore"), ".lunet/\nbin/\nobj/\n*.lunet-tmp\n");
        Directory.CreateDirectory(Path.Combine(dir, ".lunet", "autosave"));
        File.WriteAllText(Path.Combine(dir, ".lunet", "autosave", "Game.cs.buf"), "rascunho");

        repo.StageAll();
        var first = repo.Commit("primeiro", Me);
        Assert.Empty(repo.GetStatus());
        var tracked = Git(dir, "ls-files");
        Assert.Contains("lunet.json", tracked);
        Assert.Contains("Content/Audio/beep.wav", tracked);
        Assert.DoesNotContain(".lunet/", tracked);
        Git(dir, "fsck", "--strict");

        File.WriteAllText(Path.Combine(dir, "Game.cs"), GameText + "\n// mudou\n");
        Assert.Equal(FileChange.Modified, repo.GetStatus().Single().Unstaged);
        Assert.Contains("+// mudou", repo.DiffWorking("Game.cs"));

        repo.CheckoutNewBranch("experimento");
        repo.StageAll();
        repo.Commit("experimento", Me);
        repo.Checkout("main");
        Assert.DoesNotContain("// mudou", GameText);
        repo.Checkout("experimento");
        Assert.Contains("// mudou", GameText);

        File.WriteAllText(Path.Combine(dir, "Game.cs"), "sujo");
        Assert.Contains("Game.cs", Assert.Throws<GitException>(() => repo.Checkout("main")).Message);
        repo.DiscardChanges("Game.cs");

        var head = repo.Head!.Value;
        repo.Revert(head, Me);
        Assert.DoesNotContain("// mudou", GameText);
        _ = first;
    }

    // ---------- I. Recuperação ----------

    [Fact]
    public void I1_UnsavedBufferIsRecoveredAfterACrash_WithHistory()
    {
        var journal = new AutosaveJournal(_project);
        journal.WriteBuffer("Game.cs", GameText + "\n// v1");
        journal.WriteBuffer("Game.cs", GameText + "\n// v2");
        var recoveries = new AutosaveJournal(_project).FindRecoveries();
        var recovery = Assert.Single(recoveries);
        Assert.Contains("// v2", recovery.RecoveredText);
        new AutosaveJournal(_project).Restore(recovery);
        Assert.Contains("// v2", GameText);
        Assert.Empty(new AutosaveJournal(_project).FindRecoveries());
    }
}
