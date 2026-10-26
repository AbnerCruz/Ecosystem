using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Urbe.Core.Assistant;

namespace Urbe.Client;

public sealed partial class AssistantPanel
{
    /// <summary>Exports this conversation, not the configuration with provider credentials.</summary>
    private async Task ExportConversationAsync(AiConversation requested)
    {
        var conversation = requested.Id == _conv.Id ? _conv :
            _activeConversations.GetValueOrDefault(requested.Id) ?? _store.GetConversation(requested.Id) ?? requested;
        var chosen = await _app.Dialogs.ChooseAsync("Exportar conversa",
        [
            new DialogItem("Leve (.md)", Detail: "Mensagens legíveis, sequência de ferramentas e consumo agregado."),
            new DialogItem("Completo (.json)", Detail: "Mensagens, contexto oculto, argumentos e respostas de ferramentas, execuções e métricas.")
        ], "O arquivo pode conter informações privadas compartilhadas no chat. Nenhuma chave cadastrada de provedor será incluída.");
        if (chosen is null) return;
        bool complete = chosen == 1;
        var content = AssistantExport.Conversation(conversation, complete, UrbeApp.InstalledVersion);
        await SaveAssistantExportAsync("Urbe-conversa-" + SafeFileId(conversation.Id) + (complete ? ".json" : ".md"),
            content, complete);
    }

    /// <summary>Only this agent's scoped memory; no cross-agent or global/vault records.</summary>
    private async Task ExportAgentMemoryAsync()
    {
        var agentId = _conv.AgentId;
        var chosen = await _app.Dialogs.ChooseAsync("Exportar memória do agente",
        [
            new DialogItem("Leve (.md)", Detail: "Lembranças individuais em Markdown, com tipo e estado."),
            new DialogItem("Completo (.json)", Detail: "Todos os registros do agente, incluindo histórico, fontes, confiança e relações.")
        ], "Somente memórias individuais do agente " + agentId + ". Memórias globais ou do vault não serão atribuídas indevidamente a ele. O arquivo pode conter dados privados.");
        if (chosen is null) return;
        bool complete = chosen == 1;
        var content = AssistantExport.Memory(_cfg, agentId, complete, UrbeApp.InstalledVersion);
        await SaveAssistantExportAsync("Urbe-memoria-" + SafeFileId(agentId) + (complete ? ".json" : ".md"),
            content, complete);
    }

    private static string SafeFileId(string id) => new(id.Where(c =>
        char.IsAsciiLetterOrDigit(c) || c is '-' or '_').Take(64).ToArray());

    private async Task SaveAssistantExportAsync(string filename, string content, bool json)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
                throw new NotSupportedException("Este dispositivo não oferece uma pasta para exportação.");
            var extension = json ? "json" : "md";
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Salvar exportação do Assistente",
                SuggestedFileName = filename,
                DefaultExtension = extension,
                FileTypeChoices =
                [
                    new FilePickerFileType(json ? "JSON" : "Markdown")
                    {
                        Patterns = [json ? "*.json" : "*.md"],
                        MimeTypes = [json ? "application/json" : "text/markdown"]
                    }
                ]
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(Encoding.UTF8.GetBytes(content));
            await stream.FlushAsync();
            _app.Notify("Exportação do Assistente salva.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            await _app.Dialogs.AlertAsync("Exportar Assistente", e.Message);
        }
    }

    /// <summary>Back goes to chat from subviews first, then closes the assistant without stopping runs.</summary>
    public bool HandleBack()
    {
        if (!IsVisible) return false;
        if (_view.IsVisible) CloseView();
        else Close();
        return true;
    }
}
