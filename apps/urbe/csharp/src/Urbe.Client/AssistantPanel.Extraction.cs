using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>Host boundary for a user-selected chat attachment. Never writes outside the selected vault.</summary>
public interface IChatAttachmentVault
{
    Task SaveChatAttachmentAsync(string path, string? text, byte[]? bytes);
    Task OpenChatAttachmentAsync(string path);
}

public sealed partial class AssistantPanel
{
    private async Task<string> ExtractMessageToVaultAsync(AiChatMessage message, string fileName, bool open)
    {
        if (_app is not IChatAttachmentVault vault)
            throw new NotSupportedException("O vault deste cliente não aceita arquivos da conversa.");
        var path = await AssistantAttachmentExtraction.SaveAsync(message, fileName, _app.FileExists,
            vault.SaveChatAttachmentAsync);
        if (open) await vault.OpenChatAttachmentAsync(path);
        return path;
    }

    private async Task ExtractMessageFileAsync(AiChatMessage message, string name)
    {
        try
        {
            var path = await ExtractMessageToVaultAsync(message, name, open: true);
            _app.Notify("Arquivo extraído para " + path);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _app.Notify("Não foi possível extrair " + name + ": " + e.Message);
        }
    }
}
