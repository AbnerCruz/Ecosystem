using System.IO.Compression;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class ReusableAssetLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-global-library-" + Guid.NewGuid().ToString("N"));
    private ProjectStore Projects => new(Path.Combine(_root, "Projects"));
    private string LibraryRoot => Path.Combine(_root, "Library");
    private ReusableAssetLibrary Library => new(LibraryRoot);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private LunetProject CreateSpriteProject(string name, string json = "{\"frames\":[1]}")
    {
        var project = Projects.Create(name);
        project.WriteText("Content/Art/wing.sprite.json", json);
        using var png = new MemoryStream([137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3]);
        project.Import("Content/Art", "wing.png", png);
        return project;
    }

    [Fact]
    public void FindImportConflicts_ReportsExistingFilesWithoutChangingProject()
    {
        var source = CreateSpriteProject("ConflictSource");
        var destination = Projects.Create("ConflictDestination");
        destination.WriteText("Content/Art/wing.sprite.json", "manual edit");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json", "Wings", "Author", "CC0");

        var conflicts = Library.FindImportConflicts(asset.Id, asset.Version, destination);

        Assert.Equal(new[] { "Content/Art/wing.sprite.json" }, conflicts);
        Assert.Equal("manual edit", destination.ReadText("Content/Art/wing.sprite.json"));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.png")));
        Assert.Throws<IOException>(() => Library.ImportToProject(asset.Id, asset.Version, destination, TestContext.Current.CancellationToken));
        Assert.Equal("manual edit", destination.ReadText("Content/Art/wing.sprite.json"));
    }

    [Fact]
    public void FindImportConflicts_ReportsAncestorFileWithoutChangingDestination()
    {
        var source = CreateSpriteProject("AncestorSource");
        var destination = Projects.Create("AncestorDestination");
        destination.WriteText("Content/Art", "not a directory");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Wings", "Author", "CC0");

        var conflicts = Library.FindImportConflicts(asset.Id, asset.Version, destination);

        Assert.Equal(2, conflicts.Count);
        Assert.Contains("Content/Art/wing.sprite.json", conflicts);
        Assert.Contains("Content/Art/wing.png", conflicts);
        Assert.Equal("not a directory", destination.ReadText("Content/Art"));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.png")));
    }

    [Fact]
    public void ImportToProject_AncestorFileConflictFailsBeforeCreatingStaging()
    {
        var source = CreateSpriteProject("BlockedSource");
        var destination = Projects.Create("BlockedTarget");
        destination.WriteText("Content/Art", "manual content");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Wings", "Author", "CC0");
        var before = destination.ReadText("Game.cs");

        var conflicts = Library.FindImportConflicts(asset.Id, asset.Version, destination);
        Assert.Equal(2, conflicts.Count);
        Assert.Throws<IOException>(() => Library.ImportToProject(asset.Id, asset.Version, destination, TestContext.Current.CancellationToken));
        Assert.Equal("manual content", destination.ReadText("Content/Art"));
        Assert.Equal(before, destination.ReadText("Game.cs"));
        Assert.False(Directory.Exists(Path.Combine(destination.Directory, ".lunet", "import-")));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.png")));
    }

    [Fact]
    public void PublishRevision_RejectsSymlinkedAssetDirectoryWithoutTouchingExternalFiles()
    {
        var source = CreateSpriteProject("SymlinkSource");
        var library = Library;
        var first = library.Publish(source, "Content/Art/wing.sprite.json", "Wings", "Author", "CC0");
        var external = Path.Combine(_root, "external-assets");
        Directory.CreateDirectory(external);
        var original = Path.Combine(LibraryRoot, first.Id);
        var backup = Path.Combine(_root, "original-assets");
        Directory.Move(original, backup);
        try
        {
            try { Directory.CreateSymbolicLink(original, external); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                // Filesystems without symlink support cannot exercise this platform-specific guard.
                return;
            }
            source.WriteText("Content/Art/wing.sprite.json", "revision");
            Assert.Throws<InvalidDataException>(() =>
                library.Publish(source, "Content/Art/wing.sprite.json", "Wings", "Author", "CC0",
                    existingAssetId: first.Id));
            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
        }
        finally
        {
            if (Directory.Exists(original)) Directory.Delete(original);
            Directory.Move(backup, original);
        }
    }

    [Fact]
    public void PublishAndImport_CopyBothEditableSourceAndDerivedSpriteWithoutSharedDependency()
    {
        var source = CreateSpriteProject("One");
        var other = Projects.Create("Two");
        var library = Library;
        var package = library.Publish(source, "Content/Art/wing.sprite.json",
            "Asas", "Eu", "CC0", tags: ["montaria", "pixel art"]);

        Assert.Equal(1, package.Version);
        Assert.Equal(ProjectAssetKind.Sprite, package.Kind);
        Assert.Equal(2, package.Files.Length);
        Assert.Equal("CC0", package.License);
        Assert.Contains(package.Files, x => x.Path.EndsWith(".png"));
        Assert.Single(library.List());

        var imported = library.ImportToProject(package.Id, 1, other, TestContext.Current.CancellationToken);
        Assert.Equal(2, imported.Count);
        Assert.Equal(source.ReadText("Content/Art/wing.sprite.json"),
            other.ReadText("Content/Art/wing.sprite.json"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(source.Directory, "Content/Art/wing.png")),
            File.ReadAllBytes(Path.Combine(other.Directory, "Content/Art/wing.png")));

        source.WriteText("Content/Art/wing.sprite.json", "modificado depois");
        Assert.NotEqual(source.ReadText("Content/Art/wing.sprite.json"),
            other.ReadText("Content/Art/wing.sprite.json"));
        Assert.Equal("{\"frames\":[1]}", other.ReadText("Content/Art/wing.sprite.json"));
        Assert.NotNull(library.Find(package.Id, 1));
    }

    [Fact]
    public void RevisionsKeepOlderVersionsImmutableAndAllowIndependentImports()
    {
        var source = CreateSpriteProject("Origin", "primeira versão");
        var library = Library;
        var first = library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Autor", "CC-BY");
        source.WriteText("Content/Art/wing.sprite.json", "segunda versão");
        var next = library.Publish(source, "Content/Art/wing.sprite.json",
            "Asas", "Autor", "CC-BY", existingAssetId: first.Id);

        Assert.Equal(first.Id, next.Id);
        Assert.Equal(2, next.Version);
        Assert.Equal(2, library.List().Count);
        var oldGame = Projects.Create("Old");
        var newGame = Projects.Create("New");
        library.ImportToProject(first.Id, 1, oldGame, TestContext.Current.CancellationToken);
        library.ImportToProject(first.Id, 2, newGame, TestContext.Current.CancellationToken);
        Assert.Equal("primeira versão", oldGame.ReadText("Content/Art/wing.sprite.json"));
        Assert.Equal("segunda versão", newGame.ReadText("Content/Art/wing.sprite.json"));
    }

    [Fact]
    public void CollisionInOneFile_RejectsWholeImportWithoutTouchingOthers()
    {
        var source = CreateSpriteProject("Source");
        var library = Library;
        var asset = library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        var destination = Projects.Create("Destination");
        destination.WriteText("Content/Art/wing.sprite.json", "edição pessoal");

        Assert.Throws<IOException>(() => library.ImportToProject(asset.Id, 1, destination, TestContext.Current.CancellationToken));
        Assert.Equal("edição pessoal", destination.ReadText("Content/Art/wing.sprite.json"));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.png")));
    }

    [Fact]
    public void CorruptedStoredBytes_RefuseImportAndLeaveTargetUntouched()
    {
        var source = CreateSpriteProject("Source");
        var library = Library;
        var asset = library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        var png = Path.Combine(LibraryRoot, asset.Id, "v1", "files", "Content", "Art", "wing.png");
        File.WriteAllBytes(png, [99, 98, 97]);

        var destination = Projects.Create("Target");
        Assert.Throws<InvalidDataException>(() => library.ImportToProject(asset.Id, 1, destination, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.png")));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.sprite.json")));
        Assert.True(File.Exists(png)); // preserved for repair, never silently deleted.
    }

    [Fact]
    public void UntrustedManifestTraversal_FailsClosedWithoutTouchingProjects()
    {
        var source = CreateSpriteProject("Origin");
        var library = Library;
        var asset = library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        var manifest = Path.Combine(LibraryRoot, asset.Id, "v1", "asset.json");
        var text = File.ReadAllText(manifest);
        File.WriteAllText(manifest, text.Replace("Content/Art/wing.sprite.json", "../../Game.cs"));

        var target = Projects.Create("Target");
        var before = target.ReadText("Game.cs");
        Assert.Throws<InvalidDataException>(() => library.ImportToProject(asset.Id, 1, target, TestContext.Current.CancellationToken));
        Assert.Equal(before, target.ReadText("Game.cs"));
        Assert.Contains("../../Game.cs", File.ReadAllText(manifest));
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Files")]
    [InlineData("FilePath")]
    [InlineData("FileHash")]
    public void NullManifestFields_FailClosedWithoutChangingProjects(string field)
    {
        var source = CreateSpriteProject("Source");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        var manifestPath = Path.Combine(LibraryRoot, asset.Id, "v1", "asset.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        switch (field)
        {
            case "Id": document["Id"] = null; break;
            case "Files": document["Files"] = null; break;
            case "FilePath": document["Files"]![0]!["Path"] = null; break;
            case "FileHash": document["Files"]![0]!["Sha256"] = null; break;
        }
        var malformed = document.ToJsonString();
        File.WriteAllText(manifestPath, malformed);
        var destination = Projects.Create("Destination");
        var originalGame = destination.ReadText("Game.cs");

        Assert.Throws<InvalidDataException>(() => Library.ImportToProject(asset.Id, 1, destination, TestContext.Current.CancellationToken));
        Assert.Equal(originalGame, destination.ReadText("Game.cs"));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.png")));
        Assert.Equal(malformed, File.ReadAllText(manifestPath));
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("License")]
    [InlineData("Author")]
    [InlineData("Attribution")]
    [InlineData("Tags")]
    [InlineData("TagItem")]
    [InlineData("InvalidHash")]
    public void InvalidPersistedMetadata_CannotCrashLibraryOrModifyTarget(string field)
    {
        var source = CreateSpriteProject("Source");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        var manifestPath = Path.Combine(LibraryRoot, asset.Id, "v1", "asset.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        switch (field)
        {
            case "TagItem":
                document["Tags"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null);
                break;
            case "InvalidHash":
                document["Files"]![0]!["Sha256"] = new string('z', 64);
                break;
            default:
                document[field] = null;
                break;
        }
        var original = document.ToJsonString();
        File.WriteAllText(manifestPath, original);
        var target = Projects.Create("Target");
        var originalGame = target.ReadText("Game.cs");

        Assert.Throws<InvalidDataException>(() => Library.List());
        Assert.Throws<InvalidDataException>(() => Library.ImportToProject(asset.Id, 1, target, TestContext.Current.CancellationToken));
        Assert.Equal(originalGame, target.ReadText("Game.cs"));
        Assert.False(File.Exists(Path.Combine(target.Directory, "Content/Art/wing.sprite.json")));
        Assert.Equal(original, File.ReadAllText(manifestPath));
    }

    [Fact]
    public void ExportZip_ContainsManifestAndImmutableSourceAndDerivedFiles()
    {
        var source = CreateSpriteProject("Source");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        var zip = Path.Combine(_root, "export.zip");
        Library.ExportZip(asset.Id, asset.Version, zip);
        using var archive = ZipFile.OpenRead(zip);
        Assert.NotNull(archive.GetEntry("asset.json"));
        Assert.NotNull(archive.GetEntry("files/Content/Art/wing.sprite.json"));
        Assert.NotNull(archive.GetEntry("files/Content/Art/wing.png"));
        using var reader = new StreamReader(archive.GetEntry("files/Content/Art/wing.sprite.json")!.Open());
        Assert.Equal(source.ReadText("Content/Art/wing.sprite.json"), reader.ReadToEnd());
        Assert.Throws<IOException>(() => Library.ExportZip(asset.Id, asset.Version, zip));
    }

    [Fact]
    public void ExportZip_ArchivedBytesMatchManifestHashesAndLengths()
    {
        var source = CreateSpriteProject("Source");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Asas", "Autor", "CC-BY");
        var zip = Path.Combine(_root, "verified.zip");
        Library.ExportZip(asset.Id, 1, zip);

        using var archive = ZipFile.OpenRead(zip);
        foreach (var file in asset.Files)
        {
            var entry = archive.GetEntry("files/" + file.Path);
            Assert.NotNull(entry);
            using var input = entry!.Open();
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            Assert.Equal(file.Length, bytes.Length);
            Assert.Equal(file.Sha256, Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes.ToArray())));
        }
    }

    [Fact]
    public void ExportZip_RejectsCorruptedBytesWithoutCreatingDestination()
    {
        var source = CreateSpriteProject("Source");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        File.WriteAllText(Path.Combine(LibraryRoot, asset.Id, "v1", "files", "Content", "Art",
            "wing.sprite.json"), "tampered");
        var zip = Path.Combine(_root, "export.zip");
        Assert.Throws<InvalidDataException>(() => Library.ExportZip(asset.Id, 1, zip));
        Assert.False(File.Exists(zip));
    }

    [Fact]
    public void ExportZip_UsesRequestedVersionAndPreservesLicenseMetadata()
    {
        var source = CreateSpriteProject("Source", "primeira");
        var first = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Asas", "Artista", "CC-BY", attribution: "Crédito obrigatório");
        source.WriteText("Content/Art/wing.sprite.json", "segunda");
        var second = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Asas", "Artista", "CC-BY", attribution: "Crédito obrigatório",
            existingAssetId: first.Id);

        var oldZip = Path.Combine(_root, "old.zip");
        var newZip = Path.Combine(_root, "new.zip");
        Library.ExportZip(first.Id, first.Version, oldZip);
        Library.ExportZip(second.Id, second.Version, newZip);
        using var oldArchive = ZipFile.OpenRead(oldZip);
        using var newArchive = ZipFile.OpenRead(newZip);
        static string Read(System.IO.Compression.ZipArchive archive, string path)
        {
            using var reader = new StreamReader(archive.GetEntry(path)!.Open());
            return reader.ReadToEnd();
        }
        Assert.Equal("primeira", Read(oldArchive, "files/Content/Art/wing.sprite.json"));
        Assert.Equal("segunda", Read(newArchive, "files/Content/Art/wing.sprite.json"));
        Assert.Contains("CC-BY", Read(oldArchive, "asset.json"));
        Assert.Equal("Crédito obrigatório", System.Text.Json.JsonDocument.Parse(Read(newArchive, "asset.json")).RootElement.GetProperty("Attribution").GetString());
    }

    [Fact]
    public void Search_FiltersAcrossMetadataAndTagsWithoutChangingSavedVersions()
    {
        var project = CreateSpriteProject("SearchSource");
        var library = Library;
        var first = library.Publish(project, "Content/Art/wing.sprite.json",
            "Asas", "Ana", "CC-BY", "Crédito obrigatório", tags: ["voo", "pixel"]);
        project.WriteText("Content/Art/wing.sprite.json", "versão nova");
        library.Publish(project, "Content/Art/wing.sprite.json",
            "Asas", "Ana", "CC-BY", "Crédito obrigatório", tags: ["voo", "pixel"],
            existingAssetId: first.Id);

        Assert.Equal(2, library.Search("voo ana").Count);
        Assert.Equal(2, library.Search("CC-BY pixel").Count);
        Assert.Empty(library.Search("voo licença-inexistente"));
        Assert.Equal(2, library.Search(null).Count);
        Assert.Equal(2, new ReusableAssetLibrary(LibraryRoot).Search("asas").Count);
    }

    [Fact]
    public void Search_MatchesAccentsCaseAndDecomposedUnicodeAcrossFields()
    {
        var source = CreateSpriteProject("SearchUnicode");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Dragão Ágil", "João", "CC-BY", "Crédito obrigatório",
            tags: ["Animação", "Vôo"]);
        var library = new ReusableAssetLibrary(LibraryRoot);

        Assert.Equal(asset.Id, Assert.Single(library.Search("DRAGAO joao")).Id);
        Assert.Equal(asset.Id, Assert.Single(library.Search("credito animacao")).Id);
        Assert.Equal(asset.Id, Assert.Single(library.Search("draga\u0303o voO")).Id);
        Assert.Empty(library.Search("dragao inexistente"));
        Assert.Equal("Dragão Ágil", library.Find(asset.Id, 1).Title);
        Assert.Equal("Crédito obrigatório", library.Find(asset.Id, 1).Attribution);
    }

    [Fact]
    public void Search_FindsSourceAndDerivedFilenamesAfterRestart()
    {
        var source = CreateSpriteProject("FilenameSearch");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Flying wings", "Artist", "CC0");
        var reopened = new ReusableAssetLibrary(LibraryRoot);

        Assert.Equal(asset.Id, Assert.Single(reopened.Search("wing.sprite.json")).Id);
        Assert.Equal(asset.Id, Assert.Single(reopened.Search("content/art/wing.png")).Id);
        Assert.Equal(asset.Id, Assert.Single(reopened.Search("flying wing.png")).Id);
        Assert.Empty(reopened.Search("unrelated.png"));
        Assert.Equal("Flying wings", reopened.Find(asset.Id, 1).Title);
    }

    [Fact]
    public void PublishedTagsRemainSearchableAfterLibraryRestart()
    {
        var project = CreateSpriteProject("TaggedSource");
        var published = Library.Publish(project, "Content/Art/wing.sprite.json",
            "Asas", "Ana", "CC0", tags: ["Montaria", "Voo livre"]);
        var reopened = new ReusableAssetLibrary(LibraryRoot);
        var match = Assert.Single(reopened.Search("montaria voo"));
        Assert.Equal(published.Id, match.Id);
        Assert.Contains("Voo livre", match.Tags);
        Assert.Empty(reopened.Search("montaria submarino"));
    }

    [Fact]
    public void VerifyIntegrity_ReportsCorruptionWithoutDeletingStoredVersion()
    {
        var source = CreateSpriteProject("VerifySource");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Asas", "Eu", "CC0");
        Library.VerifyIntegrity(asset.Id, asset.Version);
        var file = Path.Combine(LibraryRoot, asset.Id, "v1", "files",
            "Content", "Art", "wing.png");
        File.WriteAllBytes(file, [1, 2, 3]);
        Assert.Throws<InvalidDataException>(() => Library.VerifyIntegrity(asset.Id, asset.Version));
        Assert.True(File.Exists(file));
        Assert.Equal(asset.Id, Library.Find(asset.Id, asset.Version).Id);
    }

    [Theory]
    [InlineData("Kind", "99999")]
    [InlineData("CreatedUtc", "0001-01-01T00:00:00")]
    public void InvalidManifestIdentityMetadata_RejectsWithoutChangingLibraryOrProject(string field, string value)
    {
        var source = CreateSpriteProject("MetadataSource");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json", "Asas", "Eu", "CC0");
        var manifestPath = Path.Combine(LibraryRoot, asset.Id, "v1", "asset.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        if (field == "Kind")
            document[field] = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        else
            document[field] = value;
        var malformed = document.ToJsonString();
        File.WriteAllText(manifestPath, malformed);
        var target = Projects.Create("MetadataTarget");
        var gameBefore = target.ReadText("Game.cs");

        Assert.Throws<InvalidDataException>(() => Library.Find(asset.Id, asset.Version));
        Assert.Throws<InvalidDataException>(() => Library.ImportToProject(asset.Id, asset.Version, target, TestContext.Current.CancellationToken));
        Assert.Equal(gameBefore, target.ReadText("Game.cs"));
        Assert.Equal(malformed, File.ReadAllText(manifestPath));
    }

    [Theory]
    [InlineData("Content/Art/WING.PNG")]
    [InlineData("Content/ART/other.png")]
    public void ImportRejectsCaseInsensitiveFileAndDirectoryCollisions(string conflictingPath)
    {
        var source = CreateSpriteProject("CaseSource");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Asas", "Eu", "CC0");
        var destination = Projects.Create("CaseTarget");
        destination.WriteText(conflictingPath, "personal data");
        var originalGame = destination.ReadText("Game.cs");

        var conflicts = Library.FindImportConflicts(asset.Id, 1, destination);
        Assert.NotEmpty(conflicts);
        Assert.Throws<IOException>(() => Library.ImportToProject(asset.Id, 1, destination, TestContext.Current.CancellationToken));
        Assert.Equal("personal data", destination.ReadText(conflictingPath));
        Assert.Equal(originalGame, destination.ReadText("Game.cs"));
        Assert.False(File.Exists(Path.Combine(destination.Directory, "Content/Art/wing.sprite.json")));
    }

    [Theory]
    [InlineData("Content/Art")]
    [InlineData("content/art")]
    public void ManifestWithFileAsAncestor_IsRejectedBeforeImportOrExport(string ancestor)
    {
        var source = CreateSpriteProject("AncestorManifestSource");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Wings", "Author", "CC0");
        var manifestPath = Path.Combine(LibraryRoot, asset.Id, "v1", "asset.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        var files = document["Files"]!.AsArray();
        var existing = files[0]!.DeepClone();
        existing["Path"] = ancestor;
        files.Add(existing);
        var malformed = document.ToJsonString();
        File.WriteAllText(manifestPath, malformed);
        var target = Projects.Create("AncestorManifestTarget");
        var originalGame = target.ReadText("Game.cs");
        var zip = Path.Combine(_root, "invalid-ancestor.zip");

        Assert.Throws<InvalidDataException>(() => Library.Find(asset.Id, 1));
        Assert.Throws<InvalidDataException>(() => Library.ImportToProject(asset.Id, 1, target, TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(() => Library.ExportZip(asset.Id, 1, zip));
        Assert.Equal(originalGame, target.ReadText("Game.cs"));
        Assert.False(File.Exists(zip));
        Assert.Equal(malformed, File.ReadAllText(manifestPath));
    }

    [Fact]
    public void UnsupportedPathCannotBecomeAReusableAsset()
    {
        var source = CreateSpriteProject("Origin");
        var library = Library;
        Assert.Throws<InvalidDataException>(() => library.Publish(source, "../Game.cs", "Bad", "Eu", "CC0"));
        Assert.Throws<InvalidDataException>(() => library.Publish(source, "Game.cs", "Bad", "Eu", "CC0"));
        Assert.Empty(library.List());
    }
    [Fact]
    public void LowercaseManifestSha256_ImportsAndExportsWithoutChangingStoredMetadata()
    {
        var source = CreateSpriteProject("LowercaseSource");
        var asset = Library.Publish(source, "Content/Art/wing.sprite.json",
            "Wings", "Author", "CC0");
        var manifestPath = Path.Combine(LibraryRoot, asset.Id, "v1", "asset.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!;
        foreach (var file in document["Files"]!.AsArray())
            file!["Sha256"] = file["Sha256"]!.GetValue<string>().ToLowerInvariant();
        var lowercaseManifest = document.ToJsonString();
        File.WriteAllText(manifestPath, lowercaseManifest);

        Library.VerifyIntegrity(asset.Id, 1);
        var destination = Projects.Create("LowercaseTarget");
        Assert.Equal(2, Library.ImportToProject(asset.Id, 1, destination, TestContext.Current.CancellationToken).Count);
        Assert.Equal(source.ReadText("Content/Art/wing.sprite.json"),
            destination.ReadText("Content/Art/wing.sprite.json"));
        Assert.Equal(File.ReadAllBytes(Path.Combine(source.Directory, "Content", "Art", "wing.png")),
            File.ReadAllBytes(Path.Combine(destination.Directory, "Content", "Art", "wing.png")));
        var zip = Path.Combine(_root, "lowercase.zip");
        Library.ExportZip(asset.Id, 1, zip);
        using (var archive = ZipFile.OpenRead(zip))
            Assert.Equal(3, archive.Entries.Count);
        Assert.Equal(lowercaseManifest, File.ReadAllText(manifestPath));
    }

}
