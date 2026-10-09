using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class VaultWriterTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void IdentityFingerprintMatchesHistoricalJavascriptContract()
    {
        Assert.Equal("28e4c71066812094:3", VaultIdentity.Fingerprint("a\r\nb  \n\n"));
        Assert.Equal(VaultIdentity.Fingerprint("a\nb"), VaultIdentity.Fingerprint("a\r\nb  \n\n"));
        Assert.Equal(string.Empty, VaultIdentity.Fingerprint("  \n\n"));
    }

    [Fact]
    public void FirstLegacyWriteCreatesRestorableBackupAndMigrationOnlyOnce()
    {
        var files = LoadFixture("v1-mapa-v4");
        var original = files.ToDictionary(file => file.Path, file => Hash(file.Bytes.Span), StringComparer.OrdinalIgnoreCase);
        var snapshot = VaultReader.Read(files);
        var documents = ToWriteDocuments(snapshot)
            .Select(document => document.Path == "Alfa.md"
                ? document with { Content = document.Content + "edição\n" }
                : document)
            .ToArray();
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = documents,
            MetadataJson = Text(files, ".urbe/mapa.json"),
            AppVersion = "1.8.3-beta",
            Now = now
        });

        Assert.True(result.Writable);
        Assert.True(result.Migrated);
        Assert.NotNull(result.BackupDirectory);
        Assert.StartsWith(".urbe/backup/20261004-120000-1-2", result.BackupDirectory);

        using var vault = JsonDocument.Parse(result.Files[".urbe/vault.json"].Bytes);
        Assert.Equal(2, vault.RootElement.GetProperty("formatVersion").GetInt32());
        var migrations = vault.RootElement.GetProperty("migrations");
        Assert.Equal(1, migrations.GetArrayLength());
        Assert.Equal(1, migrations[0].GetProperty("from").GetInt32());
        Assert.Equal(2, migrations[0].GetProperty("to").GetInt32());
        Assert.Equal(result.BackupDirectory, migrations[0].GetProperty("backup").GetString());
        Assert.Equal("2026-10-04T12:00:00.000Z", migrations[0].GetProperty("at").GetString());
        Assert.Equal("2026-10-04T12:00:00.000Z", vault.RootElement.GetProperty("created").GetString());

        foreach (var path in new[]
                 {
                     ".urbe/history.json",
                     ".urbe/trash.json",
                     ".urbe/compositions.json"
                 })
            Assert.Equal(original[path], Hash(result.Files[path].Bytes.Span));

        foreach (var path in new[]
                 {
                     ".urbe/history.v2.json",
                     ".urbe/trash.v2.json",
                     ".urbe/compositions.v2.json",
                     ".urbe/identity.json"
                 })
            Assert.True(result.Files.ContainsKey(path), path);

        var manifestPath = result.BackupDirectory + "/manifest.json";
        using var manifest = JsonDocument.Parse(result.Files[manifestPath].Bytes);
        var backed = manifest.RootElement.GetProperty("files")
            .EnumerateArray()
            .Select(item => item.GetProperty("path").GetString())
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            new[]
            {
                ".urbe/compositions.json",
                ".urbe/history.json",
                ".urbe/mapa.json",
                ".urbe/trash.json"
            },
            backed);

        foreach (var item in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = item.GetProperty("path").GetString()!;
            var stored = result.BackupDirectory + "/files/" +
                         (path.StartsWith(".urbe/", StringComparison.Ordinal)
                             ? "urbe/" + path[6..]
                             : path);
            Assert.True(result.Files.ContainsKey(stored));
            Assert.Equal(original[path], Hash(result.Files[stored].Bytes.Span));
            Assert.Equal(original[path], item.GetProperty("sha256").GetString());
        }

        var second = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = result.Files.Values,
            Documents = ToWriteDocuments(VaultReader.Read(result.Files.Values)),
            AppVersion = "1.8.3-beta",
            Now = now.AddHours(1)
        });

        Assert.False(second.Migrated);
        Assert.Null(second.BackupDirectory);
        Assert.Equal(
            1,
            second.Files.Keys.Count(path =>
                path.StartsWith(".urbe/backup/", StringComparison.Ordinal) &&
                path.EndsWith("/manifest.json", StringComparison.Ordinal)));

        using var vaultAgain = JsonDocument.Parse(second.Files[".urbe/vault.json"].Bytes);
        Assert.Equal(1, vaultAgain.RootElement.GetProperty("migrations").GetArrayLength());

        var corrupted = second.Files.Values
            .Where(file => !file.Path.Equals(".urbe/history.json", StringComparison.OrdinalIgnoreCase))
            .Select(file => new VaultFile(file.Path, file.Bytes.ToArray()))
            .Append(new VaultFile(".urbe/history.json", Encoding.UTF8.GetBytes("CORROMPIDO")))
            .ToArray();

        var restored = VaultWriter.RestoreBackup(corrupted, result.BackupDirectory!);
        Assert.Equal(original[".urbe/history.json"], Hash(restored.Files[".urbe/history.json"].Bytes.Span));
    }

    [Fact]
    public void RecoveryCommitsJournalMetadataAndSidecarsBeforeRemovingJournal()
    {
        var journal = """
        {
          "version": 2,
          "timestamp": 1,
          "documents": [
            {"id":"doc_a","path":"A.md","content":"recuperado\n"},
            {"id":"doc_b","path":"B.md","content":"novo\n"}
          ],
          "metadata": {"v":4,"mundo":"journal","notas":{"A.md":{"id":"doc_a"},"B.md":{"id":"doc_b"}}},
          "history": {"version":1,"documents":{"doc_a":[{"timestamp":1,"path":"A.md","content":"antigo"}]}},
          "trash": {"version":1,"items":[]},
          "compositions": {"version":1,"items":[{"id":"cmp_1","name":"J","sources":["doc_a","doc_b"]}]}
        }
        """;
        var files = new[]
        {
            TextFile("A.md", "disco\n"),
            TextFile(".urbe/mapa.json", """{"v":4,"mundo":"disco","notas":{"A.md":{"id":"doc_a"}}}"""),
            TextFile(".urbe/journal.v2.json", journal),
            TextFile(".urbe/history.json", """{"version":1,"documents":{}}""")
        };
        var snapshot = VaultReader.Read(files);
        Assert.True(snapshot.RecoveredFromJournal);

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = ToWriteDocuments(snapshot),
            AppVersion = "1.8.3-beta",
            Now = new DateTimeOffset(2026, 10, 4, 12, 30, 0, TimeSpan.Zero)
        });

        Assert.False(result.Files.ContainsKey(".urbe/journal.v2.json"));
        Assert.Equal("recuperado\n", Encoding.UTF8.GetString(result.Files["A.md"].Bytes.Span));
        Assert.Equal("novo\n", Encoding.UTF8.GetString(result.Files["B.md"].Bytes.Span));

        using var map = JsonDocument.Parse(result.Files[".urbe/mapa.json"].Bytes);
        Assert.Equal("journal", map.RootElement.GetProperty("mundo").GetString());

        using var history = JsonDocument.Parse(result.Files[".urbe/history.v2.json"].Bytes);
        Assert.True(history.RootElement.GetProperty("documents").TryGetProperty("doc_a", out _));

        using var compositions = JsonDocument.Parse(result.Files[".urbe/compositions.v2.json"].Bytes);
        Assert.Equal("cmp_1", compositions.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void FutureV2JournalDoesNotHideSupportedLegacyJournal()
    {
        var files = new[]
        {
            TextFile("A.md", "disco\n"),
            TextFile(".urbe/journal.v2.json", """{"version":9,"documents":[{"id":"bad","path":"Bad.md","content":"bad"}]}"""),
            TextFile(".urbe/journal.json", """{"version":1,"documents":[{"id":"doc_a","path":"A.md","content":"v1\n"}]}""")
        };

        var snapshot = VaultReader.Read(files);
        Assert.True(snapshot.RecoveredFromJournal);
        Assert.Equal(".urbe/journal.json", snapshot.RecoveryJournalPath);
        Assert.Equal("v1\n", Assert.Single(snapshot.Documents).Text);
        Assert.Contains(".urbe/journal.v2.json", snapshot.FutureFiles);

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = ToWriteDocuments(snapshot),
            AppVersion = "1.8.3-beta"
        });

        Assert.True(result.Files.ContainsKey(".urbe/journal.v2.json"));
        Assert.False(result.Files.ContainsKey(".urbe/journal.json"));
    }

    [Fact]
    public void FutureArtifactsStayByteIdenticalWhileOrdinaryNotesCanBeWritten()
    {
        var files = LoadFixture("futuro-v2");
        var snapshot = VaultReader.Read(files);
        var protectedHashes = snapshot.FutureFiles.ToDictionary(
            path => path,
            path => Hash(snapshot.Files[path].Bytes.Span),
            StringComparer.OrdinalIgnoreCase);
        var documents = ToWriteDocuments(snapshot)
            .Select(document => document with { Content = document.Content + "\neditada" })
            .ToArray();

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = documents,
            MetadataJson = Text(files, ".urbe/mapa.json"),
            AppVersion = "1.8.3-beta",
            Now = new DateTimeOffset(2026, 10, 4, 13, 0, 0, TimeSpan.Zero)
        });

        Assert.True(result.Writable);
        Assert.False(result.JournalUsed);
        foreach (var pair in protectedHashes)
            Assert.Equal(pair.Value, Hash(result.Files[pair.Key].Bytes.Span));

        Assert.Contains("editada", Encoding.UTF8.GetString(result.Files["Alfa.md"].Bytes.Span));
    }

    [Fact]
    public void FutureVaultRejectsMutationWithoutChangingAnyByte()
    {
        var files = LoadFixture("vault-futuro");
        var before = files.ToDictionary(file => file.Path, file => Hash(file.Bytes.Span), StringComparer.OrdinalIgnoreCase);
        var snapshot = VaultReader.Read(files);
        var documents = ToWriteDocuments(snapshot)
            .Select(document => document with { Content = "tentativa\n" })
            .ToArray();

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = documents,
            AppVersion = "1.8.3-beta"
        });

        Assert.False(result.Writable);
        Assert.Empty(result.Operations);
        Assert.Equal(before.Count, result.Files.Count);
        foreach (var pair in before)
            Assert.Equal(pair.Value, Hash(result.Files[pair.Key].Bytes.Span));
    }

    [Fact]
    public void MultiDocumentMutationUsesTransientJournalBeforeWrites()
    {
        var files = new[]
        {
            TextFile("A.md", "a\n"),
            TextFile("B.md", "b\n")
        };
        var initial = VaultReader.Read(files);
        var documents = ToWriteDocuments(initial)
            .Select(document => document with { Content = document.Content.ToUpperInvariant() })
            .ToArray();

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = documents,
            AppVersion = "1.8.3-beta",
            Now = new DateTimeOffset(2026, 10, 4, 14, 0, 0, TimeSpan.Zero)
        });

        Assert.True(result.JournalUsed);
        var journalWrite = result.Operations
            .Select((operation, index) => (operation, index))
            .Single(item => item.operation.Kind == VaultOperationKind.Write &&
                            item.operation.Path == ".urbe/journal.v2.json").index;
        var firstDocumentWrite = result.Operations
            .Select((operation, index) => (operation, index))
            .Where(item => item.operation.Kind == VaultOperationKind.Write &&
                           (item.operation.Path == "A.md" || item.operation.Path == "B.md"))
            .Min(item => item.index);
        var journalRemove = result.Operations
            .Select((operation, index) => (operation, index))
            .Single(item => item.operation.Kind == VaultOperationKind.Remove &&
                            item.operation.Path == ".urbe/journal.v2.json").index;

        Assert.True(journalWrite < firstDocumentWrite);
        Assert.True(journalRemove > firstDocumentWrite);
        Assert.False(result.Files.ContainsKey(".urbe/journal.v2.json"));
    }

    [Fact]
    public void IdentitySeenIsStableUntilPathOrContentChanges()
    {
        var now = new DateTimeOffset(2026, 10, 4, 15, 0, 0, TimeSpan.Zero);
        var document = new VaultWriteDocument("doc_fixed", "A.md", "A\n");

        var first = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = Array.Empty<VaultFile>(),
            Documents = [document],
            AppVersion = "1.8.3-beta",
            Now = now
        });
        var firstIdentity = VaultIdentity.Parse(first.Files[".urbe/identity.json"].Bytes, out var firstFuture);
        Assert.False(firstFuture);
        Assert.NotNull(firstIdentity);
        var seen = firstIdentity!.Documents["doc_fixed"].Seen;
        Assert.Equal("2026-10-04T15:00:00.000Z", seen);

        var second = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = first.Files.Values,
            Documents = [document],
            AppVersion = "1.8.3-beta",
            Now = now.AddDays(1)
        });
        var secondIdentity = VaultIdentity.Parse(second.Files[".urbe/identity.json"].Bytes, out var secondFuture);
        Assert.False(secondFuture);
        Assert.Equal(seen, secondIdentity!.Documents["doc_fixed"].Seen);

        var third = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = second.Files.Values,
            Documents = [document with { Content = "B\n" }],
            AppVersion = "1.8.3-beta",
            Now = now.AddDays(2)
        });
        var thirdIdentity = VaultIdentity.Parse(third.Files[".urbe/identity.json"].Bytes, out _);
        Assert.NotEqual(seen, thirdIdentity!.Documents["doc_fixed"].Seen);
    }

    [Fact]
    public void GarbageCollectorMatchesLegacyFixtureAndIsIdempotent()
    {
        var files = LoadFixture("v1-orfaos");
        var originals = files.ToDictionary(file => file.Path, file => Hash(file.Bytes.Span), StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.Parse("2026-09-30T00:00:00Z").ToUnixTimeMilliseconds());

        var dry = VaultGarbageCollector.Run(files, now);
        Assert.True(dry.DryRun);
        Assert.Equal(1, dry.Plan.HistoryCount);
        Assert.Equal("doc_00000007-0000-4000-8000-000000000000", Assert.Single(dry.Plan.History).Id);
        Assert.Equal(0, dry.Plan.TrashCount);
        Assert.Equal(1, dry.Plan.CompositionReferenceCount);
        Assert.Null(dry.Write);

        var applied = VaultGarbageCollector.Run(files, now, dryRun: false, appVersion: "1.8.3-beta");
        Assert.False(applied.DryRun);
        Assert.NotNull(applied.Write);
        var result = applied.Write!;

        using var history = JsonDocument.Parse(result.Files[".urbe/history.v2.json"].Bytes);
        var historyKeys = history.RootElement.GetProperty("documents").EnumerateObject().Select(item => item.Name).ToArray();
        Assert.DoesNotContain("doc_00000007-0000-4000-8000-000000000000", historyKeys);
        Assert.Contains("doc_00000009-0000-4000-8000-000000000000", historyKeys);

        using var compositions = JsonDocument.Parse(result.Files[".urbe/compositions.v2.json"].Bytes);
        var sources = compositions.RootElement.GetProperty("items")[0].GetProperty("sources")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.DoesNotContain("doc_00000007-0000-4000-8000-000000000000", sources);
        Assert.Contains("doc_00000009-0000-4000-8000-000000000000", sources);

        using var trash = JsonDocument.Parse(result.Files[".urbe/trash.v2.json"].Bytes);
        Assert.Equal(1, trash.RootElement.GetProperty("items").GetArrayLength());

        using var vault = JsonDocument.Parse(result.Files[".urbe/vault.json"].Bytes);
        var maintenance = vault.RootElement.GetProperty("maintenance");
        var lastMaintenance = maintenance[maintenance.GetArrayLength() - 1];
        Assert.Equal("gc", lastMaintenance.GetProperty("kind").GetString());
        Assert.Equal(1, lastMaintenance.GetProperty("removed").GetProperty("history").GetInt32());
        Assert.Equal(1, vault.RootElement.GetProperty("migrations").GetArrayLength());
        Assert.False(string.IsNullOrWhiteSpace(vault.RootElement.GetProperty("migrations")[0].GetProperty("backup").GetString()));

        Assert.Equal(originals[".urbe/history.json"], Hash(result.Files[".urbe/history.json"].Bytes.Span));
        Assert.Equal(originals["Alfa.md"], Hash(result.Files["Alfa.md"].Bytes.Span));

        var second = VaultGarbageCollector.Run(result.Files.Values, now.AddMinutes(1), dryRun: false, appVersion: "1.8.3-beta");
        Assert.Equal(0, second.Plan.HistoryCount);
        Assert.Equal(0, second.Plan.TrashCount);
        Assert.Equal(0, second.Plan.CompositionReferenceCount);
    }

    [Fact]
    public void GarbageCollectorHonorsRetentionAndRefusesFutureVault()
    {
        var files = LoadFixture("v1-orfaos");
        var now = DateTimeOffset.Parse("2026-09-30T00:00:00Z");
        var plan = VaultGarbageCollector.Plan(files, now, orphanDays: 0, trashDays: 7);
        Assert.Equal(2, plan.HistoryCount);
        Assert.Equal(1, plan.TrashCount);
        Assert.Equal(1, plan.CompositionReferenceCount);

        Assert.Throws<InvalidOperationException>(() =>
            VaultGarbageCollector.Run(
                LoadFixture("vault-futuro"),
                now,
                dryRun: false,
                appVersion: "1.8.3-beta"));
    }

    [Fact]
    public void MovingMarkdownWritesAndVerifiesDestinationBeforeRemovingSourceAndKeepsAssets()
    {
        var files = LoadFixture("v1-mapa-v4")
            .Append(TextFile("Bairro/foto.png", "imagem conservada"))
            .ToArray();
        var original = VaultReader.Read(files);
        var documents = ToWriteDocuments(original)
            .Select(note => string.Equals(note.Path, "Alfa.md", StringComparison.Ordinal)
                ? note with { Path = "Bairro/Alfa.md" }
                : note)
            .ToArray();

        var plan = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = documents,
            AppVersion = "urbe-csharp-beta",
            Now = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)
        });

        Assert.True(plan.Writable);
        Assert.True(plan.Files.ContainsKey("Bairro/Alfa.md"));
        Assert.False(plan.Files.ContainsKey("Alfa.md"));
        Assert.Equal(
            Text(files, "Alfa.md"),
            Encoding.UTF8.GetString(plan.Files["Bairro/Alfa.md"].Bytes.Span));
        Assert.Equal(
            "imagem conservada",
            Encoding.UTF8.GetString(plan.Files["Bairro/foto.png"].Bytes.Span));

        var writeAt = plan.Operations.ToList().FindIndex(operation =>
            operation.Kind == VaultOperationKind.Write &&
            operation.Path == "Bairro/Alfa.md");
        var removeAt = plan.Operations.ToList().FindIndex(operation =>
            operation.Kind == VaultOperationKind.Remove &&
            operation.Path == "Alfa.md");

        Assert.True(writeAt >= 0, "Nova nota deve constar do plano");
        Assert.True(removeAt > writeAt,
            "O original não pode ser removido antes de o destino ser gravado.");
    }


    [Fact]
    public void UntouchedLegacyEncodingBytesArePreservedWhileAnotherNoteIsEdited()
    {
        var latin1 = new byte[] { (byte)'C', (byte)'a', (byte)'f', 0xE9, (byte)'\n' };
        var files = new[]
        {
            new VaultFile("Legado.md", latin1),
            TextFile("Atual.md", "início")
        };
        var snapshot = VaultReader.Read(files);
        var desired = ToWriteDocuments(snapshot)
            .Select(doc => doc.Path == "Atual.md"
                ? doc with { Content = "editado" }
                : doc)
            .ToArray();

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = desired,
            AppVersion = "urbe-csharp-beta"
        });

        Assert.Equal(latin1, result.Files["Legado.md"].Bytes.ToArray());
        Assert.DoesNotContain(result.Operations, op =>
            op.Kind == VaultOperationKind.Write && op.Path == "Legado.md");
        Assert.Equal("editado",
            Encoding.UTF8.GetString(result.Files["Atual.md"].Bytes.Span));
    }

    [Fact]
    public void EditingOrMovingNonUtf8MarkdownFailsClosedInsteadOfReplacingOriginalBytes()
    {
        var files = new[] { new VaultFile("Legado.md", new byte[] { 0xE9, 0xF1 }) };
        var snapshot = VaultReader.Read(files);
        var note = ToWriteDocuments(snapshot).Single();

        Assert.Throws<InvalidDataException>(() =>
            VaultWriter.Plan(new VaultWriteRequest
            {
                Files = files,
                Documents = [note with { Content = "novo texto" }]
            }));
        Assert.Throws<InvalidDataException>(() =>
            VaultWriter.Plan(new VaultWriteRequest
            {
                Files = files,
                Documents = [note with { Path = "Bairro/Legado.md" }]
            }));
        Assert.Equal(new byte[] { 0xE9, 0xF1 }, files[0].Bytes.ToArray());
    }

    [Fact]
    public void RecoveryJournalIsNotCreatedFromLossyNonUtf8DocumentStrings()
    {
        var latin1 = new byte[] { 0xC0, (byte)'X' };
        var files = new[]
        {
            new VaultFile("Antigo.md", latin1),
            TextFile("NovoA.md", "a"),
            TextFile("NovoB.md", "b")
        };
        var snapshot = VaultReader.Read(files);
        var docs = ToWriteDocuments(snapshot)
            .Select(doc => doc.Path switch
            {
                "NovoA.md" => doc with { Content = "aa" },
                "NovoB.md" => doc with { Content = "bb" },
                _ => doc
            })
            .ToArray();

        var result = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = docs
        });

        Assert.False(result.JournalUsed);
        Assert.Equal(latin1, result.Files["Antigo.md"].Bytes.ToArray());
        Assert.DoesNotContain(result.Operations, op =>
            op.Path == ".urbe/journal.v2.json" &&
            op.Kind == VaultOperationKind.Write);
    }

    private static VaultWriteDocument[] ToWriteDocuments(VaultSnapshot snapshot) =>
        snapshot.Documents.Select(document =>
            new VaultWriteDocument(
                document.Id,
                document.Path,
                document.Text ?? Encoding.UTF8.GetString(document.Bytes.Span)))
            .ToArray();

    private static VaultFile[] LoadFixture(string id)
    {
        var fixture = Path.Combine(Root, "..", "tests", "fixtures", "vaults", id);
        return Directory.EnumerateFiles(fixture, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "expect.json", StringComparison.Ordinal))
            .Select(path => new VaultFile(
                Path.GetRelativePath(fixture, path).Replace('\\', '/'),
                File.ReadAllBytes(path)))
            .ToArray();
    }

    private static string Text(IEnumerable<VaultFile> files, string path) =>
        Encoding.UTF8.GetString(files.Single(file =>
            string.Equals(file.Path, path, StringComparison.OrdinalIgnoreCase)).Bytes.Span);

    private static VaultFile TextFile(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

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
