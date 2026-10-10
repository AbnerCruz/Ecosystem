using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// Byte-for-byte original-JS oracle, limited to the test process. No JS enters
/// the native application. Covers coast, depth, biome transitions, terrain
/// elevations, ground decorations, world and negative chunk coordinates.
/// </summary>
public sealed class LegacyChunkPixelOracleTests
{
    [Fact]
    public void OriginalPixelArtChunkIsIdenticalToPortableCSharpRgba()
    {
        var original = Locate(Path.Combine("src", "world", "pixel-art.js"));
        var harness = Locate(Path.Combine("csharp", "tools", "chunk-pixels-oracle.mjs"));

        var command = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        command.ArgumentList.Add(harness);
        command.ArgumentList.Add(original);
        using var process = Process.Start(command)
            ?? throw new InvalidOperationException("Não foi possível iniciar o oráculo Node.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30_000), "Oráculo JS excedeu 30 segundos.");
        Assert.True(process.ExitCode == 0, "Oráculo JS falhou: " + error);

        using var json = JsonDocument.Parse(output);
        Assert.Equal(4, json.RootElement.EnumerateObject().Count());
        foreach (var (id, cx, cy) in new[]
        {
            ("deep", 0, 0), ("coast", 0, 0),
            ("mixed", 1, -1), ("negative", -2, 1)
        })
        {
            byte[] expected = Convert.FromBase64String(
                json.RootElement.GetProperty(id).GetString()!);
            byte[] actual = LegacyChunkPixels.Render(
                (x, y) => Terrain(id, x, y),
                (x, y) => Decorations(id, x, y),
                cx, cy);
            Assert.Equal(expected.Length, actual.Length);

            int mismatch = -1;
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i])
                {
                    mismatch = i;
                    break;
                }
            if (mismatch >= 0)
            {
                int pixel = mismatch / 4;
                Assert.Fail(
                    $"1.8.4-beta difere de C# em {id}, chunk ({cx},{cy}), " +
                    $"pixel ({pixel % 256},{pixel / 256}), canal {mismatch % 4}: " +
                    $"JS={expected[mismatch]}, C#={actual[mismatch]}. " +
                    "Não modificar o oráculo para obter verde.");
            }
        }
    }

    private static LegacyTileSampler.Tile Terrain(string id, int x, int y)
    {
        var biome = id switch
        {
            "deep" => LegacyBiome.Deep,
            "coast" => x < 8 ? LegacyBiome.Sea : LegacyBiome.Meadow,
            "mixed" => new[]
            {
                LegacyBiome.Grass, LegacyBiome.Meadow, LegacyBiome.Forest,
                LegacyBiome.Hills, LegacyBiome.Taiga, LegacyBiome.Mountain,
                LegacyBiome.Peak, LegacyBiome.Snow, LegacyBiome.Steppe
            }[Mod(x + y * 2, 9)],
            "negative" => x < -24 ? LegacyBiome.Lake : LegacyBiome.Savanna,
            _ => throw new ArgumentException("Fixture desconhecida.", nameof(id))
        };
        double elevation = id switch
        {
            "deep" => .1,
            "coast" => x < 8 ? .34 : .56,
            "mixed" => .42 + (x % 7) * .007 - (y % 5) * .004,
            "negative" => x < -24 ? .38 : .58,
            _ => throw new ArgumentException("Fixture desconhecida.", nameof(id))
        };
        return new LegacyTileSampler.Tile(biome, elevation, .55, .55, false, 0);
    }

    private static IReadOnlyList<string> Decorations(string id, int x, int y)
    {
        if (id == "coast" && x == 9 && y == 7) return ["flowers", "tallgrass"];
        if (id == "mixed" && x == 20 && y == -5) return ["fern", "rock"];
        if (id == "negative" && x == -20 && y == 19) return ["drygrass"];
        return [];
    }

    private static int Mod(int value, int m) => (value % m + m) % m;

    private static string Locate(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var root = Path.Combine(dir.FullName, relative);
            if (File.Exists(root)) return root;
        }
        throw new FileNotFoundException("Arquivo original do oráculo ausente: " + relative);
    }
}
