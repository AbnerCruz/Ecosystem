using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PageToPublicationTests
{
    [Fact]
    public void AuthoredTextBecomesNotesNotesBecomeReferencesAndTheRestIsReported()
    {
        const string json = """
        {"kind":"page","version":1,"meta":{"title":"Portfólio"},"sections":[
          {"id":"a","type":"hero","props":{"title":"Ana","subtitle":"Designer"}},
          {"id":"b","type":"text","props":{"markdown":"Sobre **mim**."}},
          {"id":"c","type":"note","props":{"path":"Projetos/Casa"}},
          {"id":"d","type":"quote","props":{"text":"Menos é mais.","author":"Mies"}},
          {"id":"e","type":"notes","props":{"source":"list","paths":"Projetos/Ponte.md\nProjetos/Sumida"}},
          {"id":"f","type":"notes","props":{"source":"folder","folder":"Diário"}},
          {"id":"g","type":"contact","props":{"email":"a@b.c"}},
          {"id":"h","type":"image","props":{"src":"Imagens/capa.png","caption":"Capa"}}
        ]}
        """;
        var ids = new Dictionary<string, string> { ["Projetos/Casa.md"] = "id-casa", ["Projetos/Ponte.md"] = "id-ponte" };
        var result = PageToPublication.Convert(PageDocument.Parse(json), p => ids.GetValueOrDefault(p));
        Assert.Equal(5, result.Pieces.Count);
        Assert.Equal("# Ana\n\nDesigner\n\nSobre **mim**.\n", result.Pieces[0].Markdown);
        Assert.Equal("id-casa", result.Pieces[1].NoteId);
        Assert.Contains("> — Mies", result.Pieces[2].Markdown);
        Assert.Equal("id-ponte", result.Pieces[3].NoteId);
        Assert.Equal("![[Imagens/capa.png]]\n\nCapa\n", result.Pieces[4].Markdown);
        Assert.Contains(result.Report, r => r == "Nota não encontrada: Projetos/Sumida");
        Assert.Contains(result.Report, r => r.Contains("folder: Diário", StringComparison.Ordinal));
        Assert.Contains(result.Report, r => r.Contains("“contact”", StringComparison.Ordinal));
    }
}
