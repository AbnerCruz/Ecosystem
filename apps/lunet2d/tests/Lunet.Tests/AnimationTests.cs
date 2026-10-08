using System.Numerics;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class AnimationTests
{
    private static Texture2D Texture() => Texture2D.CreateSolid(new GraphicsDevice(new RecordingBackend(), 360, 640), 64, 16, Color.White);
    private static SpriteAnimationClip Clip(Texture2D texture, bool loop = true) => new(texture,
        [new(new(0, 0, 16, 16), 0.125), new(new(16, 0, 16, 16), 0.375), new(new(32, 0, 16, 16), 0.5)], loop);

    [Fact]
    public void Clip_CopiesFrames_AndSupportsNonUniformRegionsAndDurations()
    {
        using var texture = Texture();
        SpriteAnimationFrame[] frames = [new(new(0, 0, 16, 16), 0.125), new(new(16, 0, 32, 8), 0.375)];
        var clip = new SpriteAnimationClip(texture, frames);
        frames[0] = default;
        Assert.Equal(0.5, clip.DurationSeconds);
        Assert.Equal(16, clip.Frame(0).Source.Width);
        Assert.Equal(32, clip.Frame(1).Source.Width);
        Assert.Same(texture, clip.Texture);
        var sheet = new SpriteSheet(texture, 16, 16);
        var ordered = SpriteAnimationClip.FromSheet(sheet, [3, 0, 3], 0.25, false);
        Assert.Equal(sheet.Frame(3), ordered.Frame(0).Source);
        Assert.Equal(sheet.Frame(0), ordered.Frame(1).Source);
        Assert.Equal(0.75, ordered.DurationSeconds);
        Assert.False(ordered.Loop);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.124, 0)]
    [InlineData(0.125, 1)]
    [InlineData(0.499, 1)]
    [InlineData(0.5, 2)]
    [InlineData(0.999, 2)]
    [InlineData(1, 0)]
    [InlineData(1000000.5, 2)]
    public void Animator_SamplesExactBoundariesAndLargeDeltas(double delta, int frame)
    {
        using var texture = Texture();
        var animator = new SpriteAnimator(Clip(texture));
        animator.Update(delta);
        Assert.Equal(frame, animator.FrameIndex);
        Assert.Equal(animator.Clip.Frame(frame).Source, animator.Source);
        Assert.False(animator.IsComplete);
    }

    [Fact]
    public void Animator_OneShotHoldsLastFrame_AndPauseRestartAndSwitchAreIndependent()
    {
        using var texture = Texture();
        var clip = Clip(texture, false);
        var a = new SpriteAnimator(clip);
        var b = new SpriteAnimator(clip);
        a.Update(0.25);
        a.Pause(); a.Update(100);
        Assert.Equal(0.25, a.PositionSeconds);
        Assert.False(a.IsComplete);
        a.Resume(); a.Update(double.MaxValue);
        Assert.True(a.IsComplete); Assert.False(a.IsPlaying);
        Assert.Equal(2, a.FrameIndex); Assert.Equal(1, a.PositionSeconds);
        a.Resume(); Assert.False(a.IsPlaying);
        a.Restart(); Assert.True(a.IsPlaying); Assert.Equal(0, a.FrameIndex);
        a.Update(0.25); a.Play(clip);
        Assert.Equal(0.25, a.PositionSeconds); // repetir Play não prende a animação no primeiro quadro
        a.Play(clip, true); Assert.Equal(0, a.PositionSeconds);
        a.Play(Clip(texture)); Assert.True(a.Clip.Loop);
        Assert.Equal(0, b.PositionSeconds);
    }

    [Fact]
    public void InvalidClipsAndDeltasFailBeforeChangingPlayback()
    {
        using var texture = Texture();
        Assert.Throws<ArgumentException>(() => new SpriteAnimationClip(texture, []));
        foreach (var seconds in new[] { 0, -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentException>(() => new SpriteAnimationClip(texture, [new(new(0, 0, 16, 16), seconds)]));
        RectangleF[] invalid = [new(-1, 0, 16, 16), new(0, 0, 0, 16), new(0, 0, float.NaN, 16), new(60, 0, 16, 16)];
        foreach (var region in invalid)
            Assert.Throws<ArgumentException>(() => new SpriteAnimationClip(texture, [new(region, 1)]));
        Assert.Throws<ArgumentException>(() => new SpriteAnimationClip(texture,
            [new(new(0, 0, 16, 16), double.MaxValue), new(new(0, 0, 16, 16), 1)]));
        var animator = new SpriteAnimator(Clip(texture));
        animator.Update(0.25);
        foreach (var delta in new[] { -1, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => animator.Update(delta));
        Assert.Throws<ArgumentNullException>(() => animator.Play(null!));
        Assert.Equal(0.25, animator.PositionSeconds); Assert.Equal(1, animator.FrameIndex);
    }

    [Fact]
    public void PlaybackRateDoesNotDependOnUpdateFrequency_AndHugeDurationsDoNotOverflow()
    {
        using var texture = Texture();
        var a = new SpriteAnimator(Clip(texture));
        var b = new SpriteAnimator(a.Clip);
        for (int i = 0; i < 120; i++) a.Update(1.0 / 120);
        for (int i = 0; i < 30; i++) b.Update(1.0 / 30);
        // Somar ponto flutuante pode chegar imediatamente antes do wrap: comparar tempo circular.
        Assert.True(Math.Min(Math.Abs(a.PositionSeconds - b.PositionSeconds), 1 - Math.Abs(a.PositionSeconds - b.PositionSeconds)) < 1e-12);
        var huge = new SpriteAnimator(new SpriteAnimationClip(texture, [new(new(0, 0, 16, 16), double.MaxValue)]));
        huge.Update(double.MaxValue * 0.75); huge.Update(double.MaxValue * 0.75);
        Assert.True(double.IsFinite(huge.PositionSeconds));
        Assert.InRange(huge.PositionSeconds / double.MaxValue, 0.49, 0.51);
    }

    [Theory]
    [InlineData(Ease.Linear, 0.25)]
    [InlineData(Ease.InQuad, 0.0625)]
    [InlineData(Ease.OutQuad, 0.4375)]
    [InlineData(Ease.InOutQuad, 0.125)]
    [InlineData(Ease.SmoothStep, 0.15625)]
    public void Tween_CurvesHaveCorrectEndpointsAndMonotonicProgress(Ease ease, float quarter)
    {
        var tween = new Tween(1, ease);
        Assert.Equal(0, tween.Amount);
        tween.Update(0.25); Assert.Equal(quarter, tween.Amount, 6);
        float previous = tween.Amount;
        for (int i = 0; i < 100; i++)
        {
            tween.Update(0.01);
            Assert.InRange(tween.Amount, previous, 1);
            previous = tween.Amount;
        }
        Assert.Equal(1, tween.Amount); Assert.True(tween.IsComplete); Assert.False(tween.IsPlaying);
    }

    [Fact]
    public void Tween_InterpolatesScalarsVectorsAndRgba_WithoutEndpointOverflow()
    {
        var tween = new Tween(2);
        Assert.Equal(-float.MaxValue, tween.Value(-float.MaxValue, float.MaxValue));
        tween.Update(1);
        Assert.Equal(0, tween.Value(-float.MaxValue, float.MaxValue));
        Assert.Equal(new Vector2(5, 15), tween.Value(Vector2.Zero, new Vector2(10, 30)));
        Assert.Equal(new Color(128, 10, 128, 128), tween.Value(new Color(0, 0, 255, 0), new Color(255, 20, 0, 255)));
        tween.Update(double.MaxValue);
        Assert.Equal(float.MaxValue, tween.Value(-float.MaxValue, float.MaxValue));
        Assert.Equal(Color.Blue, tween.Value(Color.Red, Color.Blue));
    }

    [Fact]
    public void Tween_PauseResumeRestartZeroDurationAndInvalidInputs()
    {
        var tween = new Tween(1);
        tween.Update(0.25); tween.Pause(); tween.Update(10);
        Assert.Equal(0.25, tween.ElapsedSeconds);
        tween.Resume(); tween.Update(10); tween.Resume(); Assert.False(tween.IsPlaying);
        tween.Restart(); Assert.True(tween.IsPlaying); Assert.Equal(0, tween.Progress);
        foreach (var delta in new[] { -1, double.NaN, double.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => tween.Update(delta));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tween(delta));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tween(1, (Ease)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => tween.Value(float.NaN, 1));
        var instant = new Tween(0, Ease.SmoothStep);
        Assert.True(instant.IsComplete); Assert.False(instant.IsPlaying);
        Assert.Equal(1, instant.Progress); Assert.Equal(5, instant.Value(0, 5));
        instant.Restart(); instant.Resume(); Assert.False(instant.IsPlaying);
    }

    [Fact]
    public void AnimatedFramesReachTheExistingSpriteBatchWithCorrectUvsAndTint()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture2D.CreateSolid(device, 64, 16, Color.White);
        var animator = new SpriteAnimator(Clip(texture));
        var batch = new SpriteBatch(device);
        for (int i = 0; i < 3; i++)
        {
            batch.Begin();
            batch.Draw(texture, new Vector2(20, 30), animator.Source, Color.Green);
            batch.End();
            var vertices = backend.Batches[^1].Vertices;
            Assert.Equal(new Vector2(i * 0.25f, 0), vertices[0].TexCoord);
            Assert.Equal(new Vector2((i + 1) * 0.25f, 1), vertices[2].TexCoord);
            Assert.Equal(new Vector2(20, 30), vertices[0].Position);
            Assert.Equal(new Vector2(36, 46), vertices[2].Position);
            Assert.All(vertices, v => Assert.Equal(Color.Green.PackedRgba, v.Color));
            animator.Update(i == 0 ? 0.125 : 0.375);
        }
    }

    [Fact]
    public void RoundingToTheEndpointAlsoStopsPlayback()
    {
        using var texture = Texture();
        var animator = new SpriteAnimator(Clip(texture, false));
        var tween = new Tween(1);
        animator.Update(0.5); tween.Update(0.5);
        animator.Update(Math.BitDecrement(0.5)); tween.Update(Math.BitDecrement(0.5));
        Assert.True(animator.IsComplete); Assert.False(animator.IsPlaying);
        Assert.True(tween.IsComplete); Assert.False(tween.IsPlaying);
    }

    [Fact]
    public void PlaybackAndTweenDoNotAllocateInTheFrameLoop()
    {
        using var texture = Texture();
        var animator = new SpriteAnimator(Clip(texture));
        var tween = new Tween(1, Ease.InOutQuad);
        void Frame()
        {
            animator.Update(1.0 / 60); _ = animator.Source;
            if (tween.IsComplete) tween.Restart();
            tween.Update(1.0 / 60); _ = tween.Value(Vector2.Zero, Vector2.One); _ = tween.Value(Color.Red, Color.Blue);
        }
        for (int i = 0; i < 1000; i++) Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void DemoTemplateAndOfflineGuideCompileAndRunTouchPauseRetargetAndRestart()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "docs/guides/animacao.md"))) root = root.Parent;
        Assert.NotNull(root);
        var code = File.ReadAllText(Path.Combine(root!.FullName, "docs/guides/animacao.md")).Split("```csharp\n")[1].Split("```")[0];
        Assert.Equal(code.Trim(), ProjectTemplates.AnimationSource("AnimationDemo").Trim());
        var projectRoot = Path.Combine(Path.GetTempPath(), "lunet-animation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(projectRoot).Create("AnimationDemo", ProjectTemplate.Animation);
            Assert.Equal(code.Trim(), project.ReadText("Game.cs").Trim());
            var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var result = compiler.Compile("AnimationDemo", [new Lunet.Compiler.SourceFile("Game.cs", code)]);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
            using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
            var backend = new RecordingBackend();
            var host = new GameHost(loaded.Game, backend);
            Assert.True(host.Start(720, 1280)); // toque físico 2×, coordenadas virtuais 360×640
            void Frame(int n = 1) { for (int i = 0; i < n; i++) host.Tick(1.0 / 60); }
            T Field<T>(string name) => (T)loaded.Game.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(loaded.Game)!;
            void Tap(float x, float y)
            {
                host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Moved, new Vector2(x, y) * 2)]); Frame();
                host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Released, new Vector2(x, y) * 2)]); Frame(2);
                host.SetSurfaceTouches([]); Frame(2);
            }
            Frame(); Tap(280, 400); Frame(60);
            Assert.Equal(new Vector2(280, 400), Field<Vector2>("position"));
            Tap(80, 64); var index = Field<SpriteAnimator>("animator").FrameIndex;
            var pos = Field<Vector2>("position");
            Tap(70, 200); Frame(60);
            Assert.Equal(pos, Field<Vector2>("position"));
            Assert.Equal(index, Field<SpriteAnimator>("animator").FrameIndex);
            Tap(80, 64); Frame(60);
            Assert.Equal(new Vector2(70, 200), Field<Vector2>("position"));
            Tap(270, 64); Assert.Equal(new Vector2(180, 320), Field<Vector2>("position"));
            Assert.True(Field<SpriteAnimator>("animator").IsPlaying);
            Assert.NotEmpty(backend.Batches); Assert.False(host.IsFaulted, host.Fault?.ToString());
        }
        finally { if (Directory.Exists(projectRoot)) Directory.Delete(projectRoot, true); }
    }
}
