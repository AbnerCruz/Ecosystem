using System.Net;
using System.Text.Json;
using AgentRuntime;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>
/// LUNET-498: the AI understands whether the person wants to learn or wants it done, starts knowing the project
/// (fewer round-trips), organizes code in files and folders, researches references and has richer settings.
/// </summary>
public sealed class LunetAiIntentTests
{
    // ---------- Teach vs Do ----------

    [Theory]
    [InlineData("Como faço para o personagem pular?")]
    [InlineData("como eu faço pra criar um inimigo no jogo?")]
    [InlineData("Me explica como funciona o SpriteBatch")]
    [InlineData("o que é o GameTime?")]
    [InlineData("Quero aprender a fazer um menu")]
    [InlineData("Qual a diferença entre Update e Draw?")]
    [InlineData("por que meu sprite está borrado?")]
    [InlineData("me ensina a tocar um som quando colidir")]
    [InlineData("Como funciona a física no Lunet")]
    [InlineData("o inimigo deveria seguir o jogador?")]
    public void Classify_QuestionsAndLearningAreTeach(string prompt) =>
        Assert.Equal(LunetAiMode.Teach, LunetAiIntent.Classify(prompt));

    [Theory]
    [InlineData("Faça o herói pular quando tocar na tela")]
    [InlineData("crie um inimigo que persegue o jogador")]
    [InlineData("adicione um placar no canto da tela")]
    [InlineData("Como faço para o personagem pular? Faz pra mim")]
    [InlineData("você pode criar um menu inicial?")]
    [InlineData("quero que você implemente o pulo duplo")]
    [InlineData("corrige o erro de compilação")]
    [InlineData("inimigos que perseguem o jogador")]
    [InlineData("Separe o Game.cs em arquivos por responsabilidade")]
    public void Classify_OrdersAreDo(string prompt) =>
        Assert.Equal(LunetAiMode.Do, LunetAiIntent.Classify(prompt));

    [Fact]
    public void Resolve_ExplicitChoiceWins()
    {
        Assert.Equal(LunetAiMode.Do, LunetAiIntent.Resolve(LunetAiMode.Do, "como faço para pular?"));
        Assert.Equal(LunetAiMode.Teach, LunetAiIntent.Resolve(LunetAiMode.Teach, "crie um inimigo"));
        Assert.Equal(LunetAiMode.Teach, LunetAiIntent.Resolve(LunetAiMode.Auto, "como faço para pular?"));
    }

    [Fact]
    public void Normalize_RemovesAccentsCaseAndExtraSpaces() =>
        Assert.Equal("como faco para pular?", LunetAiIntent.Normalize("  Como   FAÇO\tpara pular? "));

    [Fact]
    public async Task TeachMode_DoesNotOfferWriteToolsAndNeverChangesTheProject()
    {
        var root = Temp();
        try
        {
            var project = new ProjectStore(root).Create("AiTeach");
            var before = project.ReadText("Game.cs");
            var provider = new Recording(("lunet.files.write", new { path = "Game.cs", content = "quebrado", expected_sha256 = "" }));
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            var reply = await session.AskAsync("Como faço para o personagem pular?", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m" }, "chave-de-teste-123456789", LunetAiEditorContext.Empty,
                Build: new NoBuild(), Provider: provider), TestContext.Current.CancellationToken);
            Assert.Equal(LunetAiMode.Teach, session.LastMode);
            var tools = provider.Requests[0].Tools.Select(t => t.Name).ToArray();
            Assert.DoesNotContain(LunetAiProjectTool.WriteName, tools);
            Assert.DoesNotContain(LunetAiProjectTool.ReplaceName, tools);
            Assert.DoesNotContain(LunetAiSpriteTool.Name, tools);
            Assert.Contains(LunetAiBuildTool.Name, tools); // compiling changes nothing and helps explain errors
            Assert.Contains(LunetAiOutlineTool.Name, tools);
            Assert.Equal(before, project.ReadText("Game.cs"));
            Assert.Empty(reply.Changes);
            Assert.Contains("MODO ENSINAR", provider.Requests[0].Messages[0].Content.OfType<TextBlock>().First().Text);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task DoMode_OffersWriteToolsAndSendsTheProjectMapAndQuickReference()
    {
        var root = Temp();
        try
        {
            var project = new ProjectStore(root).Create("AiDo");
            var provider = new Recording();
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            await session.AskAsync("crie um inimigo", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m" }, "chave-de-teste-123456789", LunetAiEditorContext.Empty,
                Build: new NoBuild(), Provider: provider), TestContext.Current.CancellationToken);
            Assert.Equal(LunetAiMode.Do, session.LastMode);
            var request = provider.Requests[0];
            Assert.Contains(LunetAiProjectTool.WriteName, request.Tools.Select(t => t.Name));
            Assert.Contains(LunetAiBuildTool.Name, request.Tools.Select(t => t.Name));
            var task = request.Messages[0].Content.OfType<TextBlock>().First().Text;
            Assert.Contains("MODO FAZER", task);
            Assert.Contains("MAPA DO PROJETO", task);
            Assert.Contains("Game.cs", task);
            Assert.Contains("protected override void LoadContent", task); // the outline names the real members
            Assert.Contains("SpriteBatch", task); // quick reference
            Assert.Contains("ARQUITETURA", request.System);
        }
        finally { Delete(root); }
    }

    [Fact]
    public async Task WritesDisabled_DoRequestFallsBackToTeaching()
    {
        var root = Temp();
        try
        {
            var project = new ProjectStore(root).Create("AiReadOnly");
            var provider = new Recording();
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            await session.AskAsync("crie um inimigo", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m", AllowWrites = false }, "chave-de-teste-123456789",
                LunetAiEditorContext.Empty, Provider: provider), TestContext.Current.CancellationToken);
            Assert.Equal(LunetAiMode.Teach, session.LastMode);
            Assert.DoesNotContain(LunetAiProjectTool.WriteName, provider.Requests[0].Tools.Select(t => t.Name));
        }
        finally { Delete(root); }
    }

    // ---------- Project map, outline and architecture ----------

    [Fact]
    public void Parse_ListsTypesFieldsPropertiesAndMethodsWithLines()
    {
        var symbols = LunetAiProjectMap.Parse("Entities/Player.cs", """
            namespace Jogo;
            public sealed class Player : Entity
            {
                private float _speed = 120f;
                public int Lives { get; private set; } = 3;
                public Player(Vector2 start) { }
                public void Jump(float force) { }
            }
            public enum State { Idle, Run }
            """);
        Assert.Contains(symbols, s => s.Kind == "class" && s.Name == "Player" && s.Signature.Contains(": Entity") && s.Line == 2);
        Assert.Contains(symbols, s => s.Kind == "field" && s.Name == "_speed" && s.Container == "Player");
        Assert.Contains(symbols, s => s.Kind == "property" && s.Name == "Lives");
        Assert.Contains(symbols, s => s.Kind == "method" && s.Signature == "public void Jump(float force)" && s.Line == 7);
        Assert.Contains(symbols, s => s.Kind == "values" && s.Signature == "Idle, Run");
    }

    [Fact]
    public void Describe_IncludesFoldersMediaAndCodeWithinBudget()
    {
        var root = Temp();
        try
        {
            var project = new ProjectStore(root).Create("AiMap");
            project.WriteText("Entities/Enemy.cs", "namespace AiMap; public sealed class Enemy { public void Chase() { } }");
            Directory.CreateDirectory(Path.Combine(project.Directory, "Content", "Textures"));
            File.WriteAllBytes(Path.Combine(project.Directory, "Content", "Textures", "hero.png"), [1, 2, 3]);
            var map = new LunetAiProjectMap(project).Describe();
            Assert.Contains("Entities/: Enemy.cs", map);
            Assert.Contains("Content/Textures/hero.png", map);
            Assert.Contains("public void Chase()", map);
            Assert.True(new LunetAiProjectMap(project).Describe(200).Length < 300);
        }
        finally { Delete(root); }
    }

    [Fact]
    public void Architecture_FlagsGodFilesAndManyClassesInOneFile()
    {
        var symbols = LunetAiProjectMap.Parse("Game.cs",
            "class Game {} class Player {} class Enemy {} class Hud {}");
        var warnings = LunetAiArchitecture.Review(symbols, new Dictionary<string, int> { ["Game.cs"] = 900 });
        Assert.Contains(warnings, w => w.Contains("4 tipos"));
        Assert.Contains(warnings, w => w.Contains("900 linhas"));
        Assert.Empty(LunetAiArchitecture.Review(LunetAiProjectMap.Parse("Entities/Player.cs", "class Player {}"),
            new Dictionary<string, int> { ["Entities/Player.cs"] = 40 }));
    }

    [Fact]
    public async Task OutlineTool_FiltersByFileAndName()
    {
        var root = Temp();
        try
        {
            var project = new ProjectStore(root).Create("AiOutline");
            project.WriteText("Systems/Score.cs", "class Score { public int Points; public void Add(int n) {} }");
            var tool = new LunetAiOutlineTool(project);
            var outcome = await tool.InvokeAsync(new ToolInvocation("c", LunetAiOutlineTool.Name,
                JsonSerializer.SerializeToElement(new { query = "Add" })), Context(), TestContext.Current.CancellationToken);
            Assert.False(outcome.IsError);
            Assert.Contains("public void Add(int n)", outcome.Content);
            Assert.DoesNotContain("LoadContent", outcome.Content);
        }
        finally { Delete(root); }
    }

    // ---------- Web research ----------

    [Theory]
    [InlineData("http://localhost/x")]
    [InlineData("http://127.0.0.1/x")]
    [InlineData("http://192.168.0.10/")]
    [InlineData("http://10.0.0.2/")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:pw@example.com/")]
    public void Web_RejectsPrivateAndNonHttpAddresses(string url) =>
        Assert.Throws<ArgumentException>(() => LunetAiWebSession.ValidateUrl(url));

    [Fact]
    public void Web_AcceptsPublicHttps() =>
        Assert.Equal("github.com", LunetAiWebSession.ValidateUrl("https://github.com/MonoGame/MonoGame.Samples").Host);

    [Fact]
    public void Web_ParsesBingRssAndKeepsCodeLines()
    {
        var hits = LunetAiWebSession.ParseRss("""
            <rss><channel>
              <item><title>Platformer jump</title><link>https://example.com/jump</link><description>Coyote &lt;b&gt;time&lt;/b&gt;</description></item>
              <item><title>bad</title><link>javascript:alert(1)</link><description>x</description></item>
            </channel></rss>
            """, 5);
        var hit = Assert.Single(hits);
        Assert.Equal("https://example.com/jump", hit.Url);
        Assert.Equal("Coyote time", hit.Snippet.Replace("  ", " "));
        var text = LunetAiWebSession.HtmlToText("<html><body><script>evil()</script><p>Intro</p><pre><code>if (x)\n    Jump();</code></pre></body></html>");
        Assert.DoesNotContain("evil", text);
        Assert.Contains("    Jump();", text);
        Assert.Contains("Intro", text);
    }

    [Fact]
    public void Web_BlocksSecretsAndCopiesOfTheProjectCode()
    {
        var code = "public sealed class MeuJogoSecreto : Game { private readonly Dictionary<string, int> _placarDosJogadores = new(); }";
        Assert.NotNull(LunetAiWebTool.OutboundProblem("token=abcdef123456", []));
        Assert.NotNull(LunetAiWebTool.OutboundProblem(code[..80], [code]));
        Assert.Null(LunetAiWebTool.OutboundProblem("monogame platformer coyote time c#", [code]));
    }

    [Fact]
    public async Task WebTools_AreOfferedOnlyWhenResearchIsAvailable()
    {
        var root = Temp();
        try
        {
            var project = new ProjectStore(root).Create("AiWeb");
            var with = new Recording(("lunet.web.search", new { query = "c# platformer jump" }));
            var fake = new FakeWeb();
            await new LunetAiSession(project, Path.Combine(root, "a.json")).AskAsync("crie um pulo", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m" }, "chave-de-teste-123456789", LunetAiEditorContext.Empty,
                Provider: with, Web: fake), TestContext.Current.CancellationToken);
            Assert.Contains(LunetAiWebTool.SearchName, with.Requests[0].Tools.Select(t => t.Name));
            Assert.Equal("c# platformer jump", fake.LastQuery);
            var without = new Recording();
            await new LunetAiSession(project, Path.Combine(root, "b.json")).AskAsync("crie um pulo", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m" }, "chave-de-teste-123456789", LunetAiEditorContext.Empty,
                Provider: without), TestContext.Current.CancellationToken);
            Assert.DoesNotContain(LunetAiWebTool.SearchName, without.Requests[0].Tools.Select(t => t.Name));
        }
        finally { Delete(root); }
    }

    // ---------- Settings (multi-provider keys, models, connection test) ----------

    [Fact]
    public void Settings_KeepOneKeyPerProviderAndReadOldSharedKey()
    {
        var root = Temp();
        try
        {
            var vault = new MemorySecretVault();
            vault.Set(LunetAiSettingsStore.SecretName, "chave-antiga-compartilhada-123");
            var store = new LunetAiSettingsStore(Path.Combine(root, "s.json"), vault);
            Assert.Equal("chave-antiga-compartilhada-123", store.KeyFor("openrouter"));
            store.Save(LunetAiSettings.Default with { ProviderId = "openai", Model = "gpt" }, "chave-openai-1234567890");
            store.Save(LunetAiSettings.Default with { ProviderId = "groq", Model = "llama" }, "chave-groq-1234567890");
            Assert.Equal("chave-openai-1234567890", store.KeyFor("openai"));
            Assert.Equal("chave-groq-1234567890", store.Load().ApiKey);
            Assert.True(store.HasKey("openai"));
            store.ClearKey("groq");
            Assert.False(store.HasKey("groq"));
            Assert.True(store.HasKey("openai"));
        }
        finally { Delete(root); }
    }

    [Fact]
    public void Settings_NewOptionsRoundTripAndOldFilesGetDefaults()
    {
        var root = Temp();
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "s.json");
            File.WriteAllText(path, """{"ProviderId":"openai","Endpoint":"https://api.openai.com/v1/chat/completions","Model":"gpt","MaxSteps":32,"AllowWrites":true}""");
            var store = new LunetAiSettingsStore(path, new MemorySecretVault());
            var old = store.Load().Settings;
            Assert.True(old.WebResearch);
            Assert.Equal(LunetAiMode.Auto, old.DefaultMode);
            store.Save(old with { WebResearch = false, DefaultMode = LunetAiMode.Teach }, null);
            var reloaded = store.Load().Settings;
            Assert.False(reloaded.WebResearch);
            Assert.Equal(LunetAiMode.Teach, reloaded.DefaultMode);
        }
        finally { Delete(root); }
    }

    [Theory]
    [InlineData("https://openrouter.ai/api/v1/chat/completions", "https://openrouter.ai/api/v1/models")]
    [InlineData("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "https://generativelanguage.googleapis.com/v1beta/openai/models")]
    public void Models_UrlSitsNextToChatEndpoint(string chat, string models) =>
        Assert.Equal(models, LunetAiModelCatalog.ModelsUrl(chat).ToString());

    [Fact]
    public void Models_ParseOpenAiOpenRouterAndGeminiShapes()
    {
        var openRouter = LunetAiModelCatalog.Parse("""
            {"data":[{"id":"z/no-tools","name":"Z","supported_parameters":["temperature"]},
                     {"id":"a/coder","name":"A Coder","context_length":200000,"supported_parameters":["tools"]}]}
            """);
        Assert.Equal("a/coder", openRouter[0].Id); // tool-capable first
        Assert.Equal(200000, openRouter[0].Context);
        Assert.False(openRouter[1].Tools);
        var gemini = LunetAiModelCatalog.Parse("""{"models":[{"name":"models/gemini-3-pro"}]}""");
        Assert.Equal("gemini-3-pro", Assert.Single(gemini).Id);
        Assert.Empty(LunetAiModelCatalog.Parse("""{"object":"list"}"""));
    }

    [Fact]
    public async Task Models_ListAndTestUseTheKeyAndExplainFailures()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/models")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"id":"m1"}]}""") }
            : new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("""{"error":{"message":"Incorrect API key sk-abcdef123456"}}""") });
        var catalog = new LunetAiModelCatalog(new HttpClient(handler));
        var models = await catalog.ListAsync("openai", "https://api.openai.com/v1/chat/completions", "chave-1234567890", TestContext.Current.CancellationToken);
        Assert.Equal("m1", Assert.Single(models).Id);
        Assert.Equal("Bearer chave-1234567890", handler.LastAuthorization);
        var check = await catalog.TestAsync("openai", "https://api.openai.com/v1/chat/completions", "chave-1234567890", "m1", TestContext.Current.CancellationToken);
        Assert.False(check.Ok);
        Assert.Contains("chave foi recusada", check.Message);
        Assert.DoesNotContain("sk-abcdef123456", check.Message);
    }

    // ---------- helpers ----------

    private static string Temp() => Path.Combine(Path.GetTempPath(), "lunet-ai-intent-" + Guid.NewGuid().ToString("N"));
    private static void Delete(string root) { if (Directory.Exists(root)) Directory.Delete(root, true); }

    private static ToolContext Context() =>
        new("run", new AgentIdentity("a", "a", "a"), ContextPath.Create(new ContextStep(ContextLevel.Project, "p")));

    private sealed class Recording(params (string Tool, object Input)[] calls) : IModelProvider
    {
        private int _step;
        public List<ModelRequest> Requests { get; } = [];
        public string ProviderId => "chat-completions";
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var usage = new Usage(1, 1, Money.Zero("USD"));
            if (_step < calls.Length)
            {
                var (tool, input) = calls[_step++];
                return ValueTask.FromResult(new ModelResponse([new ToolUseBlock("c" + _step, tool, JsonSerializer.SerializeToElement(input))], StopReason.ToolUse, usage));
            }
            return ValueTask.FromResult(new ModelResponse([new TextBlock("ok")], StopReason.EndTurn, usage));
        }
    }

    private sealed class MemorySecretVault : ISecretVault
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Get(string name) => _values.GetValueOrDefault(name);
        public void Set(string name, string value) => _values[name] = value;
        public void Delete(string name) => _values.Remove(name);
    }

    private sealed class NoBuild : ILunetAiBuild
    {
        public Task<LunetAiBuildReport> CompileAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new LunetAiBuildReport(true, []));
    }

    private sealed class FakeWeb : ILunetAiWeb
    {
        public string? LastQuery { get; private set; }
        public Task<IReadOnlyList<LunetAiWebHit>> SearchAsync(string query, int count, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<LunetAiWebHit>>([new LunetAiWebHit("t", "https://example.com", "s")]);
        }
        public Task<string> FetchAsync(Uri url, int startLine, int maxLines, CancellationToken cancellationToken) => Task.FromResult("texto");
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public string? LastAuthorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAuthorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(answer(request));
        }
    }
}
