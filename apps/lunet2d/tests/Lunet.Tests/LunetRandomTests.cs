using System.Buffers.Binary;
using System.Numerics;
using Lunet.Compiler;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.Runtime;
using Lunet.Storage;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace Lunet.Tests;

public sealed class LunetRandomTests
{
    [Theory]
    [InlineData(0UL, 0x99ec5f36cb75f2b4UL, 0xbf6e1f784956452aUL)]
    [InlineData(42UL, 0x15780b2e0c2ec716UL, 0x6104d9866d113a7eUL)]
    [InlineData(ulong.MaxValue, 0x8f5520d52a7ead08UL, 0xc476a018caa1802dUL)]
    public void XoshiroMatchesReference(ulong seed, ulong first, ulong second)
    {
        var rng = new RandomSource(seed);
        Assert.Equal(first, rng.NextUInt64());
        Assert.Equal(second, rng.NextUInt64());
        var automatic = new RandomSource();
        Assert.Equal(new RandomSource(automatic.Seed).NextUInt64(), automatic.NextUInt64());
    }

    [Theory]
    [InlineData(0, 0, 0U, 0U, 0UL, 0xe169c58d6627e8d5UL)]
    [InlineData(-1, -1, uint.MaxValue, uint.MaxValue, ulong.MaxValue, 0x41c83b0e408f276dUL)]
    [InlineData(0x243f6a88, unchecked((int)0x85a308d3U), 0x13198a2eU, 0x03707344U, 0x299f31d0a4093822UL, 0x94fdccebd16cfe09UL)]
    public void PhiloxMatchesRandom123(int x, int y, uint channel, uint sample, ulong seed, ulong word)
    {
        var field = new RandomField(seed);
        Assert.Equal(word, field.UInt64At(x, y, channel, sample));
        _ = field.UInt64At(y, x, sample, channel);
        Assert.Equal(word, field.UInt64At(x, y, channel, sample));
        Assert.Equal((word >> 40) / 16777216f, field.FloatAt(x, y, channel, sample));
        Assert.Equal((word >> 11) / 9007199254740992d, field.DoubleAt(x, y, channel, sample));
        Assert.Equal(word, field.StreamAt(x, y, channel, sample).Seed);
    }

    [Fact]
    public void RangesHandleSignedLimitsIeee754EdgesAndRejection()
    {
        var rng = new RandomSource(42);
        for (int i = 0; i < 200_000; i++)
        {
            Assert.InRange(rng.NextInt(int.MinValue, int.MaxValue), int.MinValue, int.MaxValue - 1);
            Assert.InRange(rng.NextInt64(long.MinValue, long.MaxValue), long.MinValue, long.MaxValue - 1);
            Assert.InRange(rng.NextDouble(-double.MaxValue, double.MaxValue), -double.MaxValue, Math.BitDecrement(double.MaxValue));
            Assert.InRange(rng.NextFloat(-float.MaxValue, float.MaxValue), -float.MaxValue, MathF.BitDecrement(float.MaxValue));
            Assert.Equal(1f, rng.NextFloat(1f, MathF.BitIncrement(1f)));
            Assert.Equal(1d, rng.NextDouble(1d, Math.BitIncrement(1d)));
            Assert.Equal(0f, rng.NextFloat(0, float.Epsilon));
            Assert.Equal(-double.Epsilon, rng.NextDouble(-double.Epsilon, 0));
        }
        var rejected = RandomSource.FromState(new RandomState(1, 42, 1, 0, 0, 0));
        var reference = rejected.Clone();
        Assert.Equal(0UL, reference.NextUInt64());
        _ = reference.NextUInt64();
        Assert.Equal(0, rejected.NextInt(3));
        Assert.Equal(reference.CaptureState(), rejected.CaptureState());
        for (int bound = 1; bound <= 255; bound++)
        {
            var counts = new int[bound];
            for (int x = 0; x <= 255; x++)
            {
                var product = x * bound;
                if ((product & 255) >= 256 % bound) counts[product >> 8]++;
            }
            Assert.Single(counts.Distinct());
        }
    }

    [Fact]
    public void InvalidInputsAndConstantResultsDoNotConsumeAWord()
    {
        var rng = new RandomSource(19);
        var before = rng.CaptureState();
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt64(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextFloat(float.NegativeInfinity, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextDouble(0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextPoint(new Point(0, 10), new Point(4, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextVector2(new Vector2(0, 10), new Vector2(4, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.InsideCircle(new Vector2(float.MaxValue, 0), float.Epsilon));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.OnCircle(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.InsideRectangle(new Rectangle(int.MaxValue, 0, 2, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.InsideRectangle(new RectangleF(1, 0, float.Epsilon, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Chance(20));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextGaussian(0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextExponential(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextTriangular(1, 4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextWeightedIndex([1, double.NaN]));
        Assert.Throws<ArgumentException>(() => rng.NextWeightedIndex([0, 0]));
        Assert.Throws<ArgumentException>(() => rng.Shuffle(Array.AsReadOnly(new[] { 1, 2 })));
        Assert.Throws<ArgumentException>(() => rng.PickWeighted(new[] { 1, 2 }, [1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.Sample(new[] { 1, 2 }, 3));
        Assert.Throws<ArgumentException>(() => rng.RestoreState(default));
        Assert.Throws<System.Text.EncoderFallbackException>(() => rng.Fork(new string((char)0xD800, 1)));
        Assert.Equal(0, rng.NextInt(0));
        Assert.Equal(10, rng.NextInt(10, 11));
        Assert.Equal(long.MinValue, rng.NextInt64(long.MinValue, long.MinValue + 1));
        Assert.Equal(2d, rng.NextGaussian(2, 0));
        Assert.Equal(1d, rng.NextTriangular(1, 1, 1));
        Assert.False(rng.Chance(0));
        Assert.True(rng.Chance(1));
        Assert.Equal(1, rng.NextWeightedIndex([0, double.MaxValue, 0]));
        Assert.Empty(rng.Sample(Array.Empty<int>(), 0));
        rng.NextBytes(Span<byte>.Empty);
        Assert.Equal(before, rng.CaptureState());
    }

    [Fact]
    public void CheckpointForkJumpAndBytesMatchTheSpecifiedEncoding()
    {
        var rng = new RandomSource(ulong.MaxValue);
        Span<byte> bytes = stackalloc byte[10];
        Span<byte> words = stackalloc byte[16];
        var reference = rng.Clone();
        BinaryPrimitives.WriteUInt64LittleEndian(words, reference.NextUInt64());
        BinaryPrimitives.WriteUInt64LittleEndian(words[8..], reference.NextUInt64());
        rng.NextBytes(bytes);
        Assert.True(bytes.SequenceEqual(words[..10]));
        Assert.Equal(reference.CaptureState(), rng.CaptureState());
        var storage = new SaveData(new MemorySaveStore());
        storage.Save("rng", rng.CaptureState());
        var restored = RandomSource.FromState(storage.Load("rng", default(RandomState)));
        Assert.Equal(rng.CaptureState(), restored.CaptureState());
        Assert.Equal(rng.NextUInt32(), restored.NextUInt32());

        rng = new RandomSource(42);
        var before = rng.CaptureState();
        Assert.Equal(0xf8ee84ed75ed25f2UL, rng.Fork("terrain").Seed);
        Assert.Equal(0xe4a79e9d114feb55UL, rng.Fork(7UL).Seed);
        Assert.NotEqual(rng.Fork("7").Seed, rng.Fork(7UL).Seed);
        Assert.NotEqual(rng.Fork("Terreno").Seed, rng.Fork("terreno").Seed);
        Assert.NotEqual(rng.Fork("é").Seed, rng.Fork("e\u0301").Seed);
        Assert.Equal(before, rng.CaptureState());
        rng.Jump();
        Assert.Equal(0x50086ef83cbf4f4aUL, rng.NextUInt64());
        Assert.Equal(0xf8ee84ed75ed25f2UL, rng.Fork("terrain").Seed);
        rng.RestoreState(before);
        rng.LongJump();
        Assert.Equal(0xa0a4cb7719d49439UL, rng.NextUInt64());
    }

    [Fact]
    public void CollectionsAndGeometryUseTheSpecifiedOrderAndRanges()
    {
        var rng = new RandomSource(42);
        var expected = rng.Clone();
        Assert.Equal(new Point(expected.NextInt(-20, 10), expected.NextInt(-10, 20)), rng.NextPoint(new Point(-20, -10), new Point(10, 20)));
        Assert.Equal(new Point(int.MaxValue, 0), rng.InsideRectangle(new Rectangle(int.MaxValue, 0, 1, 1)));
        Assert.Equal(new Vector2(expected.NextFloat(1, 2), expected.NextFloat(1, 2)), rng.NextVector2(1, 2));
        var word = expected.NextUInt64();
        Assert.Equal(unchecked(new Color((byte)(word >> 56), (byte)(word >> 48), (byte)(word >> 40), 7)), rng.NextColor(7));
        Assert.True(new RectangleF(-2, 10, 3, 4).Contains(rng.InsideRectangle(new RectangleF(-2, 10, 3, 4))));
        Assert.InRange(rng.OnCircle(5).Length(), 4.99999f, 5.00001f);
        Assert.InRange(rng.InsideCircle(5).Length(), 0f, 5.00001f);

        var original = Enumerable.Range(0, 12).ToArray();
        Assert.Equal(original, rng.Sample(original, original.Length).Order());
        Assert.Equal(new[] { 4, 4 }, rng.Sample(new[] { 4, 4 }, 2));
        var array = original.ToArray();
        var list = original.ToList();
        rng.Shuffle(array);
        rng.Shuffle(list);
        rng.Shuffle(array.AsSpan());
        Assert.Equal(original, array.Order());
        Assert.Equal(original, list.Order());
        Assert.Equal(Enumerable.Range(0, 12), original);
        object[] covariant = new[] { "x", "y" };
        rng.Shuffle(covariant);
        Assert.Equal(new object[] { "x", "y" }, covariant.OrderBy(x => x));
        Assert.Equal("B", rng.PickWeighted(new[] { "A", "B" }, [0, double.MaxValue]));
    }

    [Fact]
    public void FrequenciesAndMomentsMatchFixedSeedExpectations()
    {
        const int n = 100_000;
        var rng = new RandomSource(53);
        double[] weights = [70, 25, 0, 5];
        var loot = new int[4];
        var dice = new int[6];
        int heads = 0, belowOne = 0;
        double mean = 0, square = 0, exponential = 0, triangular = 0, radius = 0, angle = 0;
        for (int i = 0; i < n; i++)
        {
            loot[rng.NextWeightedIndex(weights)]++;
            dice[rng.NextInt(6)]++;
            if (rng.NextBool()) heads++;
            var normal = rng.NextGaussian();
            mean += normal;
            square += normal * normal;
            if (normal < 1) belowOne++;
            exponential += rng.NextExponential(2);
            triangular += rng.NextTriangular(0, 1, 5);
            radius += rng.InsideCircle().LengthSquared();
            angle += rng.NextDirection().X;
            Assert.InRange(rng.NextTriangular(-double.MaxValue, 0, double.MaxValue), -double.MaxValue, double.MaxValue);
        }
        Assert.All(dice, count => Assert.InRange(count, 15_800, 17_500));
        Assert.InRange(heads, 49_000, 51_000);
        Assert.InRange(loot[0], 69_000, 71_000);
        Assert.InRange(loot[1], 24_100, 25_900);
        Assert.Equal(0, loot[2]);
        Assert.InRange(loot[3], 4_550, 5_450);
        Assert.InRange(mean / n, -0.018, 0.018);
        Assert.InRange(square / n, 0.975, 1.025);
        Assert.InRange(belowOne, 83_500, 84_800);
        Assert.InRange(exponential / n, 0.49, 0.51);
        Assert.InRange(triangular / n, 1.97, 2.03);
        Assert.InRange(radius / n, 0.493, 0.507);
        Assert.InRange(angle / n, -0.014, 0.014);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void ShuffleReachesEverySmallPermutation(int n)
    {
        var rng = new RandomSource(113);
        var seen = new HashSet<int>();
        var items = new int[n];
        for (var i = 0; i < 24_000; i++)
        {
            for (var j = 0; j < n; j++) items[j] = j + 1;
            rng.Shuffle(items);
            seen.Add(items.Aggregate(0, (acc, value) => acc * 10 + value));
        }
        Assert.Equal(n == 3 ? 6 : 24, seen.Count);
    }

    [Theory]
    [InlineData("amplo")]
    [InlineData("essencial")]
    [InlineData("explicito")]
    public void CompilerAndLoaderResolveContextWithoutShadowingSystemRandom(string profile)
    {
        string player = profile switch
        {
            "amplo" => "public class Player { public ulong Seed() => Rng.Seed; }",
            "essencial" => "public class Player { public ulong Seed() => Context.Rng.Seed; }",
            _ => "using RngContext = Lunet.Context; public class Player { public ulong Seed() => RngContext.Rng.Seed; }",
        };
        var files = new List<SourceFile>
        {
            new("Player.cs", player),
            new("Game.cs", """
                public sealed class RngGame : Lunet.Game
                {
                    public RngGame() : base(42) {}
                    protected override void Initialize()
                    {
                        if (new Player().Seed() != 42 || Rng.Seed != 42 ||
                            !object.ReferenceEquals(Services.Get<Lunet.RandomSource>(), Rng) ||
                            object.ReferenceEquals(Rng, VisualRng) || new System.Random(42).Next() < 0)
                            throw new System.InvalidOperationException("RNG incorreto");
                    }
                }
                """)
        };
        if (profile != "explicito") files.Add(new SourceFile(ProjectImports.Path, ProjectImports.Source(profile == "amplo")));
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("rng_" + profile, files);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var game = loaded.Game;
        var visualBefore = game.VisualRng.CaptureState();
        var host = new GameHost(game, new HeadlessGraphicsBackend());
        Assert.True(host.Start(100, 100), host.Fault?.ToString());
        host.Pause();
        host.Resume();
        host.Stop();
        Assert.Equal(visualBefore, game.VisualRng.CaptureState());
        Assert.Throws<InvalidOperationException>(() => _ = Context.Rng);
    }

    [Fact]
    public void SampleRendersRepeatableCoinsCellsAndCircleThroughThePreviewBackend()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples", "random-coins"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var project = new ProjectStore(Path.Combine(dir.FullName, "samples")).Open("random-coins");
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("RandomCoinsPreview", project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToArray());
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        using var loaded = GameLoader.Load(result.Assembly!, result.Symbols);
        var graphics = new SoftwareGraphicsBackend(360, 640);
        var host = new GameHost(loaded.Game, graphics);
        Assert.True(host.Start(360, 640), host.Fault?.ToString());
        try
        {
            host.Tick(1d / 30);
            var before = new byte[360 * 640 * 4];
            graphics.CopyScreenRgba(before);
            Assert.True(graphics.QuadsDrawn > 150);
            var repeat = new Vector2(230, 590);
            host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Pressed, repeat)]);
            host.Tick(1d / 30);
            host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Released, repeat)]);
            host.Tick(1d / 30);
            var after = new byte[before.Length];
            graphics.CopyScreenRgba(after);
            Assert.True(before.AsSpan().SequenceEqual(after));
            Assert.False(host.IsFaulted, host.Fault?.ToString());
            if (Environment.GetEnvironmentVariable("LUNET_RANDOM_CAPTURE") is { Length: > 0 } image)
                File.WriteAllBytes(image, PngWriter.Encode(360, 640, after));
        }
        finally { host.Stop(); }
    }
}
