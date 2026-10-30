using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>DEC-0059: "Abrir ao lado" reaches any note of the vault, not only the 60 most recent.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Split_AnOldNoteIsReachedThroughTheSearch() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var notes = Enumerable.Range(1, 70).Select(i => ($"Nota {i:00}.md", $"# Nota {i:00}\n")).ToArray();
        using var vault = await OpenFaultyVaultAsync([("Antiga.md", "# Antiga\n"), .. notes]);
        // the oldest note of all
        File.SetLastWriteTimeUtc(Path.Combine(vault.Folder, "Antiga.md"), new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var view = vault.View;
        await view.OpenVaultAsync();
        await Settle(vault.Window, view);
        view.OpenNoteByPath("Nota 01.md");
        await Settle(vault.Window, view);
        var choosing = view.ChooseSplitAsync();
        for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        ClickButtonWithText(view.Dialogs, "Buscar entre todas as notas…");
        await choosing;
        Assert.True(view.Search.QuickOpen.IsOpen);
        view.Search.QuickOpen.Input.Text = "Antiga";
        Dispatcher.UIThread.RunJobs();
        view.Search.QuickOpen.PickActive();
        await Settle(vault.Window, view);
        Assert.True(view.SplitState.Enabled);
        Assert.Equal("Antiga.md", view.SplitState.Secondary);
        Assert.Equal("Nota 01.md", view.Editor.Current?.Path);   // the open note stays; the old one is beside it
        return true;
    }, CancellationToken.None));
}
