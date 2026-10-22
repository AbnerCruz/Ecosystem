using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// The Assistente's appearance tools (src/customize/ai-tools.js) recorded by csharp/tools/assistant-customize-oracle.mjs
/// over the real customize.js: the schema text, the label, the preview, the text for the model (or the error), the
/// change and the tema.json written must be the original's.
/// </summary>
public sealed class AssistantCustomizeOracleTests
{
    private sealed class Appearance(DocumentStore docs) : IAssistantAppearance
    {
        private bool Saved(string path) => docs.Get(path) is not null;
        public JsonObject Current { get; set; } = LegacyCustomize.Normalize(docs.Get(LegacyCustomize.FilePath)?.Content, null).Config;
        public LegacyCustomize.Result Normalize(JsonNode config) => LegacyCustomize.Normalize(config, Saved);
        public Task ReplaceAsync(JsonObject config)
        {
            var n = LegacyCustomize.Normalize(config, Saved);
            var text = LegacyCustomize.FileText(n.Config);
            var d = docs.Get(LegacyCustomize.FilePath);
            if (d?.Content != text) docs.Upsert(new DocumentInput { Id = d?.Id, Path = LegacyCustomize.FilePath, Content = text });
            Current = n.Config;
            return Task.CompletedTask;
        }
        public IReadOnlyList<string> SavedThemes => docs.List().Where(d => d.Path.StartsWith("Personalização/temas/", StringComparison.Ordinal) && d.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(d => d.Path).ToList();
        public IReadOnlyList<string> Packs => docs.List().Where(d => d.Path.StartsWith("Personalização/texturas/", StringComparison.Ordinal) && d.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(d => d.Path).ToList();
    }

    private sealed class Host : IAssistantHost
    {
        public Host(JsonObject files)
        {
            int i = 0;
            foreach (var (path, content) in files) Documents.Upsert(new DocumentInput { Id = "doc_" + i++, Path = path, Content = (string)content! });
            Knowledge = new KnowledgeIndex(Documents);
            Appearance = new Appearance(Documents);
        }
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public IAssistantAppearance? Appearance { get; }
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public int ReadChars => 2000;
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static JsonNode Fixture() =>
        JsonNode.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "assistant", "customize-run-a.json")))!;

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
        var f = Fixture();
        var c = f["calls"]![index]!;
        IAssistantHost host = new Host((JsonObject)f["files"]!);
        var tool = AssistantCustomizeTools.All.Single(t => t.Name == (string)c["name"]!);
        var input = (JsonObject)c["input"]!;
        var name = tool.Name + " " + input.ToJsonString();
        Assert.Equal(((string)c["access"]!, WithoutTheCity.Appearance((string)c["description"]!), S(c["parameters"]), (string)c["label"]!), (tool.Access, tool.Description, S(tool.Parameters), tool.Label(input)));

        static bool SameError(string expected, string? actual) =>
            expected.StartsWith("config não é JSON válido: ", StringComparison.Ordinal) ? actual?.StartsWith("config não é JSON válido: ", StringComparison.Ordinal) == true : expected == actual;

        if (tool.Preview is not null && c["preview"] is JsonObject pv)
        {
            try
            {
                var p = tool.Preview((JsonObject)input.DeepClone(), host);
                Assert.True(pv["error"] is null, name + " preview should fail");
                Assert.Equal(((string?)pv["path"], (string?)pv["before"], (string?)pv["after"], (string?)pv["note"]), (p.Path, p.Before, p.After, p.Note));
            }
            catch (AiToolException e) { Assert.True(SameError((string)pv["error"]!, e.Message), name + " preview: " + e.Message); }
        }
        AiToolOutput? output = null;
        string? error = null;
        try { output = await tool.Run((JsonObject)input.DeepClone(), host); }
        catch (AiToolException e) { error = e.Message; }
        if (c["error"] is JsonValue ev) Assert.True(SameError((string)ev!, error), name + ": " + error);
        else
        {
            Assert.True(error is null, name + ": " + error);
            Assert.Equal(WithoutTheCity.Appearance((string)c["content"]!), output!.Content);
            Assert.Equal(S(c["changes"]), output.Changes is null ? "null" : S(new JsonArray(output.Changes.Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Id, ["before"] = x.Before is null ? null : new JsonObject { ["path"] = x.Before.Path, ["content"] = x.Before.Content },
                ["after"] = x.After is null ? null : new JsonObject { ["path"] = x.After.Path, ["content"] = x.After.Content }
            }).ToArray())));
        }
        Assert.Equal((string?)c["file"], host.Documents.Get(LegacyCustomize.FilePath)?.Content);
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
