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
        public required string Key { get; init; }
        public required StackPanel Root { get; init; }
        public required StackPanel Head { get; init; }
        public required Button Open { get; init; }
        public StackPanel? List { get; set; }
        public IReadOnlyList<AiChatMessage> Messages { get; set; } = [];
        public Dictionary<string, (string Signature, Control View)> Items { get; } = new(StringComparer.Ordinal);
        /// <summary>#50: the stages drawn, [Start, End) of the chronology; null until the panel first opens (then: the latest).</summary>
        public (int Start, int End)? Window { get; set; }
        /// <summary>How many stages the chronology had at the last sync.</summary>
        public int Total { get; set; }
        public Button? Older { get; set; }
        public Button? Newer { get; set; }
        /// <summary>#1000: what the folded line last said; the same words keep the same controls.</summary>
        public string? HeadWords { get; set; }
        /// <summary>The working bar, one for the whole run (a new one each render started its animation over).</summary>
        public ProgressBar? Bar { get; set; }
        /// <summary>The model's summary of what it is doing, in the folded line; its text follows the stream.</summary>
        public TextBlock? Thought { get; set; }
    }

    /// <summary>
    /// #50 (owner, beta.58): expanding hundreds of steps inside the conversation froze the app and there was no quick way to
    /// close them. The steps open in a panel that rises from the bottom, with a fixed X, in chronological order, drawing
    /// only a window of them — "Ver anteriores" and "Ver mais recentes" move it. Closing never stops the run.
    /// </summary>
    public const int StepsPage = 20, StepsMax = 60;
    private readonly Ui.BottomSheet _stepsSheet = new();
    private Timeline? _stepsShown;
    /// <summary>The panel of steps (tests).</summary>
    public Ui.BottomSheet StepsSheet => _stepsSheet;
    /// <summary>The list in the panel of steps (tests).</summary>
    public StackPanel? StepsList => _stepsShown?.List;

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

    /// <summary>
    /// A saved/tool-streamed JSON object can contain a duplicate key. System.Text.Json.Nodes
    /// throws ArgumentException when it first materializes that object's dictionary.
    /// A broken tool argument must not crash the entire conversation when we read the steps.
    /// The original tool input is preserved for debugging, and the stage remains visible.
    /// </summary>
    private static JsonNode? ActivityProperty(JsonNode? input, string name)
    {
        try { return input is JsonObject obj ? obj[name] : null; }
        catch (ArgumentException) { return null; }
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

        if (!running && !needsApproval && !needsAnswer && !needsForm &&
            ActivityProperty(calls.LastOrDefault(x => x.Block.Name == "update_plan").Block?.Input, "steps") is JsonArray remaining &&
            remaining.Any(x => x is not JsonObject || JsonText(ActivityProperty(x, "status")) != "done"))
            headline = "Execução encerrada · plano com pendências";
        var key = KeyOf(request);
        _timelinesUsed.Add(key);
        if (!_timelines.TryGetValue(key, out var timeline))
        {
            var head = new StackPanel { Spacing = 5 };
            var open = new Button
            {
                Content = head, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = UrbeTokens.MinTouch
            };
            timeline = new Timeline { Key = key, Root = new StackPanel { Spacing = 9 }, Head = head, Open = open };
            var made = timeline;
            open.Click += (_, _) => OpenSteps(made);
            _timelines[key] = timeline;
        }
        timeline.Messages = messages;

        // what the folded line says, in words first: the controls are made again only when the words change (#1000:
        // every streamed token cleared and remade them, and the indeterminate bar started over)
        string Step(AiChatBlock b)
        {
            try { return ToolByName(b.Name)?.Label(b.Input as JsonObject ?? new JsonObject()) ?? b.Name; }
            catch (Exception e) when (e is not OutOfMemoryException) { return b.Name; }
        }
        // #50: what the model says it is doing, in sight while it runs (its own summary; nothing is made up)
        string? tail = null;
        if (running && messages.LastOrDefault(m => m.Role == "assistant" && m.Meta?.Reasoning is { Length: > 0 })?.Meta?.Reasoning is { } thinking)
        {
            tail = thinking.Trim().Replace("\r", "");
            if (tail.Length > 240) tail = "…" + tail[^240..].TrimStart();
        }
        // the steps in words, the latest three in sight even folded (the way Claude and Cursor show what the agent does)
        var rows = new List<(string Mark, bool Error, string Text)>();
        foreach (var (block, _) in calls.Where(x => x.Block.Name is not ("update_plan" or "ask_user")).TakeLast(_cfg.StepsOpen ? 6 : 3))
        {
            var result = ResultFor(block.Id);
            rows.Add((result is null ? "⋯" : result.IsError ? "✗" : "✓", result is { IsError: true }, result is { IsError: false } ? StepWords.Past(Step(block)) : Step(block)));
        }
        int failures = 0, reads = 0, writes = 0;
        foreach (var (block, _) in calls)
        {
            if (ResultFor(block.Id) is not { } r) continue;
            if (r.IsError) { failures++; continue; }
            if (block.Name is "read_note" or "read_notes" or "web_fetch") reads++;
            if (ToolByName(block.Name)?.Access == "write") writes++;
        }
        static string Count(int n, string one, string many) => n + " " + (n == 1 ? one : many);
        var parts = new List<string>();
        if (reads > 0) parts.Add("leu " + Count(reads, "fonte", "fontes"));
        if (writes > 0) parts.Add("alterou " + Count(writes, "nota", "notas"));
        if (failures > 0) parts.Add(Count(failures, "alerta", "alertas"));
        if (calls.Count > 0) parts.Add(Count(calls.Count, "etapa", "etapas") + " · tocar para ver as etapas");
        var detail = calls.Count == 0 ? (running ? "Preparando as próximas etapas" : "Processamento concluído") : string.Join(" · ", parts);
        if (detail.Length > 0) detail = char.ToUpper(detail[0]) + detail[1..];
        bool bar = running && !UrbeTokens.Motion.Reduced;
        var words = string.Join("\n", [UrbeTheme.Text + UrbeTheme.Accent, headline, running ? "1" : "0", tail is null ? "" : "pensando", .. rows.Select(r => r.Mark + r.Text), detail, bar ? "1" : "0"]);
        if (words != timeline.HeadWords)
        {
            timeline.HeadWords = words;
            var header = timeline.Head;
            header.Children.Clear();
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*") };
            var icon = UrbeTheme.Icon(UrbeTheme.Icons.Assistant, 16, running ? UrbeTheme.Accent : UrbeTheme.Text2);
            var heading = Wrap(headline, 13, UrbeTheme.Text, FontWeight.SemiBold);
            Grid.SetColumn(heading, 1);
            line.Children.Add(icon);
            line.Children.Add(heading);
            header.Children.Add(line);
            if (tail is not null)
            {
                var thought = timeline.Thought ??= Wrap("", 12, UrbeTheme.Text3);
                thought.FontStyle = FontStyle.Italic;
                thought.MaxLines = 3;
                thought.TextTrimming = TextTrimming.CharacterEllipsis;
                header.Children.Add(thought);
            }
            foreach (var (mark, error, text) in rows)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*") };
                var sign = Label(mark, 12, error ? UrbeTheme.Danger : UrbeTheme.Text3);
                sign.HorizontalAlignment = HorizontalAlignment.Center;
                var what = Wrap(text, 12, UrbeTheme.Text2);
                Grid.SetColumn(what, 1);
                row.Children.Add(sign);
                row.Children.Add(what);
                header.Children.Add(row);
            }
            header.Children.Add(Wrap(detail, 11, UrbeTheme.Text3));
            if (bar)
                header.Children.Add(timeline.Bar ??= new ProgressBar
                {
                    IsIndeterminate = true, Height = 2, Minimum = 0, Maximum = 1,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Accent),
                    Background = UrbeTheme.Brush(UrbeTheme.Line2)
                });
        }

        // the summary of the reasoning grows with every token: only its text changes
        if (tail is not null && timeline.Thought is { } shown && shown.Text != tail) shown.Text = tail;

        // the open panel keeps its controls: only new or changed stages are made (CST-112)
        if (ReferenceEquals(_stepsShown, timeline) && _stepsSheet.IsOpen) SyncTimeline(timeline);
        Avalonia.Automation.AutomationProperties.SetName(timeline.Open, "Atividade do agente: " + headline + ". " + detail);
        var root = timeline.Root;
        if (root.Children.Count == 0)
            root.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(6, 7, 2, 7),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = timeline.Open
            });
        while (root.Children.Count > 1) root.Children.RemoveAt(1);

        // Actionable requests must never be buried inside a collapsed operation history.
        if (needsApproval && _pending is { } action) root.Children.Add(Approval(action.Request));
        if (needsAnswer && lastQuestion is not null) root.Children.Add(Clarification(lastQuestion));

        return root;
    }

    /// <summary>Opens the panel of steps of a request, at its latest steps.</summary>
    private void OpenSteps(Timeline timeline)
    {
        _stepsShown = timeline;
        timeline.Window = null;
        // The previous sheet may still be animating closed: its ScrollViewer still owns
        // the old list. Never attach the same visual StackPanel to a second presenter.
        // Preserve the underlying messages/expanded state; remount only view controls.
        timeline.List = null;
        timeline.Items.Clear();
        timeline.Older = null;
        timeline.Newer = null;
        var title = Label("Etapas · " + timeline.Messages.Sum(m => m.Content.Count(b => b.Type == "tool_use")), 15, UrbeTheme.Text);
        title.FontWeight = FontWeight.SemiBold;
        title.VerticalAlignment = VerticalAlignment.Center;
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 18, UrbeTheme.Text2), MinWidth = UrbeTokens.MinTouch, MinHeight = UrbeTokens.MinTouch,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Right
        };
        Avalonia.Automation.AutomationProperties.SetName(close, "Fechar etapas");
        ToolTip.SetTip(close, "Fechar (a IA continua trabalhando)");
        close.Click += (_, _) => _ = _stepsSheet.CloseAsync();
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(close, Dock.Right);
        bar.Children.Add(close);
        bar.Children.Add(title);
        var scroll = new ScrollViewer { Content = SyncTimeline(timeline), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var body = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        body.Children.Add(bar);
        body.Children.Add(scroll);
        _stepsSheet.Open(body);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => scroll.ScrollToEnd(), Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// The chronology (reasoning, every operation, intermediate drafts) in the panel: only the window's stages are
    /// controls, reusing the ones whose state did not change; parts are still loaded only when the person opens them.
    /// While the window reaches the latest step it follows the run.
    /// </summary>
    private StackPanel SyncTimeline(Timeline timeline)
    {
        if (timeline.List is null)
        {
            timeline.List = new StackPanel { Spacing = 5, Margin = new Thickness(0, 2, 0, 2) };
            Ui.Deferred.Watch(timeline.List);
        }
        var list = timeline.List;
        // #1000: every step is listed, but only the window's ones work out their signature and controls
        var all = new List<(string Key, Func<string> Signature, Func<Control> Make, double Estimate)>();
        foreach (var message in timeline.Messages)
        {
            var mk = KeyOf(message);
            if (message.Meta?.Reasoning is { Length: > 0 } thought)
                all.Add((mk + ":think", () => thought.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    () => Lazy(ThoughtPreview(thought), () => Markdown(thought)), 48));
            for (int i = 0; i < message.Content.Count; i++)
            {
                var block = message.Content[i];
                if (block.Type == "tool_use")
                {
                    var stageKey = block.Id.Length > 0 && !block.Pending ? "t:" + block.Id : mk + ":tool:" + i;
                    all.Add((stageKey, () => StageSignature(block, message), () => ActivityStage(block, message), 65));
                }
                else if (block.Type == "text" && !block.Hidden && !string.IsNullOrEmpty(block.Text))
                {
                    var draft = block.Text;
                    bool isDraft = message.Meta?.Draft == true;
                    all.Add((mk + ":text:" + i, () => (isDraft ? "d" : "o") + draft.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        () => Lazy(Label(isDraft ? "Rascunho intermediário" : "Observação do agente", 12, UrbeTheme.Text2), () => Markdown(draft)), 48));
                }
            }
        }
        // the window: the latest page at first; it follows new steps while it shows the last one
        int total = all.Count;
        var (start, end) = timeline.Window is { } w ? (w.Start, w.End) : (Math.Max(0, total - StepsPage), total);
        if (timeline.Window is { } was && was.End >= timeline.Total) end = total;   // it was at the latest step: it follows
        timeline.Total = total;
        end = Math.Min(end, total);
        if (end - start > StepsMax) start = end - StepsMax;
        start = Math.Clamp(start, 0, end);
        timeline.Window = (start, end);

        var desired = new List<Control>();
        // the two page buttons are kept: a streamed token does not rebuild the list around them
        timeline.Older ??= PageButton("Ver as etapas anteriores", () =>
        {
            var (s2, e2) = timeline.Window!.Value;
            s2 = Math.Max(0, s2 - StepsPage);
            timeline.Window = (s2, Math.Min(e2, s2 + StepsMax));
            SyncTimeline(timeline);
        });
        timeline.Newer ??= PageButton("Ver as etapas mais recentes", () =>
        {
            var (s2, e2) = timeline.Window!.Value;
            e2 = Math.Min(timeline.Total, e2 + StepsPage);
            timeline.Window = (Math.Max(s2, e2 - StepsMax), e2);
            SyncTimeline(timeline);
        });
        timeline.Older.Content = "Ver anteriores (" + start + ")";
        timeline.Newer.Content = "Ver mais recentes (" + (total - end) + ")";
        if (start > 0) desired.Add(timeline.Older);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, signatureOf, make, estimate) in all.Skip(start).Take(end - start))
        {
            seen.Add(key);
            var signature = signatureOf();
            if (timeline.Items.TryGetValue(key, out var kept) && kept.Signature == signature) { desired.Add(kept.View); continue; }
            var view = new Ui.Deferred(make, estimate);
            if (kept.View is Ui.Deferred { IsMade: true }) view.Make();   // a step that changed on screen is redrawn at once
            timeline.Items[key] = (signature, view);
            desired.Add(view);
        }
        if (end < total) desired.Add(timeline.Newer);
        foreach (var gone in timeline.Items.Keys.Where(k => !seen.Contains(k)).ToArray()) timeline.Items.Remove(gone);
        if (desired.Count == 0) desired.Add(Hint("O modelo ainda não registrou operações."));
        // #1000: a step that changes (the one being written, every token) is swapped in its place and a new one is added
        // at the end; clearing and refilling the list made all of it be laid out again on each token
        Replace(list.Children, desired, new HashSet<Control>(desired));
        return list;
    }

    /// <summary>#50: the model's summary of its reasoning, in sight (its start; a tap shows all of it).</summary>
    private static Control ThoughtPreview(string thought)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(thought.Trim(), @"\s+", " ");
        var preview = Wrap(text.Length > 220 ? text[..220] + "…" : text, 12, UrbeTheme.Text3);
        preview.FontStyle = FontStyle.Italic;
        preview.MaxLines = 3;
        preview.TextTrimming = TextTrimming.CharacterEllipsis;
        return preview;
    }

    private static Button PageButton(string name, Action run)
    {
        var b = new Button
        {
            MinHeight = UrbeTokens.MinTouch, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center, Background = Brushes.Transparent
        };
        Avalonia.Automation.AutomationProperties.SetName(b, name);
        b.Click += (_, _) => run();
        return b;
    }

    /// <summary>What a stage shows: when any of it changes, the stage is made again.</summary>
    private string StageSignature(AiChatBlock block, AiChatMessage message)
    {
        var result = ResultFor(block.Id);
        var sb = new System.Text.StringBuilder();
        sb.Append(block.Pending).Append('|').Append(block.Name).Append('|').Append(block.Partial?.Length ?? 0).Append('|')
            .Append(result?.Status).Append('|').Append(result?.IsError).Append('|').Append(result?.Content.Length ?? -1).Append('|')
            .Append(_pending?.Request.Call.Id == block.Id).Append('|').Append(message.Meta?.Streaming).Append('|').Append(_running is null).Append('|')
            .Append(_progress.GetValueOrDefault(block.Id)).Append('|').Append(_nested.GetValueOrDefault(block.Id)?.Count).Append('|').Append(_toolElapsed.GetValueOrDefault(block.Id)).Append('|')
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
        "write_publication" or "publication_schema" => "Preparando uma publicação",
        "review_publication" => "Revisando a publicação",
        "create_video" => "Criando o vídeo",
        "look_at_video" => "Olhando os quadros do vídeo",
        "edit_video_scene" => "Ajustando uma cena",
        "render_video" => "Gerando o MP4",
        "delegate_research" => "Pesquisando com outro agente",
        "consult_agent" => "Consultando especialista",
        "delegate_to_agent" => "Delegando a um especialista",
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
            if (_nested.TryGetValue(block.Id, out var nested))
                foreach (var step in nested) details.Children.Add(Wrap("↳ " + step, 12, UrbeTheme.Text2));
            if (block.Name == "update_plan" && ActivityProperty(block.Input, "steps") is JsonArray steps && block.Id == LatestPlanId())
                details.Children.Add(Plan(steps));
            if (block.Name == "ask_user" && ActivityProperty(block.Input, "question") is JsonValue question)
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
