using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Configurações → Diagnóstico de toques (touch-debug.js): each touch is a line, green when the button got it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task TouchDebug_RegistersEachTouchAndReportsIt() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, _, dir) = await OpenCreateVault();
            try
            {
                view.SetTouchDebug(false);   // the device keeps it between runs
                Assert.False(view.TouchDebugOn);
                _ = view.ShowSettingsAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Para descobrir botões que não respondem", view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                ClickButtonWithText(view.Dialogs, "Diagnóstico de toques");
                for (int i = 0; i < 40 && !view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Diagnóstico ligado"); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(view.TouchDebugOn);
                ClickButtonWithText(view.Dialogs, "Ok");
                for (int i = 0; i < 40 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }

                // a touch on a button below the panel (the panel sits on top, as the 1.8.4's): the click reaches it
                var target = view.GetLogicalDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && ToolTip.GetTip(b) is string && !view.TouchDebug.IsLogicalAncestorOf(b))
                    .Select(b => (B: b, At: b.TranslatePoint(new Point(b.Bounds.Width / 2, b.Bounds.Height / 2), window) ?? new Point(-1, -1))).Where(x => x.At.Y > 120 && x.At.Y < 750).OrderByDescending(x => x.At.Y).First();
                // the panel names a button as a screen reader does: its name, else its tip
                var tip = Avalonia.Automation.AutomationProperties.GetName(target.B) is { Length: > 0 } name ? name : (string)ToolTip.GetTip(target.B)!;
                var at = target.At;
                window.MouseDown(at, MouseButton.Left);
                window.MouseUp(at, MouseButton.Left);
                for (int i = 0; i < 100 && view.TouchDebug.Lines.Count == 0; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var line = Assert.Single(view.TouchDebug.Lines);
                Assert.StartsWith("✓ Button “" + (tip.Length > 24 ? tip[..24] : tip) + "”", line);
                for (int i = 0; i < 5 && view.Dialogs.IsOpen; i++) window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Dispatcher.UIThread.RunJobs();

                Snapshot(window, "native-touch-debug.png");
                var report = view.TouchDebug.Report();
                Assert.StartsWith("Relatório de toques — Urbe nativo", report);
                Assert.Contains(line, report);

                // the × turns it off, on this device too
                ClickButtonWithText(view.TouchDebug, "×");
                Assert.False(view.TouchDebugOn);
                Assert.Null(view.Customization.Device.Get("urbe.touchDebug"));
                window.Close();
                return true;
            }
            finally
            {
                view.SetTouchDebug(false);
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
