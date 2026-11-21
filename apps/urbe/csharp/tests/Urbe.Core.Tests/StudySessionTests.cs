using Urbe.Core.Study;

namespace Urbe.Core.Tests;

public sealed class StudySessionTests
{
    [Fact]
    public void ASessionReferencesSourcesByIdAndFollowsRenames()
    {
        var s = new StudySession { Title = "Física", Objectives = ["Entender a 2ª lei"] }
            .Add("doc1", "Livros/Halliday.pdf").Add("doc2", "Aulas/Newton.md").Add("doc1", "duplicado");
        Assert.Equal(2, s.Sources.Count);
        var paths = new Dictionary<string, string> { ["doc1"] = "Biblioteca/Halliday.pdf" };
        var (present, missing) = s.Resolve(id => paths.GetValueOrDefault(id));
        Assert.Equal([("doc1", "Biblioteca/Halliday.pdf")], present);
        Assert.Equal("doc2", Assert.Single(missing).DocumentId);
        var back = StudySession.Parse(s.Serialize());
        Assert.Equal(s.Sources, back.Sources);
        Assert.Equal("Entender a 2ª lei", back.Objectives.Single());
        Assert.DoesNotContain("conteudo", s.Serialize());   // metadata only
    }

    [Fact]
    public void UnknownFieldsSurviveAndAFutureVersionIsRefused()
    {
        var back = StudySession.Parse("{\"version\":1,\"title\":\"x\",\"sources\":[],\"futuro\":{\"a\":1}}");
        Assert.Contains("futuro", back.Serialize());
        Assert.Throws<NotSupportedException>(() => StudySession.Parse("{\"version\":2}"));
    }
}
