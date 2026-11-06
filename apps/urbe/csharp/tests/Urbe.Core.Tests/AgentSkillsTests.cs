using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-177/214: Agent Skills — catalog by name and description, instructions on demand, files read as text.</summary>
public sealed class AgentSkillsTests
{
    private const string Skill = "---\nname: revisar-livro\ndescription: Revisar capítulos de um livro com checklist editorial.\nlicense: MIT\nallowed-tools: Bash\n---\n# Revisão\n\nLeia checklist.md antes.\n";

    private sealed class Host : IAssistantHost
    {
        public DocumentStore Documents { get; } = new();
        public KnowledgeIndex Knowledge { get; }
        public Host() => Knowledge = new KnowledgeIndex(Documents);
        public int ReadChars => 4000;
        public (string? Path, string? Selection) EditorContext() => (null, null);
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => throw new NotSupportedException();
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string path) => throw new NotSupportedException();
        public Task TrashNoteAsync(UrbeDocument doc) => throw new NotSupportedException();
        public Task CreateFolderAsync(string path) => throw new NotSupportedException();
        public void OpenNote(string path) { }
        public void Remember(string fact) { }
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private static Host WithSkill()
    {
        var host = new Host();
        host.Documents.Upsert(new DocumentInput { Path = "Personalização/Skills/revisar-livro/SKILL.md", Content = Skill });
        host.Documents.Upsert(new DocumentInput { Path = "Personalização/Skills/revisar-livro/checklist.md", Content = "- ortografia\n- coerência" });
        host.Documents.Upsert(new DocumentInput { Path = "Diário/segredo.md", Content = "PRIVADO" });
        host.Documents.Upsert(new DocumentInput { Path = "Personalização/Skills/quebrada/SKILL.md", Content = "# sem cabeçalho" });
        return host;
    }

    private static IReadOnlyList<AgentSkill> Of(Host h) => AgentSkills.Discover(h.Documents.List().Select(d => (d.Path, d.Content)));

    [Fact]
    public void Discover_ReadsFormat_ReportsProblems_AndAddsTheAppsSkills()
    {
        var skills = Of(WithSkill());
        var mine = skills.Single(s => s.Name == "revisar-livro");
        Assert.Equal("vault", mine.Origin);
        Assert.Equal("MIT", mine.License);
        Assert.Equal(["checklist.md"], mine.Files);
        Assert.Contains(mine.Problems!, p => p.Contains("allowed-tools é ignorado"));
        Assert.Contains(skills, s => s.Name == "calculo-urbe" && s.Origin == "app");
        var broken = skills.Single(s => s.Folder.EndsWith("quebrada"));
        Assert.False(AgentSkills.Usable(broken));
        var catalog = AgentSkills.Catalog(skills);
        Assert.Contains("- revisar-livro: Revisar capítulos", catalog);
        Assert.DoesNotContain("Leia checklist.md", catalog); // instructions only on demand
        Assert.DoesNotContain("quebrada", catalog);
    }

    [Fact]
    public async Task Tools_LoadInstructions_AndReadOnlyTheSkillsOwnFiles()
    {
        var host = WithSkill();
        var tools = AgentSkills.Tools(h => Of((Host)h));
        Assert.All(tools, t => Assert.Equal("read", t.Access));
        var loaded = await tools[0].Run(new JsonObject { ["name"] = "revisar-livro" }, host);
        Assert.Contains("Leia checklist.md antes.", loaded.Content);
        Assert.Contains("checklist.md", loaded.Content);
        var file = await tools[1].Run(new JsonObject { ["name"] = "revisar-livro", ["file"] = "checklist.md" }, host);
        Assert.Contains("ortografia", file.Content);
        foreach (var escape in new[] { "../../../Diário/segredo.md", "/Diário/segredo.md", "..\\SKILL.md" })
        {
            var refused = await tools[1].Run(new JsonObject { ["name"] = "revisar-livro", ["file"] = escape }, host);
            Assert.DoesNotContain("PRIVADO", refused.Content);
        }
        Assert.Contains("Não há skill", (await tools[0].Run(new JsonObject { ["name"] = "inexistente" }, host)).Content);
    }

    [Fact]
    public void PersonsSkill_WinsOverTheAppsWithTheSameName()
    {
        var host = new Host();
        host.Documents.Upsert(new DocumentInput { Path = "Personalização/Skills/calculo-urbe/SKILL.md", Content = "---\nname: calculo-urbe\ndescription: A minha versão.\n---\nMinha" });
        Assert.Equal("A minha versão.", Of(host).Single(s => s.Name == "calculo-urbe").Description);
    }
}
