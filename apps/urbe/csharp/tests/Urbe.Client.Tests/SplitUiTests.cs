using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>editor.split: another note beside the open one, read only; swap and close as the 1.8.4.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Split_ShowsAnotherNoteBesideTheEditor() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                view.OpenNoteByPath("Comece.md");
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Editor.IsVisible);
                Assert.False(view.Search.Execute("editor.split.close"));   // nothing to close yet

                // Abrir em painel dividido: "Abrir ao lado" lists the other notes
                Assert.True(view.Search.Execute("editor.split.open"));
                for (int i = 0; i < 40 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Abrir ao lado", texts);
                Assert.Contains("Bairro/Um.md", texts);
                Assert.DoesNotContain("Comece.md", texts);
                ClickButtonWithText(view.Dialogs, "Bairro/Um.md");
                for (int i = 0; i < 40 && !view.Editor.Split.IsVisible; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(view.Editor.Split.IsVisible);
                Assert.Equal("Bairro/Um.md", view.Editor.Split.Path);
                Assert.Contains("Um", view.Editor.Split.ShownText);
                Assert.Equal((true, "Comece.md", "Bairro/Um.md"), view.SplitState);
                window.UpdateLayout();
                Assert.InRange(view.Editor.Split.Width, 200, 1280);
                Snapshot(window, "native-editor-split.png");

                // Trocar painéis (⇄): primary and secondary change places
                ClickButtonWithText(view.Editor.Split, "⇄");
                Assert.Equal((true, "Bairro/Um.md", "Comece.md"), view.SplitState);
                Assert.Equal("Comece.md", view.Editor.Split.Path);

                // Fechar (×)
                ClickButtonWithText(view.Editor.Split, "×");
                Assert.False(view.Editor.Split.IsVisible);
                Assert.False(view.SplitState.Enabled);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
