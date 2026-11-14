using System.Text.Json;
using AgentRuntime;
using Lunet.Ai;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>Regression: an interrupted agent must never destroy a user's saved code.</summary>
public sealed class LunetAiRecoveryTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(), "lunet-ai-recovery-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SurgicalEdit_CreatesDurableCheckpoint_ReopenAndRestoreAfterRestart()
    {
        var root = Root();
        try
        {
            var project = new ProjectStore(root).Create("Recovery");
            var path = project.Manifest.EntryPoint;
            var before = project.ReadText(path);
            const string anchor = "// Atualize a lógica do jogo aqui.";
            var provider = new SingleEditProvider("lunet.files.replace", new {
                path, old_text = anchor, new_text = anchor + "\n// alterado pela IA",
                expected_sha256 = LunetAiProjectFiles.Hash(before)
            });
            var session = new LunetAiSession(project, Path.Combine(root, "history.json"));
            var reply = await session.AskAsync("acrescente comentário", Request(provider), TestContext.Current.CancellationToken);
            Assert.Equal(RunStatus.Succeeded, reply.Status);
            Assert.Contains("// alterado pela IA", project.ReadText(path));

            var point = Assert.Single(LunetAiCheckpoint.Available(project));
            Assert.Contains(path, point.Paths);
            var reopened = LunetAiCheckpoint.Reopen(project, point.Id);
            Assert.Equal(before, Assert.Single(reopened.Changes()).Before);
            reopened.Revert();
            Assert.Equal(before, project.ReadText(path));

            // Checkpoint survives the undo; recovery history is not destroyed.
            Assert.Single(LunetAiCheckpoint.Available(project));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PreviousUserEditIsNeverOverwrittenByAnOldAiCheckpoint()
    {
        var root = Root();
        try
        {
            var project = new ProjectStore(root).Create("Conflict");
            var path = project.Manifest.EntryPoint;
            var before = project.ReadText(path);
            const string anchor = "// Atualize a lógica do jogo aqui.";
            var provider = new SingleEditProvider("lunet.files.replace", new {
                path, old_text = anchor, new_text = anchor + "\n// AI changed",
                expected_sha256 = LunetAiProjectFiles.Hash(before)
            });
            await new LunetAiSession(project, Path.Combine(root, "history.json"))
                .AskAsync("adicione um comentário", Request(provider), TestContext.Current.CancellationToken);
            var point = Assert.Single(LunetAiCheckpoint.Available(project));
            project.WriteText(path, project.ReadText(path) + "\n// edicao humana");
            var checkpoint = LunetAiCheckpoint.Reopen(project, point.Id);
            Assert.Throws<InvalidOperationException>(() => checkpoint.Revert());
            Assert.Contains("// edicao humana", project.ReadText(path));

            var copied = checkpoint.SaveOriginalAsCopy(path);
            Assert.StartsWith("Notes/Recovery/", copied, StringComparison.Ordinal);
            Assert.Equal(before, project.ReadText(copied));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AgentCannotReplaceOrClearExistingDocumentInOneToolCall()
    {
        var root = Root();
        try
        {
            var project = new ProjectStore(root).Create("SafeEdits");
            var path = project.Manifest.EntryPoint;
            var before = project.ReadText(path);
            await new LunetAiSession(project, Path.Combine(root, "history.json"))
                .AskAsync("altere o jogo", Request(new SingleEditProvider("lunet.files.write",
                    new { path, content = "// vazio", expected_sha256 = LunetAiProjectFiles.Hash(before) })),
                    TestContext.Current.CancellationToken);
            Assert.Equal(before, project.ReadText(path));

            Assert.Throws<InvalidOperationException>(() => new LunetAiProjectFiles(project)
                .Replace(path, before, "", LunetAiProjectFiles.Hash(before)));
            Assert.Equal(before, project.ReadText(path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancellationAfterAWriteRestoresCreatedFile()
    {
        var root = Root();
        try
        {
            var project = new ProjectStore(root).Create("Cancel");
            var provider = new StallingProvider();
            var session = new LunetAiSession(project, Path.Combine(root, "history.json"));
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var pending = session.AskAsync("crie Enemy.cs", Request(provider), cancel.Token);
            await provider.SecondRequest.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.True(File.Exists(Path.Combine(project.Directory, "Enemy.cs")));
            cancel.Cancel();
            try { await pending; }
            catch (OperationCanceledException) { }
            Assert.False(File.Exists(Path.Combine(project.Directory, "Enemy.cs")));
            Assert.Contains("Enemy.cs", session.LastRolledBackFiles);
            Assert.Single(LunetAiCheckpoint.Available(project));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static LunetAiRequest Request(IModelProvider provider) =>
        new(LunetAiSettings.Default with { Model = "test-model", AllowWrites = true },
            "chave-de-teste-123456789", LunetAiEditorContext.Empty, Provider: provider);

    private sealed class SingleEditProvider(string tool, object arguments) : IModelProvider
    {
        private int _step;
        public string ProviderId => "chat-completions";
        public ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            if (_step++ > 0) return ValueTask.FromResult(Text("Concluído."));
            var name = request.Tools.FirstOrDefault(t => t.Name == tool || t.Name == tool.Replace('.', '_'))?.Name ?? tool;
            return ValueTask.FromResult(new ModelResponse(
                [new ToolUseBlock("tool-1", name, JsonSerializer.SerializeToElement(arguments))],
                StopReason.ToolUse, new Usage(1, 1, Money.Zero("USD"))));
        }
    }

    private sealed class StallingProvider : IModelProvider
    {
        private int _step;
        public TaskCompletionSource<bool> SecondRequest { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ProviderId => "chat-completions";
        public async ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            if (_step++ == 0)
            {
                var name = request.Tools.FirstOrDefault(t => t.Name == "lunet.files.write" || t.Name == "lunet_files_write")?.Name ?? "lunet.files.write";
                return new ModelResponse(
                    [new ToolUseBlock("tool-1", name, JsonSerializer.SerializeToElement(
                        new { path = "Enemy.cs", content = "public sealed class Enemy {}\n", expected_sha256 = "" }))],
                    StopReason.ToolUse, new Usage(1, 1, Money.Zero("USD")));
            }
            SecondRequest.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Text("não chega aqui");
        }
    }

    private static ModelResponse Text(string value) =>
        new([new TextBlock(value)], StopReason.EndTurn, new Usage(1, 1, Money.Zero("USD")));
}
