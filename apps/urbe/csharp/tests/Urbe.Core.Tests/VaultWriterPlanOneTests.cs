using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// UC-60: VaultWriter.PlanOne gives, for one document, exactly what the full Plan gives it — the same refusal or the
/// same bytes — on every fixture vault and on vaults made to hit each rule (BOM, Latin-1 files, a future format, a
/// legacy vault that would migrate). The adapters write a single file with it, so this equality is their safety.
/// </summary>
public sealed class VaultWriterPlanOneTests
{
    private static string Root
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d is not null && !File.Exists(Path.Combine(d.FullName, "Urbe.Portable.slnx"))) d = d.Parent;
            return d?.FullName ?? throw new DirectoryNotFoundException("Execute os testes dentro do checkout do Urbe.");
        }
    }

    private static IEnumerable<(string Name, VaultFile[] Files)> Vaults()
    {
        var fixtures = Path.Combine(Root, "..", "tests", "fixtures", "vaults");
        if (Directory.Exists(fixtures))
            foreach (var dir in Directory.GetDirectories(fixtures).Order(StringComparer.Ordinal))
                yield return (Path.GetFileName(dir), Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Where(p => Path.GetFileName(p) != "expect.json")
                    .Select(p => new VaultFile(Path.GetRelativePath(dir, p).Replace('\\', '/'), File.ReadAllBytes(p))).ToArray());
        static VaultFile T(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));
        var current = T(".urbe/vault.json", """{"formatVersion":2,"created":"2026-01-01T00:00:00.000Z"}""");
        yield return ("simples", [current, T("Comece.md", "# Comece\n"), T("Bairro/Um.md", "# Um\n\n[[Comece]]\n"), T("dados.json", "{}")]);
        yield return ("sem .urbe (migraria)", [T("Comece.md", "# Comece\n"), T("Outra.txt", "texto")]);
        yield return ("BOM", [current, new VaultFile("Bom.md", [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("# Bom\n")]), T("Comece.md", "# Comece\n")]);
        yield return ("Latin-1", [current, new VaultFile("Velha.md", [0x23, 0x20, 0x43, 0x61, 0x66, 0xe9, 0x0a]), T("Comece.md", "# Comece\n")]);
        yield return ("futuro", [T(".urbe/vault.json", """{"formatVersion":9}"""), T("Comece.md", "# Comece\n")]);
        yield return ("lixeira futura", [current, T(".urbe/trash.v2.json", """{"version":7,"items":[]}"""), T("Comece.md", "# Comece\n")]);
    }

    private static string Full(VaultFile[] files, VaultSnapshot snapshot, string target, string content, bool create)
    {
        try
        {
            // as the adapters asked before: every document read as text, the target with its new text
            var documents = snapshot.Documents.Where(d => d.Text is not null)
                .Select(d => new VaultWriteDocument(d.Id, d.Path, string.Equals(d.Path, target, StringComparison.OrdinalIgnoreCase) ? content : d.Text!))
                .ToList();
            if (create) documents.Add(new VaultWriteDocument("doc-novo", target, content));
            var plan = VaultWriter.Plan(new VaultWriteRequest { Files = files, Documents = documents, AppVersion = "urbe-native-preview" });
            return !plan.Writable || !plan.Files.ContainsKey(target) ? "null" : Convert.ToHexString(plan.Files[target].Bytes.Span);
        }
        catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
    }

    private static string One(VaultFile[] files, VaultSnapshot snapshot, string target, string content)
    {
        try { return VaultWriter.PlanOne(files, snapshot, target, content) is { } b ? Convert.ToHexString(b) : "null"; }
        catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
    }

    [Fact]
    public void PlanOneIsTheFullPlanForThatDocument()
    {
        int cases = 0;
        foreach (var (name, files) in Vaults())
        {
            var snapshot = VaultReader.Read(files);
            var writable = snapshot.Documents.Where(d => d.Text is not null).ToList();
            foreach (var doc in writable.Concat(snapshot.Documents.Where(d => d.Text is null)).Take(12))
            {
                if (snapshot.FutureFiles.Contains(doc.Path, StringComparer.OrdinalIgnoreCase)) continue;
                foreach (var content in new[] { doc.Text ?? "", (doc.Text ?? "") + "edição ç\n" })
                {
                    if (doc.Text is null) continue;
                    Assert.True(Full(files, snapshot, doc.Path, content, false) == One(files, snapshot, doc.Path, content), name + " · " + doc.Path);
                    cases++;
                }
            }
            foreach (var created in new[] { "Nova nota.md", "Pasta nova/Nota.md" })
            {
                if (files.Any(f => string.Equals(f.Path, created, StringComparison.OrdinalIgnoreCase))) continue;
                Assert.True(Full(files, snapshot, created, "# Nova\n", true) == One(files, snapshot, created, "# Nova\n"), name + " · " + created);
                cases++;
            }
        }
        Assert.True(cases > 20, cases + " casos");
    }

    [Fact]
    public void PlanOneRefusesWhatThePlanRefuses()
    {
        static VaultFile T(string path, string text) => new(path, Encoding.UTF8.GetBytes(text));
        var current = T(".urbe/vault.json", """{"formatVersion":2,"created":"2026-01-01T00:00:00.000Z"}""");
        VaultFile[] latin = [current, new VaultFile("Velha.md", [0x23, 0x20, 0x43, 0x61, 0x66, 0xe9, 0x0a])];
        Assert.Throws<InvalidDataException>(() => VaultWriter.PlanOne(latin, VaultReader.Read(latin), "Velha.md", "# Café\n"));
        VaultFile[] future = [T(".urbe/vault.json", """{"formatVersion":9}"""), T("Comece.md", "# Comece\n")];
        Assert.Null(VaultWriter.PlanOne(future, VaultReader.Read(future), "Comece.md", "outro"));
        Assert.Throws<InvalidDataException>(() => VaultWriter.PlanOne([current], VaultReader.Read([current]), ".urbe/x.md", "x"));
        Assert.Throws<InvalidDataException>(() => VaultWriter.PlanOne([current], VaultReader.Read([current]), "../fora.md", "x"));
        VaultFile[] bom = [current, new VaultFile("Bom.md", [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("# Bom\n")])];
        var same = VaultWriter.PlanOne(bom, VaultReader.Read(bom), "Bom.md", Encoding.UTF8.GetString(bom[1].Bytes.Span));
        Assert.Equal(bom[1].Bytes.ToArray(), same);   // unchanged text keeps its original bytes
    }
}
