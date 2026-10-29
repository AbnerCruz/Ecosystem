using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.Plugins;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task NativePlugins_ClockIsReachableFromTheEditorOnAPhone() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-052: "o relógio não apareceu no editor" — on a phone-wide window the button is in the editor's bar, whole,
        // and the panel opens over the editor
        var dir = Path.Combine(Path.GetTempPath(), "urbe-clock-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Um título de nota bastante comprido para o cabeçalho.md"), "# Nota\n\ntexto\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        view.Customization.Device = new MemoryDeviceStore();
        var window = new Window { Width = 360, Height = 760, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.SetNativePlugin("relogio", true);
            view.OpenNoteByPath("Um título de nota bastante comprido para o cabeçalho.md");
            await Settle(window, view);
            var button = view.Editor.PluginButtons.Children.OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Relógio");
            Assert.True(button.IsEffectivelyVisible);
            var at = button.TranslatePoint(new Point(0, 0), window);
            Assert.NotNull(at);
            Assert.True(at!.Value.X >= 0 && at.Value.X + button.Bounds.Width <= window.Width + 0.5, "button at " + at + " width " + button.Bounds.Width);
            Assert.True(button.Bounds.Width >= 40);
            // CST-105/106: with the plugin's button there, the note's title is still readable on a phone
            var title = view.Editor.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Um título de nota bastante comprido para o cabeçalho.md");
            Assert.True(title.Bounds.Width >= 70, "title width " + title.Bounds.Width);   // it had none
            // and Visual/Fonte is still one tap away, as one button
            var toggle = view.Editor.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Mudar para Fonte");
            Assert.True(toggle.IsEffectivelyVisible);
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(EditorMode.Fonte, view.Editor.Mode);
            Assert.Equal("Mudar para Visual", Avalonia.Automation.AutomationProperties.GetName(toggle));
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(EditorMode.Visual, view.Editor.Mode);
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            var clock = Assert.IsType<ClockPlugin>(view.RunningPlugin("relogio"));
            Assert.True(clock.Panel!.IsOpen);
            Snapshot(window, "native-plugin-clock-editor-phone.png");
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
