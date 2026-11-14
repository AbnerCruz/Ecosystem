using System.Text.Json;
using AgentRuntime;
using Lunet.Ai;
using Lunet.Core;
using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>LUNET-488/489: the AI knows Lunet, sees images as pixel grids and keeps the project's art direction.</summary>
public sealed class LunetAiExpertiseTests
{
    private static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "docs", "api", "lunet-framework.json"))) dir = Path.GetDirectoryName(dir)!;
        return dir;
    }

    private static LunetAiKnowledge RealKnowledge() => LunetAiKnowledge.Create(
        File.ReadAllText(Path.Combine(Root, "docs", "api", "lunet-framework.json")),
        Directory.GetFiles(Path.Combine(Root, "docs", "guides"), "*.md")
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f), File.ReadAllText));

    private static ToolContext Ctx() => new("run", new AgentIdentity("a", "a", "a"), ContextPath.Create(new ContextStep(ContextLevel.Project, "p")));

    // ---------- Knowledge ----------

    [Fact]
    public void Knowledge_FindsApiTypesGuidesAndCapabilities()
    {
        var knowledge = RealKnowledge();
        Assert.True(knowledge.Count > 150);
        var animation = knowledge.Search("animação spritesheet", 5);
        Assert.Contains(animation, h => h.Id is "api:Lunet.Graphics.SpriteAnimator" or "api:Lunet.Graphics.SpriteSheet" or "guide:animacao");
        var physics = knowledge.Search("RigidBody2D", 3);
        Assert.Equal("api:Lunet.Physics.RigidBody2D", physics[0].Id);
        var missing = knowledge.Search("multiplayer online plugins", 5);
        Assert.Contains(missing, h => h.Id == "lunet:capacidades");
    }

    [Fact]
    public void Knowledge_ReadsMembersAndGuides_Bounded()
    {
        var knowledge = RealKnowledge();
        var type = knowledge.Read("api:Lunet.Graphics.SpriteBatch");
        Assert.NotNull(type);
        Assert.Contains("Draw", type);
        Assert.Contains("public", type);
        var guide = knowledge.Read("guide:primeiros-passos");
        Assert.Contains("Game", guide);
        Assert.Contains("ainda NÃO existe", knowledge.Read("lunet:capacidades"), StringComparison.OrdinalIgnoreCase);
        Assert.Null(knowledge.Read("api:NaoExiste"));
        Assert.All(knowledge.Search("a", 50), h => Assert.True(h.Snippet.Length <= 400));
    }

    [Fact]
    public async Task DocsTools_ReturnJsonResults()
    {
        var knowledge = RealKnowledge();
        var search = new LunetAiDocsTool(knowledge, LunetAiDocsTool.SearchName);
        var found = await search.InvokeAsync(new ToolInvocation("1", LunetAiDocsTool.SearchName,
            JsonSerializer.SerializeToElement(new { query = "tilemap colisão" })), Ctx(), TestContext.Current.CancellationToken);
        Assert.False(found.IsError);
        Assert.Contains("tilemap", found.Content, StringComparison.OrdinalIgnoreCase);
        var read = new LunetAiDocsTool(knowledge, LunetAiDocsTool.ReadName);
        var missing = await read.InvokeAsync(new ToolInvocation("2", LunetAiDocsTool.ReadName,
            JsonSerializer.SerializeToElement(new { id = "api:Inexistente" })), Ctx(), TestContext.Current.CancellationToken);
        Assert.True(missing.IsError);
    }

    // ---------- Image perception ----------

    private static LunetAiImage Drawing()
    {
        // 120x120 "paper" (off-white with noise), a black outlined circle and a red filled square.
        var rgba = new byte[120 * 120 * 4];
        var random = new Random(7);
        for (int y = 0; y < 120; y++)
            for (int x = 0; x < 120; x++)
            {
                int i = (y * 120 + x) * 4;
                byte paper = (byte)(238 + random.Next(10));
                (byte r, byte g, byte b) = (paper, paper, (byte)(paper - 6));
                double d = Math.Sqrt((x - 40) * (x - 40) + (y - 40) * (y - 40));
                if (d is > 24 and < 30) (r, g, b) = (20, 20, 25);
                if (x is >= 70 and < 110 && y is >= 70 and < 110) (r, g, b) = (220, 30, 40);
                rgba[i] = r; rgba[i + 1] = g; rgba[i + 2] = b; rgba[i + 3] = 255;
            }
        return new LunetAiImage(120, 120, rgba);
    }

    [Fact]
    public void ImageGrid_RemovesPaperAndKeepsShapesAndColors()
    {
        var grid = LunetAiImageGrid.From(Drawing(), maxSide: 24, maxColors: 6);
        Assert.Equal(24, grid.Width);
        Assert.Equal(24, grid.Height);
        Assert.Equal('.', grid.Rows[0][0]);              // paper became transparent
        Assert.Equal('.', grid.Rows[8][8]);              // inside the circle stays empty
        char red = grid.Rows[18][18];
        Assert.NotEqual('.', red);
        var redColor = grid.Palette[red];
        Assert.True((redColor >> 16 & 0xff) > 180 && (redColor >> 8 & 0xff) < 80);
        char ring = grid.Rows[8][2];                      // left edge of the ring (x≈12px → cell 2)
        Assert.NotEqual('.', ring);
        Assert.True((grid.Palette[ring] >> 16 & 0xff) < 80);
        Assert.True(grid.Palette.Count <= 7);
        Assert.Contains("legenda", grid.Describe(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageGrid_CanSnapToTheProjectPalette()
    {
        var palette = new uint[] { 0xff101018, 0xffd02838 };
        var grid = LunetAiImageGrid.From(Drawing(), 24, 6, palette);
        var used = grid.Palette.Where(p => p.Key != '.').Select(p => p.Value).ToHashSet();
        Assert.Subset(palette.ToHashSet(), used);
    }

    [Fact]
    public async Task ImageTool_ReadsProjectImagesThroughTheHost()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-img-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiImage");
            Directory.CreateDirectory(Path.Combine(project.Directory, "Notes"));
            File.WriteAllBytes(Path.Combine(project.Directory, "Notes", "desenho.jpg"), [1, 2, 3]);
            var images = new FakeImages(Drawing());
            var tool = new LunetAiImageTool(project, images);
            var outcome = await tool.InvokeAsync(new ToolInvocation("1", LunetAiImageTool.Name,
                JsonSerializer.SerializeToElement(new { path = "Notes/desenho.jpg", max_side = 32 })), Ctx(), TestContext.Current.CancellationToken);
            Assert.False(outcome.IsError, outcome.Content);
            Assert.Contains("32×32", outcome.Content);
            Assert.Equal("Notes/desenho.jpg", images.LastPath);
            var outside = await tool.InvokeAsync(new ToolInvocation("2", LunetAiImageTool.Name,
                JsonSerializer.SerializeToElement(new { path = "../x.png" })), Ctx(), TestContext.Current.CancellationToken);
            Assert.True(outside.IsError);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class FakeImages(LunetAiImage image) : ILunetAiImages
    {
        public string? LastPath { get; private set; }
        public LunetAiImage? Load(string fullPath, string relativePath) { LastPath = relativePath; return image; }
    }

    // ---------- Style & coherence ----------

    [Fact]
    public async Task Style_SummarizesPaletteSizesNamesAndStyleNotes()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-style-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiStyle");
            var hero = SpriteDocument.Create(16);
            hero.Brush(0, 0, 0, 15, 0, 0xff2b1d3a, 1);
            hero.Brush(0, 0, 1, 15, 1, 0xfff2c27b, 1);
            project.WriteText("Assets/heroi.sprite.json", hero.ToJson());
            project.WriteText("Assets/slime.sprite.json", SpriteDocument.Create(16).ToJson());
            project.WriteText("Notes/estilo-visual.md", "# Estilo visual\n\nPixel art 16x16, contorno escuro, luz vinda de cima à esquerda.\n");
            var style = LunetAiProjectStyle.Analyze(project);
            Assert.Contains(0xff2b1d3au, style.Palette);
            Assert.Contains(0xfff2c27bu, style.Palette);
            Assert.Equal((16, 16), style.CommonSpriteSize);
            Assert.Contains("Assets/heroi", style.SpriteNames);
            Assert.Contains("contorno escuro", style.StyleNotes);
            var text = style.Describe();
            Assert.Contains("#2B1D3A", text);
            Assert.Contains("16×16", text);

            var tool = new LunetAiStyleTool(project);
            var outcome = await tool.InvokeAsync(new ToolInvocation("1", LunetAiStyleTool.Name, JsonSerializer.SerializeToElement(new { })), Ctx(), TestContext.Current.CancellationToken);
            Assert.False(outcome.IsError);
            Assert.Contains("contorno escuro", outcome.Content);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SpriteTool_WarnsWhenArtBreaksTheProjectStyle()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-style-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiCoherence");
            var hero = SpriteDocument.Create(16);
            hero.Brush(0, 0, 0, 15, 15, 0xff2b1d3a, 2);
            project.WriteText("Assets/heroi.sprite.json", hero.ToJson());
            var tool = new LunetAiSpriteTool(project, new LunetAiCheckpoint(project));
            var outcome = await tool.InvokeAsync(new ToolInvocation("1", LunetAiSpriteTool.Name, JsonSerializer.SerializeToElement(new
            {
                path = "Assets/gema",
                palette = new Dictionary<string, string> { ["."] = "transparent", ["g"] = "#00FF00" },
                frames = new[] { new[] { ".g.", "ggg", ".g." } },
            })), Ctx(), TestContext.Current.CancellationToken);
            Assert.False(outcome.IsError, outcome.Content);
            Assert.Contains("coerência", outcome.Content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("#00FF00", outcome.Content);
            Assert.Contains("16×16", outcome.Content);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Session_DrawingAttachmentBecomesASprite_WithKnowledgeAndStyleTools()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-flow-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiFlow");
            Directory.CreateDirectory(Path.Combine(project.Directory, "Notes"));
            File.WriteAllBytes(Path.Combine(project.Directory, "Notes", "desenho.jpg"), [1]);
            var provider = new Scripted(
                ("lunet.project.style", new { }),
                ("lunet.image.inspect", new { path = "Notes/desenho.jpg", max_side = 16 }),
                ("lunet.assets.sprite", new
                {
                    path = "Assets/desenho",
                    palette = new Dictionary<string, string> { ["."] = "transparent", ["a"] = "#D02838" },
                    frames = new[] { new[] { "aa", "aa" } },
                }));
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            var reply = await session.AskAsync("transforme meu desenho em sprite", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m", AllowWrites = true }, "chave-de-teste-123456789",
                LunetAiEditorContext.Empty, null, provider, Knowledge: RealKnowledge(), Images: new FakeImages(Drawing()),
                Attachments: ["Notes/desenho.jpg"]), TestContext.Current.CancellationToken);

            Assert.Equal(RunStatus.Succeeded, reply.Status);
            var first = provider.Requests[0];
            var tools = first.Tools.Select(t => t.Name).ToArray();
            Assert.Contains("lunet.docs.search", tools);
            Assert.Contains("lunet.image.inspect", tools);
            Assert.Contains("lunet.project.style", tools);
            Assert.Contains("Notes/desenho.jpg", string.Join("\n", first.Messages.SelectMany(m => m.Content).OfType<TextBlock>().Select(t => t.Text)));
            Assert.Contains("COERÊNCIA", first.System);
            // The model saw the grid produced from the image.
            Assert.Contains(provider.Requests.SelectMany(r => r.Messages).SelectMany(m => m.Content).OfType<ToolResultBlock>(),
                r => r.Content.Contains("Legenda", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(project.Directory, "Assets", "desenho.png")));
            Assert.Contains(reply.Changes, c => c.Path == "Assets/desenho.sprite.json");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class Scripted(params (string Tool, object Input)[] calls) : IModelProvider
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
            return ValueTask.FromResult(new ModelResponse([new TextBlock("Sprite criado em Assets/desenho.")], StopReason.EndTurn, usage));
        }
    }

    [Fact]
    public void SystemPrompt_DemandsCoherenceAndHonestyAboutMissingFeatures()
    {
        var prompt = LunetAiSession.SystemPrompt;
        Assert.Contains("lunet.project.style", prompt);
        Assert.Contains("lunet.docs.search", prompt);
        Assert.Contains("lunet.image.inspect", prompt);
        Assert.Contains("coerência", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("não existe", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
