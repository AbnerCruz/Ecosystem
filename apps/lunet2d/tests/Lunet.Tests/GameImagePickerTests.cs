using Lunet.Compiler;
using Lunet.Content;

namespace Lunet.Tests;

/// <summary>LUNET-508: photos picked inside a game arrive upright, and the picker contract is consumed once.</summary>
public sealed class GameImagePickerTests
{
    // 3×2 image; each pixel's R channel is its id (row-major): 0 1 2 / 3 4 5.
    private static byte[] Sample()
    {
        var rgba = new byte[3 * 2 * 4];
        for (int i = 0; i < 6; i++) { rgba[i * 4] = (byte)i; rgba[i * 4 + 3] = 255; }
        return rgba;
    }

    private static int[] Ids(byte[] rgba) => Enumerable.Range(0, rgba.Length / 4).Select(i => (int)rgba[i * 4]).ToArray();

    [Theory]
    [InlineData(1, 3, 2, new[] { 0, 1, 2, 3, 4, 5 })]
    [InlineData(2, 3, 2, new[] { 2, 1, 0, 5, 4, 3 })]
    [InlineData(3, 3, 2, new[] { 5, 4, 3, 2, 1, 0 })]
    [InlineData(4, 3, 2, new[] { 3, 4, 5, 0, 1, 2 })]
    [InlineData(5, 2, 3, new[] { 0, 3, 1, 4, 2, 5 })]
    [InlineData(6, 2, 3, new[] { 3, 0, 4, 1, 5, 2 })]
    [InlineData(7, 2, 3, new[] { 5, 2, 4, 1, 3, 0 })]
    [InlineData(8, 2, 3, new[] { 2, 5, 1, 4, 0, 3 })]
    [InlineData(0, 3, 2, new[] { 0, 1, 2, 3, 4, 5 })]
    [InlineData(99, 3, 2, new[] { 0, 1, 2, 3, 4, 5 })]
    public void ExifOrientation_TurnsTheImageUpright(int orientation, int width, int height, int[] expected)
    {
        var (w, h, rgba) = ImageOrientation.Apply(3, 2, Sample(), orientation);
        Assert.Equal(width, w);
        Assert.Equal(height, h);
        Assert.Equal(expected, Ids(rgba));
        Assert.All(Enumerable.Range(0, 6), i => Assert.Equal(255, rgba[i * 4 + 3]));
    }

    [Fact]
    public void ExifOrientation_RejectsMismatchedPixels() =>
        Assert.Throws<ArgumentException>(() => ImageOrientation.Apply(3, 2, new byte[5], 6));

    [Fact]
    public void Picker_IsFoundThroughServices_AndEachResultIsConsumedOnce()
    {
        var services = new GameServices();
        Assert.False(services.TryGet<IGameImagePicker>(out _)); // host without picker: games must check
        var picker = new FakePicker();
        services.Add<IGameImagePicker>(picker);
        Assert.True(services.TryGet<IGameImagePicker>(out var found));
        Assert.True(found.CanPick);

        Assert.False(found.TryReceive(out _));
        found.Pick();
        Assert.True(found.TryReceive(out var image));
        Assert.Equal(2, image.Width);
        Assert.NotNull(image.Rgba);
        Assert.False(found.TryReceive(out _));

        picker.CancelNext = true;
        found.Pick();
        Assert.True(found.TryReceive(out var cancelled));
        Assert.Null(cancelled.Rgba);
        Assert.Null(cancelled.Error);
    }

    private sealed class FakePicker : IGameImagePicker
    {
        private PickedGameImage? _pending;
        public bool CancelNext { get; set; }
        public bool CanPick => true;
        public void Pick() => _pending = CancelNext ? new PickedGameImage(0, 0, null) : new PickedGameImage(2, 1, new byte[8]);
        public bool TryReceive(out PickedGameImage result)
        {
            result = _pending!;
            _pending = null;
            return result is not null;
        }
    }

    [Fact]
    public void PhotoGuide_ExampleCompilesAsAGame()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "guides", "fotos-no-jogo.md"))) dir = dir.Parent;
        var guide = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "guides", "fotos-no-jogo.md"));
        int start = guide.IndexOf("```csharp\n", StringComparison.Ordinal) + "```csharp\n".Length;
        var code = guide[start..guide.IndexOf("\n```", start, StringComparison.Ordinal)];
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Lunet.Game).Assembly)).Compile("guide_photo", [new SourceFile("Game.cs", code)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }
}
