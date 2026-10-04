using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class VaultReaderTests
{
    private static readonly string Root = FindRoot();

    public static TheoryData<string> FixtureIds => new()
    {
        "futuro-desconhecido",
        "futuro-v2",
        "v1-cidades-mescladas",
        "v1-journal-pendente",
        "v1-mapa-v2",
        "v1-mapa-v4",
        "v1-mundo-antigo",
        "v1-notas-sem-id",
        "v1-orfaos",
        "v1-paginas",
        "v1-personalizacao",
        "vault-futuro"
    };

    [Theory]
    [MemberData(nameof(FixtureIds))]
    public void ReadsCanonicalFixtureWithoutMutatingPhysicalBytes(string fixtureId)
    {
        var fixture = Path.Combine(Root, "..", "tests", "fixtures", "vaults", fixtureId);
        using var expectation = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture, "expect.json")));

        var inputs = Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "expect.json", StringComparison.Ordinal))
            .Select(path => new VaultFile(Relative(fixture, path), File.ReadAllBytes(path)))
            .ToArray();

        var physicalHashes = inputs.ToDictionary(
            file => file.Path,
            file => Hash(file.Bytes.Span),
            StringComparer.OrdinalIgnoreCase);

        var snapshot = VaultReader.Read(inputs);
        var expected = expectation.RootElement;

        Assert.Equal(
            ExpectedStrings(expected, "notes").Order(StringComparer.Ordinal),
            snapshot.Documents.Select(document => document.Path).Order(StringComparer.Ordinal));

        if (expected.TryGetProperty("ids", out var ids))
        {
            foreach (var property in ids.EnumerateObject())
            {
                var document = Assert.Single(snapshot.Documents, item =>
                    string.Equals(item.Path, property.Name, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(property.Value.GetString(), document.Id);
                Assert.False(document.IdGenerated);
            }
        }

        if (expected.TryGetProperty("positions", out var positions))
        {
            foreach (var property in positions.EnumerateObject())
            {
                var document = Assert.Single(snapshot.Documents, item =>
                    string.Equals(item.Path, property.Name, StringComparison.OrdinalIgnoreCase));
                var pair = property.Value.EnumerateArray().Select(item => item.GetDouble()).ToArray();
                Assert.NotNull(document.X);
                Assert.NotNull(document.Y);
                Assert.Equal(pair[0], document.X.Value);
                Assert.Equal(pair[1], document.Y.Value);
            }
        }

        if (expected.TryGetProperty("generatedIds", out var generated) && generated.GetBoolean())
        {
            Assert.All(snapshot.Documents, document =>
            {
                Assert.True(document.IdGenerated);
                Assert.StartsWith("doc_", document.Id);
            });
            Assert.Equal(snapshot.Documents.Count, snapshot.Documents.Select(document => document.Id).Distinct().Count());
        }

        if (expected.TryGetProperty("readOnly", out var readOnly))
            Assert.Equal(readOnly.GetBoolean(), snapshot.IsReadOnly);

        if (expected.TryGetProperty("futureMapa", out var futureMap))
            Assert.Equal(futureMap.GetBoolean(), snapshot.IsMapReadOnly);

        if (expected.TryGetProperty("recovered", out var recovered))
        {
            Assert.Equal(recovered.GetBoolean(), snapshot.RecoveredFromJournal);
            if (recovered.GetBoolean())
            {
                Assert.Equal(".urbe/journal.json", snapshot.RecoveryJournalPath);
                Assert.True(snapshot.Files.ContainsKey(".urbe/journal.json"));
            }
        }

        if (expected.TryGetProperty("vaultCurrent", out var vaultCurrent) && vaultCurrent.GetBoolean())
            Assert.Equal(VaultFormatState.Current, snapshot.FormatState);

        if (expected.TryGetProperty("mundo", out var mundo))
            Assert.Equal(mundo.GetString(), snapshot.Mundo);

        if (expected.TryGetProperty("trash", out var trash))
            Assert.Equal(trash.GetInt32(), snapshot.TrashCount);

        if (expected.TryGetProperty("history", out var history))
            Assert.Equal(history.GetInt32(), snapshot.HistoryCount);

        if (expected.TryGetProperty("compositions", out var compositions))
            Assert.Equal(compositions.GetInt32(), snapshot.CompositionCount);

        if (expected.TryGetProperty("futureFiles", out var futureFiles))
        {
            Assert.Equal(
                futureFiles.EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal),
                snapshot.FutureFiles.Order(StringComparer.Ordinal));
        }

        if (expected.TryGetProperty("contents", out var contents))
        {
            foreach (var property in contents.EnumerateObject())
            {
                var document = Assert.Single(snapshot.Documents, item =>
                    string.Equals(item.Path, property.Name, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(property.Value.GetString(), document.Text);
            }
        }

        AssertHashes(expected, "noteHashes", snapshot, preferDocument: false);
        AssertHashes(expected, "futureHashes", snapshot, preferDocument: false);
        AssertHashes(expected, "keepHashes", snapshot, preferDocument: false);

        Assert.Equal(physicalHashes.Count, snapshot.Files.Count);
        foreach (var pair in physicalHashes)
        {
            Assert.True(snapshot.Files.TryGetValue(pair.Key, out var current));
            Assert.Equal(pair.Value, Hash(current.Bytes.Span));
        }
    }

    [Fact]
    public void LegacyDocumentWithoutPersistedIdentityGetsFreshIdOnEachRead()
    {
        var first = VaultReader.Read([Text("A.md", "A")]);
        var second = VaultReader.Read([Text("A.md", "A")]);

        var firstDocument = Assert.Single(first.Documents);
        var secondDocument = Assert.Single(second.Documents);
        Assert.True(firstDocument.IdGenerated);
        Assert.True(secondDocument.IdGenerated);
        Assert.NotEqual(firstDocument.Id, secondDocument.Id);
        Assert.StartsWith("doc_", firstDocument.Id);
        Assert.StartsWith("doc_", secondDocument.Id);
    }

    [Fact]
    public void V2SidecarTakesPrecedenceOverLegacyV1()
    {
        var snapshot = VaultReader.Read(
        [
            Text(".urbe/history.json", """{"version":1,"documents":{"legacy":[]}}"""),
            Text(".urbe/history.v2.json", """{"version":2,"documents":{"first":[],"second":[]}}"""),
            Text("A.md", "A")
        ]);

        Assert.Equal(2, snapshot.HistoryCount);
        Assert.DoesNotContain(".urbe/history.json", snapshot.FutureFiles);
        Assert.DoesNotContain(".urbe/history.v2.json", snapshot.FutureFiles);
    }

    [Fact]
    public void FutureV2SidecarDoesNotFallBackToLegacy()
    {
        var snapshot = VaultReader.Read(
        [
            Text(".urbe/history.json", """{"version":1,"documents":{"legacy":[]}}"""),
            Text(".urbe/history.v2.json", """{"version":3,"documents":{"future":[]}}"""),
            Text("A.md", "A")
        ]);

        Assert.Equal(0, snapshot.HistoryCount);
        Assert.Contains(".urbe/history.v2.json", snapshot.FutureFiles);
    }

    [Fact]
    public void RecoverableJournalProjectsEffectiveDocumentsButLeavesDiskUntouched()
    {
        var original = Encoding.UTF8.GetBytes("original");
        var snapshot = VaultReader.Read(
        [
            new VaultFile("A.md", original),
            Text("B.md", "B"),
            Text(".urbe/journal.json",
                """{"version":1,"documents":[{"id":"doc_fixed","path":"A.md","content":"recovered"},{"id":null,"path":"C.md","content":"C"}],"metadata":{"v":4,"notas":{"A.md":{"id":"doc_fixed","x":7,"y":9}}}}""")
        ]);

        Assert.Equal(["A.md", "C.md"], snapshot.Documents.Select(document => document.Path).ToArray());
        var a = Assert.Single(snapshot.Documents, document => document.Path == "A.md");
        Assert.Equal("recovered", a.Text);
        Assert.Equal("doc_fixed", a.Id);
        Assert.Equal(7d, a.X);
        Assert.Equal(9d, a.Y);
        Assert.Equal(Hash(original), Hash(snapshot.Files["A.md"].Bytes.Span));
        Assert.True(snapshot.Files.ContainsKey(".urbe/journal.json"));
        Assert.True(snapshot.RecoveredFromJournal);
    }

    [Fact]
    public void FutureJournalIsPreservedAndNeverProjected()
    {
        var snapshot = VaultReader.Read(
        [
            Text("A.md", "disk"),
            Text(".urbe/journal.v2.json",
                """{"version":3,"documents":[{"id":"doc_bad","path":"Injected.md","content":"future"}]}""")
        ]);

        Assert.Equal(["A.md"], snapshot.Documents.Select(document => document.Path).ToArray());
        Assert.False(snapshot.RecoveredFromJournal);
        Assert.Contains(".urbe/journal.v2.json", snapshot.FutureFiles);
    }

    [Fact]
    public void FutureVaultMakesWholeSystemMetadataReadOnlyButStillReadsNotes()
    {
        var snapshot = VaultReader.Read(
        [
            Text(".urbe/vault.json", """{"formatVersion":3}"""),
            Text(".urbe/mapa.json", """{"v":4,"notas":{"A.md":{"id":"doc_a"}}}"""),
            Text("A.md", "A")
        ]);

        Assert.True(snapshot.IsReadOnly);
        Assert.Equal(VaultFormatState.Future, snapshot.FormatState);
        Assert.Contains(".urbe/vault.json", snapshot.FutureFiles);
        Assert.Contains(".urbe/mapa.json", snapshot.FutureFiles);
        Assert.Equal("doc_a", Assert.Single(snapshot.Documents).Id);
    }

    [Fact]
    public void FutureMapProtectsMapButCanProjectKnownReadFields()
    {
        var snapshot = VaultReader.Read(
        [
            Text(".urbe/mapa.json", """{"v":99,"mundo":"future","notas":{"A.md":{"id":"doc_a","x":12,"y":13}}}"""),
            Text("A.md", "A")
        ]);

        var document = Assert.Single(snapshot.Documents);
        Assert.True(snapshot.IsMapReadOnly);
        Assert.False(snapshot.IsReadOnly);
        Assert.Equal("doc_a", document.Id);
        Assert.Equal(12d, document.X);
        Assert.Equal(13d, document.Y);
        Assert.Equal("future", snapshot.Mundo);
        Assert.DoesNotContain(".urbe/mapa.json", snapshot.FutureFiles);
    }

    [Fact]
    public void CorruptSidecarIsPreservedInsteadOfFallingBack()
    {
        var corrupt = Encoding.UTF8.GetBytes("{not-json");
        var snapshot = VaultReader.Read(
        [
            Text(".urbe/trash.json", """{"version":1,"items":[{"id":"legacy"}]}"""),
            new VaultFile(".urbe/trash.v2.json", corrupt),
            Text("A.md", "A")
        ]);

        Assert.Equal(0, snapshot.TrashCount);
        Assert.Contains(".urbe/trash.v2.json", snapshot.FutureFiles);
        Assert.Equal(Hash(corrupt), Hash(snapshot.Files[".urbe/trash.v2.json"].Bytes.Span));
    }

    [Theory]
    [InlineData("../fora.md")]
    [InlineData("Pasta/../fora.md")]
    [InlineData("/absoluto.md")]
    [InlineData("Pasta//nota.md")]
    public void RejectsUnsafePhysicalPaths(string path)
    {
        Assert.Throws<InvalidDataException>(() => VaultReader.Read([Text(path, "x")]));
    }

    [Fact]
    public void RejectsDuplicatePathsAfterSeparatorNormalization()
    {
        Assert.Throws<InvalidDataException>(() => VaultReader.Read(
        [
            Text("Pasta/Nota.md", "a"),
            Text(@"Pasta\Nota.md", "b")
        ]));
    }

    private static void AssertHashes(
        JsonElement expected,
        string propertyName,
        VaultSnapshot snapshot,
        bool preferDocument)
    {
        if (!expected.TryGetProperty(propertyName, out var hashes))
            return;

        foreach (var property in hashes.EnumerateObject())
        {
            ReadOnlyMemory<byte> bytes;
            var document = preferDocument
                ? snapshot.Documents.FirstOrDefault(item =>
                    string.Equals(item.Path, property.Name, StringComparison.OrdinalIgnoreCase))
                : null;

            if (document is not null)
                bytes = document.Bytes;
            else
            {
                Assert.True(snapshot.Files.TryGetValue(property.Name, out var file),
                    $"Arquivo esperado ausente: {property.Name}");
                bytes = file.Bytes;
            }

            Assert.Equal(property.Value.GetString(), Hash(bytes.Span));
        }
    }

    private static string[] ExpectedStrings(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value)
            ? value.EnumerateArray().Select(item => item.GetString()!).ToArray()
            : [];

    private static VaultFile Text(string path, string value) =>
        new(path, Encoding.UTF8.GetBytes(value));

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Urbe.Portable.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Execute os testes dentro do checkout do Urbe.");
    }
}
