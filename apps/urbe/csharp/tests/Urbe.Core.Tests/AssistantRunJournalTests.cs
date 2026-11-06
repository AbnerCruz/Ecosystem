using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-216: a run's journal survives the process dying in the middle of the work. After the restart the journal says
/// what was done (and verified in the vault), what was not and what cannot be verified; resuming never repeats a write,
/// an image or a model step already paid for, and what cannot be verified waits for the person.
/// </summary>
public sealed class AssistantRunJournalTests
{
    /// <summary>A vault in memory whose writes and drawings can hang forever: the process "dies" there.</summary>
    private sealed class Host : IAssistantHost, IAssistantImages
    {
        private int _seq = 100;
        public Host(params (string Path, string Content)[] notes)
        {
            int i = 0;
            foreach (var (path, content) in notes) Documents.Upsert(new DocumentInput { Id = "doc_" + i++, Path = path, Content = content });
            Knowledge = new KnowledgeIndex(Documents);
        }
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 2000;
        /// <summary>Writing this note hangs; with <see cref="WriteBeforeHang"/> the given text is stored first.</summary>
        public string? HangOn { get; set; }
        public string? WriteBeforeHang { get; set; }
        public bool HangDrawing { get; set; }
        public TaskCompletionSource Hung { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Drawn { get; private set; }
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = "doc_" + _seq++, Path = path, Content = content }));
        public async Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content)
        {
            if (doc.Path == HangOn)
            {
                if (WriteBeforeHang is { } written)
                    Documents.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Content = written == "*" ? content : written });
                Hung.TrySetResult();
                await new TaskCompletionSource().Task;   // the process is gone
            }
            return Documents.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Title = doc.Title, Content = content, Created = doc.Created, Modified = doc.Modified });
        }
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = newPath, Content = doc.Content }));
        public Task TrashNoteAsync(UrbeDocument doc) { Documents.Remove(doc.Id); return Task.CompletedTask; }
        public Task CreateFolderAsync(string path) => Task.CompletedTask;
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
        public IAssistantImages? Images => this;
        public bool CanDraw => true;
        public async Task<byte[]> DrawAsync(string prompt, string size, CancellationToken cancel)
        {
            Drawn++;   // the provider charged for it
            if (HangDrawing) { Hung.TrySetResult(); await new TaskCompletionSource().Task; }
            return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        }
        public Task SaveAsync(string path, byte[] bytes) { Files[path] = bytes; return Task.CompletedTask; }
        public bool Exists(string path) => Files.ContainsKey(path) || Documents.Get(path) is not null;
    }

    private static AiResult Tools(params (string Id, string Name, JsonObject Input)[] calls) =>
        new([.. calls.Select(c => AiBlock.ToolUse(c.Id, c.Name, c.Input))], "", "tool_use", new AiUsage(100, 10, 0.01), "m-1");

    private static AiResult Say(string text) => new([AiBlock.TextBlock(text)], "", "end", new AiUsage(100, 10, 0.01), "m-1");

    private static JsonObject Append() => new() { ["path"] = "A.md", ["content"] = "Linha nova." };
    private static JsonObject Image() => new() { ["prompt"] = "um gato azul" };
    private static JsonObject EditB() => new() { ["path"] = "B.md", ["old_text"] = "Velho.", ["new_text"] = "Novo." };

    private sealed class Counter { public int Approvals; public int ModelCalls; }

    private static Task<AiAgentResult> Run(Host host, List<AiChatMessage> messages, Queue<AiResult> replies, AssistantRunJournal journal, Counter count) =>
        AssistantAgent.RunAsync(new AiAgentRun
        {
            Agent = AssistantAgent.AgentById("geral"), Policy = "ask", Messages = messages, Host = host, Cancel = CancellationToken.None,
            Provider = new AiProviderConfig("p1", ""), Model = "m-1", VaultName = "Cofre", Now = new DateTime(2026, 11, 5, 15, 0, 0),
            Stream = (_, _, _, _) => { count.ModelCalls++; return Task.FromResult(replies.Dequeue()); },
            Approve = (_, _) => { count.Approvals++; return Task.FromResult(AiApproval.Approve); },
            Journal = journal, Tools = [.. AssistantTools.All, AssistantImageTools.Tool]
        });

    private static (List<AiChatMessage> Messages, AssistantRunJournal Journal) Start(IAssistantStorage storage)
    {
        var request = new AiChatMessage { Id = "u1", Role = "user", Content = [AiChatBlock.TextBlock("Complete A, ilustre e atualize B")] };
        var journal = AssistantRunJournal.Begin(storage, new AiRunStart("r1", "c1", "Cofre", "geral", "m-1", "Complete A, ilustre e atualize B", 1, request));
        return ([request], journal);
    }

    private static int Count(string text, string part) => (text.Length - text.Replace(part, "").Length) / part.Length;

    [Fact]
    public async Task ProcessDiesBeforeAWrite_ResumeDoesNotRepeatWritesImagesOrPaidSteps()
    {
        var storage = new MemoryAssistantStorage();
        var host = new Host(("A.md", "# A\n"), ("B.md", "# B\n\nVelho.\n")) { HangOn = "B.md" };
        var count = new Counter();
        var (messages, journal) = Start(storage);
        var replies = new Queue<AiResult>([Tools(("t1", "append_to_note", Append())), Tools(("t2", "generate_image", Image())), Tools(("t3", "edit_note", EditB()))]);
        _ = Run(host, messages, replies, journal, count);
        await host.Hung.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        // ---- the process died while B was about to be written; a new one opens the journal from the device
        host.HangOn = null;
        var reopened = AssistantRunJournal.Open(storage, "r1")!;
        Assert.Single(AssistantRunJournal.Interrupted(storage, "Cofre"));
        var effects = reopened.Effects(host.Documents);
        Assert.Equal([AiRunEffect.Done, AiRunEffect.Done, AiRunEffect.NotDone], effects.Select(e => e.State));
        Assert.Contains("Imagens/", effects[1].Path);
        Assert.False(reopened.NeedsYou(host.Documents));
        Assert.Equal(300, reopened.Usage.Input);   // three steps already paid for
        var rebuilt = reopened.Rebuild([messages[0]], host.Documents);
        Assert.Equal(7, rebuilt.Count);
        Assert.Contains(rebuilt.SelectMany(m => m.Content), b => b.ToolUseId == "t3" && b.IsError && b.Content.Contains("Não executada"));
        var note = reopened.ResumeNote(host.Documents);
        reopened.Resume(note);
        rebuilt.Add(note);
        // ---- resumed: the model asks for the same append and image again (new call ids), then the edit, then answers
        var again = new Queue<AiResult>([Tools(("n1", "append_to_note", Append()), ("n2", "generate_image", Image())), Tools(("n3", "edit_note", EditB())), Say("Pronto.")]);
        count.ModelCalls = 0;
        var result = await Run(host, rebuilt, again, reopened, count);
        Assert.Equal("end", result.Stopped);
        Assert.Equal(1, Count(host.Documents.Get("A.md")!.Content, "Linha nova."));
        Assert.Equal(1, host.Drawn);
        Assert.Single(host.Files);
        Assert.Equal("# B\n\nNovo.\n", host.Documents.Get("B.md")!.Content);
        Assert.Equal(3, count.ModelCalls);   // only the new steps
        Assert.Equal(4, count.Approvals);   // B's edit was asked again: an approval is never carried over a restart
        var reused = rebuilt.SelectMany(m => m.Content).Where(b => b.ToolUseId is "n1" or "n2").ToList();
        Assert.All(reused, b => Assert.Equal("reused", b.Status));
        Assert.True(AssistantRunJournal.Open(storage, "r1")!.Finished);
    }

    [Fact]
    public async Task ProcessDiesAfterTheWriteLanded_TheWriteIsVerifiedAndNotRepeated()
    {
        var storage = new MemoryAssistantStorage();
        var host = new Host(("A.md", "# A\n"), ("B.md", "# B\n\nVelho.\n")) { HangOn = "B.md", WriteBeforeHang = "*" };
        var (messages, journal) = Start(storage);
        _ = Run(host, messages, new Queue<AiResult>([Tools(("t1", "edit_note", EditB()))]), journal, new Counter());
        await host.Hung.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        host.HangOn = null;
        var reopened = AssistantRunJournal.Open(storage, "r1")!;
        var effect = Assert.Single(reopened.Effects(host.Documents));
        Assert.Equal(AiRunEffect.Done, effect.State);
        var rebuilt = reopened.Rebuild([messages[0]], host.Documents);
        Assert.Contains(rebuilt.SelectMany(m => m.Content), b => b.ToolUseId == "t1" && !b.IsError && b.Content.Contains("Concluída"));
        reopened.Resume(reopened.ResumeNote(host.Documents));
        var count = new Counter();
        await Run(host, rebuilt, new Queue<AiResult>([Tools(("n1", "edit_note", EditB())), Say("Pronto.")]), reopened, count);
        Assert.Equal("# B\n\nNovo.\n", host.Documents.Get("B.md")!.Content);
        Assert.Equal(0, count.Approvals);
    }

    [Fact]
    public async Task AWriteThatCannotBeVerified_NeedsThePersonAndIsNeverRetried()
    {
        var storage = new MemoryAssistantStorage();
        var host = new Host(("B.md", "# B\n\nVelho.\n")) { HangOn = "B.md", WriteBeforeHang = "# B\n\nVel" };
        var (messages, journal) = Start(storage);
        _ = Run(host, messages, new Queue<AiResult>([Tools(("t1", "edit_note", EditB()))]), journal, new Counter());
        await host.Hung.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        host.HangOn = null;
        var reopened = AssistantRunJournal.Open(storage, "r1")!;
        Assert.Equal(AiRunEffect.Uncertain, Assert.Single(reopened.Effects(host.Documents)).State);
        Assert.True(reopened.NeedsYou(host.Documents));
        var rebuilt = reopened.Rebuild([messages[0]], host.Documents);
        Assert.Contains(rebuilt.SelectMany(m => m.Content), b => b.ToolUseId == "t1" && b.Status == "needs_you");
        Assert.Contains("Não verificável", reopened.ResumeNote(host.Documents).Content[0].Text);
        reopened.Resume(reopened.ResumeNote(host.Documents));
        var count = new Counter();
        await Run(host, rebuilt, new Queue<AiResult>([Tools(("n1", "edit_note", EditB())), Say("Preciso que você confira B.")]), reopened, count);
        Assert.Equal("# B\n\nVel", host.Documents.Get("B.md")!.Content);   // untouched: the person decides
        Assert.Equal(0, count.Approvals);
        Assert.Contains(rebuilt.SelectMany(m => m.Content), b => b.ToolUseId == "n1" && b.IsError && b.Content.StartsWith("Precisa do usuário", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnImageCutWhileBeingGenerated_IsNotGeneratedAgainWithoutThePerson()
    {
        var storage = new MemoryAssistantStorage();
        var host = new Host(("A.md", "# A\n")) { HangDrawing = true };
        var (messages, journal) = Start(storage);
        _ = Run(host, messages, new Queue<AiResult>([Tools(("t1", "generate_image", Image()))]), journal, new Counter());
        await host.Hung.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        host.HangDrawing = false;
        var reopened = AssistantRunJournal.Open(storage, "r1")!;
        var effect = Assert.Single(reopened.Effects(host.Documents));
        Assert.Equal(AiRunEffect.Uncertain, effect.State);
        Assert.Contains("cobrada", effect.Detail);
        var rebuilt = reopened.Rebuild([messages[0]], host.Documents);
        reopened.Resume(reopened.ResumeNote(host.Documents));
        await Run(host, rebuilt, new Queue<AiResult>([Tools(("n1", "generate_image", Image())), Say("Confira a pasta Imagens.")]), reopened, new Counter());
        Assert.Equal(1, host.Drawn);
        Assert.Empty(host.Files);
    }

    [Fact]
    public async Task AModelStepCutMidway_IsReportedAsPossiblyChargedWithoutResult()
    {
        var storage = new MemoryAssistantStorage();
        var host = new Host(("A.md", "# A\n"));
        var (messages, journal) = Start(storage);
        var hung = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = AssistantAgent.RunAsync(new AiAgentRun
        {
            Agent = AssistantAgent.AgentById("geral"), Policy = "auto", Messages = messages, Host = host, Cancel = CancellationToken.None,
            Provider = new AiProviderConfig("p1", ""), Model = "m-1", Journal = journal,
            Stream = async (_, _, _, _) => { hung.TrySetResult(); await new TaskCompletionSource().Task; return Say("nunca"); }
        });
        await hung.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var reopened = AssistantRunJournal.Open(storage, "r1")!;
        Assert.True(reopened.ModelCallCut);
        Assert.Empty(reopened.Effects(host.Documents));
        Assert.Single(reopened.Rebuild([messages[0]], host.Documents));
    }

    [Fact]
    public void Journal_IgnoresALineCutByTheCrashAndRefusesANewerSchema()
    {
        var storage = new MemoryAssistantStorage();
        var journal = AssistantRunJournal.Begin(storage, new AiRunStart("r1", "c1", "Cofre", "geral", "m-1", "Objetivo", 0));
        journal.Request(1);
        storage.Write(AssistantRunJournal.FileOf("r1"), storage.Read(AssistantRunJournal.FileOf("r1")) + "{\"t\":\"respo");
        var reopened = AssistantRunJournal.Open(storage, "r1")!;
        Assert.Equal(2, reopened.Entries.Count);
        Assert.Equal("Objetivo", reopened.Goal);
        Assert.True(reopened.ModelCallCut);
        storage.Write(AssistantRunJournal.FileOf("r2"), "{\"v\":2,\"t\":\"begin\",\"run\":\"r2\"}\n");
        Assert.Null(AssistantRunJournal.Open(storage, "r2"));
        Assert.DoesNotContain(AssistantRunJournal.All(storage), j => j.RunId == "r2");
        Assert.NotNull(storage.Read(AssistantRunJournal.FileOf("r2")));   // never rewritten nor deleted
    }

    [Fact]
    public void Journal_ClosedRunsAreNotOfferedAndOldOnesArePruned()
    {
        var storage = new MemoryAssistantStorage();
        for (int i = 0; i < 5; i++)
        {
            var j = AssistantRunJournal.Begin(storage, new AiRunStart("r" + i, "c", "Cofre", "geral", "m", "g" + i, 0), now: 1000 + i);
            if (i < 4) { j.Finish("end", null); j.Close("ended"); }
        }
        Assert.Equal("r4", Assert.Single(AssistantRunJournal.Interrupted(storage, "Cofre")).RunId);
        Assert.Empty(AssistantRunJournal.Interrupted(storage, "Outro"));
        AssistantRunJournal.Prune(storage, keep: 2);
        Assert.Equal(["r2", "r3", "r4"], AssistantRunJournal.All(storage).Select(j => j.RunId).Order());
        AssistantRunJournal.DeleteFor(storage, "geral");
        Assert.Empty(AssistantRunJournal.All(storage));
    }

    [Fact]
    public async Task Journal_RecordsGoalSourcesDecisionsAndReceipts()
    {
        var storage = new MemoryAssistantStorage();
        var host = new Host(("A.md", "# A\n"));
        var request = new AiChatMessage { Id = "u1", Role = "user", Content = [AiChatBlock.TextBlock("Leia e complete A")] };
        var journal = AssistantRunJournal.Begin(storage, new AiRunStart("r1", "c1", "Cofre", "geral", "m-1", "Leia e complete A", 1, request,
            [("A.md", AssistantRunJournal.Sha("# A\n"))]));
        var count = new Counter();
        await Run(host, [request], new Queue<AiResult>([Tools(("t1", "read_note", new JsonObject { ["path"] = "A.md" })), Tools(("t2", "append_to_note", Append())), Say("Feito.")]), journal, count);
        var lines = storage.Read(AssistantRunJournal.FileOf("r1"))!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonNode.Parse(l)!.AsObject()).ToList();
        Assert.All(lines, l => Assert.Equal(1, (int)l["v"]!));
        Assert.Equal(["begin", "request", "response", "result", "request", "response", "approval", "intent", "result", "request", "response", "finished"], lines.Select(l => (string)l["t"]!));
        Assert.Equal("A.md", (string)lines[0]["sources"]![0]!["path"]!);
        Assert.Equal("approve", (string)lines[6]["decision"]!);
        var receipt = lines[8];
        Assert.Equal("A.md", (string)receipt["changes"]![0]!["after"]!);
        Assert.Equal(AssistantRunJournal.Sha(host.Documents.Get("A.md")!.Content), (string)receipt["changes"]![0]!["afterSha"]!);
        Assert.Equal(AssistantRunJournal.Sha("# A\n"), (string)lines[3]["seen"]![0]!["sha"]!);
        Assert.DoesNotContain(storage.List(), n => n.EndsWith(".md", StringComparison.Ordinal));   // private state, never in the vault
    }
}
