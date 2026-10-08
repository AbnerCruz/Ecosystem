using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MathAuthoring.Core;
using MathAuthoring.Persistence;

namespace MathAuthoring.Core.Tests;

public sealed class PersistenceTests
{
    private static PortableProject Make(long revision, string title = "Δx → 0") =>
        new(ProjectDocument.Create("math-demo", title), revision,
            Array.AsReadOnly(new[]
            {
                new ProjectAsset("graph", "image/png", new byte[] { 137, 80, 78, 71, 1, 2, 3 }),
                new ProjectAsset("thumb", "image/png", new byte[] { 137, 80, 78, 71, 1, 2, 3 }),
                new ProjectAsset("audio", "audio/wav", new byte[] { 82, 73, 70, 70, 4, 5 })
            }));

    private static byte[] Pack(PortableProject project)
    {
        using var stream = new MemoryStream();
        ProjectPackage.Write(stream, project);
        return stream.ToArray();
    }

    private static PortableProject Unpack(byte[] data) => ProjectPackage.Read(new MemoryStream(data));

    private static byte[] ModifyZip(byte[] source, Action<ZipArchive> change)
    {
        using var stream = new MemoryStream();
        stream.Write(source, 0, source.Length);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
            change(zip);
        return stream.ToArray();
    }

    private static void Overwrite(ZipArchive archive, string name, byte[] data)
    {
        archive.GetEntry(name)?.Delete();
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(data, 0, data.Length);
    }

    [Fact]
    public void Portable_round_trip_keeps_ids_revision_and_asset_bytes()
    {
        var bytes = Pack(Make(7));
        var project = Unpack(bytes);

        Assert.Equal(7, project.Revision);
        Assert.Equal("math-demo", project.Document.ProjectId);
        Assert.Equal("Δx → 0", project.Document.Metadata.Title);
        Assert.Equal(3, project.Assets.Count);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 1, 2, 3 }, project.Assets[0].Bytes);
        Assert.Equal(Pack(Make(7)), bytes);
        using var archive = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(4, archive.Entries.Count); // duplicated asset content stored only once
        Assert.All(archive.Entries, e => Assert.DoesNotContain("..", e.FullName));
    }

    [Fact]
    public void Corrupted_document_or_asset_is_rejected_by_hash()
    {
        var sample = Pack(Make(1));
        var damagedDocument = ModifyZip(sample,
            zip => Overwrite(zip, "project.json", Encoding.UTF8.GetBytes("{}")));
        Assert.Throws<InvalidDataException>(() => Unpack(damagedDocument));

        var hash = Convert.ToHexStringLower(SHA256.HashData(Make(1).Assets[0].Bytes));
        var damagedAsset = ModifyZip(sample,
            zip => Overwrite(zip, $"assets/{hash}.bin", new byte[] { 1, 2, 3, 4, 5, 6, 7 }));
        Assert.Throws<InvalidDataException>(() => Unpack(damagedAsset));
    }

    [Fact]
    public void Archive_traversal_extra_entries_and_zip_bomb_are_rejected()
    {
        var sample = Pack(Make(1));
        var traversal = ModifyZip(sample, zip => Overwrite(zip, "../escape.txt", new byte[] { 1 }));
        Assert.Throws<InvalidDataException>(() => Unpack(traversal));
        var extra = ModifyZip(sample, zip => Overwrite(zip, "assets/orphan", new byte[] { 1 }));
        Assert.Throws<InvalidDataException>(() => Unpack(extra));

        // highly compressible payload is limited by expanded entry size
        var bomb = ModifyZip(sample, zip => Overwrite(zip,
            "assets/" + new string('a', 64) + ".bin", new byte[ProjectPackage.MaxAssetBytes + 1]));
        Assert.Throws<InvalidDataException>(() => Unpack(bomb));
    }

    [Fact]
    public void Manifest_unknown_fields_duplicate_keys_and_future_versions_fail()
    {
        var sample = Pack(Make(1));
        using var reader = new ZipArchive(new MemoryStream(sample));
        using var manifestStream = reader.GetEntry("manifest.json")!.Open();
        using var stringReader = new StreamReader(manifestStream);
        var original = stringReader.ReadToEnd();
        var unknown = ModifyZip(sample, zip => Overwrite(zip, "manifest.json",
            Encoding.UTF8.GetBytes(original.Replace("\"bundleVersion\":1", "\"bundleVersion\":1,\"exec\":true", StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => Unpack(unknown));

        var duplicate = ModifyZip(sample, zip => Overwrite(zip, "manifest.json",
            Encoding.UTF8.GetBytes(original.Replace("\"bundleVersion\":1", "\"bundleVersion\":1,\"bundleVersion\":1", StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => Unpack(duplicate));

        var future = ModifyZip(sample, zip => Overwrite(zip, "manifest.json",
            Encoding.UTF8.GetBytes(original.Replace("\"bundleVersion\":1", "\"bundleVersion\":2", StringComparison.Ordinal))));
        Assert.Throws<InvalidDataException>(() => Unpack(future));
    }

    [Fact]
    public void Untrusted_asset_ids_or_media_types_are_never_written_as_paths()
    {
        var bad = new PortableProject(ProjectDocument.Create("p", "Title"), 1,
            new[] { new ProjectAsset("../etc/passwd", "application/x-msdownload", new byte[] { 1 }) });
        Assert.Throws<InvalidDataException>(() => Pack(bad));
        var dup = new PortableProject(ProjectDocument.Create("p", "Title"), 1,
            new[] { new ProjectAsset("asset", "image/png", new byte[] { 1 }),
                    new ProjectAsset("asset", "image/png", new byte[] { 2 }) });
        Assert.Throws<InvalidDataException>(() => Pack(dup));
    }

    [Fact]
    public void Fault_before_replace_preserves_original_and_cleans_temporary()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "lesson.maproj");
            ProjectStorage.Save(path, Make(1, "Original"));
            Assert.Throws<IOException>(() => ProjectStorage.Save(path, Make(2, "Edited"),
                checkpoint => { if (checkpoint == SaveCheckpoint.AfterTempSync) throw new IOException("Injected"); }));

            var recovery = ProjectStorage.Recover(path);
            Assert.Equal(ProjectCopy.Primary, recovery.SelectedCopy);
            Assert.Equal(1, recovery.Project.Revision);
            Assert.Equal("Original", recovery.Project.Document.Metadata.Title);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        });
    }

    [Fact]
    public void Fault_after_replace_leaves_a_valid_latest_or_backup_checkpoint()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "lesson.maproj");
            ProjectStorage.Save(path, Make(1));
            Assert.Throws<IOException>(() => ProjectStorage.Save(path, Make(2),
                checkpoint => { if (checkpoint == SaveCheckpoint.AfterPublish) throw new IOException("Injected"); }));

            var recovered = ProjectStorage.Recover(path);
            Assert.Equal(2, recovered.Project.Revision);
            using var backup = File.OpenRead(path + ".bak");
            Assert.Equal(1, ProjectPackage.Read(backup).Revision);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        });
    }

    [Fact]
    public void Corrupt_primary_recovers_from_backup_without_overwriting_corruption()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "lesson.maproj");
            ProjectStorage.Save(path, Make(1));
            ProjectStorage.Save(path, Make(2));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
            Assert.Throws<InvalidDataException>(() => ProjectStorage.Save(path, Make(3)));

            var recovery = ProjectStorage.Recover(path);
            Assert.Equal(ProjectCopy.Backup, recovery.SelectedCopy);
            Assert.Equal(1, recovery.Project.Revision);
            Assert.NotEmpty(recovery.Warnings);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void Autosave_newer_than_primary_is_reported_without_rewriting_files()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "lesson.maproj");
            ProjectStorage.Save(path, Make(3));
            ProjectStorage.Autosave(path, Make(4));
            var recovered = ProjectStorage.Recover(path);
            Assert.Equal(ProjectCopy.Autosave, recovered.SelectedCopy);
            Assert.Equal(4, recovered.Project.Revision);
            using var stream = File.OpenRead(path);
            Assert.Equal(3, ProjectPackage.Read(stream).Revision);
            Assert.Throws<InvalidDataException>(() => ProjectStorage.Save(path, Make(2)));
        });
    }

    [Fact]
    public void Unrelated_project_cannot_overwrite_or_mix_recovery_checkpoints()
    {
        InTempDirectory(directory =>
        {
            var path = Path.Combine(directory, "lesson.maproj");
            ProjectStorage.Save(path, Make(1));
            var other = Make(2) with { Document = ProjectDocument.Create("unrelated", "Outro") };
            Assert.Throws<InvalidDataException>(() => ProjectStorage.Save(path, other));
            ProjectStorage.Autosave(path, other);
            Assert.Throws<InvalidDataException>(() => ProjectStorage.Recover(path));
        });
    }

    private static void InTempDirectory(Action<string> action)
    {
        var folder = Path.Combine(Path.GetTempPath(), "math-authoring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try { action(folder); }
        finally { Directory.Delete(folder, recursive: true); }
    }
}
