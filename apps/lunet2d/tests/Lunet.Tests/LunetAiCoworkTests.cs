using System.Text.Json;
using AgentRuntime;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>LUNET-481: IA configurada uma vez, com contexto do editor, compilação como verificação e checkpoint.</summary>
public sealed class LunetAiCoworkTests
{
    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "lunet-ai-cowork-" + Guid.NewGuid().ToString("N"));

    // ---------- Providers ----------

    [Fact]
    public void Presets_CoverMainProvidersWithValidEndpoints()
    {
        var ids = LunetAiProviders.All.Select(p => p.Id).ToArray();
        Assert.Contains("openrouter", ids);
        Assert.Contains("openai", ids);
        Assert.Contains("anthropic", ids);
        Assert.Contains("gemini", ids);
        Assert.Contains("custom", ids);
        foreach (var preset in LunetAiProviders.All.Where(p => p.Id != "custom"))
        {
            LunetAiEndpoint.Validate(preset.Endpoint);
            Assert.NotEmpty(preset.SuggestedModels);
            Assert.StartsWith("https://", preset.KeyUrl, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Presets_FindFallsBackToCustom()
    {
        Assert.Equal("openrouter", LunetAiProviders.Find("openrouter").Id);
        Assert.Equal("custom", LunetAiProviders.Find("nao-existe").Id);
        Assert.Equal("custom", LunetAiProviders.Find(null).Id);
    }

    // ---------- Settings & secret ----------

    [Fact]
    public void Settings_RoundTripWithoutEverWritingTheKey()
    {
        var root = TempRoot();
        try
        {
            var vault = new MemorySecretVault();
            var store = new LunetAiSettingsStore(Path.Combine(root, "ai-settings.json"), vault);
            Assert.False(store.Load().IsConfigured);

            store.Save(new LunetAiSettings("anthropic", LunetAiProviders.Find("anthropic").Endpoint, "claude-sonnet-5", 64, true),
                "chave-de-teste-anthropic-123456");
            var reopened = new LunetAiSettingsStore(Path.Combine(root, "ai-settings.json"), vault).Load();
            Assert.True(reopened.IsConfigured);
            Assert.Equal("anthropic", reopened.Settings.ProviderId);
            Assert.Equal("claude-sonnet-5", reopened.Settings.Model);
            Assert.Equal(64, reopened.Settings.MaxSteps);
            Assert.True(reopened.Settings.AllowWrites);
            Assert.Equal("chave-de-teste-anthropic-123456", reopened.ApiKey);
            Assert.DoesNotContain("chave-de-teste", File.ReadAllText(Path.Combine(root, "ai-settings.json")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Settings_CorruptFileFallsBackToDefaults()
    {
        var root = TempRoot();
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "ai-settings.json");
            File.WriteAllText(path, "{ not json");
            var loaded = new LunetAiSettingsStore(path, new MemorySecretVault()).Load();
            Assert.False(loaded.IsConfigured);
            Assert.Equal("openrouter", loaded.Settings.ProviderId);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Settings_ClearKeyRemovesSecret()
    {
        var vault = new MemorySecretVault();
        var store = new LunetAiSettingsStore(Path.Combine(TempRoot(), "s.json"), vault);
        store.Save(LunetAiSettings.Default with { Model = "x" }, "chave-de-teste-123456789");
        store.ClearKey();
        Assert.Null(vault.Get(LunetAiSettingsStore.SecretName));
        Assert.False(store.Load().IsConfigured);
    }

    // ---------- Context ----------

    [Fact]
    public void EditorContext_IsComposedBoundedAndLabeled()
    {
        var context = new LunetAiEditorContext(
            OpenFile: "Game.cs",
            Selection: "player.X += speed;",
            CaretLine: 42,
            Diagnostics: ["Game.cs(10,5): error CS0103: O nome 'speed' não existe"],
            ConsoleTail: ["[Info] iniciou"]);
        var text = context.Describe();
        Assert.Contains("Game.cs", text);
        Assert.Contains("linha 42", text);
        Assert.Contains("player.X += speed;", text);
        Assert.Contains("CS0103", text);
        Assert.Contains("iniciou", text);

        var huge = new LunetAiEditorContext("A.cs", new string('x', 50_000), 1,
            Enumerable.Range(0, 500).Select(i => "erro " + i).ToArray(), []);
        Assert.True(huge.Describe().Length <= LunetAiEditorContext.MaxLength);
        Assert.True(LunetAiEditorContext.Empty.Describe().Length == 0);
    }

    // ---------- Diff ----------

    [Fact]
    public void LineDiff_ShowsAddedAndRemovedLines()
    {
        var diff = LunetAiDiff.Lines("a\nb\nc\n", "a\nB\nc\nd\n");
        Assert.Contains(diff, l => l.Kind == DiffKind.Removed && l.Text == "b");
        Assert.Contains(diff, l => l.Kind == DiffKind.Added && l.Text == "B");
        Assert.Contains(diff, l => l.Kind == DiffKind.Added && l.Text == "d");
        Assert.Contains(diff, l => l.Kind == DiffKind.Same && l.Text == "a");
        Assert.Equal((2, 1), LunetAiDiff.Count(diff));
    }

    [Fact]
    public void LineDiff_HandlesLargeFilesWithoutQuadraticBlowUp()
    {
        var before = string.Join("\n", Enumerable.Range(0, 20_000).Select(i => "line " + i));
        var after = before.Replace("line 10000", "line changed");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var diff = LunetAiDiff.Lines(before, after);
        Assert.True(sw.ElapsedMilliseconds < 3_000);
        Assert.Equal((1, 1), LunetAiDiff.Count(diff));
    }

    // ---------- Session: build verification + checkpoint ----------

    [Fact]
    public async Task Agent_MustRepairUntilProjectCompiles_AndCheckpointCanRevert()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("AiCowork");
            var entry = project.Manifest.EntryPoint;
            var original = project.ReadText(entry);
            var sha = LunetAiProjectFiles.Hash(original);
            var build = new FakeBuild(project);
            var provider = new ScriptedProvider(
                Tool("lunet.files.write", new { path = entry, content = original + "\nBROKEN", expected_sha256 = sha }),
                Text("Pronto."),
                // After verification fails with the build error the agent must fix it.
                Tool("lunet.build.compile", new { }),
                Tool("lunet.files.write", new { path = entry, content = original + "\n// corrigido\n",
                    expected_sha256 = LunetAiProjectFiles.Hash(original + "\nBROKEN") }),
                Text("Corrigido e compilado."));
            var session = new LunetAiSession(project, Path.Combine(root, "history.json"));
            var reply = await session.AskAsync("adicione um comentário", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m", AllowWrites = true }, "chave-de-teste-123456789",
                LunetAiEditorContext.Empty, build, provider), TestContext.Current.CancellationToken);

            Assert.Equal(RunStatus.Succeeded, reply.Status);
            Assert.True(build.Compilations >= 2);
            Assert.Contains("// corrigido", project.ReadText(entry));
            Assert.Contains(provider.Requests.SelectMany(r => r.Messages).SelectMany(m => m.Content).OfType<TextBlock>(),
                t => t.Text.Contains("BROKEN", StringComparison.Ordinal) && t.Text.Contains("erro", StringComparison.OrdinalIgnoreCase));

            var change = Assert.Single(reply.Changes);
            Assert.Equal(entry, change.Path);
            Assert.Equal(original, change.Before);
            Assert.Contains("// corrigido", change.After);

            reply.Checkpoint.Revert();
            Assert.Equal(original, project.ReadText(entry));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Checkpoint_RevertDeletesFilesCreatedByTheAgent()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("AiCreate");
            var provider = new ScriptedProvider(
                Tool("lunet.files.write", new { path = "Enemy.cs", content = "class Enemy {}\n", expected_sha256 = "" }),
                Text("Criei Enemy.cs."));
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            var reply = await session.AskAsync("crie um inimigo", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m", AllowWrites = true }, "chave-de-teste-123456789",
                LunetAiEditorContext.Empty, new FakeBuild(project), provider), TestContext.Current.CancellationToken);
            Assert.Equal(RunStatus.Succeeded, reply.Status);
            var change = Assert.Single(reply.Changes);
            Assert.Null(change.Before);
            Assert.True(File.Exists(Path.Combine(project.Directory, "Enemy.cs")));
            reply.Checkpoint.Revert();
            Assert.False(File.Exists(Path.Combine(project.Directory, "Enemy.cs")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task EditorContextReachesTheModel_AndReadOnlyHidesWriteTools()
    {
        var root = TempRoot();
        try
        {
            var project = new ProjectStore(root).Create("AiAsk");
            var provider = new ScriptedProvider(Text("A variável speed não foi declarada."));
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            var reply = await session.AskAsync("por que não compila?", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m", AllowWrites = false }, "chave-de-teste-123456789",
                new LunetAiEditorContext("Game.cs", "x += speed;", 7, ["error CS0103 speed"], []), new FakeBuild(project), provider), TestContext.Current.CancellationToken);
            Assert.Equal(RunStatus.Succeeded, reply.Status);
            var first = provider.Requests[0];
            var prompt = string.Join("\n", first.Messages.SelectMany(m => m.Content).OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains("x += speed;", prompt);
            Assert.Contains("CS0103", prompt);
            Assert.DoesNotContain(first.Tools, t => t.Name.Contains("write", StringComparison.Ordinal));
            Assert.Contains(first.Tools, t => t.Name.Contains("compile", StringComparison.Ordinal));
            Assert.Empty(reply.Changes);
            Assert.Equal(2, session.History.Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // ---------- helpers ----------

    private static ModelResponse Text(string text) =>
        new([new TextBlock(text)], StopReason.EndTurn, new Usage(1, 1, Money.Zero("USD")));

    private static ModelResponse Tool(string name, object input) =>
        new([new ToolUseBlock("call-" + Guid.NewGuid().ToString("N")[..6], name, JsonSerializer.SerializeToElement(input))],
            StopReason.ToolUse, new Usage(1, 1, Money.Zero("USD")));

    private sealed class ScriptedProvider(params ModelResponse[] script) : IModelProvider
    {
        private int _next;
        public List<ModelRequest> Requests { get; } = [];
        public string ProviderId => "chat-completions";
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            // Map the logical names used in the script to the wire names advertised in the request.
            var response = script[Math.Min(_next++, script.Length - 1)];
            var mapped = response.Content.Select(b => b is ToolUseBlock use
                ? use with { Name = request.Tools.FirstOrDefault(t => t.Name == use.Name || t.Name == use.Name.Replace('.', '_'))?.Name ?? use.Name }
                : b).ToArray();
            return ValueTask.FromResult(response with { Content = mapped });
        }
    }

    private sealed class FakeBuild(LunetProject project) : ILunetAiBuild
    {
        public int Compilations { get; private set; }
        public Task<LunetAiBuildReport> CompileAsync(CancellationToken cancellationToken)
        {
            Compilations++;
            var broken = project.LoadSources().FirstOrDefault(s => s.Text.Contains("BROKEN", StringComparison.Ordinal));
            return Task.FromResult(broken is null
                ? new LunetAiBuildReport(true, [])
                : new LunetAiBuildReport(false, [$"{broken.Path}(1,1): error CS0103: O nome 'BROKEN' não existe"]));
        }
    }

    private sealed class MemorySecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Get(string name) => _values.GetValueOrDefault(name);
        public void Set(string name, string value) => _values[name] = value;
        public void Delete(string name) => _values.Remove(name);
    }
}
