using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// Native, progressive-disclosure representation of the agent's real operations.
/// No messages, tool arguments, results, approval requests or outputs are discarded.
/// </summary>
public sealed partial class AssistantPanel
{
    private readonly Dictionary<string, bool> _activityExpanded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _stageExpanded = new(StringComparer.Ordinal);

    private Control ActivityTimeline(AiChatMessage request, IReadOnlyList<AiChatMessage> messages)
    {
        var calls = messages.SelectMany(message => message.Content
            .Where(block => block.Type == "tool_use")
            .Select(block => (Block: block, Message: message))).ToList();
        bool running = _running is not null;
        bool needsApproval = _pending is { } pending &&
            calls.Any(x => x.Block.Id == pending.Request.Call.Id && x.Block.Id.Length > 0);
        var lastQuestion = calls.LastOrDefault(x => x.Block.Name == "ask_user").Block;
        bool needsAnswer = !running && lastQuestion is not null &&
            _conv.Runs.LastOrDefault()?.Stopped == "needs_input" &&
            ResultFor(lastQuestion.Id) is { IsError: false };
        var last = calls.LastOrDefault().Block;
        var headline = needsApproval ? "Aguardando sua aprovação"
            : needsAnswer ? "Aguardando sua resposta"
            : running && last is not null ? ActivityVerb(last.Name)
            : running ? "Analisando o pedido"
            : calls.Any(x => ResultFor(x.Block.Id) is { IsError: true }) ? "Atividade concluída com alertas"
            : calls.Count > 0 ? "Trabalho realizado" : "Análise concluída";

        var header = new StackPanel { Spacing = 5 };
        var line = new DockPanel();
        var leading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center };
        leading.Children.Add(UrbeTheme.Icon(UrbeTheme.Icons.Assistant, 16,
            running ? UrbeTheme.Accent : UrbeTheme.Text2));
        leading.Children.Add(Label(headline, 13, UrbeTheme.Text, FontWeight.SemiBold));
        line.Children.Add(leading);
        header.Children.Add(line);

        var detail = calls.Count == 0 ? (running ? "Preparando as próximas etapas" : "Processamento concluído")
            : calls.Count == 1 ? "1 operação · tocar para ver os detalhes"
            : calls.Count + " operações · tocar para explorar as etapas";
        header.Children.Add(Wrap(detail, 11, UrbeTheme.Text3));
        if (running && !UrbeTokens.Motion.Reduced)
            header.Children.Add(new ProgressBar
            {
                IsIndeterminate = true, Height = 2, Minimum = 0, Maximum = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Foreground = UrbeTheme.Brush(UrbeTheme.Accent),
                Background = UrbeTheme.Brush(UrbeTheme.Line2)
            });

        var fold = Lazy(header, () =>
        {
            var list = new StackPanel { Spacing = 5, Margin = new Thickness(0, 8, 0, 2) };
            // Preserve the *entire* chronology, including model reasoning and intermediate drafts.
            // Every part is loaded only when the person requests that part of the history.
            foreach (var message in messages)
            {
                if (message.Meta?.Reasoning is { Length: > 0 } thought)
                    list.Children.Add(Lazy(Label("Raciocínio fornecido pelo modelo", 12, UrbeTheme.Text2),
                        () => Wrap(thought, 12, UrbeTheme.Text2)));
                foreach (var block in message.Content)
                {
                    if (block.Type == "tool_use") list.Children.Add(ActivityStage(block, message));
                    else if (block.Type == "text" && !block.Hidden && !string.IsNullOrEmpty(block.Text))
                    {
                        var draft = block.Text;
                        list.Children.Add(Lazy(Label(message.Meta?.Draft == true ? "Rascunho intermediário" : "Observação do agente", 12, UrbeTheme.Text2),
                            () => Markdown(draft)));
                    }
                }
            }
            if (list.Children.Count == 0)
                list.Children.Add(Hint("O modelo ainda não registrou operações."));
            return list;
        });
        fold.IsExpanded = _activityExpanded.GetValueOrDefault(request.Id);
        fold.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty) _activityExpanded[request.Id] = fold.IsExpanded;
        };
        Avalonia.Automation.AutomationProperties.SetName(fold, "Atividade do agente: " + headline + ". " + detail);
        var root = new StackPanel { Spacing = 9 };
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(14), Padding = new Thickness(11, 8),
            Background = UrbeTheme.Brush(UrbeTheme.Surface, .7),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
            Child = fold
        });

        // Actionable requests must never be buried inside a collapsed operation history.
        if (needsApproval && _pending is { } action) root.Children.Add(Approval(action.Request));
        if (needsAnswer && lastQuestion is not null) root.Children.Add(Clarification(lastQuestion));

        // A generated picture is a user-facing result, not merely a log attachment.
        foreach (var path in calls.Where(x => x.Block.Name == "generate_image")
            .Select(x => ResultFor(x.Block.Id) is { IsError: false } result
                ? AssistantImageTools.SavedImagePath(result.Content) : null)
            .OfType<string>().Distinct(StringComparer.Ordinal))
            root.Children.Add(GeneratedImagePreview(path));
        return root;
    }

    private static string ActivityVerb(string tool) => tool switch
    {
        "search_notes" or "grep_notes" or "list_notes" or "workspace_overview" => "Pesquisando no vault",
        "read_note" or "read_notes" or "get_links" or "get_editor_context" => "Lendo documentos",
        "update_plan" => "Organizando o trabalho",
        "ask_user" => "Preparando uma pergunta",
        "create_note" or "edit_note" or "write_note" or "append_to_note" => "Escrevendo no vault",
        "generate_image" => "Criando uma imagem",
        "write_page" or "page_schema" => "Preparando uma página",
        "delegate_research" => "Pesquisando com outro agente",
        _ => "Executando uma etapa"
    };

    private Control ActivityStage(AiChatBlock block, AiChatMessage message)
    {
        var result = ResultFor(block.Id);
        var tool = ToolByName(block.Name);
        var title = tool is not null && !block.Pending ? SafeLabel(tool, block.Input)
            : block.Pending ? "Preparando " + block.Name : block.Name;
        var pending = _pending?.Request.Call.Id == block.Id && block.Id.Length > 0;
        var status = result is { IsError: true } ? "Falhou"
            : result?.Status == "rejected" ? "Recusada"
            : result is not null ? "Concluída"
            : pending ? "Aguardando aprovação"
            : message.Meta?.Streaming == true ? "Preparando"
            : _running is not null ? "Em andamento" : "Interrompida";
        var header = new DockPanel { MinHeight = 34 };
        var state = Label(status, 11, result is { IsError: true } ? "#c0504d" : UrbeTheme.Text3);
        DockPanel.SetDock(state, Dock.Right);
        header.Children.Add(state);
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(UrbeTheme.Icon(AccessIcon.GetValueOrDefault(tool?.Access ?? "", UrbeTheme.Icons.Layers),
            15, UrbeTheme.Text2));
        label.Children.Add(Wrap(title, 12, UrbeTheme.Text));
        header.Children.Add(label);
        var expander = Lazy(header, () =>
        {
            var details = new StackPanel { Spacing = 8, Margin = new Thickness(4, 4, 4, 8) };
            if (_progress.TryGetValue(block.Id, out var progress)) details.Children.Add(Wrap(progress, 12, UrbeTheme.Text2));
            if (block.Name == "update_plan" && block.Input?["steps"] is JsonArray steps && block.Id == LatestPlanId())
                details.Children.Add(Plan(steps));
            if (block.Name == "ask_user" && block.Input?["question"] is JsonValue question)
                details.Children.Add(Wrap(LegacyCustomize.JsToString(question), 13, UrbeTheme.Text));
            if (block.Input is not null && !block.Pending)
            {
                details.Children.Add(Hint("Parâmetros enviados"));
                details.Children.Add(Pre(LegacyCustomize.Stringify(block.Input, 2), false));
            }
            if (result is not null)
            {
                details.Children.Add(Hint("Resultado devolvido ao agente"));
                details.Children.Add(Pre(result.Content, result.IsError));
            }
            if (details.Children.Count == 0) details.Children.Add(Hint("Esta etapa ainda está sendo preparada."));
            return details;
        });
        expander.IsExpanded = _stageExpanded.GetValueOrDefault(block.Id);
        expander.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty) _stageExpanded[block.Id] = expander.IsExpanded;
        };
        Avalonia.Automation.AutomationProperties.SetName(expander, "Etapa: " + title + ". " + status + ". Expandir detalhes");
        return new Border
        {
            Child = expander, CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 3),
            Background = UrbeTheme.Brush(UrbeTheme.Bg, .6)
        };
    }
}
