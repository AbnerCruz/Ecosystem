using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// The real 'urbe' world, chunk by chunk, against the unmodified 1.8.4-beta terrain.js +
/// pixel-art.js run by Node only in the test process: near ground (chunkPixels) and the
/// far ground with canopies (chunkFarPixels) must be byte-identical.
/// </summary>
public sealed class LegacyWorldChunksOracleTests
{
    private static readonly string[] Chunks = ["2,1", "1,1", "0,2", "3,0", "-1,1", "4,3", "1,-1"];
    // 300×300 tiles around the 1.8.4 start, rivers, lakes and coast included.
    private static readonly (int X0, int Y0, int X1, int Y1) BiomeArea = (-114, -125, 186, 175);

    [Fact]
    public async Task RealWorldChunksMatchTheOriginalNearAndFarPixels()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "csharp", "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "csharp", "tools", "world-chunks-oracle.mjs"));
        start.ArgumentList.Add(Path.Combine(root.FullName, "src", "world"));
        foreach (var c in Chunks) start.ArgumentList.Add(c);
        start.ArgumentList.Add($"biomes:{BiomeArea.X0},{BiomeArea.Y0},{BiomeArea.X1},{BiomeArea.Y1}");
        using var process = Process.Start(start)!;
        var token = TestContext.Current.CancellationToken;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + await stderr);
        using var json = JsonDocument.Parse(await stdout);

        var climate = LegacyElevationClimateField.Generate("urbe", 512);
        var hydrology = LegacyHydrologyField.Generate(climate);
        var sampler = new LegacyTileSampler(hydrology, LegacyWorldSpawn.Choose(hydrology));
        var chunks = new LegacyTileChunks(sampler);
        var vegetation = new LegacyVegetation(sampler);
        var biomes = Convert.FromBase64String(json.RootElement.GetProperty("biomes").GetString()!);
        int width = BiomeArea.X1 - BiomeArea.X0, mismatches = 0, rivers = 0;
        for (int y = BiomeArea.Y0; y < BiomeArea.Y1; y++)
        for (int x = BiomeArea.X0; x < BiomeArea.X1; x++)
        {
            var expected = biomes[(y - BiomeArea.Y0) * width + (x - BiomeArea.X0)];
            if ((byte)chunks.At(x, y).Biome != expected) mismatches++;
            if (expected == (byte)LegacyBiome.River) rivers++;
        }
        Assert.True(rivers > 100, $"area must contain rivers ({rivers})");
        Assert.Equal(0, mismatches);
        int trees = 0;
        foreach (var c in Chunks)
        {
            var parts = c.Split(',').Select(int.Parse).ToArray();
            var expected = json.RootElement.GetProperty(c);
            var ground = LegacyChunkPixels.Render(chunks, vegetation, parts[0], parts[1]);
            Assert.Equal(Convert.FromBase64String(expected.GetProperty("ground").GetString()!), ground);
            var far = LegacyChunkFarPixels.Render(ground, vegetation.TreeAt, parts[0], parts[1]);
            Assert.Equal(Convert.FromBase64String(expected.GetProperty("far").GetString()!), far);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                if (vegetation.TreeAt(parts[0] * 16 + x, parts[1] * 16 + y) is not null) trees++;
        }
        Assert.True(trees > 20, $"fixture chunks must contain trees ({trees})");
    }
}
