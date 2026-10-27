using Urbe.Core;

namespace Urbe.Client;

/// <summary>Received files are placed in the existing vault, never in an invisible private copy.</summary>
public sealed partial class MainView : IChatAttachmentVault
{
    public async Task SaveChatAttachmentAsync(string path, string? text, byte[]? bytes)
    {
        RequireWritable();
        if (((IAssistantApp)this).FileExists(path))
            throw new IOException("Já existe um arquivo neste caminho: " + path);
        if ((text is null) == (bytes is null))
            throw new InvalidDataException("Informe exatamente um conteúdo de anexo.");
        await EnsureFolderAsync(DirOf(path));
        if (text is not null)
        {
            IUrbeVaultStorage.EnsureWritableTextPath(path);
            await _vault!.CreateTextFileAsync(path, text);
            _contents[path] = text;
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
                _library!.AddNote(path, text, Now, "user");
        }
        else
        {
            IUrbeVaultStorage.EnsureWritableBinaryPath(path);
            await _vault!.CreateBinaryFileAsync(path, bytes!);
            _attachments.Add(path);
        }
        AfterAssistantWrite(path);
    }

    public async Task OpenChatAttachmentAsync(string path)
    {
        if ((path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)) &&
            ((IAssistantApp)this).OpenReference(path))
            return;
        await ShowFileAsync(path);
    }
}
