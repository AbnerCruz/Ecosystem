using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>The Assistente's page tools (src/pages/ai-tools.js) against csharp/tools/assistant-pages-oracle.mjs (ids aside).</summary>
public sealed class AssistantPagesOracleTests
{
    private sealed class Host : IAssistantHost
    {
        private int _seq = 100;
        public Host(JsonArray notes)
        {
            int i = 0;
            foreach (var n in notes) Documents.Upsert(new DocumentInput { Id = "doc_" + i++, Path = (string)n!["path"]!, Content = (string)n["content"]! });
            Knowledge = new KnowledgeIndex(Documents);
        }
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public int ReadChars => 2000;
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => Task.FromResult(Documents.Upsert(new DocumentInput { Id = "doc_" + _seq++, Path = path, Content = content }));
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => Task.FromResult(Documents.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Content = content }));
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static JsonNode Fixture() =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "assistant", "pages-run-a.json")))!;

    private static JsonNode? Strip(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.Where(p => p.Key != "id").Select(p => KeyValuePair.Create(p.Key, Strip(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Strip).ToArray()),
        _ => n?.DeepClone()
    };

    private static string? FileOf(string? text) => text is null ? null : LegacyCustomize.Stringify(Strip(JsonNode.Parse(text)));
    private static string S(JsonNode? n) => LegacyCustomize.Stringify(n);

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
        AssistantPageTools.Now = () => new DateTime(2025, 10, 9, 12, 0, 0);
        var f = Fixture();
        var c = f["calls"]![index]!;
        IAssistantHost host = new Host(f["notes"]!.AsArray());
        var tool = AssistantPageTools.All.Single(t => t.Name == (string)c["name"]!);
        var input = (JsonObject)c["input"]!;
        var name = tool.Name + " " + input.ToJsonString();
        Assert.Equal(((string)c["access"]!, (string)c["description"]!, S(c["parameters"]), (string)c["label"]!), (tool.Access, tool.Description, S(tool.Parameters), tool.Label(input)));
        static bool Same(string e, string? a) => e.StartsWith("spec não é JSON válido: ", StringComparison.Ordinal) ? a?.StartsWith("spec não é JSON válido: ", StringComparison.Ordinal) == true : e == a;
        if (tool.Preview is not null && c["preview"] is JsonObject pv)
        {
            try
            {
                var p = tool.Preview((JsonObject)input.DeepClone(), host);
                Assert.True(pv["error"] is null, name + " preview should fail");
                Assert.Equal(((string?)pv["path"], (string?)pv["before"], (string?)pv["after"], (string?)pv["note"]), (p.Path, p.Before, FileOf(p.After), p.Note));
            }
            catch (AiToolException e) { Assert.True(Same((string)pv["error"]!, e.Message), name + " preview: " + e.Message); }
        }
        AiToolOutput? output = null;
        string? error = null;
        try { output = await tool.Run((JsonObject)input.DeepClone(), host); }
        catch (AiToolException e) { error = e.Message; }
        if (c["error"] is JsonValue ev) Assert.True(Same((string)ev!, error), name + ": " + error);
        else
        {
            Assert.True(error is null, name + ": " + error);
            Assert.Equal((string)c["content"]!, output!.Content);
            Assert.Equal(S(c["changes"]), output.Changes is null ? "null" : S(new JsonArray(output.Changes.Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Id, ["before"] = x.Before is null ? null : new JsonObject { ["path"] = x.Before.Path, ["content"] = x.Before.Content },
                ["after"] = new JsonObject { ["path"] = x.After!.Path, ["content"] = FileOf(x.After.Content) }
            }).ToArray())));
        }
        Assert.Equal(c["files"]!.AsArray().Select(x => (string)x!["path"]! + "\n" + (string?)x["content"]),
            host.Documents.List().Select(d => d.Path + "\n" + (d.Path.EndsWith(".json", StringComparison.Ordinal) ? FileOf(d.Content) : d.Content)));
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
