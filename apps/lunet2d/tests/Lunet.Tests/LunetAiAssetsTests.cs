using System.Text.Json;
using AgentRuntime;
using Lunet.Ai;
using Lunet.Content;
using Lunet.Core;
using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>LUNET-487: the AI builds assets too — sprites the Sprite Studio can open, with a PNG sheet the game loads.</summary>
public sealed class LunetAiAssetsTests
{
    [Fact]
    public void PngWriter_ProducesAPngTheFrameworkDecoderReads()
    {
        var rgba = new byte[] { 255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 255, 0, 0, 0, 0 };
        var png = PngWriter.Encode(2, 2, rgba);
        var image = PngDecoder.Decode(png);
        Assert.Equal(2, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(rgba, image.Rgba);
    }

    [Fact]
    public async Task SpriteTool_WritesStudioSourceAndSheet_AndCheckpointReverts()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-assets-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiAssets");
            var checkpoint = new LunetAiCheckpoint(project);
            var tool = new LunetAiSpriteTool(project, checkpoint);
            var input = JsonSerializer.SerializeToElement(new
            {
                path = "Assets/heroi",
                fps = 6,
                palette = new Dictionary<string, string> { ["."] = "transparent", ["a"] = "#7C6CFF", ["b"] = "#FFFFFF" },
                frames = new[]
                {
                    new[] { "..a..", ".aba.", "aaaaa", ".a.a.", "a...a" },
                    new[] { "..a..", ".aba.", "aaaaa", ".a.a.", ".a.a." },
                },
            });
            var outcome = await tool.InvokeAsync(new ToolInvocation("c1", LunetAiSpriteTool.Name, input),
                new ToolContext("run", new AgentIdentity("a", "a", "a"), ContextPath.Create(new ContextStep(ContextLevel.Project, "p"))),
                TestContext.Current.CancellationToken);
            Assert.False(outcome.IsError, outcome.Content);
            Assert.Contains("Assets/heroi.png", outcome.Content);

            var sprite = SpriteDocument.Parse(project.ReadText("Assets/heroi.sprite.json"));
            Assert.Equal(5, sprite.Width);
            Assert.Equal(2, sprite.Frames.Count);
            Assert.Equal(6, sprite.Fps);
            Assert.Equal(0xff7c6cffu, sprite.Frames[0][2]);
            Assert.Equal(0xffffffffu, sprite.Frames[0][7]);
            Assert.Equal(0u, sprite.Frames[0][0]);

            var sheet = PngDecoder.Decode(File.ReadAllBytes(Path.Combine(project.Directory, "Assets/heroi.png")));
            Assert.Equal(10, sheet.Width);
            Assert.Equal(5, sheet.Height);
            Assert.Equal(sprite.SheetRgba(), sheet.Rgba);

            Assert.Equal(2, checkpoint.Changes().Count);
            checkpoint.Revert();
            Assert.False(File.Exists(Path.Combine(project.Directory, "Assets/heroi.png")));
            Assert.False(File.Exists(Path.Combine(project.Directory, "Assets/heroi.sprite.json")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Checkpoint_IgnoresPathsOutsideTheProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-assets-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiEscape");
            var outside = Path.Combine(root, "segredo.txt");
            File.WriteAllText(outside, "original");
            var checkpoint = new LunetAiCheckpoint(project);
            var provider = new ScriptedEscape();
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            var reply = await session.AskAsync("x", new LunetAiRequest(LunetAiSettings.Default with { Model = "m" },
                "chave-de-teste-123456789", LunetAiEditorContext.Empty, null, provider), TestContext.Current.CancellationToken);
            File.WriteAllText(outside, "alterado por outro processo");
            reply.Checkpoint.Revert();
            Assert.Equal("alterado por outro processo", File.ReadAllText(outside));
            Assert.Empty(reply.Changes);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class ScriptedEscape : IModelProvider
    {
        private int _step;
        public string ProviderId => "chat-completions";
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_step++ == 0
                ? new ModelResponse([new ToolUseBlock("c", "lunet.files.write", JsonSerializer.SerializeToElement(
                    new { path = "../segredo.txt", content = "x", expected_sha256 = "" }))], StopReason.ToolUse, new Usage(1, 1, Money.Zero("USD")))
                : new ModelResponse([new TextBlock("ok")], StopReason.EndTurn, new Usage(1, 1, Money.Zero("USD"))));
    }

    [Theory]
    [InlineData("{\"path\":\"../fora\",\"palette\":{\"a\":\"#fff\"},\"frames\":[[\"a\"]]}")]
    [InlineData("{\"path\":\"Assets/x\",\"palette\":{\"a\":\"#ffffff\"},\"frames\":[[\"ab\"]]}")]
    [InlineData("{\"path\":\"Assets/x\",\"palette\":{\"a\":\"#ffffff\"},\"frames\":[[\"aa\",\"a\"]]}")]
    [InlineData("{\"path\":\"Assets/x\",\"palette\":{\"a\":\"#ffffff\"},\"frames\":[]}")]
    public async Task SpriteTool_RejectsInvalidInputWithoutWriting(string json)
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-assets-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiAssetsBad");
            var before = project.ListFiles().Count;
            var tool = new LunetAiSpriteTool(project, new LunetAiCheckpoint(project));
            var outcome = await tool.InvokeAsync(new ToolInvocation("c1", LunetAiSpriteTool.Name, JsonDocument.Parse(json).RootElement),
                new ToolContext("run", new AgentIdentity("a", "a", "a"), ContextPath.Create(new ContextStep(ContextLevel.Project, "p"))),
                TestContext.Current.CancellationToken);
            Assert.True(outcome.IsError);
            Assert.Equal(before, project.ListFiles().Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
