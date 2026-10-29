using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-004/006/007: every operation on the vault with the storage failing on the way — before writing (nothing
/// happened) or after (the bytes are there, the call still failed: a phone's folder that wrote and then lost the
/// answer). Each one: no success announced that did not happen, nothing lost, the screen shows what is on the disk,
/// the failure says what failed with a way to try again, and the vault reopens as it is.
/// </summary>
public sealed partial class HeadlessUiTests
{
    /// <summary>The real folder storage, with any operation made to fail once, before or after it runs.</summary>
    private sealed class FaultyStorage(FolderVaultStorage inner) : IUrbeVaultStorage
    {
        public enum When { Before, After }
        private readonly Dictionary<string, When> _faults = new(StringComparer.Ordinal);
        public List<string> Calls { get; } = [];

        public void Fail(string operation, When when = When.Before) => _faults[operation] = when;

        private async Task Run(string operation, Func<Task> call)
        {
            Calls.Add(operation);
            bool failing = _faults.Remove(operation, out var when);
            if (failing && when == When.Before) throw new IOException("O armazenamento recusou a gravação (" + operation + ").");
            await call();
            if (failing) throw new IOException("O armazenamento não confirmou a gravação (" + operation + ").");
        }

        public bool IsConnected => inner.IsConnected;
        public string? AtlasVaultSessionKey => inner.AtlasVaultSessionKey;
        public string? VaultName => inner.VaultName;
        public IImportStorage? ImportStorage => inner.ImportStorage;
        public Task<VaultSnapshot?> PickAsync(CancellationToken c = default) => inner.PickAsync(c);
        public Task<VaultSnapshot?> RestoreAsync(CancellationToken c = default) => inner.RestoreAsync(c);
        public Task<VaultSnapshot?> ReloadAsync(CancellationToken c = default) => inner.ReloadAsync(c);
        public Task<string?> PeekTextAsync(string path, CancellationToken c = default) => inner.PeekTextAsync(path, c);
        public Task<IReadOnlyList<VaultFile>> ReadAllFilesAsync(CancellationToken c = default) => inner.ReadAllFilesAsync(c);
        public Task<IReadOnlyList<string>> ListFilePathsAsync(CancellationToken c = default) => inner.ListFilePathsAsync(c);
        public Task<VaultFileRead?> ReadFileAsync(string path, long maxBytes, CancellationToken c = default) => inner.ReadFileAsync(path, maxBytes, c);
        public Task<bool> OpenExternallyAsync(string path, CancellationToken c = default) => inner.OpenExternallyAsync(path, c);
        public Task<bool> RemoveEmptyDirectoryAsync(string path, CancellationToken c = default) => inner.RemoveEmptyDirectoryAsync(path, c);
        public Task SaveExistingNoteAsync(string path, string content, CancellationToken c = default) => Run("save", () => inner.SaveExistingNoteAsync(path, content, c));
        public Task SaveExistingTextFileAsync(string path, string content, CancellationToken c = default) => Run("save", () => inner.SaveExistingTextFileAsync(path, content, c));
        public Task SaveExistingTextFileIfUnchangedAsync(string path, string content, byte[] expectedSha256, CancellationToken c = default) =>
            Run("save", () => inner.SaveExistingTextFileIfUnchangedAsync(path, content, expectedSha256, c));
        public Task CreateTextFileAsync(string path, string content, CancellationToken c = default) => Run("create", () => inner.CreateTextFileAsync(path, content, c));
        public Task CreateBinaryFileAsync(string path, byte[] bytes, CancellationToken c = default) => Run("create", () => inner.CreateBinaryFileAsync(path, bytes, c));
        public Task CreateImportedFileAsync(string path, byte[] bytes, CancellationToken c = default) => Run("create", () => inner.CreateImportedFileAsync(path, bytes, c));
        public Task CreateDirectoryAsync(string path, CancellationToken c = default) => Run("folder", () => inner.CreateDirectoryAsync(path, c));
        public Task MoveFileAsync(string from, string to, CancellationToken c = default) => Run("move", () => inner.MoveFileAsync(from, to, c));
        public Task MoveTextFileAsync(string from, string to, CancellationToken c = default) => Run("move", () => inner.MoveTextFileAsync(from, to, c));
        public Task DeleteFileAsync(string path, CancellationToken c = default) => Run("delete", () => inner.DeleteFileAsync(path, c));
        public Task RenameFolderAsync(string from, string to, CancellationToken c = default) => Run("renameFolder", () => inner.RenameFolderAsync(from, to, c));
        public Task TrashTextFileAsync(string path, string trashJson, CancellationToken c = default) => Run("trash", () => inner.TrashTextFileAsync(path, trashJson, c));
        public Task TrashTextFilesAsync(IReadOnlyList<string> paths, string trashJson, CancellationToken c = default) => Run("trash", () => inner.TrashTextFilesAsync(paths, trashJson, c));
        public Task SaveTrashAsync(string trashJson, CancellationToken c = default) => Run("saveTrash", () => inner.SaveTrashAsync(trashJson, c));
        public Task SaveTutorialMarkAsync(string json, CancellationToken c = default) => inner.SaveTutorialMarkAsync(json, c);
        public Task SaveHistoryAsync(string historyJson, CancellationToken c = default) => Run("history", () => inner.SaveHistoryAsync(historyJson, c));
        public Task SaveIdentityAsync(byte[] identityJson, CancellationToken c = default) => inner.SaveIdentityAsync(identityJson, c);
        public Task SaveMaintenanceAsync(System.Text.Json.Nodes.JsonObject maintenance, CancellationToken c = default) => inner.SaveMaintenanceAsync(maintenance, c);
        public Task SaveNotesRecordAsync(string json, CancellationToken c = default) => Run("record", () => inner.SaveNotesRecordAsync(json, c));
        public Task SaveAreaLayoutAsync(string json, CancellationToken c = default) => inner.SaveAreaLayoutAsync(json, c);
        public Task SaveConstellationAsync(string json, CancellationToken c = default) => inner.SaveConstellationAsync(json, c);
    }

    private sealed record FaultyVault(string Folder, FaultyStorage Storage, MainView View, Window Window) : IDisposable
    {
        public string Read(string path) => File.ReadAllText(System.IO.Path.Combine(Folder, path));
        public bool Exists(string path) => File.Exists(System.IO.Path.Combine(Folder, path));
        public void Dispose() => Window.Close();
    }

    private static async Task<FaultyVault> OpenFaultyVaultAsync(params (string Path, string Text)[] notes)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "urbe-faults-" + Guid.NewGuid().ToString("N"));
        var folder = System.IO.Path.Combine(dir, "vault");
        Directory.CreateDirectory(folder);
        foreach (var (path, text) in notes)
        {
            var file = System.IO.Path.Combine(folder, path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
        }
        var storage = new FaultyStorage(new FolderVaultStorage(_ => Task.FromResult<string?>(folder), System.IO.Path.Combine(dir, "sel.json")));
        var view = new MainView(storage);
        var window = new Window { Width = 1100, Height = 800, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        return new FaultyVault(folder, storage, view, window);
    }

    /// <summary>The vault as a new start of the app reads it.</summary>
    private static async Task<IReadOnlyList<string>> ReopenedPathsAsync(FaultyVault vault)
    {
        var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault.Folder), System.IO.Path.Combine(vault.Folder, "..", "sel2.json"));
        var view = new MainView(storage);
        var window = new Window { Width = 1100, Height = 800, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            return view.Documents!.List().Select(d => d.Path).OrderBy(p => p, StringComparer.Ordinal).ToList();
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Runs <paramref name="flow"/>; each dialog it opens is recorded and answered by the button <paramref name="answer"/>
    /// names for it (null: cancelled).
    /// </summary>
    private static async Task<List<string>> AnsweringAsync(FaultyVault vault, Func<Task> flow, Func<string, string?> answer)
    {
        var seen = new List<string>();
        var running = flow();
        var view = vault.View;
        for (int i = 0; i < 1500 && !running.IsCompleted; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            if (view.Dialogs.IsOpen)
            {
                var text = string.Join(" | ", view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)));
                seen.Add(text);
                if (answer(text) is { } button) ClickButtonWithText(view.Dialogs, button);
                else view.Dialogs.Cancel();
                Dispatcher.UIThread.RunJobs();
            }
            await Task.Delay(10);
        }
        Assert.True(running.IsCompleted, "a operação não terminou: " + string.Join(" || ", seen));
        await running;
        await Settle(vault.Window, view);
        return seen;
    }

    [Fact]
    public async Task Storage_ACreateRefusedSaysSoAndTriesAgain() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"));
        var view = vault.View;
        vault.Storage.Fail("create");
        CityNote? made = null;
        var dialogs = await AnsweringAsync(vault, async () => made = await view.CreateNoteAsync(typedName: "Ideias"),
            text => text.Contains("Tentar de novo") ? "Tentar de novo" : "Ok");
        Assert.Contains(dialogs, d => d.Contains("Não foi possível criar a nota") && d.Contains("Tentar de novo"));
        Assert.NotNull(made);   // the second try created it
        Assert.Equal("Ideias.md", made!.Path);
        Assert.True(vault.Exists("Ideias.md"));
        Assert.Contains("Ideias.md", await ReopenedPathsAsync(vault));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Storage_ACreateThatWroteButFailedShowsTheNoteAndDoesNotMakeACopy() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"));
        var view = vault.View;
        vault.Storage.Fail("create", FaultyStorage.When.After);
        var dialogs = await AnsweringAsync(vault, () => view.CreateNoteAsync(typedName: "Ideias"), text => text.Contains("Tentar de novo") ? "Tentar de novo" : "Ok");
        Assert.True(vault.Exists("Ideias.md"));
        // the file is there: the screen shows it, and trying again does not stop at "já existe" nor make "Ideias 2"
        Assert.NotNull(view.Documents!.Get("Ideias.md"));
        Assert.DoesNotContain(view.Documents.List(), d => d.Path.StartsWith("Ideias ", StringComparison.Ordinal));
        Assert.DoesNotContain(dialogs, d => d.Contains("já existe", StringComparison.OrdinalIgnoreCase) || d.Contains("Já existe", StringComparison.Ordinal));
        Assert.Equal(["Comece.md", "Ideias.md"], await ReopenedPathsAsync(vault));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Storage_ARenameThatMovedButFailedShowsTheNoteWhereItIs() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Rascunho.md", "texto do rascunho\n"));
        var view = vault.View;
        var id = view.Documents!.Get("Rascunho.md")!.Id;
        vault.Storage.Fail("move", FaultyStorage.When.After);
        await AnsweringAsync(vault, () => view.RenameNoteAsync("Rascunho.md", "Final.md"), _ => "Ok");
        Assert.False(vault.Exists("Rascunho.md"));
        Assert.Equal("texto do rascunho\n", vault.Read("Final.md"));
        // the screen follows the disk: one note, at its new name, the same note (its id)
        Assert.Null(view.Documents.Get("Rascunho.md"));
        Assert.Equal(id, view.Documents.Get("Final.md")?.Id);
        Assert.Equal(["Comece.md", "Final.md"], await ReopenedPathsAsync(vault));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Storage_ARenameRefusedLeavesTheNoteAndTriesAgain() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Rascunho.md", "texto\n"));
        var view = vault.View;
        vault.Storage.Fail("move");
        var dialogs = await AnsweringAsync(vault, () => view.RenameNoteAsync("Rascunho.md", "Final.md"), text => text.Contains("Tentar de novo") ? "Tentar de novo" : "Ok");
        Assert.Contains(dialogs, d => d.Contains("Não foi possível renomear") && d.Contains("Tentar de novo"));
        Assert.True(vault.Exists("Final.md"));
        Assert.NotNull(view.Documents!.Get("Final.md"));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Storage_ATrashThatWroteButFailedShowsTheNoteInTheTrash() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Velha.md", "conteúdo antigo\n"));
        var view = vault.View;
        vault.Storage.Fail("trash", FaultyStorage.When.After);
        await AnsweringAsync(vault, () => view.DeleteNoteAsync("Velha.md", confirmed: true), _ => "Ok");
        Assert.False(vault.Exists("Velha.md"));
        Assert.Contains("conteúdo antigo", vault.Read(".urbe/trash.v2.json"));
        // not a note on the screen that is no longer on the disk
        Assert.Null(view.Documents!.Get("Velha.md"));
        view.ShowTrashView();
        Assert.Contains(view.Explorer.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Velha");
        Assert.Equal(["Comece.md"], await ReopenedPathsAsync(vault));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Storage_ARestoreHalfDoneIsFinishedByTryingAgain() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("Velha.md", "conteúdo antigo\n"));
        var view = vault.View;
        var id = view.Documents!.Get("Velha.md")!.Id;
        Assert.True(await view.DeleteNoteAsync("Velha.md", confirmed: true));
        // the note is written back, then the trash fails to be written: it was "Não foi possível restaurar", with the
        // note on the disk but not on the screen, and every new try stopped at "Já existe um documento"
        vault.Storage.Fail("saveTrash");
        var dialogs = await AnsweringAsync(vault, () => view.RestoreFromTrashAsync(id), text => text.Contains("Tentar de novo") ? "Tentar de novo" : "Ok");
        Assert.DoesNotContain(dialogs, d => d.Contains("Já existe", StringComparison.Ordinal));
        Assert.Equal("conteúdo antigo\n", vault.Read("Velha.md"));
        Assert.Equal(id, view.Documents.Get("Velha.md")?.Id);
        Assert.DoesNotContain("conteúdo antigo", vault.Read(".urbe/trash.v2.json"));
        Assert.Equal(["Comece.md", "Velha.md"], await ReopenedPathsAsync(vault));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Storage_AMoveRefusedHalfWayShowsWhatMovedAndWhatDidNot() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"), ("A.md", "a\n"), ("B.md", "b\n"), ("Destino/.keep.md", "x\n"));
        var view = vault.View;
        // the first moves, the second is moved by the storage but the answer is lost
        await view.MoveNotesToAsync(["A.md"], "Destino");
        vault.Storage.Fail("move", FaultyStorage.When.After);
        await AnsweringAsync(vault, () => view.MoveNotesToAsync(["B.md"], "Destino"), _ => "Ok");
        Assert.True(vault.Exists("Destino/A.md"));
        Assert.True(vault.Exists("Destino/B.md"));
        Assert.NotNull(view.Documents!.Get("Destino/B.md"));
        Assert.Null(view.Documents.Get("B.md"));
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Storage_ASaveRefusedIsNotSavedAndKeepsTheText() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var vault = await OpenFaultyVaultAsync(("Comece.md", "# Comece\n"));
        var view = vault.View;
        view.Editor.Open(new CityNote("Comece.md", "# Comece\n"));
        view.Editor.SetMode(EditorMode.Fonte);
        await Settle(vault.Window, view);
        view.Editor.Editor.Text = "# Comece\n\ntexto novo\n";
        vault.Storage.Fail("save");
        Assert.False(await view.Editor.SaveAsync());
        Assert.Equal("# Comece\n", vault.Read("Comece.md"));       // nothing claimed
        Assert.True(view.Editor.HasChanges);                          // the text is still there to save
        Assert.StartsWith("Não foi possível salvar “Comece”.", view.Editor.NoticeText);   // with Tentar de novo
        // the bytes written and the answer lost: it is saved, not reported as a failure
        view.Editor.Editor.Text = "# Comece\n\noutro texto\n";
        vault.Storage.Fail("save", FaultyStorage.When.After);
        Assert.True(await view.Editor.SaveAsync());
        Assert.False(view.Editor.HasChanges);
        Assert.Equal("# Comece\n\noutro texto\n", vault.Read("Comece.md"));
        view.Editor.Editor.Text = "# Comece\n\ntexto novo\n";
        Assert.True(await view.Editor.SaveAsync());
        Assert.Equal("# Comece\n\ntexto novo\n", vault.Read("Comece.md"));
        return true;
    }, CancellationToken.None));

    [Fact]
    public void Storage_TheSystemsErrorsAreSaidInThePersonsWords()
    {
        Assert.Contains("permissão", StorageProblems.Explain(new UnauthorizedAccessException("Access to the path '/x' is denied.")));
        Assert.Contains("espaço livre", StorageProblems.Explain(new IOException("No space left on device : '/x'")));
        Assert.Contains("espaço livre", StorageProblems.Explain(new IOException("disk full", unchecked((int)0x80070070))));
        Assert.Contains("não está mais lá", StorageProblems.Explain(new FileNotFoundException("Could not find file '/x'.")));
        Assert.Contains("não está mais lá", StorageProblems.Explain(new DirectoryNotFoundException("Could not find a part of the path.")));
        // the app's own messages, already in the person's words, stay as they are
        Assert.Equal("Vault em modo somente leitura.", StorageProblems.Explain(new InvalidOperationException("Vault em modo somente leitura.")));
    }

    [Fact]
    public async Task Storage_AWriteCutByTheAppClosingIsResolvedAtTheNextOpening() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // the storages keep the previous text next to a file before overwriting it; the app killed half-way left that
        // copy and an empty or cut note, and nothing at the next opening looked at it
        const string g1 = "0123456789abcdef0123456789abcdef", g2 = "fedcba9876543210fedcba9876543210", g3 = "00000000000000000000000000000000";
        using var vault = await OpenFaultyVaultAsync(
            ("Comece.md", "# Comece\n"),
            ("Cortada.md", ""), ("Cortada.md.urbe-recovery-" + g1, "o texto completo de antes\n"),
            ("Pasta/Dupla.md", "texto novo, talvez cortado"), ("Pasta/Dupla.md.urbe-recovery-" + g2, "texto anterior inteiro\n"),
            ("Igual.md", "igual\n"), ("Igual.md.urbe-recovery-" + g3, "igual\n"));
        var view = vault.View;
        Assert.Equal("o texto completo de antes\n", vault.Read("Cortada.md"));
        Assert.Equal("texto novo, talvez cortado", vault.Read("Pasta/Dupla.md"));
        Assert.Equal("texto anterior inteiro\n", vault.Read("Pasta/Dupla (antes da gravação interrompida).md"));
        Assert.Equal("igual\n", vault.Read("Igual.md"));
        Assert.Empty(Directory.GetFiles(vault.Folder, "*.urbe-recovery-*", SearchOption.AllDirectories));
        Assert.NotNull(view.Documents!.Get("Pasta/Dupla (antes da gravação interrompida).md"));
        Assert.Equal("o texto completo de antes\n", view.Documents.Get("Cortada.md")!.Content);
        // the person is told, naming the notes
        Assert.Contains(view.Notices, n => n.Contains("Cortada") && n.Contains("Dupla"));
        return true;
    }, CancellationToken.None));
}
