using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-81 contextual assistant: the phone keeps one navigation destination, and
/// the assistant is a persistent, accessible, quiet launcher over the workspace.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Dock_NotesCenteredWithAssistantFloatingInsteadOfSecondTab() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                await Pump(200);
                var text = view.GetLogicalDescendants().OfType<TextBlock>()
                    .First(t => t.Text == "Notas" && t.IsEffectivelyVisible &&
                        t.FindLogicalAncestorOfType<Button>() is { } b && b.Parent is not null && Grid.GetColumn(b) == 0);
                var button = text.FindLogicalAncestorOfType<Button>()!;
                Assert.InRange(button.Bounds.Width, 398, 402);
                var centre = text.TranslatePoint(new Point(text.Bounds.Width / 2, 0), view)!.Value.X;
                Assert.InRange(centre, 197, 203);

                var floating = view.AssistantLauncher;
                Assert.True(floating.IsEffectivelyVisible);
                Assert.Equal("Abrir Assistente", AutomationProperties.GetName(floating));
                Assert.Equal(UrbeTokens.MinTouch, floating.Width);
                Assert.Equal(UrbeTokens.MinTouch, floating.Height);
                Assert.False(view.AssistantMini.IsVisible);
                floating.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.True(view.Assistant.IsVisible);
                Assert.False(floating.IsVisible);
                view.Assistant.Close();
                Assert.True(floating.IsVisible);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
