using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// "Versões anteriores": the text of .urbe/history.v2.json after each edit and after a note goes to the trash is the
/// 1.8.4's (csharp/tools/legacy-history-oracle.mjs): typing within 5 s keeps no new version, a later change keeps the
/// previous text, the trash keeps the removed text, and the file is created empty on the first open.
/// </summary>
public sealed class LegacyHistoryOracleTests
{
    [Fact]
    public void EveryStepLeavesTheHistoryFileOfTheOriginal()
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", "history-run-a.json")));
        var root = json.RootElement;
        var history = LegacyHistory.Read(null, null, out var readOnly);
        Assert.False(readOnly);
        Assert.Equal(root.GetProperty("initial").GetString(), history.Export());
        var docs = root.GetProperty("documents").EnumerateArray().ToDictionary(d => d.GetProperty("path").GetString()!,
            d => (Id: d.GetProperty("id").GetString()!, Content: d.GetProperty("content").GetString()!));
        foreach (var step in root.GetProperty("steps").EnumerateArray())
        {
            long now = step.GetProperty("now").GetInt64();
            if (step.TryGetProperty("trash", out var trash))
            {
                var (id, content) = docs[trash.GetString()!];
                var store = new DocumentStore();
                var d = store.Upsert(new DocumentInput { Id = id, Path = trash.GetString()!, Content = content });
                history.CaptureRemoved(d, now);
            }
            else
            {
                var path = step.GetProperty("path").GetString()!;
                var text = step.GetProperty("text").GetString()!;
                var (id, before) = docs[path];
                history.Capture(id, path, before, text, now);
                docs[path] = (id, text);
            }
            Assert.Equal(step.GetProperty("history").GetString(), history.Export());
        }

        // read back: the same file, newest first; a v2 file of another version is never written
        var again = LegacyHistory.Read(history.Export(), null, out _);
        Assert.Equal(history.Export(), again.Export());
        var leituras = again.List(docs["Área/Leituras.md"].Id);
        Assert.Equal("# Leituras\n\nUm. Dois.\n", leituras[0].Content);
        Assert.Equal(root.GetProperty("T0").GetInt64() + 40000, leituras[0].Timestamp);
        Assert.Equal(history.List(docs["Área/Leituras.md"].Id), leituras);   // kept in memory or read back, the same
        Assert.True(again.List(docs["Projetos/Tarefas.md"].Id)[0].Removed);
        LegacyHistory.Read("{\"version\":3,\"documents\":{}}", null, out readOnly);
        Assert.True(readOnly);
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
