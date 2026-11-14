using System.Text.Json;
using AgentRuntime;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>LUNET-490: market-grade chat — real Markdown blocks and a visible, structured work timeline.</summary>
public sealed class LunetAiChatTests
{
    [Fact]
    public void Markdown_ParsesHeadingsListsCodeQuotesAndParagraphs()
    {
        var blocks = ChatMarkdown.Parse("""
            ## Plano
            Vou fazer **duas** coisas:

            1. Adicionar gravidade
            2. Pular ao tocar
            - item solto
            > dica importante

            ```csharp
            if (Input.Touch.Pressed)
                Jump();
            ```
            Pronto.
            """);
        Assert.Collection(blocks,
            b => { Assert.Equal(ChatBlockKind.Heading, b.Kind); Assert.Equal("Plano", b.Text); Assert.Equal(2, b.Level); },
            b => { Assert.Equal(ChatBlockKind.Paragraph, b.Kind); Assert.Contains("**duas**", b.Text); },
            b => { Assert.Equal(ChatBlockKind.Numbered, b.Kind); Assert.Equal("Adicionar gravidade", b.Text); Assert.Equal(1, b.Level); },
            b => { Assert.Equal(ChatBlockKind.Numbered, b.Kind); Assert.Equal(2, b.Level); },
            b => { Assert.Equal(ChatBlockKind.Bullet, b.Kind); Assert.Equal("item solto", b.Text); },
            b => { Assert.Equal(ChatBlockKind.Quote, b.Kind); Assert.Equal("dica importante", b.Text); },
            b => { Assert.Equal(ChatBlockKind.Code, b.Kind); Assert.Equal("csharp", b.Language); Assert.Equal("if (Input.Touch.Pressed)\n    Jump();", b.Text); },
            b => { Assert.Equal(ChatBlockKind.Paragraph, b.Kind); Assert.Equal("Pronto.", b.Text); });
    }

    [Fact]
    public void Markdown_UnclosedFenceAndTablesStayReadable()
    {
        var blocks = ChatMarkdown.Parse("Antes\n```\nsem fim");
        Assert.Equal(ChatBlockKind.Code, blocks[^1].Kind);
        Assert.Equal("sem fim", blocks[^1].Text);
        var table = ChatMarkdown.Parse("| a | b |\n|---|---|\n| 1 | 2 |");
        Assert.Equal(ChatBlockKind.Table, Assert.Single(table).Kind);
        Assert.Equal([["a", "b"], ["1", "2"]], Assert.Single(table).Rows);
    }

    [Fact]
    public void Inline_SplitsBoldItalicCodeAndLinks()
    {
        var spans = ChatMarkdown.Inline("Use **SpriteBatch** com `Begin()` e *cuidado*: [guia](guide:graficos).");
        Assert.Contains(spans, s => s.Style == ChatInline.Bold && s.Text == "SpriteBatch");
        Assert.Contains(spans, s => s.Style == ChatInline.Code && s.Text == "Begin()");
        Assert.Contains(spans, s => s.Style == ChatInline.Italic && s.Text == "cuidado");
        Assert.Contains(spans, s => s.Style == ChatInline.Link && s.Text == "guia" && s.Target == "guide:graficos");
        Assert.Equal("Use SpriteBatch com Begin() e cuidado: guia.", string.Concat(spans.Select(s => s.Text)));
    }

    [Fact]
    public async Task Steps_ReportWhatTheAgentDoesWithFilesAndOutcome()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-steps-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("AiSteps");
            var steps = new List<LunetAiStep>();
            var provider = new Script(
                ("lunet.files.read", new { path = "Game.cs" }),
                ("lunet.files.read", new { path = "NaoExiste.cs" }));
            var session = new LunetAiSession(project, Path.Combine(root, "h.json"));
            await session.AskAsync("leia", new LunetAiRequest(LunetAiSettings.Default with { Model = "m" }, "chave-de-teste-123456789",
                LunetAiEditorContext.Empty, Provider: provider, Steps: new SyncProgress<LunetAiStep>(steps.Add)), TestContext.Current.CancellationToken);
            var reads = steps.Where(s => s.Kind == LunetAiStepKind.Read).ToArray();
            Assert.Contains(reads, s => s.Detail == "Game.cs" && s.Status == LunetAiStepStatus.Running);
            Assert.Contains(reads, s => s.Detail == "Game.cs" && s.Status == LunetAiStepStatus.Done);
            Assert.Contains(reads, s => s.Detail == "NaoExiste.cs" && s.Status == LunetAiStepStatus.Failed);
            Assert.All(steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Label)));
            Assert.Equal("Lendo Game.cs", reads.First().Label);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void StepSummary_GroupsExplorationLikeProTools()
    {
        var steps = new[]
        {
            new LunetAiStep("1", LunetAiStepKind.Read, "Lendo Game.cs", "Game.cs", LunetAiStepStatus.Done),
            new LunetAiStep("2", LunetAiStepKind.Search, "Procurando \"Jump\"", "Jump", LunetAiStepStatus.Done),
            new LunetAiStep("3", LunetAiStepKind.Edit, "Editando Game.cs", "Game.cs", LunetAiStepStatus.Done),
            new LunetAiStep("4", LunetAiStepKind.Compile, "Compilando", null, LunetAiStepStatus.Done),
        };
        Assert.Equal("Explorou 2 itens · editou 1 arquivo · compilou 1×", LunetAiStep.Summarize(steps));
    }

    [Fact]
    public async Task History_IsPersistedWhileModelRunsAndAfterItFinishes()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-ai-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("History");
            var historyPath = Path.Combine(root, "history.json");
            var session = new LunetAiSession(project, historyPath);
            var model = new WaitingProvider();
            var run = session.AskAsync("Faça um teste", new LunetAiRequest(
                LunetAiSettings.Default with { Model = "m" }, "chave-de-teste-123456789",
                LunetAiEditorContext.Empty, Provider: model));
            await model.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var during = new LunetAiSession(project, historyPath);
            Assert.Equal(2, during.History.Count);
            Assert.Contains("trabalhando", during.History[^1].Text);
            model.Release.TrySetResult(true);
            await run;
            var restored = new LunetAiSession(project, historyPath);
            Assert.Equal("Faça um teste", restored.History[0].Text);
            Assert.Equal("Concluído", restored.History[1].Text);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class WaitingProvider : IModelProvider
    {
        public string ProviderId => "chat-completions";
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Entered.TrySetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return new ModelResponse([new TextBlock("Concluído")], StopReason.EndTurn,
                new Usage(1, 1, Money.Zero("USD")));
        }
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class Script(params (string Tool, object Input)[] calls) : IModelProvider
    {
        private int _step;
        public string ProviderId => "chat-completions";
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            var usage = new Usage(1, 1, Money.Zero("USD"));
            if (_step < calls.Length)
            {
                var (tool, input) = calls[_step++];
                return ValueTask.FromResult(new ModelResponse([new ToolUseBlock("c" + _step, tool, JsonSerializer.SerializeToElement(input))], StopReason.ToolUse, usage));
            }
            return ValueTask.FromResult(new ModelResponse([new TextBlock("ok")], StopReason.EndTurn, usage));
        }
    }
}
