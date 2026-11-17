using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-072: the permission matrix over the whole catalogue. A tool whose name says it changes something is classified
/// as a change, and a read-only agent (or a read-only policy) is never given one — so a prohibition cannot be
/// walked around through another tool that was filed as "read".
/// </summary>
public sealed class ToolPermissionMatrixTests
{
    private static readonly string[] Changing = ["write_", "edit_", "create_", "rename_", "delete", "move_", "append_", "add_", "generate_", "set_appearance", "trash", "restore"];

    [Fact]
    public void EveryToolThatChangesTheVaultIsFiledAsAChange()
    {
        var wrong = AssistantTools.All
            .Where(t => Changing.Any(p => t.Name.StartsWith(p, StringComparison.Ordinal)))
            .Where(t => t.Access is "read" or "ui" or "plan" or "network" or "memory")
            .Select(t => t.Name + " (" + t.Access + ")").ToList();
        Assert.Empty(wrong);
    }

    [Fact]
    public void AReadOnlyAgentOrPolicyGetsNoToolThatWrites()
    {
        var reader = AssistantAgent.Agents.Single(a => a.Id == "pesquisador");
        var writer = AssistantAgent.Agents.Single(a => a.Id == "geral");
        foreach (var tool in AssistantTools.All)
        {
            var writes = tool.Access is "write" or "destructive" or "plugin";
            if (!writes) continue;
            Assert.False(AssistantAgent.AllowTool(reader, "ask", tool.Access), tool.Name);
            Assert.False(AssistantAgent.AllowTool(writer, "readonly", tool.Access), tool.Name);
        }
        // the escritor writes but never destroys
        var escritor = AssistantAgent.Agents.Single(a => a.Id == "escritor");
        Assert.DoesNotContain(AssistantTools.All, t => t.Access == "destructive" && AssistantAgent.AllowTool(escritor, "ask", t.Access));
    }

    /// <summary>A host that counts every attempt to change the vault (or the agent's memory).</summary>
    private sealed class WatchingHost : IAssistantHost
    {
        public WatchingHost()
        {
            Documents.Upsert(new DocumentInput { Id = "d1", Path = "Comece.md", Content = "# Comece\n\nVeja [[Plano]].\n" });
            Documents.Upsert(new DocumentInput { Id = "d2", Path = "Plano.md", Content = "# Plano\n\n- um\n- dois\n" });
            Knowledge = new KnowledgeIndex(Documents);
        }
        public List<string> Writes { get; } = [];
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public int ReadChars => 4000;
        public (string? Path, string? Selection) EditorContext() => ("Comece.md", null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) { Writes.Add("create " + path); return Task.FromResult(Documents.Get("Comece.md")!); }
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) { Writes.Add("save " + doc.Path); return Task.FromResult(doc); }
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) { Writes.Add("rename " + doc.Path); return Task.FromResult(doc); }
        public Task TrashNoteAsync(UrbeDocument doc) { Writes.Add("trash " + doc.Path); return Task.CompletedTask; }
        public Task CreateFolderAsync(string path) { Writes.Add("folder " + path); return Task.CompletedTask; }
        public void OpenNote(string path) { }
        public void Remember(string fact) => Writes.Add("remember");
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static JsonObject ArgsFor(AiTool tool)
    {
        var args = new JsonObject();
        var props = tool.Parameters["properties"] as JsonObject ?? [];
        foreach (var (name, schema) in props)
        {
            args[name] = (string?)schema?["type"] switch
            {
                "integer" or "number" => 1,
                "boolean" => false,
                "array" => new JsonArray(),
                "object" => new JsonObject(),
                _ => name is "path" or "note" or "from" or "to" ? "Comece" : "um"
            };
        }
        return args;
    }

    /// <summary>
    /// The behaviour, not the name: every tool filed as "read" is run against a vault, with arguments built from its own
    /// schema, and none of them may create, save, rename, trash, make a folder or remember anything.
    /// </summary>
    [Fact]
    public async Task EveryReadToolRunsWithoutChangingTheVault()
    {
        var offenders = new List<string>();
        int ran = 0, total = 0;
        foreach (var tool in AssistantTools.All.Where(t => t.Access == "read"))
        {
            var host = new WatchingHost();
            var before = host.Documents.List().Select(d => d.Path + "\u0000" + d.Content).ToList();
            total++;
            try { await tool.Run(ArgsFor(tool), host); ran++; }
            catch (Exception e) when (e is AiToolException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or NotSupportedException) { }
            var after = host.Documents.List().Select(d => d.Path + "\u0000" + d.Content).ToList();
            if (host.Writes.Count > 0 || !before.SequenceEqual(after)) offenders.Add(tool.Name + ": " + string.Join(", ", host.Writes));
        }
        Assert.Empty(offenders);
        Assert.Equal(total, ran);   // every read tool really ran with those arguments: the check is not empty
        // control: the same watch sees a real write
        var control = new WatchingHost();
        await AssistantTools.All.Single(t => t.Name == "write_note").Run(new JsonObject { ["path"] = "Plano", ["content"] = "# Plano\n\nnovo\n" }, control);
        Assert.NotEmpty(control.Writes);
    }
}
