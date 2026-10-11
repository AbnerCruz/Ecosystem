using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>"Nova nota" names against the unmodified 1.8.4-beta safeName, v21UniqueName and
/// separarNomeExtNota run by Node (tools/note-name-oracle.mjs).</summary>
public sealed class LegacyNewNoteNameOracleTests
{
    private static readonly string[] Typed =
        ["Ideias", "ideias.MD", "dados.json", "a/b:c?", "  ", "...oculto.", "Relatório final.txt", "nome.exe", "x.Markdown",
         "Comece aqui", "Notas\\Sub", "tab\tbed", "café.yaml", "Plano.HTML", new string('x', 120), "x.md.md", ".md"];
    private static readonly string[] Taken = ["Ideias.md", "Ideias (2).md", "Comece aqui.md", "dados.JSON"];

    [Fact]
    public async Task NamesOfANewNoteMatchTheOriginal()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "note-name-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src"));
        start.ArgumentList.Add(JsonSerializer.Serialize(Typed));
        start.ArgumentList.Add(JsonSerializer.Serialize(Taken));
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        using var json = JsonDocument.Parse(await stdout);

        var city = new LegacyCity(new FlatTerrain(), p => p);
        int i = 0;
        foreach (var f in Taken)
            city.Buildings.Add(new LegacyCityBuilding("b" + i++, "Pasta/" + f, Path.GetFileNameWithoutExtension(f), "r1", i * 10, 0, "house1"));
        city.Buildings.Add(new LegacyCityBuilding("other", "Ideias (3).md", "Ideias (3)", null, 99, 0, "house1")); // another neighbourhood
        foreach (var e in json.RootElement.EnumerateArray())
        {
            var typed = e.GetProperty("typed").GetString()!;
            var unique = city.UniqueNoteName(typed, "r1");
            Assert.Equal(e.GetProperty("unique").GetString(), unique);
            var (name, ext) = LegacyCity.SplitNoteName(unique);
            Assert.Equal(e.GetProperty("name").GetString(), name);
            Assert.Equal(e.GetProperty("ext").GetString(), ext);
        }
        Assert.Equal(Typed.Length, json.RootElement.GetArrayLength());
    }

    private sealed class FlatTerrain : ILegacyCityTerrain
    {
        public bool Buildable(int x, int y) => true;
        public bool Roadable(int x, int y) => true;
        public double RoadCost(int x, int y) => 1;
    }
}
