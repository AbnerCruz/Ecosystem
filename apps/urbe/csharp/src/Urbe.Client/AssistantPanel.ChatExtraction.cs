using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>One extraction pipeline for the user interface and the agent tool. No temporary copies or parallel vault.</summary>
public sealed partial class AssistantPanel
{
    private AiConversation? AttachmentConversation(string conversationId) =>
        _conv.Id == conversationId ? _conv
        : _activeConversations.GetValueOrDefault(conversationId) ?? _store.GetConversation(conversationId);

    private IReadOnlyList<ChatExtractableAttachment> ChatFilesFor(string conversationId) =>
        AttachmentConversation(conversationId) is { } c ? AssistantChatAttachments.List(c) : [];

    private async Task<string> ExtractChatFileAsync(string conversationId, string attachmentId, string? destination)
    {
        var attachment = ChatFilesFor(conversationId).FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new AiToolException("Anexo não encontrado nesta conversa. Consulte list_chat_attachments.");
        var target = string.IsNullOrWhiteSpace(destination)
            ? AssistantChatAttachments.DefaultPath(attachment.Name) : destination.Trim();
        AssistantChatAttachments.ValidatePath(target);
        if (_app.FileExists(target))
            throw new AiToolException("Já existe " + target + ". Escolha outro nome ou pasta; nada será sobrescrito.");
        await _app.CreateImportedFileAsync(target, attachment.Bytes);
        return "Arquivo salvo e verificado no vault: " + target
            + (attachment.Exact
                ? " (bytes originais preservados)."
                : " (reconstruído do texto de uma conversa antiga; bytes originais indisponíveis).");
    }

    private async Task ExtractChatFileFromUiAsync(string conversationId, string attachmentId)
    {
        var attachment = ChatFilesFor(conversationId).FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null) { _app.Notify("O anexo não está mais disponível nesta conversa."); return; }
        string defaultPath;
        try { defaultPath = AssistantChatAttachments.DefaultPath(attachment.Name); }
        catch (AiToolException e) { await _app.Dialogs.AlertAsync("Extrair arquivo", e.Message); return; }
        if (!attachment.Exact && !await _app.Dialogs.ConfirmAsync("Anexo antigo sem bytes originais",
                "O Urbe antigo guardou apenas o texto decodificado de “" + attachment.Name +
                "”. É possível reconstruir o conteúdo, mas não garantir codificação e bytes idênticos.",
                "Reconstruir")) return;
        var path = await _app.Dialogs.PromptAsync(new PromptOptions("Extrair arquivo para o vault",
            Label: "Caminho no vault", Value: defaultPath, Confirm: "Extrair"));
        if (path is null) return;
        try
        {
            var result = await ExtractChatFileAsync(conversationId, attachmentId, path);
            _app.Notify(result);
        }
        catch (Exception e) when (e is AiToolException or IOException or InvalidOperationException or
            NotSupportedException or UnauthorizedAccessException or InvalidDataException)
        {
            await _app.Dialogs.AlertAsync("Não foi possível extrair", e.Message);
        }
    }
}
