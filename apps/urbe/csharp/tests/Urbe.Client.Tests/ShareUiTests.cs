using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-036: a file or a note of the vault is shared with another app where the device can; an open note is saved first.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Share_AFileAndASavedNoteGoToAnotherApp() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Aula.md", "texto\n"));
        File.WriteAllBytes(Path.Combine(vault.Folder, "relatorio.pdf"), "%PDF-1.7"u8.ToArray());
        var view = vault.View;
        await view.OpenVaultAsync();
        await Settle(vault.Window, view);

        // a file: from its actions
        _ = view.ShowItemSheetAsync("relatorio.pdf");
        for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        ClickButtonWithText(view.Dialogs, "Compartilhar");
        for (int i = 0; i < 100 && vault.Storage.Shared.Count == 0; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.Equal(["relatorio.pdf"], vault.Storage.Shared);

        // a note with text not yet saved: saved, then shared
        view.OpenNoteByPath("Aula.md");
        view.Editor.SetMode(EditorMode.Fonte);
        await Settle(vault.Window, view);
        view.Editor.Editor.Text = "texto\n\nmais uma linha\n";
        var more = view.Editor.GetLogicalDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Mais ações");
        more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        ClickButtonWithText(view.ActionSheet, "Compartilhar");
        for (int i = 0; i < 200 && vault.Storage.Shared.Count < 2; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.Equal("Aula.md", vault.Storage.Shared[^1]);
        Assert.Equal("texto\n\nmais uma linha\n", vault.Read("Aula.md"));
        return true;
    }, CancellationToken.None));
}
