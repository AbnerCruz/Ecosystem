using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class EditorialNotesTests
{
    [Fact]
    public void AnAdaptationIsARealNoteThatNamesItsSourceAndNoticesItChange()
    {
        const string origin = "---\ntags: [bio]\n---\n# Fotossíntese\n\nTexto longo da pesquisa.\n";
        var note = EditorialNotes.Create("doc_1", "Bio/Fotossíntese.md", origin, "Resumo");
        Assert.Contains("adaptacao_de: \"[[Fotossíntese]]\"", note);
        Assert.Contains("Texto longo da pesquisa.", note);
        Assert.DoesNotContain("tags", note);
        var link = EditorialNotes.Read(note)!;
        Assert.Equal("doc_1", link.OriginId);
        Assert.Equal("Resumo", link.Kind);
        Assert.False(EditorialNotes.IsBehind(link, origin));
        Assert.False(EditorialNotes.IsBehind(link, origin.Replace("[bio]", "[x]")));   // a property is not the text
        var changed = origin + "\nUm parágrafo novo.\n";
        Assert.True(EditorialNotes.IsBehind(link, changed));
        var reviewed = EditorialNotes.Reviewed(note.Replace("Texto longo", "Texto curto"), changed);
        Assert.False(EditorialNotes.IsBehind(EditorialNotes.Read(reviewed)!, changed));
        Assert.Contains("Texto curto", reviewed);
        Assert.Null(EditorialNotes.Read(origin));
    }

    [Fact]
    public void APieceCanShowTheAdaptationAndKeepsItsPlace()
    {
        var project = PublicationProject.Create("Livro", "a").AddSource("b");
        var piece = project.Sections[0];
        var next = project.ReplacePlacementSource(piece.Id, new PublicationSourceReference("a2"));
        Assert.Equal(piece.Id, next.Sections[0].Id);
        Assert.Equal("a2", next.Sections[0].Source.DocumentId);
        Assert.Equal(["a2", "b"], next.Sources.Select(s => s.DocumentId));
        PublicationProjectCodec.Validate(next);
    }
}
