namespace Urbe.Core.Tests;

public sealed class PublicationExportRecordTests
{
    [Fact]
    public void TheRecordSaysWhatChangedLeftOrCameSinceTheExport()
    {
        var notes = new Dictionary<string, string> { ["a"] = "# A", ["b"] = "# B", ["c"] = "# C" };
        var project = PublicationProject.Create("P", "a").AddSource("b").AddSource("c");
        var record = PublicationExportRecord.Of(project, "html", id => notes.GetValueOrDefault(id), DateTimeOffset.UnixEpoch);
        Assert.Equal((0, 0), (record.Since(project, id => notes.GetValueOrDefault(id)).Changed.Count, record.Since(project, id => notes.GetValueOrDefault(id)).Added.Count));
        notes["b"] = "# B mudou";
        notes.Remove("c");
        var later = project.AddSource("d");
        var (changed, added) = record.Since(later, id => notes.GetValueOrDefault(id));
        Assert.Equal(["b", "c"], changed);
        Assert.Equal(["d"], added);
        var back = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project with { LastExport = record }));
        Assert.Equal(record.Sources, back.LastExport!.Sources);
        Assert.Equal("html", back.LastExport.Format);
    }
}
