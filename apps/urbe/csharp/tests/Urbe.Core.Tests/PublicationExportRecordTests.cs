namespace Urbe.Core.Tests;

public sealed class PublicationExportRecordTests
{
    [Fact]
    public void TheRecordSaysWhatChangedHowAndWhatCameSinceTheExport()
    {
        var notes = new Dictionary<string, string>
        {
            ["visual"] = "# A\n\nUm texto.", ["text"] = "# B\n\nUm texto.", ["structure"] = "# C\n\nVer [[X]].", ["gone"] = "# D", ["same"] = "# E"
        };
        var project = notes.Keys.Skip(1).Aggregate(PublicationProject.Create("P", "visual"), (p, id) => p.AddSource(id));
        var record = PublicationExportRecord.Of(project, "html", id => notes.GetValueOrDefault(id), DateTimeOffset.UnixEpoch);
        notes["visual"] = "# A\n\nUm **texto**.";
        notes["text"] = "# B\n\nOutro texto.";
        notes["structure"] = "# C\n\nVer [[Y]].";
        notes.Remove("gone");
        var (changed, added) = record.Since(project.AddSource("new"), id => notes.GetValueOrDefault(id));
        Assert.Equal([("gone", PublicationExportRecord.ChangeKind.Removed), ("structure", PublicationExportRecord.ChangeKind.Structure),
            ("text", PublicationExportRecord.ChangeKind.Text), ("visual", PublicationExportRecord.ChangeKind.Visual)], changed);
        Assert.Equal(["new"], added);
        var back = PublicationProjectCodec.Parse(PublicationProjectCodec.Serialize(project with { LastExport = record }));
        Assert.Equal(record.Sources, back.LastExport!.Sources);
    }
}
