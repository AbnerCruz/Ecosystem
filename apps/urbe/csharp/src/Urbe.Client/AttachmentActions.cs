using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// An attachment's actions, as the 1.8.4's menuEntradaExplorer: Renomear (renomearAsset: the extension is kept, "para
/// preservar o tipo e o preview"), Mover (moverAsset: to a folder; a building of one file goes with it) and Excluir
/// (removerAsset: "O arquivo será apagado do vault", after the person confirms). The file is changed in the vault
/// first (IUrbeVaultStorage.MoveFileAsync / DeleteFileAsync, verified); only then the city and the map follow.
/// </summary>
public sealed partial class MainView
{
    private Task ShowAttachmentActionsAsync(string path, bool withView = false)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var name = path[(path.LastIndexOf('/') + 1)..];
        var items = new List<DialogItem>();
        if (withView) items.Add(new DialogItem("Visualizar", UrbeTheme.Icons.Image, "A prévia dentro do Urbe.", Run: () => ShowFileAsync(path)));
        if (!VaultWritable) return withView ? Dialogs.MenuAsync(name, items) : Dialogs.AlertAsync(name, NoVaultMessage);
        items.AddRange(
        [
            new DialogItem("Renomear", UrbeTheme.Icons.Pencil, "Mantenha a extensão para preservar o tipo.", Run: () => RenameAttachmentAsync(path)),
            new DialogItem("Mover", UrbeTheme.Icons.Folder, "Para outra pasta.", Run: () => MoveAttachmentAsync(path)),
            new DialogItem("Excluir", UrbeTheme.Icons.Trash, "O arquivo será apagado do vault.", Danger: true, Run: () => DeleteAttachmentAsync(path))
        ]);
        return Dialogs.MenuAsync(name, items);
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

    /// <summary>moverAsset: the folder (Raiz or one of them), the file keeping its name.</summary>
    public async Task MoveAttachmentAsync(string path, string? folder = null)
    {
        folder ??= await ChooseFolderAsync();
        if (folder is null) return;
        var name = path[(path.LastIndexOf('/') + 1)..];
        var to = (folder.Length > 0 ? folder + "/" : "") + name;
        if (to == path) return;
        await MoveAttachmentToAsync(path, to, "Arquivo movido.");
    }

    private async Task MoveAttachmentToAsync(string from, string to, string notice)
    {
        try { await _vault!.MoveFileAsync(from, to); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível mover o arquivo", ex.Message);
            return;
        }
        if (_attachments.Remove(from)) _attachments.Add(to);
        World.City?.ApplyAttachmentMove(from, to, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        if (FilePreview.IsVisible && FilePreview.Current == from) await FilePreview.OpenAsync(to, World.City?.AttachmentBuilding(to));
        ShowLifeNotice(notice);
    }

    /// <summary>removerAsset: confirmed, the file is deleted from the vault, then its entry and empty building.</summary>
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
        World.City?.RemoveAttachment(path);
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        if (FilePreview.IsVisible && FilePreview.Current == path) FilePreview.Close();
        RefreshNotes();
        World.InvalidateVisual();
        ShowLifeNotice("Arquivo excluído.");
    }
}
