using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Client.Plugins;

namespace Urbe.Client.Tests;

/// <summary>ADD-0021: native plugins with declared permissions; the Relógio (UC-46) is the first.</summary>
public sealed partial class HeadlessUiTests
{
    private sealed class GreedyPlugin : IUrbePlugin
    {
        public PluginManifest Manifest { get; } = new("guloso", "Guloso", "1.0", "Pede o que não declarou.", [PluginPermissions.Notices]);
        public Exception? Refused { get; private set; }
        public void Start(IPluginApi api)
        {
            api.Notice("olá");
            try { api.Notes(); } catch (UnauthorizedAccessException ex) { Refused = ex; }
        }
    }

    [Fact]
    public async Task NativePlugins_ClockRunsWithItsPermissionsAndOthersAreRefused() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            view.Customization.Device = new MemoryDeviceStore();
            try
            {
                bool HasButton(string name) => view.GetLogicalDescendants().OfType<Button>().Any(b => Avalonia.Automation.AutomationProperties.GetName(b) == name);
                view.SyncPlugins();
                Assert.False(HasButton("Relógio"));
                Assert.Contains(view.NativePlugins(), p => p.Manifest.Id == "relogio" && !p.On);

                // on: a button in the top bar, a panel over the city and the editor
                view.SetNativePlugin("relogio", true);
                Assert.True(HasButton("Relógio"));
                var clock = Assert.IsType<ClockPlugin>(view.RunningPlugin("relogio"));
                Assert.True(view.Search.Has("plugin.relogio.abrir-relogio"));   // the palette's "Comandos"
                var now = new DateTime(2026, 10, 14, 15, 0, 0);
                clock.Clock = () => now;
                view.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Relógio")
                    .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.True(clock.Panel!.IsOpen);
                clock.SetLength(5);
                clock.ToggleTimer();
                now = now.AddMinutes(2);
                clock.Update();
                Assert.Equal("03:00", clock.TimerText);
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-plugin-clock.png");
                now = now.AddMinutes(3);
                clock.Update();
                Assert.Equal("Temporizador: 5 min terminados.", view.NoticeText);
                Assert.Equal("5", view.Customization.Device.Get("urbe.plugin.relogio.minutos"));

                // the safe mode turns it off, and everything it added goes
                view.Customization.SafeMode = true;
                Assert.Null(view.RunningPlugin("relogio"));
                Assert.False(HasButton("Relógio"));
                view.Customization.SafeMode = false;
                Assert.NotNull(view.RunningPlugin("relogio"));
                view.SetNativePlugin("relogio", false);
                Assert.False(HasButton("Relógio"));
                Assert.False(view.Search.Has("plugin.relogio.abrir-relogio"));

                // a plugin that asks for what it did not declare is refused
                var greedy = new GreedyPlugin();
                view.AddPlugin(() => greedy);
                view.SetNativePlugin("guloso", true);
                Assert.Equal("olá", view.NoticeText);
                Assert.IsType<UnauthorizedAccessException>(greedy.Refused);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
