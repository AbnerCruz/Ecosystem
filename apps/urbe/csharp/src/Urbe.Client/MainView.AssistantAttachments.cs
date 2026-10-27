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
            // Match the app's existing CreateNoteAsync pipeline: every editable text
            // document belongs to the library immediately, not only Markdown.
            _library!.AddNote(path, text, Now, "user");
        }
        else
        {
            IUrbeVaultStorage.EnsureWritableImportedPath(path);
            // Validate text before writing: a successful import must be visible and editable in the vault.
            var isText = IUrbeVaultStorage.WritableExtensions.Contains(Path.GetExtension(path));
            string decoded = "";
            if (isText && !Urbe.Core.Assistant.AssistantTextAttachments.TryDecode(path, bytes!, out decoded, out var reason))
                throw new InvalidDataException("Texto anexado inválido: " + reason);
            await _vault!.CreateImportedFileAsync(path, bytes!);
            if (isText)
            {
                _contents[path] = decoded;
                _library!.AddNote(path, decoded, Now, "user");
            }
            else _attachments.Add(path);
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
