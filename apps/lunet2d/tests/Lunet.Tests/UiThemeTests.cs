using Lunet.Graphics;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class UiThemeTests
{
    [Fact]
    public void PresetsHaveOpaqueSurfacesAndDistinctPressedDisabledStates()
    {
        foreach (var theme in new[] { UiTheme.Dark, UiTheme.Light, UiTheme.HighContrast })
        {
            Assert.Equal(255, theme.Background.A);
            Assert.Equal(255, theme.Panel.A);
            Assert.Equal(255, theme.Text.A);
            Assert.NotEqual(theme.Button.Background, theme.Button.PressedBackground);
            Assert.NotEqual(theme.Button.Background, theme.Button.DisabledBackground);
            Assert.NotEqual(theme.Slider.Knob, theme.Slider.PressedKnob);
            Assert.NotEqual(theme.Slider.Track, theme.Slider.DisabledTrack);
        }
        Assert.NotEqual(UiTheme.Dark.Background, UiTheme.Light.Background);
        Assert.NotEqual(UiTheme.Light.Panel, UiTheme.HighContrast.Panel);
        Assert.Equal(Color.Black, UiTheme.HighContrast.Button.Foreground);
        Assert.Equal(Color.White, UiTheme.HighContrast.Text);
    }

    [Fact]
    public void CustomThemePreservesAllGivenStylesWithoutGlobalMutation()
    {
        var initial = UiTheme.Dark;
        var button = TouchButtonStyle.Default;
        var slider = TouchSliderStyle.Default;
        var theme = new UiTheme(Color.Red, Color.Green, Color.Blue, button, slider);
        Assert.Equal(Color.Red, theme.Background);
        Assert.Equal(Color.Green, theme.Panel);
        Assert.Equal(Color.Blue, theme.Text);
        Assert.Equal(button.PressedBackground, theme.Button.PressedBackground);
        Assert.Equal(slider.DisabledKnob, theme.Slider.DisabledKnob);
        Assert.Equal(initial.Background, UiTheme.Dark.Background);
        Assert.Equal(initial.Button.PressedBackground, UiTheme.Dark.Button.PressedBackground);
    }

    [Fact]
    public void PresetsCanDrawActualButtonAndSliderWithoutOwningResources()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        var batch = new SpriteBatch(device);
        var font = SpriteFont.CreateDefault(device);
        var button = new TouchButton(new RectangleF(15, 30, 180, 45));
        var slider = new TouchSlider(new RectangleF(15, 95, 180, 45), value: 0.5f);

        foreach (var theme in new[] { UiTheme.Dark, UiTheme.Light, UiTheme.HighContrast })
        {
            backend.Batches.Clear();
            batch.Begin();
            batch.FillRect(new RectangleF(0, 0, 360, 640), theme.Background);
            button.Draw(batch, font, "OK", theme.Button);
            slider.Draw(batch, theme.Slider);
            batch.End();
            var vertices = backend.Batches.SelectMany(b => b.Vertices).ToArray();
            Assert.Contains(vertices, v => v.Color == theme.Background.PackedRgba);
            Assert.Contains(vertices, v => v.Color == theme.Button.Background.PackedRgba);
            Assert.Contains(vertices, v => v.Color == theme.Slider.Fill.PackedRgba);
        }
        button.IsEnabled = false;
        slider.IsEnabled = false;
        var disabledTheme = UiTheme.Dark;
        backend.Batches.Clear();
        batch.Begin();
        button.Draw(batch, font, "OK", disabledTheme.Button);
        slider.Draw(batch, disabledTheme.Slider);
        batch.End();
        var disabledVertices = backend.Batches.SelectMany(b => b.Vertices).ToArray();
        Assert.Contains(disabledVertices, v => v.Color == disabledTheme.Button.DisabledBackground.PackedRgba);
        Assert.Contains(disabledVertices, v => v.Color == disabledTheme.Slider.DisabledTrack.PackedRgba);
    }

    [Fact]
    public void ThemeSelectionAndDrawingDoNotAllocateAfterWarmup()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 200, 200);
        var batch = new SpriteBatch(device);
        var font = SpriteFont.CreateDefault(device);
        var button = new TouchButton(new RectangleF(10, 20, 150, 45));
        var slider = new TouchSlider(new RectangleF(10, 90, 150, 45));
        void Frame()
        {
            var theme = UiTheme.Dark;
            batch.Begin();
            batch.FillRect(new RectangleF(0, 0, 200, 200), theme.Panel);
            button.Draw(batch, font, "Jogar", theme.Button);
            slider.Draw(batch, theme.Slider);
            batch.End();
        }
        for (int i = 0; i < 200; i++) Frame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
