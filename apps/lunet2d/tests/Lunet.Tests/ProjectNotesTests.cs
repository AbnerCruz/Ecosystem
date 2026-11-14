using Lunet.Core;

namespace Lunet.Tests;

/// <summary>LUNET-485: notes and sketches live inside the project as ordinary Markdown/PNG files.</summary>
public sealed class ProjectNotesTests
{
    [Fact]
    public void CreateNote_WritesMarkdownInNotesFolderWithUniqueNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-notes-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("Notas");
            Assert.Empty(ProjectNotes.List(project));
            var first = ProjectNotes.CreateNote(project, "Ideias do chefe: fase 2!");
            var second = ProjectNotes.CreateNote(project, "Ideias do chefe: fase 2!");
            Assert.StartsWith("Notes/", first, StringComparison.Ordinal);
            Assert.EndsWith(".md", first, StringComparison.Ordinal);
            Assert.NotEqual(first, second);
            Assert.StartsWith("# Ideias do chefe: fase 2!", project.ReadText(first), StringComparison.Ordinal);
            var listed = ProjectNotes.List(project);
            Assert.Equal(2, listed.Count);
            Assert.All(listed, n => Assert.Equal(NoteKind.Text, n.Kind));
            Assert.Equal("Ideias do chefe: fase 2!", listed[0].Title);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Sketches_AreListedAndNotesAreNotCompiled()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-notes-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("Esbocos");
            var sketch = ProjectNotes.NewSketchPath(project, "Mapa da fase");
            Assert.Equal("Notes/mapa-da-fase.png", sketch);
            using (var png = new MemoryStream([137, 80, 78, 71])) project.Import("Notes", "mapa-da-fase.png", png);
            ProjectNotes.CreateNote(project, "Roteiro");
            var listed = ProjectNotes.List(project);
            Assert.Contains(listed, n => n.Kind == NoteKind.Sketch && n.Path == sketch);
            Assert.DoesNotContain(project.LoadSources(), s => s.Path.StartsWith("Notes/", StringComparison.Ordinal));
            Assert.Equal("Notes/mapa-da-fase-2.png", ProjectNotes.NewSketchPath(project, "Mapa da fase"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void TitleOf_ReadsFirstHeadingOrFileName()
    {
        Assert.Equal("Plano", ProjectNotes.TitleOf("Notes/x.md", "# Plano\ntexto"));
        Assert.Equal("x", ProjectNotes.TitleOf("Notes/x.md", "sem título"));
        Assert.Equal("ideia", ProjectNotes.Slug("  Idéia!! "));
        Assert.Equal("nota", ProjectNotes.Slug("!!!"));
    }
}
