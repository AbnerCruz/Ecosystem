using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// The tools of the 1.8.4's assistant (src/ai/tools.js), recorded by csharp/tools/assistant-tools-oracle.mjs over a small
/// vault: the text for the model (or the error), the label, the preview, the changes, the reads and the notes after each
/// call must be the original's; and validate(), applyEdit() and appendText() on their own.
/// </summary>
public sealed class AssistantToolsOracleTests
{
    private sealed class Host : IAssistantHost
    {
        private int _seq = 100;
        public Host(JsonArray notes)
        {
            int i = 0;
            foreach (var n in notes)
                Documents.Upsert(new DocumentInput { Id = "doc_" + i++, Path = (string)n!["path"]!, Content = (string)n["content"]!, Modified = (string?)n["modified"] });
            Knowledge = new KnowledgeIndex(Documents);
        }
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public List<string> Folders { get; } = [];
        public List<IReadOnlyList<AiPlanStep>> Plans { get; } = [];
        public List<string> Memory { get; } = [];
        public List<string> Opened { get; } = [];
        public int ReadChars => 2000;
        public (string? Path, string? Selection) EditorContext() => ("Comece.md", "Veja [[Plano]]");
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = "doc_" + _seq++, Path = path, Content = content }));
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Title = doc.Title, Content = content, Created = doc.Created, Modified = doc.Modified }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) =>
            Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = newPath, Content = doc.Content, Created = doc.Created, Modified = doc.Modified }));
        public Task TrashNoteAsync(UrbeDocument doc) { Documents.Remove(doc.Id); return Task.CompletedTask; }
        public Task CreateFolderAsync(string path) { Folders.Add(path); return Task.CompletedTask; }
        public void OpenNote(string path) => Opened.Add(path);
        public void Remember(string fact) => Memory.Add(fact);
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) => Plans.Add(steps);
    }

    private static JsonNode Fixture() =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "assistant", "tools-run-a.json")))!;

    private static string S(JsonNode? n) => LegacyCustomize.Stringify(n);

    private static JsonNode? Snap(AiNoteSnap? s) => s is null ? null : new JsonObject { ["path"] = s.Path, ["content"] = s.Content };

    [Fact]
    public void TheToolsAreTheOriginals()
    {
        var f = Fixture();
        var tools = f["tools"]!.AsArray();
        Assert.Equal(tools.Select(t => (string)t!["name"]!), AssistantTools.All.Select(t => t.Name));
        foreach (var t in tools)
        {
            var mine = AssistantTools.Get((string)t!["name"]!)!;
            Assert.Equal((string)t["access"]!, mine.Access);
            Assert.Equal((string)t["description"]!, mine.Description);
            Assert.Equal(S(t["parameters"]), S(mine.Parameters));
        }
    }

    public static TheoryData<int> Calls()
    {
        var data = new TheoryData<int>();
        for (int i = 0; i < Fixture()["calls"]!.AsArray().Count; i++) data.Add(i);
        return data;
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task EachCallIsTheOriginals(int index)
    {
        var f = Fixture();
        var c = f["calls"]![index]!;
        var host = new Host(f["notes"]!.AsArray());
        Assert.All(host.Documents.List(), d => Assert.Empty(d.Tags));   // the oracle's notes have no tags
        var tool = AssistantTools.Get((string)c["name"]!)!;
        var input = (JsonObject)c["input"]!.DeepClone();
        var name = (string)c["name"]! + " " + c["input"]!.ToJsonString();
        Assert.True((string)c["label"]! == tool.Label((JsonObject)input.DeepClone()), name + " label");

        if (tool.Preview is not null && c["preview"] is JsonObject pv)
        {
            try
            {
                var p = tool.Preview((JsonObject)input.DeepClone(), host);
                Assert.True(pv["error"] is null, name + " preview should fail");
                Assert.Equal(((string?)pv["path"], (string?)pv["before"], (string?)pv["after"], (string?)pv["note"]), (p.Path, p.Before, p.After, p.Note));
            }
            catch (AiToolException e) { Assert.True((string?)pv["error"] == e.Message, name + " preview: " + e.Message); }
        }

        AiToolOutput? output = null;
        string? error = null;
        try { output = await tool.Run((JsonObject)input.DeepClone(), host); }
        catch (AiToolException e) { error = e.Message; }
        if (c["error"] is JsonValue ev)
        {
            var expected = (string)ev!;
            if (expected.StartsWith("Expressão regular inválida: ", StringComparison.Ordinal)) Assert.StartsWith("Expressão regular inválida: ", error);
            else Assert.True(expected == error, name + ": expected error " + expected + " got " + (error ?? output?.Content));
        }
        else
        {
            Assert.True(error is null, name + ": " + error);
            Assert.True((string)c["content"]! == output!.Content, name + "\nexpected: " + (string)c["content"]! + "\nactual:   " + output.Content);
            var changes = c["changes"] as JsonArray;
            Assert.Equal(S(changes), output.Changes is null ? "null" : S(new JsonArray(output.Changes.Select(x =>
            {
                var o = new JsonObject { ["id"] = x.Id, ["before"] = Snap(x.Before), ["after"] = Snap(x.After) };
                if (x.Trashed) o["trashed"] = true;
                return (JsonNode)o;
            }).ToArray())));
            var reads = c["reads"] as JsonArray;
            Assert.Equal(S(reads), output.Reads is null ? "null" : S(new JsonArray(output.Reads.Select(r => (JsonNode)new JsonObject
            {
                ["path"] = r.Path, ["partial"] = r.Partial is null ? null : new JsonObject { ["path"] = r.Partial.Path, ["next"] = r.Partial.Next, ["total"] = r.Partial.Total }
            }).ToArray())));
        }
        // the notes after the call
        Assert.Equal(c["notes"]!.AsArray().Select(n => (string)n!["path"]! + "\n" + (string)n["content"]!).Order(StringComparer.Ordinal),
            host.Documents.List().Select(d => d.Path + "\n" + d.Content).Order(StringComparer.Ordinal));
        Assert.Equal(c["folders"]!.AsArray().Select(x => (string)x!), host.Folders);
        Assert.Equal(c["memory"]!.AsArray().Select(x => (string)x!), host.Memory);
        Assert.Equal(c["opened"]!.AsArray().Select(x => (string)x!), host.Opened);
        Assert.Equal(S(c["plans"]), S(new JsonArray(host.Plans.Select(p => (JsonNode)new JsonArray(p.Select(s => (JsonNode)new JsonObject { ["step"] = s.Step, ["status"] = s.Status }).ToArray())).ToArray())));
    }

    [Fact]
    public void ValidateApplyEditAndAppendTextAreTheOriginals()
    {
        var f = Fixture();
        foreach (var v in f["validations"]!.AsArray())
        {
            var input = v!["input"]?.DeepClone();
            var errs = AssistantTools.Validate(AssistantTools.Get((string)v["name"]!)!, input);
            Assert.Equal(v["errors"]!.AsArray().Select(x => (string)x!), errs);
            Assert.Equal(S(v["after"]), S(input));
        }
        foreach (var en in f["edits"]!.AsArray())
        {
            var e = en!;
            string? output = null, error = null;
            try { output = AssistantTools.ApplyEdit((string)e!["content"]!, (string)e["oldText"]!, (string)e["newText"]!, (bool)e["all"]!); }
            catch (AiToolException x) { error = x.Message; }
            Assert.Equal(((string?)e["out"], (string?)e["error"]), (output, error));
        }
        foreach (var an in f["appends"]!.AsArray())
        {
            var a = an!;
            string? output = null, error = null;
            try { output = AssistantTools.AppendText((string)a!["content"]!, (string)a["text"]!, (string?)a["heading"]); }
            catch (AiToolException x) { error = x.Message; }
            Assert.Equal(((string?)a["out"], (string?)a["error"]), (output, error));
        }
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
