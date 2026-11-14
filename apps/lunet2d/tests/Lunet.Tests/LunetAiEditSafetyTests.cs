using System.Text.Json;
using AgentRuntime;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>
/// LUNET-507: the AI edits excerpts instead of erasing documents, every run (also a cancelled one) can be undone,
/// the model receives the real code of the open file, and the editor never silently overwrites the AI or vice versa.
/// </summary>
public sealed class LunetAiEditSafetyTests
{
    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "lunet-ai-safety-" + Guid.NewGuid().ToString("N"));

    private static string BigSource(int methods)
    {
        var lines = new List<string> { "using Lunet;", "", "public sealed class Hero", "{" };
        for (int i = 0; i < methods; i++)
        {
            lines.Add($"    public int Value{i}()");
            lines.Add("    {");
            lines.Add($"        return {i};");
            lines.Add("    }");
        }
        lines.Add("}");
        return string.Join("\n", lines) + "\n";
    }

    // ---------- whole-file overwrite guard ----------

    [Fact]
    public void Write_RefusesToReplaceAFileWithTheExcerptTheModelSaw()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Guard");
            var files = new LunetAiProjectFiles(project);
            var original = BigSource(20);
            files.Write("Hero.cs", original, "");
            var excerpt = files.ReadExcerpt("Hero.cs", 5, 8); // hash of the WHOLE file
            var error = Assert.Throws<InvalidOperationException>(() => files.Write("Hero.cs", excerpt.Content, excerpt.Sha256));
            Assert.Contains("lunet.files.replace", error.Message);
            Assert.Equal(original, project.ReadText("Hero.cs"));
            Assert.Throws<InvalidOperationException>(() => files.Write("Hero.cs", "", excerpt.Sha256));
            Assert.Equal(original, project.ReadText("Hero.cs"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("    // ... resto do código ...")]
    [InlineData("    // ...")]
    [InlineData("    // rest of the code unchanged")]
    [InlineData("    /* código existente */")]
    [InlineData("    …")]
    public void Write_RefusesPlaceholdersThatWouldEraseCode(string placeholder)
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Placeholder");
            var files = new LunetAiProjectFiles(project);
            var original = BigSource(20);
            var sha = files.Write("Hero.cs", original, "").Sha256;
            // Same length as before, but one method replaced by an elision marker.
            var lines = original.Split('\n').ToList();
            lines[6] = placeholder;
            Assert.Throws<InvalidOperationException>(() => files.Write("Hero.cs", string.Join("\n", lines), sha));
            Assert.Equal(original, project.ReadText("Hero.cs"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Write_StillAllowsFullRewritesThatKeepTheCode()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Rewrite");
            var files = new LunetAiProjectFiles(project);
            var original = BigSource(20);
            var sha = files.Write("Hero.cs", original, "").Sha256;
            var rewritten = original.Replace("return 3;", "return 33;") + "// fim\n";
            files.Write("Hero.cs", rewritten, sha);
            Assert.Equal(rewritten, project.ReadText("Hero.cs"));
            // Tiny files can be rewritten freely.
            var small = files.Write("Small.cs", "class A {}\nclass B {}\n", "").Sha256;
            files.Write("Small.cs", "class A {}\n", small);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // ---------- surgical replace ----------

    [Fact]
    public void Replace_ToleratesIndentationLineEndingsAndLineNumberPrefixes()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Replace");
            var files = new LunetAiProjectFiles(project);
            var original = BigSource(5);
            var sha = files.Write("Hero.cs", original, "").Sha256;

            // Wrong indentation and CRLF: still one unique match, only that span changes.
            var change = files.Replace("Hero.cs", "public int Value2()\r\n{\r\n    return 2;", "public int Value2()\n    {\n        return 22;", sha);
            var after = project.ReadText("Hero.cs");
            Assert.Contains("return 22;", after);
            Assert.Equal(original.Split('\n').Length, after.Split('\n').Length);
            Assert.Contains("return 4;", after);

            // Copied with the "N| " prefixes of a numbered read.
            var numbered = LunetAiTextMatch.Numbered(after, 1).Split('\n').First(l => l.Contains("return 4;"));
            files.Replace("Hero.cs", numbered, "        return 44;", change.Sha256);
            Assert.Contains("return 44;", project.ReadText("Hero.cs"));
            Assert.DoesNotContain("| ", project.ReadText("Hero.cs"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Replace_ExplainsAmbiguityAndMissesWithLineNumbers()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Ambiguous");
            var files = new LunetAiProjectFiles(project);
            var sha = files.Write("Hero.cs", BigSource(3), "").Sha256;
            var ambiguous = Assert.Throws<InvalidOperationException>(() => files.Replace("Hero.cs", "    {", "{", sha));
            Assert.Contains("linhas", ambiguous.Message);
            var missing = Assert.Throws<InvalidOperationException>(() => files.Replace("Hero.cs", "public int Value1()\n{\n  return 999;", "x", sha));
            Assert.Contains("linhas 9", missing.Message);
            Assert.Equal(BigSource(3), project.ReadText("Hero.cs"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ReadTool_ReturnsNumberedLines()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Numbered");
            new LunetAiProjectFiles(project).Write("Hero.cs", BigSource(2), "");
            var tool = new LunetAiProjectTool(project, LunetAiProjectTool.ReadName);
            var outcome = await tool.InvokeAsync(new ToolInvocation("c1", LunetAiProjectTool.ReadName,
                JsonSerializer.SerializeToElement(new { path = "Hero.cs" })), null!, TestContext.Current.CancellationToken);
            Assert.False(outcome.IsError);
            Assert.Contains(" 3| public sealed class Hero", outcome.Content);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // ---------- undo survives cancel and restart ----------

    [Fact]
    public async Task CancelledRun_KeepsUndo_EvenAfterTheAppRestarts()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Cancel");
            var entry = project.Manifest.EntryPoint;
            var original = project.ReadText(entry);
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var provider = new ScriptedProvider(cancel,
                Tool("lunet.files.replace", new { path = entry, old_text = original.Split('\n')[0], new_text = "// mudado pela IA",
                    expected_sha256 = LunetAiProjectFiles.Hash(original) }),
                Tool("lunet.files.write", new { path = "Enemy.cs", content = "class Enemy {}\n", expected_sha256 = "" }),
                null); // third call: the person cancels
            var historyPath = Path.Combine(root, "ai", "h.json");
            var session = new LunetAiSession(project, historyPath);
            try
            {
                await session.AskAsync("mude o jogo", new LunetAiRequest(
                    LunetAiSettings.Default with { Model = "m", AllowWrites = true }, "chave-de-teste-123456789",
                    LunetAiEditorContext.Empty, new FakeBuild(), provider, Mode: LunetAiMode.Do), cancel.Token);
            }
            catch (OperationCanceledException) { }
            Assert.Contains("// mudado pela IA", project.ReadText(entry));
            Assert.True(File.Exists(Path.Combine(project.Directory, "Enemy.cs")));

            // "Restart": a new session reads the history and the persisted checkpoint from disk.
            var reopened = new LunetAiSession(project, historyPath);
            var answer = reopened.History[^1];
            Assert.Contains("cancelada", answer.Text);
            Assert.NotNull(answer.RunId);
            var checkpoint = Assert.IsType<LunetAiCheckpoint>(reopened.CheckpointFor(answer));
            Assert.Equal(2, checkpoint.Changes().Count);
            checkpoint.Revert();
            Assert.Equal(original, project.ReadText(entry));
            Assert.False(File.Exists(Path.Combine(project.Directory, "Enemy.cs")));
            Assert.True(new LunetAiSession(project, historyPath).CheckpointFor(answer)!.IsReverted);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // ---------- the model sees the real code ----------

    [Fact]
    public async Task OpenFileContentReachesTheModel_Numbered_WithItsHash()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Context");
            new LunetAiProjectFiles(project).Write("Hero.cs", BigSource(3), "");
            var provider = new ScriptedProvider(null, Text("Value1 devolve 1 (Hero.cs:11)."));
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            await session.AskAsync("o que Value1 faz?", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m", AllowWrites = false }, "chave-de-teste-123456789",
                new LunetAiEditorContext("Hero.cs", null, 9, [], []), new FakeBuild(), provider), TestContext.Current.CancellationToken);
            var prompt = string.Join("\n", provider.Requests[0].Messages.SelectMany(m => m.Content).OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("CONTEÚDO DO ARQUIVO ABERTO Hero.cs", prompt);
            Assert.Contains("11|         return 1;", prompt);
            Assert.Contains(LunetAiProjectFiles.Hash(BigSource(3)), prompt);
            Assert.Contains("lunet.files.replace", LunetAiSession.SystemPrompt);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // ---------- editor never overwrites the AI silently ----------

    [Fact]
    public void EditorSession_DetectsFilesChangedOnDisk()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("Editor");
            var entry = project.Manifest.EntryPoint;
            var original = project.ReadText(entry);
            var journal = new AutosaveJournal(project);
            var editor = new EditorSession();
            editor.Load(entry, original);

            project.WriteText(entry, original + "// IA\n"); // the AI edits while the editor is open
            Assert.True(editor.ChangedOnDisk(project));
            // Not edited locally: the stale editor text is never written over the AI's version.
            Assert.False(editor.TrySave(project, original, journal));
            Assert.Contains("// IA", project.ReadText(entry));

            // Edited locally too: conflict, nothing overwritten, the person's text is kept in the journal.
            var conflict = Assert.Throws<EditorConflictException>(() => editor.TrySave(project, original + "// minha\n", journal));
            Assert.Contains("// IA", conflict.DiskText);
            Assert.Contains("// IA", project.ReadText(entry));
            Assert.Contains(journal.FindRecoveries(), r => r.RecoveredText.Contains("// minha"));

            editor.ForceSave(project, original + "// minha\n", journal);
            Assert.Contains("// minha", project.ReadText(entry));
            Assert.False(editor.ChangedOnDisk(project));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // ---------- helpers ----------

    private static ModelResponse Text(string text) =>
        new([new TextBlock(text)], StopReason.EndTurn, new Usage(1, 1, Money.Zero("USD")));

    private static ModelResponse Tool(string name, object input) =>
        new([new ToolUseBlock("call-" + Guid.NewGuid().ToString("N")[..6], name, JsonSerializer.SerializeToElement(input))],
            StopReason.ToolUse, new Usage(1, 1, Money.Zero("USD")));

    /// <summary>Plays a script; a null entry cancels <paramref name="cancel"/> (the person pressing Stop).</summary>
    private sealed class ScriptedProvider(CancellationTokenSource? cancel, params ModelResponse?[] script) : IModelProvider
    {
        private int _next;
        public List<ModelRequest> Requests { get; } = [];
        public string ProviderId => "chat-completions";
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var response = script[Math.Min(_next++, script.Length - 1)];
            if (response is null)
            {
                cancel!.Cancel();
                throw new OperationCanceledException(cancel.Token);
            }
            return ValueTask.FromResult(response);
        }
    }

    private sealed class FakeBuild : ILunetAiBuild
    {
        public Task<LunetAiBuildReport> CompileAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new LunetAiBuildReport(true, []));
    }
}
