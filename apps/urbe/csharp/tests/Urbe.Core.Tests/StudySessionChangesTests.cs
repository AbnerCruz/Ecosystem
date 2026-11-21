using Urbe.Core;
using Urbe.Core.Study;

namespace Urbe.Core.Tests;

/// <summary>#956 acceptance 2: one note in three sessions, each one told when the note changed since it was studied there.</summary>
public sealed class StudySessionChangesTests
{
    [Fact]
    public void EverySessionWithTheNoteSeesTheChangeAndItsKindUntilReviewed()
    {
        var notes = new Dictionary<string, string> { ["d1"] = "# Cap. 1\n\nA força é massa vezes aceleração.\n", ["d2"] = "# Outra\n\nTexto.\n" };
        string? Md(string id) => notes.GetValueOrDefault(id);
        var sessions = new[] { "Física I", "Revisão", "Prova" }.Select(t => new StudySession { Title = t }.Add("d1", "Livro/Cap 1.md", Md("d1")).Add("d2", "Outra.md", Md("d2"))).ToList();
        Assert.All(sessions, s => Assert.Empty(s.Changes(Md)));

        notes["d1"] = "# Cap. 1\n\nA força resultante é massa vezes aceleração.\n";          // the original note is edited once
        Assert.All(sessions, s => Assert.Equal(("d1", PublicationExportRecord.ChangeKind.Text), (s.Changes(Md).Single().Source.DocumentId, s.Changes(Md).Single().Kind)));

        var reviewed = sessions[0].MarkSeen(Md);                                                // reviewed in one session only
        Assert.Empty(reviewed.Changes(Md));
        Assert.Single(sessions[1].Changes(Md));
        Assert.Equal(reviewed.Sources, StudySession.Parse(reviewed.Serialize()).Sources);       // what was seen is kept in the file

        notes["d2"] = "# Outra\n\n## Seção nova\n\nTexto.\n";
        Assert.Equal(PublicationExportRecord.ChangeKind.Structure, reviewed.Changes(Md).Single().Kind);
        notes.Remove("d2");
        Assert.Empty(reviewed.Changes(Md));                                                      // gone: told by Resolve, not here
        Assert.Single(reviewed.Resolve(id => notes.ContainsKey(id) ? "x" : null).Missing);
    }

    [Fact]
    public void AnOlderSessionWithoutVersionsTellsNothingUntilItsFirstBaseline()
    {
        var old = StudySession.Parse("{\"version\":1,\"title\":\"Antiga\",\"sources\":[{\"documentId\":\"d1\",\"pathHint\":\"A.md\"}]}");
        Assert.Empty(old.Changes(_ => "# mudou\n"));
        Assert.NotNull(old.MarkSeen(_ => "# A\n").Sources.Single().Seen);
    }
}
