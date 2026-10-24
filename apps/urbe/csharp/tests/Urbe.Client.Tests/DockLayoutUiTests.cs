using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>UC-81 contextual assistant: the phone keeps its Notes destination and an independent 48dp AI launcher.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Dock_NotesAndGraphsWithFloatingAssistantAreAccessibleOnPhone() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                await Pump(200);
                var notes = view.GetLogicalDescendants().OfType<Button>()
                    .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Notas" &&
                        b.IsEffectivelyVisible && b.Bounds.Width >= 198);
                Assert.True(notes.IsEffectivelyVisible);
                Assert.InRange(notes.Bounds.Width, 198, 202);
                var graphs = view.GetLogicalDescendants().OfType<Button>()
                    .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Grafos" &&
                        b.IsEffectivelyVisible && b.Bounds.Width >= 198);
                Assert.InRange(graphs.Bounds.Width, 198, 202);
                var launcher = view.AssistantLauncher;
                Assert.True(launcher.IsEffectivelyVisible);
                Assert.Equal(UrbeTokens.MinTouch, launcher.Bounds.Width);
                Assert.Equal(UrbeTokens.MinTouch, launcher.Bounds.Height);
                Assert.Equal("Abrir Assistente", Avalonia.Automation.AutomationProperties.GetName(launcher));
                var center = launcher.TranslatePoint(
                    new Point(launcher.Bounds.Width / 2, launcher.Bounds.Height / 2), view);
                Assert.NotNull(center);
                Assert.InRange(center.Value.X, 340, 390);
                Assert.True(center.Value.Y > 100);
                Assert.False(view.AssistantMini.IsVisible);
                Assert.DoesNotContain(view.GetLogicalDescendants().OfType<Button>(),
                    b => Avalonia.Automation.AutomationProperties.GetName(b) == "Assistente" && b.IsEffectivelyVisible);

                launcher.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.True(view.Assistant.IsVisible);
                Assert.False(launcher.IsVisible);
                view.Assistant.Close();
                Assert.True(launcher.IsVisible);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
