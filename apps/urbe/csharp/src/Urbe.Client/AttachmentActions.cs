using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// An attachment's actions, as the 1.8.4's menuEntradaExplorer: Renomear (renomearAsset: the extension is kept, "para
/// preservar o tipo e o preview"), Mover (moverAsset: to a folder) and Excluir (removerAsset: "O arquivo será apagado
/// do vault", after the person confirms). The file is changed in the vault first (IUrbeVaultStorage.MoveFileAsync /
/// DeleteFileAsync, verified); only then the Explorer follows.
/// </summary>
public sealed partial class MainView
{
    private Task ShowAttachmentActionsAsync(string path, bool withView = false)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var name = path[(path.LastIndexOf('/') + 1)..];
        var items = new List<DialogItem>();
        if (withView) items.Add(new DialogItem("Visualizar", UrbeTheme.Icons.Image, "A prévia dentro do Urbe.", Run: () => ShowFileAsync(path)));
        // #downloadPreviewBtn: a copy where the person chooses (only reads the vault)
        if (_vault is not null) items.Add(new DialogItem("Baixar arquivo", UrbeTheme.Icons.Download, "Uma cópia onde você escolher.", Run: () => DownloadFileAsync(path)));
        if (!VaultWritable) return items.Count > 0 ? Dialogs.MenuAsync(name, items) : Dialogs.AlertAsync(name, NoVaultMessage);
        items.AddRange(
        [
            new DialogItem("Renomear", UrbeTheme.Icons.Pencil, "Mantenha a extensão para preservar o tipo.", Run: () => RenameAttachmentAsync(path)),
            new DialogItem("Mover", UrbeTheme.Icons.Folder, "Para outra pasta.", Run: () => MoveAttachmentAsync(path)),
            new DialogItem("Excluir", UrbeTheme.Icons.Trash, "O arquivo será apagado do vault.", Danger: true, Run: () => DeleteAttachmentAsync(path))
        ]);
        return Dialogs.MenuAsync(name, items);
    }

    /// <summary>The bytes of a downloaded file and where they went (tests replace the picker).</summary>
    public Func<string, byte[], Task<bool?>>? SaveFileCopy { get; set; }

    /// <summary>Baixar arquivo (#downloadPreviewBtn): the file's bytes saved where the person chooses; the vault is only read.</summary>
    public async Task DownloadFileAsync(string path)
    {
        if (_vault is null) return;
        var name = path[(path.LastIndexOf('/') + 1)..];
        try
        {
            var read = await _vault.ReadFileAsync(path, 512L * 1024 * 1024);
            if (read is null) { await Dialogs.AlertAsync("Baixar arquivo", "Arquivo não encontrado."); return; }
            if (read.Bytes is null) { await Dialogs.AlertAsync("Baixar arquivo", "O arquivo é grande demais para copiar por aqui."); return; }
            var saved = await (SaveFileCopy ?? SaveAnyFileAsync)(name, read.Bytes);
            if (saved == true) ShowLifeNotice("Arquivo baixado.", Ui.ToastKind.Success);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { ShowLifeNotice("Não consegui salvar: " + ex.Message); }
    }

    private async Task<bool?> SaveAnyFileAsync(string name, byte[] bytes)
    {
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
            throw new NotSupportedException("este aparelho não permite salvar arquivos");
        var ext = System.IO.Path.GetExtension(name).TrimStart('.');
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Baixar arquivo", SuggestedFileName = name, DefaultExtension = ext.Length > 0 ? ext : null
        });
        if (file is null) return null;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
        return true;
    }

    /// <summary>renomearAsset: the new name in the same folder; a different extension is refused.</summary>
    public async Task RenameAttachmentAsync(string path, string? typed = null)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        typed ??= await Dialogs.PromptAsync(new PromptOptions("Renomear arquivo", Label: "Nome e extensão", Value: name, Confirm: "Renomear"));
        var next = ArtifactModel.SafeName((typed ?? "").Trim());
        if (next.Length == 0 || next == name) return;
        var oldExt = System.IO.Path.GetExtension(name).ToLowerInvariant();
        var newExt = System.IO.Path.GetExtension(next).ToLowerInvariant();
        if (oldExt.Length > 0 && newExt != oldExt)
        {
            ShowLifeNotice("Para preservar o tipo e o preview, mantenha a extensão do arquivo.");
            return;
        }
        var dir = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
        await MoveAttachmentToAsync(path, (dir.Length > 0 ? dir + "/" : "") + next, "Arquivo renomeado.");
    }

    /// <summary>moverAsset: the folder (Raiz or one of them), the file keeping its name. Also what dragging a file that is
    /// not a note onto a folder in the Explorer does (UC-54).</summary>
    public async Task MoveAttachmentAsync(string path, string? folder = null)
    {
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover", NoVaultMessage);
            return;
        }
        folder ??= await ChooseFolderAsync(current: DirOf(path));
        if (folder is null) return;
        var name = path[(path.LastIndexOf('/') + 1)..];
        var to = (folder.Length > 0 ? folder + "/" : "") + name;
        if (to == path) return;
        await MoveAttachmentToAsync(path, to, "Arquivo movido.");
    }

    private async Task MoveAttachmentToAsync(string from, string to, string notice)
    {
        // a text file that is not a note (.json, .csv…) moves as text, verified the same way (UC-54)
        try
        {
            if (IUrbeVaultStorage.TextDocument(from)) await _vault!.MoveTextFileAsync(from, to);
            else await _vault!.MoveFileAsync(from, to);
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível mover o arquivo", ex.Message);
            return;
        }
        if (_attachments.Remove(from)) _attachments.Add(to);
        _library?.AddFolderOf(to);
        NotesChanged();
        RefreshNotes();
        if (FilePreview.IsVisible && FilePreview.Current == from) await FilePreview.OpenAsync(to);
        ShowLifeNotice(notice);
    }

    /// <summary>removerAsset: confirmed, the file is deleted from the vault, then its entry.</summary>
    public async Task DeleteAttachmentAsync(string path, bool confirmed = false)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        if (!confirmed && !await Dialogs.ConfirmAsync("Excluir “" + name + "”?", "O arquivo será apagado do vault.", "Excluir", danger: true)) return;
        try { await _vault!.DeleteFileAsync(path); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível excluir", ex.Message);
            return;
        }
        _attachments.Remove(path);
        if (FilePreview.IsVisible && FilePreview.Current == path) FilePreview.Close();
        RefreshNotes();
        ShowLifeNotice("Arquivo excluído.");
    }
}
