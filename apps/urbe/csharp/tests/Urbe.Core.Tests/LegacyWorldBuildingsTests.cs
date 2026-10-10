using System.Security.Cryptography;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class LegacyWorldBuildingsTests
{
    [Theory]
    [InlineData("hills", "temperate")]
    [InlineData("forest", "temperate")]
    [InlineData("taiga", "cold")]
    [InlineData("tundra", "cold")]
    [InlineData("snow", "cold")]
    [InlineData("desert", "dry")]
    [InlineData("savanna", "dry")]
    [InlineData("steppe", "dry")]
    [InlineData("beach", "coast")]
    [InlineData("swamp", "wet")]
    public void BiomeMaterialMatchesOriginalLegacyChoice(string biome, string expected)
    {
        Assert.Equal(expected, LegacyWorldBuildings.StyleForBiome(biome));
    }

    [Fact]
    public void SevenOriginalBuildingsRenderAcrossFiveBiomesAndThreeRoofVariants()
    {
        Assert.Equal(
            new[] {"house", "tower", "hall", "workshop", "market", "store", "dyer"},
            LegacyWorldBuildings.BuildingKinds.ToArray());
        string[] styles = ["temperate", "cold", "dry", "coast", "wet"];
        foreach (var style in styles)
        foreach (var kind in LegacyWorldBuildings.BuildingKinds)
        for (var variant = 0; variant < 3; variant++)
        {
            var rgba = LegacyWorldBuildings.CreateBuilding(kind, style, variant);
            Assert.Equal(48 * 56 * 4, rgba.Length);
            Assert.Contains(rgba.Where((_, i) => i % 4 == 3), alpha => alpha > 0);
            Assert.Equal(SHA256.HashData(rgba),
                SHA256.HashData(LegacyWorldBuildings.CreateBuilding(kind,style,variant)));
            var png = LegacyWorldSprites.EncodePng(rgba,48,56);
            Assert.Equal(new byte[] {137,80,78,71,13,10,26,10},png[..8]);
        }
    }

    [Fact]
    public void TemperateHousePreservesRoofDoorAndLitWindows()
    {
        var house = LegacyWorldBuildings.CreateBuilding("house", "temperate");
        Assert.Equal(new byte[]{0xcf,0x6d,0x4f,255},Pixel(house,24,6));
        Assert.Equal(new byte[]{0x55,0x3a,0x24,255},Pixel(house,24,40));
        Assert.Equal(new byte[]{0xf3,0xc8,0x65,255},Pixel(house,10,34));
    }

    [Fact]
    public void OptionalFlowersAffectOnlyTheRequestedVariant()
    {
        var original = LegacyWorldBuildings.CreateBuilding("house","temperate",0);
        var floral = LegacyWorldBuildings.CreateBuilding("house","temperate",0,true);
        Assert.False(original.SequenceEqual(floral));
        Assert.Equal(original, LegacyWorldBuildings.CreateBuilding("house","temperate",0));
    }

    [Fact]
    public void RejectsUnknownNamesAndUnboundedVariants()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LegacyWorldBuildings.CreateBuilding(null!,"temperate"));
        Assert.Throws<ArgumentException>(() =>
            LegacyWorldBuildings.CreateBuilding("skyscraper","temperate"));
        Assert.Throws<ArgumentException>(() =>
            LegacyWorldBuildings.CreateBuilding("house","fictional"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyWorldBuildings.CreateBuilding("house","dry",-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LegacyWorldBuildings.CreateBuilding("house","dry",256));
    }

    private static byte[] Pixel(byte[] rgba,int x,int y) =>
        rgba.AsSpan((y * 48 + x) * 4,4).ToArray();
}
