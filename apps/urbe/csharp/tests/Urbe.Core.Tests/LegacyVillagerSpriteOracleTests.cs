using System.Diagnostics;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Oráculo Node só nos testes: o sprite C# dos moradores é byte a byte o do pixel-art.js 1.8.4-beta.</summary>
public sealed class LegacyVillagerSpriteOracleTests
{
    [Fact]
    public void VillagerSpritesAreByteIdenticalToTheOriginalPixelArt()
    {
        var original = Locate(Path.Combine("src", "world", "pixel-art.js"));
        var harness = Locate(Path.Combine("csharp", "tools", "villager-oracle.mjs"));
        var command = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
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
        var cases = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(6 * 3 * 4, cases.Count);
        foreach (var c in cases)
        {
            var l = c.GetProperty("look");
            string? Str(string n) => l.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var look = new LegacyVillagerSprite.Look(l.GetProperty("skin").GetInt32(), l.GetProperty("hair").GetInt32(),
                l.GetProperty("pants").GetInt32(), l.GetProperty("style").GetString()!, l.GetProperty("shirt").GetString()!,
                l.GetProperty("dress").GetInt32() != 0, Str("cap"), Str("hood"), Str("acc"));
            var dir = c.GetProperty("dir").GetString()!;
            var frame = c.GetProperty("frame").GetInt32();
            Assert.Equal(LegacyVillagerSprite.Width, c.GetProperty("width").GetInt32());
            Assert.Equal(LegacyVillagerSprite.Height, c.GetProperty("height").GetInt32());
            var expected = Convert.FromBase64String(c.GetProperty("rgba").GetString()!);
            var actual = LegacyVillagerSprite.Create(look, dir, frame);
            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
                if (expected[i] != actual[i])
                    Assert.Fail($"{look.Style}/{dir}/{frame}: byte {i} JS={expected[i]} C#={actual[i]}. Não alterar o oráculo para obter verde.");
        }
    }

    [Fact]
    public void FlippedSideFrameMirrorsEveryRow()
    {
        var look = new LegacyVillagerSprite.Look(1, 2, 3, "cap", "#3d6fb0", Cap: "#a33c32", Acc: "basket");
        var a = LegacyVillagerSprite.Create(look, "side", 1);
        var b = LegacyVillagerSprite.CreateFlipped(look, 1);
        for (int y = 0; y < LegacyVillagerSprite.Height; y++)
            for (int x = 0; x < LegacyVillagerSprite.Width; x++)
                Assert.Equal(a.AsSpan((y * 14 + x) * 4, 4).ToArray(),
                             b.AsSpan((y * 14 + 13 - x) * 4, 4).ToArray());
    }

    private static string Locate(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (var root in new[] { dir.FullName, Path.Combine(dir.FullName, "apps", "urbe") })
            {
                var candidate = Path.Combine(root, relative);
                if (File.Exists(candidate)) return candidate;
            }
        }
        throw new FileNotFoundException(relative);
    }
}
