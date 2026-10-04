using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class VaultArchiveTests
{
    [Fact]
    public void ManifestRoundTripMatchesLegacyContract()
    {
        var files = new[]
        {
            TextFile("Alfa.md", "# Alfa\n"),
            TextFile(".urbe/mapa.json", "{\"v\":4}"),
            new VaultFile("Anexos/x.bin", [0, 255, 3])
        };
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        var manifest = VaultExportManifestCodec.Build(
            files,
            "2.0.0",
            now,
            "Urbe",
            2,
            VaultExportState.Empty);

        Assert.Equal("urbe-export", manifest.Format);
        Assert.Equal(1, manifest.FormatVersion);
        Assert.Equal("2.0.0", manifest.AppVersion);
        Assert.Equal("2026-10-04T12:00:00.000Z", manifest.ExportedAt);
        Assert.Equal("Urbe", manifest.VaultName);
        Assert.Equal(2, manifest.VaultFormatVersion);
        Assert.Equal(
            new[] { ".urbe/mapa.json", "Alfa.md", "Anexos/x.bin" },
            manifest.Files.Select(file => file.Path).ToArray());
        Assert.Equal(3, manifest.Files.Single(file => file.Path == "Anexos/x.bin").Size);

        var bytes = VaultExportManifestCodec.Serialize(manifest);
        var parsed = VaultExportManifestCodec.Parse(bytes);

        Assert.Equal(VaultExportManifestState.Current, parsed.State);
        Assert.NotNull(parsed.Manifest);
        Assert.True(VaultExportManifestCodec.Verify(parsed.Manifest!, files).Ok);
    }

    [Fact]
    public void ManifestVerificationFindsTamperedMissingAndExtraFiles()
    {
        var original = new[]
        {
            TextFile("Alfa.md", "# Alfa\n"),
            TextFile(".urbe/mapa.json", "{\"v\":4}"),
            new VaultFile("Anexos/x.bin", [0, 255, 3])
        };
        var manifest = VaultExportManifestCodec.Build(
            original,
            "2.0.0",
            DateTimeOffset.UnixEpoch,
            "Urbe",
            2);

        var tampered = new[]
        {
            TextFile("Alfa.md", "# Alfx\n"),
            TextFile(".urbe/mapa.json", "{\"v\":4}"),
            TextFile("intruso.md", "x")
        };

        var verification = VaultExportManifestCodec.Verify(manifest, tampered);

        Assert.False(verification.Ok);
        Assert.Equal(new[] { "Alfa.md" }, verification.Mismatched);
        Assert.Equal(new[] { "Anexos/x.bin" }, verification.Missing);
        Assert.Equal(new[] { "intruso.md" }, verification.Extra);
    }

    [Fact]
    public void ManifestParserDistinguishesCorruptAndFuture()
    {
        Assert.Equal(
            VaultExportManifestState.Corrupt,
            VaultExportManifestCodec.Parse("{").State);
        Assert.Equal(
            VaultExportManifestState.Corrupt,
            VaultExportManifestCodec.Parse(
                """{"format":"outro","formatVersion":1,"files":[]}""").State);

        var future = VaultExportManifestCodec.Parse(
            """{"format":"urbe-export","formatVersion":9,"files":[]}""");

        Assert.Equal(VaultExportManifestState.Future, future.State);
        Assert.NotNull(future.Manifest);
        Assert.Equal(9, future.Manifest!.FormatVersion);
    }

    [Fact]
    public void PortableStateAllowsOnlyExpectedKeysAndNeverAiSecrets()
    {
        var storage = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["urbe.explorer.v2"] = "{\"favorites\":[\"a\"]}",
            ["urbe.editor.workspace.v1"] = "{\"tabs\":[]}",
            ["urbe.tip.cidade"] = "1",
            ["urbe.plugins.v1"] =
                """{"Urbe::Personalização/plugins/ola.js":"sha256:aa","Outro::p.js":"sha256:bb"}""",
            ["urbe.ai.config.v1"] = "{\"apiKey\":\"sk-SEGREDO\"}",
            ["urbe.ai.global.v21"] = "x",
            ["openai-api-key"] = "sk-2",
            ["urbe.plugin.Urbe.ola.token"] = "t",
            ["urbe.tip.apiKey"] = "k",
            ["urbe.modoSeguro"] = "1",
            ["urbe-page-theme"] = "dark"
        };

        var state = VaultExportStatePolicy.Collect(storage, "Urbe");

        Assert.Equal(
            new[]
            {
                "urbe.editor.workspace.v1",
                "urbe.explorer.v2",
                "urbe.tip.cidade"
            },
            state.LocalStorage.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.NotNull(state.Plugins);
        Assert.Single(state.Plugins!);
        Assert.Equal(
            "sha256:aa",
            state.Plugins!["Personalização/plugins/ola.js"].GetString());

        foreach (var key in new[]
                 {
                     "urbe.ai.config.v1",
                     "openai-api-key",
                     "urbe.plugin.Urbe.ola.token",
                     "urbe.tip.apiKey"
                 })
            Assert.False(VaultExportStatePolicy.Allowed(key), key);
    }

    [Fact]
    public void PortableStateApplyRemapsPluginApprovalsAndFiltersInjectedSecrets()
    {
        var manifestJson =
            """
            {
              "format":"urbe-export",
              "formatVersion":1,
              "files":[],
              "state":{
                "localStorage":{
                  "urbe.explorer.v2":"{\"favorites\":[\"a\"]}",
                  "urbe.tip.cidade":"1",
                  "urbe.ai.config.v1":"{\"apiKey\":\"sk-X\"}",
                  "urbe.qualquer":"y"
                },
                "plugins":{
                  "Personalização/plugins/ola.js":"sha256:aa"
                }
              }
            }
            """;
        var parsed = VaultExportManifestCodec.Parse(manifestJson);
        Assert.Equal(VaultExportManifestState.Current, parsed.State);

        var destination = new Dictionary<string, string>
        {
            ["urbe.plugins.v1"] = """{"Casa::x.js":"sha256:cc"}"""
        };
        var applied = VaultExportStatePolicy.Apply(
            destination,
            parsed.Manifest!.State,
            "Casa");

        Assert.Equal(
            new[]
            {
                "urbe.explorer.v2",
                "urbe.plugins.v1",
                "urbe.tip.cidade"
            },
            applied.Writes.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain("urbe.ai.config.v1", applied.Writes.Keys);
        Assert.DoesNotContain("urbe.qualquer", applied.Writes.Keys);

        using var plugins = JsonDocument.Parse(applied.Writes["urbe.plugins.v1"]);
        Assert.Equal(
            "sha256:cc",
            plugins.RootElement.GetProperty("Casa::x.js").GetString());
        Assert.Equal(
            "sha256:aa",
            plugins.RootElement
                .GetProperty("Casa::Personalização/plugins/ola.js")
                .GetString());
    }

    [Fact]
    public void ArchiveRoundTripPreservesBytesAndExcludesJournals()
    {
        var source = new[]
        {
            TextFile("Alfa.md", "# Alfa\n"),
            TextFile(".urbe/mapa.json", "{\"v\":4}"),
            TextFile(".urbe/journal.json", "legacy"),
            TextFile(".urbe/journal.v2.json", "current"),
            new VaultFile("Anexos/x.bin", [0, 255, 3, 8])
        };
        var state = VaultExportStatePolicy.Collect(
            new Dictionary<string, string>
            {
                ["urbe.tip.cidade"] = "1"
            },
            "Urbe");

        var exported = VaultArchive.Export(
            source,
            new VaultArchiveExportOptions
            {
                AppVersion = "2.0.0",
                VaultName = "Urbe",
                VaultFormatVersion = 2,
                Now = new DateTimeOffset(2026, 10, 4, 13, 0, 0, TimeSpan.Zero),
                State = state
            });

        Assert.DoesNotContain(
            exported.Files,
            file => file.Path == VaultArchive.LegacyJournalPath);
        Assert.DoesNotContain(
            exported.Files,
            file => file.Path == VaultArchive.CurrentJournalPath);

        var imported = VaultArchive.Import(exported.Bytes);

        Assert.True(imported.IsUrbeExport);
        Assert.Equal(VaultExportManifestState.Current, imported.ManifestState);
        Assert.NotNull(imported.Verification);
        Assert.True(imported.Verification!.Ok);
        Assert.Null(imported.RootPrefix);
        Assert.Contains(".urbe/mapa.json", imported.Files.Keys);
        Assert.Contains("Alfa.md", imported.Files.Keys);
        Assert.Contains("Anexos/x.bin", imported.Files.Keys);
        Assert.False(imported.Files.ContainsKey(VaultArchive.LegacyJournalPath));
        Assert.False(imported.Files.ContainsKey(VaultArchive.CurrentJournalPath));
        Assert.Equal(
            new byte[] { 0, 255, 3, 8 },
            imported.Files["Anexos/x.bin"].Bytes.ToArray());
    }

    [Fact]
    public void WrappedArchiveDetectsAndStripsSingleRootFolder()
    {
        var exported = VaultArchive.Export(
            new[]
            {
                TextFile("A.md", "a"),
                TextFile(".urbe/mapa.json", "{\"v\":4}")
            },
            new VaultArchiveExportOptions
            {
                VaultName = "Urbe",
                VaultFormatVersion = 2
            });

        var wrapped = WrapZip(exported.Bytes, "MinhaCidade/");
        var imported = VaultArchive.Import(wrapped);

        Assert.Equal("MinhaCidade/", imported.RootPrefix);
        Assert.True(imported.IsUrbeExport);
        Assert.True(imported.Verification!.Ok);
        Assert.Contains("A.md", imported.Files.Keys);
        Assert.Contains(".urbe/mapa.json", imported.Files.Keys);
        Assert.DoesNotContain(
            imported.Files.Keys,
            path => path.StartsWith("MinhaCidade/", StringComparison.Ordinal));
    }

    [Fact]
    public void CorruptManifestNameIsOrdinaryFileAndGetsNoAuthority()
    {
        var zip = BuildZip(
            [
                (VaultExportManifest.FileName, Encoding.UTF8.GetBytes("não é manifesto")),
                ("A.md", Encoding.UTF8.GetBytes("a"))
            ]);

        var imported = VaultArchive.Import(zip);

        Assert.Equal(VaultExportManifestState.Corrupt, imported.ManifestState);
        Assert.False(imported.IsUrbeExport);
        Assert.Null(imported.Manifest);
        Assert.Null(imported.Verification);
        Assert.Contains(VaultExportManifest.FileName, imported.Files.Keys);
        Assert.Contains("A.md", imported.Files.Keys);
    }

    [Fact]
    public void GenericZipWithoutManifestIsAcceptedAsGenericArchive()
    {
        var imported = VaultArchive.Import(
            BuildZip(
                [
                    ("A.md", Encoding.UTF8.GetBytes("a")),
                    (".urbe/mapa.json", Encoding.UTF8.GetBytes("{\"v\":4}"))
                ]));

        Assert.Equal(VaultExportManifestState.Absent, imported.ManifestState);
        Assert.False(imported.IsUrbeExport);
        Assert.Contains(".urbe/mapa.json", imported.Files.Keys);
    }

    [Fact]
    public void FutureManifestIsRefused()
    {
        var zip = BuildZip(
            [
                (
                    VaultExportManifest.FileName,
                    Encoding.UTF8.GetBytes(
                        """{"format":"urbe-export","formatVersion":9,"files":[]}"""))
            ]);

        var error = Assert.Throws<InvalidDataException>(() => VaultArchive.Import(zip));
        Assert.Contains("versão mais nova", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../evil.md")]
    [InlineData("/absolute.md")]
    [InlineData("C:/absolute.md")]
    [InlineData("Root/../../evil.md")]
    public void UnsafeArchivePathsAreRejected(string path)
    {
        var zip = BuildZip([(path, Encoding.UTF8.GetBytes("x"))]);

        Assert.Throws<InvalidDataException>(() => VaultArchive.Import(zip));
    }

    [Fact]
    public void DuplicatePathsAfterCaseInsensitiveNormalizationAreRejected()
    {
        var zip = BuildZip(
            [
                ("A.md", Encoding.UTF8.GetBytes("a")),
                ("a.md", Encoding.UTF8.GetBytes("b"))
            ]);

        Assert.Throws<InvalidDataException>(() => VaultArchive.Import(zip));
    }

    [Fact]
    public void ExportRejectsReservedManifestNameInPhysicalVault()
    {
        var files = new[]
        {
            TextFile(VaultExportManifest.FileName, "usuário"),
            TextFile("A.md", "a")
        };

        Assert.Throws<InvalidDataException>(() => VaultArchive.Export(files));
    }

    [Fact]
    public void MacOsMetadataDirectoryIsIgnored()
    {
        var zip = BuildZip(
            [
                ("__MACOSX/._A.md", Encoding.UTF8.GetBytes("metadata")),
                ("A.md", Encoding.UTF8.GetBytes("a"))
            ]);

        var imported = VaultArchive.Import(zip);

        Assert.Single(imported.Files);
        Assert.Contains("A.md", imported.Files.Keys);
    }

    [Fact]
    public void CurrentManifestWithUnsafeListedPathIsRejectedBeforeUse()
    {
        var manifest =
            """
            {
              "format":"urbe-export",
              "formatVersion":1,
              "files":[
                {
                  "path":"../evil.md",
                  "size":1,
                  "sha256":"00"
                }
              ]
            }
            """;
        var zip = BuildZip(
            [
                (VaultExportManifest.FileName, Encoding.UTF8.GetBytes(manifest)),
                ("A.md", Encoding.UTF8.GetBytes("a"))
            ]);

        Assert.Throws<InvalidDataException>(() => VaultArchive.Import(zip));
    }

    private static VaultFile TextFile(string path, string content) =>
        new(path, Encoding.UTF8.GetBytes(content));

    private static byte[] BuildZip(
        IEnumerable<(string Path, byte[] Bytes)> entries)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in entries)
            {
                var entry = zip.CreateEntry(item.Path);
                using var stream = entry.Open();
                stream.Write(item.Bytes);
            }
        }

        return output.ToArray();
    }

    private static byte[] WrapZip(ReadOnlyMemory<byte> source, string prefix)
    {
        using var input = new MemoryStream(source.ToArray(), writable: false);
        using var original = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        using var output = new MemoryStream();

        using (var wrapped = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in original.Entries)
            {
                if (string.IsNullOrEmpty(item.Name))
                    continue;

                var entry = wrapped.CreateEntry(prefix + item.FullName);
                using var from = item.Open();
                using var to = entry.Open();
                from.CopyTo(to);
            }
        }

        return output.ToArray();
    }
}
