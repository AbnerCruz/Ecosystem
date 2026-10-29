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

    /// <summary>
    /// CST-112: the current request's timeline, kept between renders. Each render (every streamed token) only updates the
    /// header and the stages whose state changed; the others, and their expanded details, are the same controls.
    /// </summary>
    private sealed class Timeline
    {
        public required StackPanel Root { get; init; }
        public required StackPanel Head { get; init; }
        public required Expander Fold { get; init; }
        public StackPanel? List { get; set; }
        public IReadOnlyList<AiChatMessage> Messages { get; set; } = [];
        public Dictionary<string, (string Signature, Control View)> Items { get; } = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<string, Timeline> _timelines = new(StringComparer.Ordinal);
    private readonly HashSet<string> _timelinesUsed = new(StringComparer.Ordinal);

    private static string KeyOf(AiChatMessage m) =>
        m.Id.Length > 0 ? m.Id : "#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(m).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The timelines of requests no longer on screen are dropped (called once per render of the log).</summary>
    private void ForgetUnusedTimelines()
    {
        foreach (var key in _timelines.Keys.Where(k => !_timelinesUsed.Contains(k)).ToArray()) _timelines.Remove(key);
        _timelinesUsed.Clear();
    }

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
        bool needsForm = !running && _conv.Forms.Any(f => f.State == "pending" && f.ConversationId == _conv.Id);
        var headline = needsForm ? "Aguardando o formulário" : needsApproval ? "Aguardando sua aprovação"
            : needsAnswer ? "Aguardando sua resposta"
            : running && last is not null ? ActivityVerb(last.Name)
            : running ? "Analisando o pedido"
            : _conv.Runs.LastOrDefault()?.Stopped == "no_progress" ? "Ciclo sem progresso interrompido"
            : _conv.Runs.LastOrDefault()?.Stopped == "aborted" ? "Execução interrompida"
            : _conv.Runs.LastOrDefault()?.Stopped == "error" ? "Execução com erro"
            : calls.Any(x => ResultFor(x.Block.Id) is { IsError: true }) ? "Atividade concluída com alertas"
            : calls.Count > 0 ? "Trabalho realizado" : "Análise concluída";

        if (!running && !needsApproval && !needsAnswer && !needsForm && (calls.LastOrDefault(x => x.Block.Name == "update_plan").Block?.Input as JsonObject)?["steps"] is JsonArray remaining &&
            remaining.Any(x => x is not JsonObject step || JsonText(step["status"]) != "done"))
            headline = "Execução encerrada · plano com pendências";
        var key = KeyOf(request);
        _timelinesUsed.Add(key);
        if (!_timelines.TryGetValue(key, out var timeline))
        {
            var head = new StackPanel { Spacing = 5 };
            Timeline? made = null;
            var fold = Lazy(head, () => SyncTimeline(made!));
            made = timeline = new Timeline { Root = new StackPanel { Spacing = 9 }, Head = head, Fold = fold };
            fold.IsExpanded = _activityExpanded.GetValueOrDefault(request.Id);
            fold.PropertyChanged += (_, e) =>
            {
                if (e.Property == Expander.IsExpandedProperty) _activityExpanded[request.Id] = fold.IsExpanded;
            };
            _timelines[key] = timeline;
        }
        timeline.Messages = messages;
        var header = timeline.Head;
        header.Children.Clear();
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*") };
        var icon = UrbeTheme.Icon(UrbeTheme.Icons.Assistant, 16, running ? UrbeTheme.Accent : UrbeTheme.Text2);
        var heading = Wrap(headline, 13, UrbeTheme.Text, FontWeight.SemiBold);
        Grid.SetColumn(heading, 1);
        line.Children.Add(icon);
        line.Children.Add(heading);
        header.Children.Add(line);

        var failures = calls.Count(x => ResultFor(x.Block.Id) is { IsError: true });
        var reads = calls.Count(x => x.Block.Name is "read_note" or "read_notes" or "web_fetch" && ResultFor(x.Block.Id) is { IsError: false });
        var writes = calls.Count(x => ToolByName(x.Block.Name)?.Access == "write" && ResultFor(x.Block.Id) is { IsError: false });
        var outcomes = (reads > 0 ? " · " + reads + " leituras" : "") + (writes > 0 ? " · " + writes + " alterações" : "") +
            (failures > 0 ? " · " + failures + " alertas" : "");
        var detail = calls.Count == 0 ? (running ? "Preparando as próximas etapas" : "Processamento concluído")
            : calls.Count == 1 ? "1 operação · tocar para ver os detalhes"
            : calls.Count + " operações · tocar para explorar as etapas";
        header.Children.Add(Wrap(calls.Count > 0 ? calls.Count + (calls.Count == 1 ? " operação" : " operações") + outcomes + " · Detalhes" : detail, 11, UrbeTheme.Text2));
        if (running && !UrbeTokens.Motion.Reduced)
            header.Children.Add(new ProgressBar
            {
                IsIndeterminate = true, Height = 2, Minimum = 0, Maximum = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Foreground = UrbeTheme.Brush(UrbeTheme.Accent),
                Background = UrbeTheme.Brush(UrbeTheme.Line2)
            });

        // an expanded history keeps its controls: only new or changed stages are made (CST-112)
        if (timeline.Fold.Content is not null) SyncTimeline(timeline);
        Avalonia.Automation.AutomationProperties.SetName(timeline.Fold, "Atividade do agente: " + headline + ". " + detail);
        var root = timeline.Root;
        if (root.Children.Count == 0)
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(6, 7, 2, 7),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = timeline.Fold
            });
        while (root.Children.Count > 1) root.Children.RemoveAt(1);

        // Actionable requests must never be buried inside a collapsed operation history.
        if (needsApproval && _pending is { } action) root.Children.Add(Approval(action.Request));
        if (needsAnswer && lastQuestion is not null) root.Children.Add(Clarification(lastQuestion));

        return root;
    }

    /// <summary>
    /// The whole chronology (reasoning, every operation, intermediate drafts) as controls, reusing the ones whose state did
    /// not change; parts are still loaded only when the person opens them.
    /// </summary>
    private StackPanel SyncTimeline(Timeline timeline)
    {
        var list = timeline.List ??= new StackPanel { Spacing = 5, Margin = new Thickness(0, 8, 0, 2) };
        var desired = new List<Control>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Control Item(string key, string signature, Func<Control> make)
        {
            seen.Add(key);
            if (timeline.Items.TryGetValue(key, out var kept) && kept.Signature == signature) return kept.View;
            var view = make();
            timeline.Items[key] = (signature, view);
            return view;
        }
        foreach (var message in timeline.Messages)
        {
            var mk = KeyOf(message);
            if (message.Meta?.Reasoning is { Length: > 0 } thought)
                desired.Add(Item(mk + ":think", thought.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    () => Lazy(Label("Resumo de raciocínio do modelo", 12, UrbeTheme.Text2), () => Markdown(thought))));
            for (int i = 0; i < message.Content.Count; i++)
            {
                var block = message.Content[i];
                if (block.Type == "tool_use")
                {
                    var stageKey = block.Id.Length > 0 && !block.Pending ? "t:" + block.Id : mk + ":tool:" + i;
                    desired.Add(Item(stageKey, StageSignature(block, message), () => ActivityStage(block, message)));
                }
                else if (block.Type == "text" && !block.Hidden && !string.IsNullOrEmpty(block.Text))
                {
                    var draft = block.Text;
                    bool isDraft = message.Meta?.Draft == true;
                    desired.Add(Item(mk + ":text:" + i, (isDraft ? "d" : "o") + draft.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        () => Lazy(Label(isDraft ? "Rascunho intermediário" : "Observação do agente", 12, UrbeTheme.Text2), () => Markdown(draft))));
                }
            }
        }
        foreach (var gone in timeline.Items.Keys.Where(k => !seen.Contains(k)).ToArray()) timeline.Items.Remove(gone);
        if (desired.Count == 0) desired.Add(Hint("O modelo ainda não registrou operações."));
        // appending is the common case (a new step): keep every control in place and add the new ones
        int same = 0;
        while (same < desired.Count && same < list.Children.Count && ReferenceEquals(list.Children[same], desired[same])) same++;
        if (same == list.Children.Count) for (int i = same; i < desired.Count; i++) list.Children.Add(desired[i]);
        else if (same < desired.Count || same < list.Children.Count)
        {
            list.Children.Clear();
            list.Children.AddRange(desired);
        }
        return list;
    }

    /// <summary>What a stage shows: when any of it changes, the stage is made again.</summary>
    private string StageSignature(AiChatBlock block, AiChatMessage message)
    {
        var result = ResultFor(block.Id);
        var sb = new System.Text.StringBuilder();
        sb.Append(block.Pending).Append('|').Append(block.Name).Append('|').Append(block.Partial?.Length ?? 0).Append('|')
            .Append(result?.Status).Append('|').Append(result?.IsError).Append('|').Append(result?.Content.Length ?? -1).Append('|')
            .Append(_pending?.Request.Call.Id == block.Id).Append('|').Append(message.Meta?.Streaming).Append('|').Append(_running is null).Append('|')
            .Append(_progress.GetValueOrDefault(block.Id)).Append('|').Append(_toolElapsed.GetValueOrDefault(block.Id)).Append('|')
            .Append(block.Name == "update_plan" && block.Id == LatestPlanId());
        return sb.ToString();
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
        "consult_agent" => "Consultando especialista",
        "web_search" => "Pesquisando na internet",
        "web_fetch" => "Analisando fontes públicas",
        _ => "Executando uma etapa"
    };

    private Control ActivityStage(AiChatBlock block, AiChatMessage message, bool current = true)
    {
        var result = ResultFor(block.Id);
        var tool = ToolByName(block.Name);
        var title = tool is not null && !block.Pending ? SafeLabel(tool, block.Input)
            : block.Pending ? "Preparando " + block.Name : block.Name;
        var pending = _pending?.Request.Call.Id == block.Id && block.Id.Length > 0;
        var status = result?.Status == "reused" ? "Leitura reutilizada · mesma versão" : result?.Status == "noop" ? "Sem alteração · gravação dispensada" : result?.Status == "rejected" ? "Recusada"
            : result is { IsError: true } ? "Falhou"
            : result is not null ? "Concluída"
            : pending && current ? "Aguardando aprovação"
            : message.Meta?.Streaming == true ? "Preparando"
            : current && _running is not null ? "Em andamento" : "Interrompida";
        if (block.Name == "update_plan" && result is { IsError: false }) status = "Registrado";
        if (_toolElapsed.TryGetValue(block.Id, out var elapsed)) status += " · " + (elapsed / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*"), MinHeight = 48 };
        var label = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(Wrap(title, 12, UrbeTheme.Text));
        label.Children.Add(Label(status, 11, result is { IsError: true } ? UrbeTheme.Danger : UrbeTheme.Text2));
        Grid.SetColumn(label, 1);
        header.Children.Add(UrbeTheme.Icon(AccessIcon.GetValueOrDefault(tool?.Access ?? "", UrbeTheme.Icons.Layers), 15, UrbeTheme.Text2));
        header.Children.Add(label);
        var expander = Lazy(header, () =>
        {
            var details = new StackPanel { Spacing = 8, Margin = new Thickness(4, 4, 4, 8) };
            if (_progress.TryGetValue(block.Id, out var progress)) details.Children.Add(Wrap(progress, 12, UrbeTheme.Text2));
            if (block.Name == "update_plan" && (block.Input as JsonObject)?["steps"] is JsonArray steps && block.Id == LatestPlanId())
                details.Children.Add(Plan(steps));
            if (block.Name == "ask_user" && (block.Input as JsonObject)?["question"] is JsonValue question)
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
            Child = expander, CornerRadius = new CornerRadius(0), Padding = new Thickness(0, 4),
            Background = Brushes.Transparent, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .45),
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
    }
}
