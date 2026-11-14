using Lunet;
using Lunet.Ai;
using Lunet.Compiler;
using Lunet.Core;
using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>LUNET-499: one palette per project keeps art, UI and code visually consistent.</summary>
public sealed class ProjectPaletteTests
{
    [Fact]
    public void Json_RoundTripsAndRejectsUnknownVersions()
    {
        var palette = new ProjectPalette { Name = "Bosque" };
        palette.AddRange([0xff102030, 0xffff0000]);
        var again = ProjectPalette.Parse(palette.ToJson());
        Assert.Equal("Bosque", again.Name);
        Assert.Equal([0xff102030u, 0xffff0000u], again.Argb);
        Assert.Throws<InvalidDataException>(() => ProjectPalette.Parse("""{"Version":9,"Colors":[]}"""));
    }

    [Fact]
    public void AddRange_SkipsDuplicatesAndGivesUniqueNames()
    {
        var palette = new ProjectPalette();
        Assert.Equal(2, palette.AddRange([0xffff0000, 0xffee0000, 0xffff0000]));
        Assert.Equal(["Vermelho", "Vermelho2"], palette.Colors.Select(c => c.Name));
    }

    [Fact]
    public void Ramp_GoesFromDarkToLightKeepingTheBaseInTheMiddle()
    {
        var ramp = ProjectPalette.Ramp(0xff3fb950, 5);
        Assert.Equal(5, ramp.Length);
        Assert.Equal(0xff3fb950u, ramp[2]);
        var values = ramp.Select(c => SpriteColor.ToHsv(c).Value).ToArray();
        Assert.True(values.Zip(values.Skip(1)).All(p => p.First < p.Second), string.Join(",", values));
        // hue shifting: the shadow is cooler (closer to blue) than the base green
        Assert.True(SpriteColor.ToHsv(ramp[0]).Hue > SpriteColor.ToHsv(ramp[2]).Hue);
    }

    [Theory]
    [InlineData(PaletteHarmony.Complementary, 2)]
    [InlineData(PaletteHarmony.Analogous, 3)]
    [InlineData(PaletteHarmony.Triadic, 3)]
    [InlineData(PaletteHarmony.Tetradic, 4)]
    [InlineData(PaletteHarmony.Monochrome, 5)]
    public void Harmony_ProducesTheExpectedNumberOfColors(PaletteHarmony kind, int count)
    {
        var colors = ProjectPalette.Harmony(0xffff0000, kind);
        Assert.Equal(count, colors.Length);
        Assert.All(colors, c => Assert.Equal(0xffu, c >> 24));
    }

    [Fact]
    public void Complementary_OfRedIsCyan() =>
        Assert.Equal(0xff00ffffu, ProjectPalette.Harmony(0xffff0000, PaletteHarmony.Complementary)[1]);

    [Fact]
    public void FromPixels_FindsTheMainColorsOfAnImage()
    {
        var pixels = Enumerable.Repeat(0xffff0000u, 50).Concat(Enumerable.Repeat(0xff0000ffu, 30))
            .Concat(Enumerable.Repeat(0xfffe0101u, 5)).Concat(Enumerable.Repeat(0x00000000u, 100)).ToArray();
        var main = ProjectPalette.FromPixels(pixels, 2);
        Assert.Equal(2, main.Length);
        Assert.Contains(main, c => ProjectPalette.Distance(c, 0xffff0000) < 10);
        Assert.Contains(main, c => ProjectPalette.Distance(c, 0xff0000ff) < 10);
        Assert.Empty(ProjectPalette.FromPixels([0x00ffffff], 4));
    }

    [Fact]
    public void Remap_SnapsToNearestKeepingAlphaAndTransparency()
    {
        var palette = new ProjectPalette();
        palette.AddRange([0xff000000, 0xffffffff, 0xffff0000]);
        var mapped = palette.Remap([0xfff01010, 0x80101010, 0x00123456, 0xffeeeeee]);
        Assert.Equal([0xffff0000u, 0x80000000u, 0x00123456u, 0xffffffffu], mapped);
        Assert.Equal([0xff00ff00u], palette.ColorsOutside([0xff00ff00, 0xffff0000, 0x00ffffff, 0x80ff0000]));
    }

    [Fact]
    public void Contrast_FollowsWcag()
    {
        Assert.Equal(21, ProjectPalette.Contrast(0xff000000, 0xffffffff), 1);
        Assert.Equal(1, ProjectPalette.Contrast(0xff777777, 0xff777777), 3);
    }

    [Fact]
    public void Presets_AreValidAndDistinct()
    {
        foreach (var (id, _, colors) in ProjectPalette.Presets)
        {
            var palette = ProjectPalette.FromPreset(id);
            Assert.Equal(colors.Length, palette.Colors.Count);
        }
        Assert.Throws<ArgumentException>(() => ProjectPalette.FromPreset("nao-existe"));
    }

    [Fact]
    public void GplAndHexImport_AndGplExportRoundTrip()
    {
        var gpl = "GIMP Palette\nName: X\nColumns: 4\n#\n255   0   0\tFogo\n  0   0 255\tÁgua\n";
        var palette = ProjectPalette.Import(gpl, "Importada");
        Assert.Equal(["Fogo", "Agua"], palette.Colors.Select(c => c.Name));
        Assert.Equal(palette.Argb, ProjectPalette.Import(palette.ToGpl(), "y").Argb);
        Assert.Equal([0xff112233u, 0xffaabbccu], ProjectPalette.Import("#112233\naabbcc\n", "hex").Argb);
    }

    [Theory]
    [InlineData("cor do céu", "CorDoCeu")]
    [InlineData("1 fundo", "C1Fundo")]
    [InlineData("***", "Cor")]
    public void Identifier_IsValidCSharp(string text, string expected) =>
        Assert.Equal(expected, ProjectPalette.Identifier(text));

    [Fact]
    public void CSharpExport_CompilesAndGivesTheGameNamedColors()
    {
        var palette = new ProjectPalette { Name = "Jogo" };
        palette.Colors.Add(new PaletteSwatch("Céu", 0xff4f62a8));
        palette.Colors.Add(new PaletteSwatch("Céu", 0x80ffffff));
        palette.Colors.Add(new PaletteSwatch("All", 0xff000000));
        var code = palette.ToCSharp("meu jogo");
        Assert.Contains("public static readonly Color Ceu = new(79, 98, 168, 255);", code);
        Assert.Contains("Ceu2", code);
        Assert.Contains("All2", code);
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly)).Compile("palette_game",
        [
            new SourceFile("Data/GamePalette.cs", code),
            new SourceFile("Game.cs", """
                using Lunet; using Lunet.Graphics;
                namespace MeuJogo;
                public sealed class G : Game { protected override void Draw(GameTime t) => GraphicsDevice.Clear(GamePalette.Ceu); }
                """),
        ]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }

    [Fact]
    public void CSharpExport_WithoutNamespaceForGlobalGames()
    {
        var palette = new ProjectPalette();
        palette.AddRange([0xff000000]);
        Assert.DoesNotContain("namespace", palette.ToCSharp(null));
    }

    [Fact]
    public void AiStyle_TreatsThePaletteFileAsTheOfficialColors()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-palette-ai-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(root).Create("PaletaIa");
            var palette = new ProjectPalette();
            palette.Colors.Add(new PaletteSwatch("Ceu", 0xff4f62a8));
            project.WriteText(ProjectPalette.DefaultPath, palette.ToJson());
            project.WriteText("Data/GamePalette.cs", palette.ToCSharp(null));
            var style = LunetAiProjectStyle.Analyze(project);
            Assert.Equal([0xff4f62a8u], style.Palette);
            var text = style.Describe();
            Assert.Contains("PALETA OFICIAL", text);
            Assert.Contains("Ceu=#4F62A8", text);
            Assert.Contains("GamePalette.<Nome>", text);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
