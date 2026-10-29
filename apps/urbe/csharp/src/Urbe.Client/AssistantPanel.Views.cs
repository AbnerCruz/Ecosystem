using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>The Assistente's messages, tool cards, approval with diff, run footer, empty states and overlaid views (ui.js).</summary>
public sealed partial class AssistantPanel
{
    private readonly Dictionary<string, string> _questionDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _approvalDrafts = new(StringComparer.Ordinal);
    // ---------------------------------------------------------------- messages

    private Control MessageView(AiChatMessage m)
    {
        if (m.Role == "user")
        {
            if (m.Meta?.ToolResults == true) return new Panel { IsVisible = false };
            var text = string.Join("\n\n", m.Content.Where(b => b.Type == "text" && !b.Hidden).Select(b => b.Text));
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 520, Spacing = 4 };
            if (m.Meta?.Context is { Length: > 0 } ctx)
                stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right, Children = { UrbeTheme.Icon(UrbeTheme.Icons.File, 12, UrbeTheme.Text3), Label(ctx, 11, UrbeTheme.Text3) } });
            var files = m.Content.Where(b => b.Type == "attachment").Select(b => b.FileName)
                .Concat(m.Content.Where(b => b.Hidden && b.Type == "text" && b.Text?.StartsWith("[Arquivo anexado pelo usuário:", StringComparison.Ordinal) == true)
                    .Select(b => b.Text!.Split('\n')[0].Trim('[', ']'))).ToList();
            if (files.Count > 0) stack.Children.Add(Wrap("Anexos: " + string.Join(", ", files), 11, UrbeTheme.Text2));
            var bubble = new SelectableTextBlock { Text = text, FontSize = 15, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text) };
            Ui.TextActions.Attach(bubble); // native range selection, contextual copy and select-all
            stack.Children.Add(new Border { Child = bubble, Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(16, 16, 4, 16), Background = UrbeTheme.Brush(UrbeTheme.Accent, .16) });
            var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            actions.Children.Add(LinkButton("Copiar", () => _ = CopySentMessageAsync(text)));
            actions.Children.Add(LinkButton("Selecionar trecho", () => SelectSentMessageText(text)));
            actions.Children.Add(LinkButton("Editar e reenviar", () => _ = EditSentMessageAsync(m, text)));
            foreach (var attachment in AssistantAttachmentExtraction.List(m))
                actions.Children.Add(LinkButton("Extrair " + attachment.Name, () => _ = ExtractMessageFileAsync(m, attachment.Name)));
            stack.Children.Add(actions);
            return stack;
        }
        var view = new StackPanel { Spacing = 8 };
        var meta = m.Meta;
        if (meta?.Reasoning is { Length: > 0 } reasoning)
            view.Children.Add(Lazy(Label(meta.Streaming == true && m.Content.Count == 0 ? "Pensando…" : "Resumo de raciocínio do modelo", 12, UrbeTheme.Text3), () => Wrap(reasoning, 12, UrbeTheme.Text3)));
        foreach (var b in m.Content)
        {
            if (b.Type == "text")
            {
                var md = Markdown(b.Text ?? "");
                // an answer given before finishing the reading: folded, the revised one comes next
                view.Children.Add(meta?.Draft == true
                    ? Lazy(Label("Resposta preliminar (substituída pela revisada abaixo)", 12, UrbeTheme.Text3), () => md)
                    : md);
            }
            else if (b.Type == "tool_use") view.Children.Add(ToolCard(b, m));
        }
        if (meta?.Streaming == true && m.Content.Count == 0 && string.IsNullOrEmpty(meta.Reasoning))
            view.Children.Add(Label("•••", 18, UrbeTheme.Text3));
        // UC-80: an answer to a request made in a note can be kept there, as a quote under the line that asked it
        var answer = string.Join("\n\n", m.Content.Where(b => b.Type == "text" && !string.IsNullOrWhiteSpace(b.Text)).Select(b => b.Text!.Trim()));
        if (answer.Length > 0 && meta?.Draft != true)
        {
            var actions = new WrapPanel();
            var copy = LinkButton("Copiar", () => { });
            copy.Click += async (_, _) =>
            {
                var copied = await Ui.TextActions.CopyAsync(this, string.Join("\n\n", m.Content.Where(b => b.Type == "text" && !b.Hidden).Select(b => b.Text)));
                _app.Notify(copied ? "Texto copiado." : "Não foi possível copiar o texto.");
            };
            actions.Children.Add(copy);
            actions.Children.Add(LinkButton("Selecionar texto", () =>
            {
                var body = OpenView("Selecionar texto");
                var text = new TextBox { Text = string.Join("\n\n", m.Content.Where(b => b.Type == "text" && !b.Hidden).Select(b => b.Text)),
                    IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 200 };
                Ui.TextActions.Attach(text);
                body.Children.Add(text);
                body.Children.Add(LinkButton("Selecionar tudo", () => { text.Focus(); text.SelectAll(); TouchTextScroll.ShowSelectionHandles(text); }));
                body.Children.Add(Hint("Texto original em Markdown. Selecione qualquer trecho para copiar."));
            }));
            view.Children.Add(actions);
        }
        if (meta?.Streaming != true && meta?.Draft != true && answer.Length > 0 && AskedInNote() is { } asked)
        {
            Button? pin = null;
            pin = LinkButton("Pôr na nota", () => { pin!.IsEnabled = false; _ = PinAnswerAsync(asked.Path, asked.Agent, asked.Request, answer, meta?.Model ?? ModelId()); });
            Avalonia.Automation.AutomationProperties.SetName(pin, "Pôr a resposta na nota");
            view.Children.Add(pin);
        }
        return view;
    }

    private async Task CopySentMessageAsync(string text) =>
        _app.Notify(await Ui.TextActions.CopyAsync(this, text) ? "Mensagem copiada." : "Não foi possível copiar a mensagem.");

    private void SelectSentMessageText(string original)
    {
        var body = OpenView("Selecionar trecho da mensagem");
        var field = new TextBox
        {
            Text = original, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 180, SelectionBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35)
        };
        Ui.TextActions.Attach(field);
        body.Children.Add(field);
        body.Children.Add(LinkButton("Selecionar mensagem inteira", () =>
        {
            field.Focus(); field.SelectAll(); TouchTextScroll.ShowSelectionHandles(field);
        }));
        body.Children.Add(Hint("Toque e selecione um trecho. Copie pelo menu nativo."));
    }

    private async Task EditSentMessageAsync(AiChatMessage message, string original)
    {
        if (!_conv.Messages.Contains(message)) return;
        if (!string.IsNullOrWhiteSpace(_input.Text) && _input.Text != original &&
            !await _app.Dialogs.ConfirmAsync("Substituir rascunho?",
                "Você já estava escrevendo outra mensagem. O rascunho atual será substituído; a mensagem original no histórico não muda.", "Substituir")) return;
        CloseView();
        _input.Text = original;
        _input.CaretIndex = original.Length;
        _input.Focus();
        _app.Notify("Edite e envie como novo pedido. A mensagem original permanece no histórico para preservar as respostas anteriores.");
    }

    private static readonly Dictionary<string, string> AccessIcon = new()
    {
        ["read"] = UrbeTheme.Icons.Search, ["write"] = UrbeTheme.Icons.Edit, ["destructive"] = UrbeTheme.Icons.Trash, ["ui"] = UrbeTheme.Icons.Open,
        ["memory"] = UrbeTheme.Icons.Star, ["plan"] = UrbeTheme.Icons.Check, ["plugin"] = UrbeTheme.Icons.Layers
    };

    private AiChatBlock? ResultFor(string id) => _results.GetValueOrDefault(id);

    private string? LatestPlanId() => _latestPlan;

    private Control ToolCard(AiChatBlock b, AiChatMessage m)
    {
        var tool = ToolByName(b.Name);
        string label = tool is not null && !b.Pending ? SafeLabel(tool, b.Input) : b.Pending ? "Preparando " + b.Name + "…" : b.Name;
        var r = ResultFor(b.Id);
        bool pend = _pending is { } p && ReferenceEquals(p.Request.Call, b) || _pending is { } q && q.Request.Call.Id == b.Id && b.Id.Length > 0;
        var status = r is not null ? r.Status ?? (r.IsError ? "error" : "done") : pend ? "waiting" : m.Meta?.Streaming == true ? "pending" : _running is not null ? "running" : "aborted";
        var stTxt = status switch { "error" => "falhou", "rejected" => "recusado", "aborted" => "interrompido", "waiting" => "aguardando você", _ => "" };
        var head = new DockPanel();
        Control mark = status is "running" or "pending" ? Label("…", 15, UrbeTheme.Accent, FontWeight.Bold)
            : status == "done" ? UrbeTheme.Icon(UrbeTheme.Icons.Check, 16, UrbeTheme.Accent, 2.2)
            : Label(stTxt, 12, status == "error" ? "#c0504d" : UrbeTheme.Text3);
        DockPanel.SetDock(mark, Dock.Right);
        head.Children.Add(mark);
        var ic = UrbeTheme.Icon(AccessIcon.GetValueOrDefault(tool?.Access ?? "", UrbeTheme.Icons.Layers), 16, UrbeTheme.Text2, 1.8);
        DockPanel.SetDock(ic, Dock.Left);
        head.Children.Add(ic);
        var lbl = Label(label, 13, UrbeTheme.Text, FontWeight.SemiBold);
        lbl.Margin = new Thickness(8, 0);
        head.Children.Add(lbl);
        var card = new StackPanel { Spacing = 6, Children = { head } };
        if (_progress.TryGetValue(b.Id, out var prog) && r is null) card.Children.Add(Label(prog, 12, UrbeTheme.Text3));
        // Image output is shown in the conversation itself, not just as an expandable tool log.
        // The file remains authoritative in the vault. Older beta.20 results are supported too.
        if (b.Name == "generate_image" && r is { IsError: false } &&
            AssistantImageTools.SavedImagePath(r.Content) is { } generatedPath)
            card.Children.Add(GeneratedImagePreview(generatedPath));
        // only the latest plan shows open, as a checklist
        if (b.Name == "update_plan" && !b.Pending && b.Id == LatestPlanId() && (b.Input as JsonObject)?["steps"] is JsonArray steps) card.Children.Add(Plan(steps));
        if (b.Name == "ask_user" && r is { IsError: false } && _running is null &&
            _conv.Runs.LastOrDefault()?.Stopped == "needs_input" &&
            _conv.Messages.SelectMany(msg => msg.Content).LastOrDefault(x => x.Type == "tool_use" && x.Name == "ask_user")?.Id == b.Id)
            card.Children.Add(Clarification(b));
        if (pend && _pending is { } pending) card.Children.Add(Approval(pending.Request));
        else if (r is not null || !b.Pending)
        {
            card.Children.Add(Lazy(Label("Detalhes", 12, UrbeTheme.Text3), () =>
            {
                var more = new StackPanel { Spacing = 6 };
                if (!b.Pending) more.Children.Add(Pre(LegacyCustomize.Stringify(b.Input ?? new JsonObject(), 2), false));
                if (r is not null) more.Children.Add(Pre(r.Content, r.IsError));
                return more;
            }));
        }
        var border = Card(card, status == "waiting" ? UrbeTheme.Accent : null);
        border.Padding = new Thickness(10, 8);
        return border;
    }

    /// <summary>Load the saved vault image (not a temporary provider URL) as an inline, tappable result.</summary>
    private Control GeneratedImagePreview(string path)
    {
        var frame = new StackPanel { Spacing = 8 };
        var visual = new Border
        {
            MinHeight = 80, MaxHeight = 420, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Child = Label("Carregando imagem do vault…", 12, UrbeTheme.Text3)
        };
        frame.Children.Add(visual);
        var info = new StackPanel { Spacing = 2, Children =
        {
            Label("Imagem gerada", 13, UrbeTheme.Text, FontWeight.SemiBold),
            Wrap(path, 11, UrbeTheme.Text3)
        } };
        var open = TextButton("Abrir", () =>
        {
            // Native file preview is on the main stage, underneath the assistant overlay.
            Close();
            _ = _app.OpenGeneratedImageAsync(path);
        });
        Avalonia.Automation.AutomationProperties.SetName(open, "Abrir imagem gerada " + path);
        var actions = new StackPanel { Spacing = 4 };
        actions.Children.Add(info);
        var buttons = new WrapPanel { Children = { open,
            LinkButton("Exportar", () => _ = _app.ExportGeneratedImageAsync(path)),
            LinkButton("Copiar caminho", () => _ = CopyImagePath(path)) } };
        actions.Children.Add(buttons);
        frame.Children.Add(actions);

        bool loaded = false;
        async Task CopyImagePath(string value)
        {
            _app.Notify(await Ui.TextActions.CopyAsync(this, value) ? "Caminho copiado." : "Não foi possível copiar.");
        }
        async Task Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                var bytes = await _app.ReadGeneratedImageAsync(path);
                if (bytes is null || bytes.Length == 0 ||
                    AssistantProviders.ImageExtension(bytes) is null)
                {
                    visual.Child = Wrap("A imagem está no vault, mas a prévia não está disponível.", 12, UrbeTheme.Text3);
                    return;
                }
                using var input = new MemoryStream(bytes);
                var bitmap = Bitmap.DecodeToWidth(input, 580);
                var picture = new Avalonia.Controls.Image
                {
                    Source = bitmap, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
                    MaxHeight = 420, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left
                };
                // CST-111: leaving the screen frees the decoded picture, but the log keeps this card and may put it back:
                // the image lets go of it first (a freed bitmap drawn again closed the app at the end of long image runs)
                // and coming back decodes it again
                picture.DetachedFromVisualTree += (_, _) => { picture.Source = null; bitmap.Dispose(); loaded = false; };
                picture.AttachedToVisualTree += (_, _) => { if (picture.Source is null) _ = Load(); };
                visual.Child = picture;
            }
            catch (Exception)
            {
                visual.Child = Wrap("Não foi possível exibir a prévia. Use Abrir para ver a imagem no vault.", 12, UrbeTheme.Text3);
            }
        }
        visual.EffectiveViewportChanged += (_, e) => { if (e.EffectiveViewport.Width > 0 && e.EffectiveViewport.Height > 0) _ = Load(); };
        return frame;
    }

    /// <param name="owner">The conversation that asked (a consulted specialist's, CST-091); the one on screen by default.</param>
    /// <param name="answer">Where the answer goes instead of a new message here.</param>
    private Control Clarification(AiChatBlock call, AiConversation? owner = null, Action<string>? answer = null)
    {
        var info = call.Input as JsonObject ?? new JsonObject();
        var question = (string?)info["question"] ?? "O que você prefere?";
        var reason = (string?)info["reason"];
        // CST-115: the counts, the understanding the agent declares, what is still open and what changed
        var previous = (owner ?? _conv).Messages.SelectMany(m => m.Content).TakeWhile(x => x.Id != call.Id)
            .LastOrDefault(x => x.Type == "tool_use" && x.Name == "ask_user")?.Input as JsonObject;
        var progress = QuestionProgress.From(info, previous);
        var input = new TextBox { Text = _questionDrafts.GetValueOrDefault(call.Id, ""), AcceptsReturn = true, MinHeight = 64, MaxHeight = 180,
            TextWrapping = TextWrapping.Wrap, PlaceholderText = "Responda aqui; uma resposta pode resolver várias dúvidas." };
        Ui.TextActions.Attach(input);
        input.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) _questionDrafts[call.Id] = input.Text ?? ""; };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Wrap(question, 14, UrbeTheme.Text, FontWeight.SemiBold));
        if (!string.IsNullOrWhiteSpace(reason)) panel.Children.Add(Hint(reason));
        panel.Children.Add(Label(progress.Summary + " (estimativa)", 12, UrbeTheme.Text3));
        panel.Children.Add(new ProgressBar { Minimum = 0, Maximum = progress.Total, Value = progress.Resolved, Height = 5 });
        if (progress.Change is { } change) panel.Children.Add(Wrap(change, 12, UrbeTheme.Text2));
        if (progress.Pending.Count > 0)
            panel.Children.Add(Wrap("Ainda em aberto: " + string.Join(" · ", progress.Pending), 12, UrbeTheme.Text3));
        panel.Children.Add(input);
        panel.Children.Add(TextButton("Responder e continuar", () =>
        {
            var text = (input.Text ?? "").Trim();
            if (text.Length == 0 || answer is null && _running is not null) return;
            _questionDrafts.Remove(call.Id);
            if (answer is not null) answer(text);
            else Send(text);
        }, primary: true));
        return panel;
    }

    private static string ClipText(string s, int n) => s.Length > n ? s[..n] : s;

    private static string SafeLabel(AiTool t, JsonNode? input)
    {
        try { return t.Label(input as JsonObject ?? new JsonObject()); }
        catch (Exception) { return t.Name; }
    }

    private static string? JsonText(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    // Tool results may contain entire documents or a single enormous line: all of it is shown, in pieces made as they
    // come near the screen (CodeBlockView), so expanding a step on a phone shapes only what is seen
    private static Control Pre(string text, bool error) => new Ui.CodeBlockView(text, error ? "Erro" : "Texto", 11.5);

    private static Control Plan(JsonArray steps)
    {
        var list = new StackPanel { Spacing = 6, Margin = new Thickness(2, 2, 0, 2) };
        foreach (var item in steps)
        {
            if (item is not JsonObject x) { list.Children.Add(Hint("Etapa inválida recebida do modelo. Consulte os parâmetros abaixo.")); continue; }
            var s = JsonText(x["status"]) ?? "pending";
            var mark = s switch { "done" => "✓", "in_progress" => "◐", _ => "○" };
            var description = new StackPanel { Spacing = 2 };
            description.Children.Add(Wrap(JsonText(x["step"]) ?? "Etapa sem descrição", 13, s == "done" ? UrbeTheme.Text3 : UrbeTheme.Text));
            if (JsonText(x["details"]) is { Length: > 0 } details) description.Children.Add(Wrap(details, 12, UrbeTheme.Text2));
            if (JsonText(x["acceptance"]) is { Length: > 0 } acceptance)
                description.Children.Add(Wrap("Concluído quando: " + acceptance, 11, UrbeTheme.Text3));
            list.Children.Add(Row(Label(mark, 13, s == "done" ? UrbeTheme.Accent : UrbeTheme.Text3), description));
        }
        return list;
    }

    // ---------------------------------------------------------------- approval with a preview

    /// <param name="respond">Where the answer goes (a consulted specialist's request, CST-091); the run on screen by default.</param>
    private Control Approval(AiApprovalRequest p, Action<AiApproval>? respond = null)
    {
        void Respond(AiApproval answer)
        {
            if (respond is not null) respond(answer);
            else if (_pending?.Request.Call.Id == p.Call.Id) Answer(answer);
        }
        var pv = p.Preview;
        bool danger = p.Access == "destructive", plugin = p.Access == "plugin";
        var box = new StackPanel { Spacing = 8 };
        box.Children.Add(Wrap((danger ? "Confirmar exclusão" : plugin ? "Criar e ligar plugin" : "Aprovar alteração") + (pv is { Path.Length: > 0 } ? " · " + pv.Path : ""), 13, UrbeTheme.Text, FontWeight.Bold));
        if (pv?.Note is { Length: > 0 } note) box.Children.Add(Wrap(note, 13, UrbeTheme.Text2));
        Control? diff = pv is not null && (pv.Before is not null || pv.After is not null) ? Diff(pv.Before, pv.After) : null;
        if (diff is not null) box.Children.Add(diff);
        // UC-51: the proposed text can be edited before approving; what is written is the person's version
        TextBox? editor = null;
        // the person edits the text, not the seals: the Urbe gives the result its seals (UC-51, AiAuthorship)
        var editable = pv?.After is { } proposed && pv.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? AiAuthorship.Strip(proposed) : pv?.After;
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        if (AssistantTools.CanEdit(p.Tool, pv))
        {
            var edit = TextButton("Editar", () => { });
            edit.Margin = new Thickness(0, 0, 8, 0);
            void BeginEdit(bool focus)
            {
                if (editor is not null) return;
                editor = new TextBox
                {
                    Text = _approvalDrafts.GetValueOrDefault(p.Call.Id, editable ?? ""), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, MaxHeight = 360,
                    FontFamily = UrbeTheme.MonoFont, FontSize = 13, Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
                    CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent)
                };
                Avalonia.Automation.AutomationProperties.SetName(editor, "Texto proposto");
                var at = diff is not null ? box.Children.IndexOf(diff) : box.Children.Count - 1;
                if (diff is not null) box.Children.Remove(diff);
                box.Children.Insert(Math.Max(0, at), editor);
                edit.IsVisible = false;
                Ui.TextActions.Attach(editor);
                editor.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) _approvalDrafts[p.Call.Id] = editor.Text ?? ""; };
                _approvalDrafts[p.Call.Id] = editor.Text ?? "";
                if (focus) editor.Focus();
            }
            edit.Click += (_, _) => BeginEdit(true);
            actions.Children.Add(edit);
            if (_approvalDrafts.ContainsKey(p.Call.Id)) BeginEdit(false);
        }
        var reject = TextButton("Recusar", () => Respond(AiApproval.Reject()));
        reject.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(reject);
        if (!danger && !plugin)
        {
            var all = TextButton("Aprovar todas", () => Respond(editor is not null && editor.Text != editable ? new AiApproval("approve_all", EditedText: editor.Text ?? "") : AiApproval.ApproveAll));
            ToolTip.SetTip(all, "Aprovar esta e as próximas alterações desta resposta");
            all.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(all);
        }
        actions.Children.Add(TextButton(danger ? "Excluir" : plugin ? "Ligar plugin" : "Aprovar",
            () => Respond(editor is not null && editor.Text != editable ? AiApproval.ApproveEdited(editor.Text ?? "") : AiApproval.Approve), primary: !danger, danger: danger));
        box.Children.Add(actions);
        // The surrounding timeline or contextual sheet is already a surface; don't draw another container.
        return new Border { Child = box, Padding = new Thickness(0, 8), Background = Brushes.Transparent };
    }

    /// <summary>diffLines(): an LCS line diff (huge files fall back to everything removed then added).</summary>
    public static List<(char Op, string Line)> DiffLines(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        var output = new List<(char, string)>();
        if ((long)a.Count * b.Count > 4_000_000)
        {
            output.AddRange(a.Select(l => ('-', l)));
            output.AddRange(b.Select(l => ('+', l)));
            return output;
        }
        int n = a.Count, m = b.Count;
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { output.Add((' ', a[x])); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) output.Add(('-', a[x++]));
            else output.Add(('+', b[y++]));
        }
        while (x < n) output.Add(('-', a[x++]));
        while (y < m) output.Add(('+', b[y++]));
        return output;
    }

    private static Control Diff(string? before, string? after)
    {
        var x = before is null ? [] : before.Split('\n');
        var y = after is null ? [] : after.Split('\n');
        var ops = DiffLines(x, y);
        var keep = new bool[ops.Count];
        for (int i = 0; i < ops.Count; i++)
            if (ops[i].Op != ' ')
                for (int k = Math.Max(0, i - 2); k <= Math.Min(ops.Count - 1, i + 2); k++) keep[k] = true;
        var lines = new StackPanel();
        bool gap = false;
        int shown = 0;
        const int Max = 160;
        for (int i = 0; i < ops.Count && shown < Max; i++)
        {
            if (!keep[i])
            {
                if (!gap) lines.Children.Add(Label("⋯", 12, UrbeTheme.Text3));
                gap = true;
                continue;
            }
            gap = false;
            shown++;
            var (op, line) = ops[i];
            lines.Children.Add(new Border
            {
                Background = op == '+' ? UrbeTheme.Brush("#3a9a5b", .16) : op == '-' ? UrbeTheme.Brush("#c0504d", .16) : Brushes.Transparent,
                Padding = new Thickness(4, 1),
                Child = new TextBlock { Text = op + " " + (line.Length > 0 ? line : " "), FontFamily = UrbeTheme.MonoFont, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text) }
            });
        }
        if (shown >= Max) lines.Children.Add(Label("… mais linhas", 12, UrbeTheme.Text3));
        int add = ops.Count(o => o.Op == '+'), del = ops.Count(o => o.Op == '-');
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Row(Label("+" + add, 12, "#3a9a5b", FontWeight.Bold), Label("−" + del, 12, "#c0504d", FontWeight.Bold)),
                new Border { Child = new ScrollViewer { Content = lines, MaxHeight = 320, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), Padding = new Thickness(4) }
            }
        };
    }

    // ---------------------------------------------------------------- footer of the last run: undo, errors, continue

    private Control? Tail()
    {
        var r = _conv.Runs.LastOrDefault();
        if (r is null || _running is not null) return null;
        var tail = new StackPanel { Spacing = 8 };
        Control Note(string text, string action, Action act, bool error = false)
        {
            var row = new WrapPanel { Children = { Wrap(text, 13, error ? "#c0504d" : UrbeTheme.Text2), LinkButton(action, act) } };
            return Card(row, error ? "#c0504d" : null);
        }
        if (r.Error is { Length: > 0 }) tail.Children.Add(Note(r.Error, "Tentar de novo", () => CurrentRun = StartAsync(), error: true));
        else if (r.Stopped == "aborted") tail.Children.Add(Note("Interrompido.", "Continuar", () => Send("Continue de onde parou.")));
        else if (r.Stopped == "max_steps") tail.Children.Add(Note("Este trabalho foi interrompido por um limite de execução legado.", "Continuar", () => Send("Continue de onde parou.")));
        else if (r.Stopped == "no_progress") tail.Children.Add(Note("O modelo repetiu operações sem produzir informação ou alteração nova. O ciclo foi interrompido; nenhuma operação foi escondida.", "Continuar", () => Send("Continue usando as evidências já obtidas; explique o que ainda falta de fato.")));
        else if (r.Stopped == "loop") tail.Children.Add(Note("Parei: o agente repetia uma ação que falhava.", "Continuar", () => Send("Continue de onde parou.")));
        else if (r.Stopped == "max_tokens") tail.Children.Add(Note("A resposta foi cortada pelo limite do modelo.", "Continuar", () => Send("Continue de onde parou.")));
        if (r.Efficiency.Describe() is { } measured) tail.Children.Add(Wrap(measured, 11, UrbeTheme.Text2));
        if (r.Efficiency.ReusedReads > 0 || r.Efficiency.NoOpWrites > 0 || r.Efficiency.ContextCharsSaved > 0)
            tail.Children.Add(Wrap(r.Efficiency.ReusedReads + " leituras reutilizadas · " + r.Efficiency.NoOpWrites +
                " gravações sem alteração evitadas · " + r.Efficiency.ContextCharsSaved + " caracteres redundantes não reenviados", 11, UrbeTheme.Text2));
        if (r.Changes.Count > 0)
        {
            var files = new List<string>();
            foreach (var c in r.Changes)
                if ((c.After ?? c.Before)?.Path is { } path && !files.Contains(path)) files.Add(path);
            var head = new WrapPanel
            {
                Children =
                {
                    Row(UrbeTheme.Icon(UrbeTheme.Icons.Edit, 15, UrbeTheme.Text2), Label(r.Reverted ? "Alterações desfeitas" : files.Count + " nota" + (files.Count > 1 ? "s" : "") + " alterada" + (files.Count > 1 ? "s" : ""), 13, UrbeTheme.Text, FontWeight.SemiBold))
                }
            };
            if (!r.Reverted) head.Children.Add(LinkButton("Desfazer", () => _ = UndoLastAsync()));
            var list = new WrapPanel();
            foreach (var f in files.Take(8)) list.Children.Add(LinkButton(f, () => OpenRef(f)));
            tail.Children.Add(Card(new StackPanel { Spacing = 4, Children = { head, list } }));
        }
        if (_conv.WebSources.Count > 0)
        {
            var read = _conv.WebSources.Count(s => s.Status == "página acessada");
            tail.Children.Add(LinkButton("Fontes · " + _conv.WebSources.Count + " encontradas · " + read + " consultadas", ShowSources));
        }
        return tail.Children.Count > 0 ? tail : null;
    }

    // ---------------------------------------------------------------- empty states

    private static readonly string[] OnlinePresets = ["openrouter", "anthropic", "openai", "gemini", "ollama", "custom"];

    /// <summary>The providers one can add; the model on the device first, where the device has one.</summary>
    private static string[] PresetOrder => HasDeviceAi ? ["device", .. OnlinePresets] : OnlinePresets;

    private static readonly Dictionary<string, string> PresetDetail = new()
    {
        ["openrouter"] = "Recomendado · Claude, GPT, Gemini, Llama e outros com uma chave", ["anthropic"] = "Claude direto da Anthropic", ["openai"] = "GPT direto da OpenAI",
        ["device"] = "Grátis e sem internet · baixa uma vez e funciona sempre, até no modo avião", ["gemini"] = "Gemini do Google", ["ollama"] = "Modelos no seu computador, sem internet", ["custom"] = "LM Studio, vLLM, Groq… (API compatível)"
    };

    private Control Onboarding()
    {
        var box = new StackPanel { Spacing = 10, Margin = new Thickness(4, 24, 4, 0) };
        box.Children.Add(Wrap("Conecte um modelo", 20, UrbeTheme.Text, FontWeight.Bold));
        box.Children.Add(Wrap(HasDeviceAi
            ? "Use a IA que mora no seu aparelho (grátis, sem internet) ou conecte um provedor com a sua chave. A chave fica salva só neste aparelho."
            : "O Assistente funciona com qualquer modelo: escolha um provedor e cole sua chave. Ela fica salva só neste aparelho.", 14, UrbeTheme.Text2));
        foreach (var k in PresetOrder)
        {
            var pre = AssistantProviders.Presets[k];
            var b = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(0),
                Background = Brushes.Transparent, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .55),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Content = new StackPanel { Spacing = 2, Children = { Label(pre.Name, 15, UrbeTheme.Text, FontWeight.SemiBold), Wrap(PresetDetail[k], 12, UrbeTheme.Text3) } }
            };
            Avalonia.Automation.AutomationProperties.SetName(b, pre.Name);
            b.Click += (_, _) => { if (k == "device") ShowDevice(); else ShowSettings(k); };
            box.Children.Add(b);
        }
        return box;
    }

    private Control Empty()
    {
        var e = EditorInfo();
        var a = AgentNames.Named(AssistantAgent.AgentById(_conv.AgentId, _cfg.CustomAgents), _cfg);
        var sug = new List<string>();
        if (e.Path is { Length: > 0 }) sug.AddRange(["Resuma esta nota em tópicos", "Revise o texto desta nota: clareza e erros", "Encontre notas relacionadas e sugira [[links]] para esta"]);
        sug.AddRange(["O que tem no meu vault? Dê uma visão geral", "Encontre notas sem nenhuma ligação e sugira conexões", "Crie uma nota-índice que ligue as notas por tema"]);
        var box = new StackPanel { Spacing = 10, Margin = new Thickness(4, 32, 4, 0), HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 460 };
        box.Children.Add(new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(18), Background = UrbeTheme.Brush(UrbeTheme.Accent, .14), HorizontalAlignment = HorizontalAlignment.Center, Child = UrbeTheme.Icon(UrbeTheme.Icons.Assistant, 28, UrbeTheme.Accent, 1.8) });
        var title = Wrap(a.Name, 20, UrbeTheme.Text, FontWeight.Bold);
        title.TextAlignment = TextAlignment.Center;
        box.Children.Add(title);

        foreach (var s in sug.Take(5))
        {
            var b = new Button
            {
                Content = Wrap(s, 13, UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 9),
                CornerRadius = new CornerRadius(0), Background = Brushes.Transparent,
                BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .55), BorderThickness = new Thickness(0, 0, 0, 1)
            };
            b.Click += (_, _) => { _input.Text = s; Submit(); };
            box.Children.Add(b);
        }
        return box;
    }

    // ---------------------------------------------------------------- pickers

    private Task PickAgentAsync()
    {
        ShowAgents();
        return Task.CompletedTask;
    }

    public void ShowAgents()
    {
        var body = OpenView("Agentes");
        if (_activeConversations.Count > 0)
        {
            body.Children.Add(Hint("Conversas em andamento — toque para acompanhar ou aprovar."));
            foreach (var chat in _activeConversations.Values.ToList())
            {
                var currentChat = chat;
                var status = _pendingRuns.ContainsKey(chat.Id) ? " · Aguardando aprovação" : " · Trabalhando";
                var callName = AgentNames.CallName(AssistantAgent.AgentById(chat.AgentId, _cfg.CustomAgents), _cfg);
                body.Children.Add(TextButton(callName + status, () => OpenConversation(currentChat)));
            }
        }
        foreach (var agent in AgentNames.All(_cfg))
        {
            var item = new Button
            {
                MinHeight = UrbeTokens.MinTouch, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 10),
                CornerRadius = new CornerRadius(0), Background = Brushes.Transparent,
                BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .55), BorderThickness = new Thickness(0, 0, 0, 1),
                Content = Label(AgentNames.CallName(agent, _cfg), 15, UrbeTheme.Text, FontWeight.SemiBold)
            };
            Avalonia.Automation.AutomationProperties.SetName(item, AgentNames.CallName(agent, _cfg));
            item.Click += (_, _) => SelectAgent(agent.Id);
            body.Children.Add(item);
        }
        body.Children.Add(TextButton("+ Novo agente", () => EditAgent(null)));
    }

    public void SelectAgent(string agentId)
    {
        if (!AgentNames.All(_cfg).Any(a => a.Id == agentId)) return;
        Persist();
        _agentDrafts[_conv.AgentId] = (_input.Text ?? "", _pendingAttachments.ToList());
        var draft = _agentDrafts.GetValueOrDefault(agentId);
        _input.Text = draft.Text ?? "";
        _pendingAttachments.Clear();
        if (draft.Attachments is not null) _pendingAttachments.AddRange(draft.Attachments);
        RefreshAttachments();
        _cfg.AgentId = agentId;
        _conv = NewConversation(); // Opening an agent starts a fresh chat; old conversations remain in its history.
        SyncActiveConversation();
        _views.Clear();
        SaveConfig();
        CloseView();
        RenderAll();
    }

    private string PermissionSummary(AiAgentDef agent)
    {
        var scope = _cfg.ToolScopeFor(agent);
        var policy = _cfg.PolicyFor(agent.Id);
        if (policy == "readonly" || scope == "read") return "Somente leitura";
        var capacity = scope == "all" ? "Acesso completo" : "Leitura e escrita";
        return capacity + (policy == "auto" ? " · alterações automáticas" : " · pede aprovação");
    }

    private async Task PickPolicyAsync(AiAgentDef agent)
    {
        var modes = new (string Scope, string Policy, string Title, string Icon, string Detail)[]
        {
            ("read", "readonly", "Somente leitura", UrbeTheme.Icons.Search, "Consulta notas sem modificar arquivos"),
            ("write", "ask", "Editar com aprovação", UrbeTheme.Icons.Check, "Lê e escreve; você confirma cada alteração"),
            ("write", "auto", "Editar automaticamente", UrbeTheme.Icons.Edit, "Lê e escreve sem pedir aprovação por edição"),
            ("all", "ask", "Acesso completo com aprovação", UrbeTheme.Icons.Check, "Inclui ações destrutivas e plugins; exige confirmação"),
            ("all", "auto", "Acesso completo com autonomia", UrbeTheme.Icons.Edit, "Edições automáticas; exclusões e plugins sempre pedem confirmação")
        };
        var picked = await _app.Dialogs.ChooseAsync("Permissões de " + AgentNames.CallName(agent, _cfg),
            modes.Select(m => new DialogItem(m.Title, m.Icon, m.Detail)).ToArray());
        if (picked is not { } index || index < 0 || index >= modes.Length) return;
        _cfg.AgentToolScopes[agent.Id] = modes[index].Scope;
        _cfg.AgentPolicies[agent.Id] = modes[index].Policy;
        SaveConfig();
        EditAgentProfile(agent);
    }

    private async Task PickDelegationAsync(AiAgentDef agent)
    {
        if (_activeConversations.Values.Any(c => c.AgentId == agent.Id))
        { await _app.Dialogs.AlertAsync("Agente trabalhando", "Termine a execução antes de modificar sua permissão de delegação."); return; }
        var i = await _app.Dialogs.ChooseAsync("Colaboração de " + AgentNames.CallName(agent, _cfg),
        [
            new DialogItem("Sempre pedir autorização", UrbeTheme.Icons.Check, "Confirme cada consulta; o especialista mantém seu próprio modelo e suas permissões."),
            new DialogItem("Consultar automaticamente", UrbeTheme.Icons.Edit, "Dispensa confirmação apenas quando não elevar o acesso a escrita; cada agente conserva suas proteções.")
        ]);
        if (i is not 0 and not 1) return;
        _cfg.AgentDelegationAuto[agent.Id] = i == 1;
        SaveConfig();
        EditAgentProfile(agent);
    }

    private async Task DeleteAgentAsync(AiAgentDef agent)
    {
        if (agent.Id == "geral" || !_cfg.CustomAgents.Any(a => a.Id == agent.Id)) return;
        if (_activeConversations.Values.Any(c => c.AgentId == agent.Id))
        { await _app.Dialogs.AlertAsync("Agente trabalhando", "Interrompa as conversas ativas deste agente antes de excluí-lo."); return; }
        var count = _store.ListConversations().Count(c => c.AgentId == agent.Id);
        if (!await _app.Dialogs.ConfirmAsync("Excluir " + AgentNames.CallName(agent, _cfg) + "?",
            "O agente e suas " + count + " conversa(s) em todos os vaults serão excluídos permanentemente. Esta ação não pode ser desfeita.",
            "Excluir agente e histórico", danger: true)) return;
        try
        {
            _store.DeleteAgentConversationsVerified(agent.Id);
            _cfg.CustomAgents.RemoveAll(a => a.Id == agent.Id);
            _cfg.AgentModels.Remove(agent.Id);
            _cfg.AgentImages.Remove(agent.Id);
            _cfg.AgentPolicies.Remove(agent.Id);
            _cfg.AgentToolScopes.Remove(agent.Id);
            _cfg.AgentDelegationAuto.Remove(agent.Id);
            _cfg.AgentInstructionBlocks.Remove(agent.Id);
            _cfg.AgentNames.Remove(agent.Id);
            _agentDrafts.Remove(agent.Id);
            if (_cfg.AgentId == agent.Id)
            {
                _cfg.AgentId = "geral";
                _conv = NewConversation();
                SyncActiveConversation();
                _views.Clear();
            }
            SaveConfig();
            ShowSettings();
            RenderChrome();
            _app.Notify("Agente e " + count + " conversa(s) excluídos.");
        }
        catch (IOException e)
        {
            _cfg = _store.GetConfig(); // Do not present a successful removal when disk verification failed.
            await _app.Dialogs.AlertAsync("Exclusão incompleta", e.Message);
            ShowSettings();
        }
    }

    // ---------------------------------------------------------------- overlaid views

    private StackPanel OpenView(string title, Action? back = null)
    {
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(14, 4, 14, 24) };
        var backButton = IconButton(UrbeTheme.Icons.Back, "Voltar");
        backButton.Click += (_, _) => (back ?? CloseView)();
        var head = new DockPanel { Margin = new Thickness(6, 6, 6, 4), Height = 52 };
        DockPanel.SetDock(backButton, Dock.Left);
        head.Children.Add(backButton);
        var t = Label(title, 18, UrbeTheme.Text, FontWeight.Bold);
        t.Margin = new Thickness(6, 0);
        head.Children.Add(t);
        var layout = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        layout.Children.Add(head);
        layout.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        _view.Child = layout;
        _view.IsVisible = true;
        return body;
    }

    private void CloseView() { _view.IsVisible = false; _view.Child = null; }

    /// <summary>The view open over the chat (tests): its title, or null.</summary>
    public string? ViewTitle => _view.IsVisible && _view.Child is DockPanel { Children: [DockPanel head, ..] } && head.Children.OfType<TextBlock>().FirstOrDefault() is { } t ? t.Text : null;

    private void ShowHistory()
    {
        var list = _store.ConversationsFor(_conv.AgentId, _app.VaultName);
        var body = OpenView("Conversas");
        body.Children.Add(Hint("Histórico deste agente. Conversas antigas mantêm o agente registrado na última gravação; podem conter participações anteriores de outros agentes."));
        if (list.Count == 0) { body.Children.Add(Wrap("Nenhuma conversa ainda.", 14, UrbeTheme.Text3)); return; }
        foreach (var c in list)
        {
            int n = c.Messages.Count(m => m.Role == "user" && m.Meta?.ToolResults != true);
            var a = AgentNames.Named(AssistantAgent.AgentById(c.AgentId, _cfg.CustomAgents), _cfg);
            var when = DateTimeOffset.FromUnixTimeMilliseconds(c.Updated).LocalDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
            var open = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12),
                Background = UrbeTheme.Brush(c.Id == _conv.Id ? UrbeTheme.Accent : UrbeTheme.Surface, c.Id == _conv.Id ? .12 : 1), BorderThickness = new Thickness(0),
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children = { Label(c.Title.Length > 0 ? c.Title : "Conversa", 14, UrbeTheme.Text, FontWeight.SemiBold),
                        Label(a.Name + " · " + n + " pedido" + (n == 1 ? "" : "s") + " · " + when + (c.Vault.Length > 0 && c.Vault != _app.VaultName ? " · " + c.Vault : ""), 12, UrbeTheme.Text3) }
                }
            };
            open.Click += (_, _) =>
            {
                OpenConversation(_activeConversations.GetValueOrDefault(c.Id) ?? _store.GetConversation(c.Id) ?? c);
            };
            var export = IconButton(UrbeTheme.Icons.Download, "Exportar esta conversa");
            export.Click += (_, _) => _ = ExportConversationAsync(c);
            var del = IconButton(UrbeTheme.Icons.Trash, "Excluir conversa");
            del.Click += async (_, _) =>
            {
                if (!await _app.Dialogs.ConfirmAsync("Excluir esta conversa?", null, "Excluir", danger: true)) return;
                if (_activeRuns.ContainsKey(c.Id)) { await _app.Dialogs.AlertAsync("Conversa em andamento", "Interrompa a execução antes de excluir esta conversa."); return; }
                _store.DeleteConversation(c.Id);
                if (_conv.Id == c.Id) { _conv = NewConversation(); SyncActiveConversation(); }
                ShowHistory();
                RenderAll();
            };
            var row = new DockPanel();
            DockPanel.SetDock(del, Dock.Right);
            DockPanel.SetDock(export, Dock.Right);
            row.Children.Add(del);
            row.Children.Add(export);
            row.Children.Add(open);
            body.Children.Add(row);
        }
    }

    // An editorial section: grouping is conveyed by rhythm and type, not enclosing every field in a box.
    private static Control Section(string title, params Control[] items)
    {
        var s = new StackPanel { Spacing = UrbeTokens.Space.Xs, Margin = new Thickness(0, 14, 0, 4) };
        s.Children.Add(Label(title, 16, UrbeTheme.Text, FontWeight.SemiBold));
        foreach (var i in items) s.Children.Add(i);
        return s;
    }

    private static TextBlock Hint(string text) => Wrap(text, 12, UrbeTheme.Text3);
    private async Task PriceInSettingsAsync(AiProviderConfig provider, string model, TextBlock target)
    {
        try
        {
            var quoted = await Providers.GetImageQuoteAsync(provider, model);
            target.Text = quoted?.Rate ?? "Preço não informado pelo provedor; verifique antes de gerar.";
            if (quoted is not null) ToolTip.SetTip(target, quoted.Explanation + "\nFonte: " + quoted.SourceUrl);
        }
        catch { target.Text = "Preço indisponível nesta consulta. Verifique a tabela do provedor."; }
    }


    // Separate destinations keep the full feature set without an endless configuration screen.
    private string _settingsCategory = "models";

    /// <summary>Settings grouped by mental model, with every existing control preserved.</summary>
    public void ShowSettings(string? addPreset = null)
    {
        if (addPreset is not null) _settingsCategory = "models";
        var c = _cfg;
        var v = VaultKey;
        var body = OpenView("Configurações");
        body.Children.Add(Wrap("O Assistente padrão usa um modelo básico. Configure agentes com modelos melhores conforme suas necessidades. A IA no aparelho requer download prévio do modelo e suporte neste dispositivo.", 14, UrbeTheme.Text2));
        if (HasDeviceAi) body.Children.Add(TextButton("IA no aparelho", ShowDevice));
        var choices = new (string Id, string Title, string Description)[]
        {
            ("models", "Modelos", "Provedores e imagens"),
            ("agents", "Agentes", "Identidade e perfis"),
            ("web", "Pesquisa", "Internet e fontes"),
            ("context", "Contexto", "Instruções e memória")
        };
        // Flat navigation: no grid of outlined mini-cards. Categories are ALWAYS visible and can wrap on phones.
        var navigation = new WrapPanel { Orientation = Orientation.Horizontal, ItemHeight = UrbeTokens.MinTouch };
        foreach (var choice in choices)
        {
            var selected = choice.Id == _settingsCategory;
            var item = new Button
            {
                MinHeight = UrbeTokens.MinTouch,
                Padding = new Thickness(12, 9),
                CornerRadius = new CornerRadius(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                BorderThickness = selected ? new Thickness(0, 0, 0, 2) : new Thickness(0),
                BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent),
                Background = Brushes.Transparent,
                Content = Label(choice.Title, 13, selected ? UrbeTheme.Accent : UrbeTheme.Text2,
                    selected ? FontWeight.SemiBold : FontWeight.Normal)
            };
            ToolTip.SetTip(item, choice.Description);
            Avalonia.Automation.AutomationProperties.SetName(item, "Configurações: " + choice.Title);
            item.Click += (_, _) => { _settingsCategory = choice.Id; ShowSettings(); };
            navigation.Children.Add(item);
        }
        body.Children.Add(navigation);
        body.Children.Add(Hint(choices.First(x => x.Id == _settingsCategory).Description));
        body.Children.Add(new Border { Height = 1, Background = UrbeTheme.Brush(UrbeTheme.Line2, .55),
            Margin = new Thickness(0, 8, 0, 4) });

        if (_settingsCategory == "models")
        {
        var provs = new StackPanel { Spacing = 8 };
        if (c.Providers.Count == 0) provs.Children.Add(Hint("Nenhum provedor ainda."));
        foreach (var p in c.Providers)
        {
            var pre = AssistantProviders.Presets.GetValueOrDefault(p.Preset) ?? AssistantProviders.Presets["custom"];
            var test = Hint("");
            var info = new StackPanel
            {
                Spacing = 2,
                Children = { Label(p.Name ?? p.Id, 14, UrbeTheme.Text, FontWeight.SemiBold),
                    Label((p.BaseUrl is { Length: > 0 } ? p.BaseUrl : pre.BaseUrl) + (p.ApiKey is { Length: > 0 } key ? " · chave •••" + key[^Math.Min(4, key.Length)..] : ""), 12, UrbeTheme.Text3), test }
            };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            actions.Children.Add(TextButton("Testar", () => _ = TestProviderAsync(p, test)));
            actions.Children.Add(TextButton("Editar", () => EditProvider(p, p.Preset)));
            var rm = IconButton(UrbeTheme.Icons.Trash, "Remover");
            rm.Click += async (_, _) =>
            {
                if (!await _app.Dialogs.ConfirmAsync("Remover este provedor?", "A chave salva será apagada deste aparelho.", "Remover", danger: true)) return;
                c.Providers.RemoveAll(x => x.Id == p.Id);
                c.ModelCache.Remove(p.Id);
                if (c.ProviderId == p.Id) { c.ProviderId = c.Providers.FirstOrDefault()?.Id; c.Model = ""; }
                if (c.ImageProviderId == p.Id) { c.ImageProviderId = null; c.ImageModel = ""; }
                // Keep agent model references: missing providers must not silently select a different service.

                SaveConfig();
                ShowSettings();
                RenderChrome();
            };
            actions.Children.Add(rm);
            provs.Children.Add(Card(new StackPanel { Spacing = 8, Children = { info, actions } }));
        }
        var add = TextButton("+ Adicionar provedor", () => _ = AddProviderAsync());
        body.Children.Add(Section("Provedores de modelo", Hint("As chaves ficam salvas só neste aparelho e vão direto para o provedor."), provs, add));

        body.Children.Add(Section("Modelo padrão", Wrap(ModelId(), 13, UrbeTheme.Text),
            Wrap(ModelPriceFor(null), 12, UrbeTheme.Text2), TextButton("Selecionar modelo e preço", () => _ = PickModelAsync())));
        body.Children.Add(Section("Geração de imagens", Hint("Escolha o modelo de imagem nas configurações de cada agente. Sem seleção, nenhuma imagem é gerada.")));

        }

        if (_settingsCategory == "context")
        {
        body.Children.Add(Section("Preferências de contexto", Hint("Estas informações orientam as respostas; você mantém o controle sobre o que é lembrado.")));
        var instr = new TextBox { Text = c.Instructions, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, PlaceholderText = "Ex.: escreva em português do Brasil, frases curtas; use a pasta Diário para registros do dia." };
        instr.LostFocus += (_, _) => { c.Instructions = instr.Text ?? ""; SaveConfig(); };
        instr.TextChanged += (_, _) => c.Instructions = instr.Text ?? "";
        body.Children.Add(Section("Instruções para todos os agentes", instr));
        // CST-077: the system's language by default; an explicit request in the conversation still wins
        var language = AgentLanguage.Choices.FirstOrDefault(x => x.Code == c.Language);
        body.Children.Add(Section("Idioma das respostas", Hint("Os agentes respondem neste idioma, salvo se você pedir outro na conversa."),
            TextButton(language.Name ?? AgentLanguage.Name(c.Language), () => _ = PickLanguageAsync())));

        var nmem = c.MemoryRecords.Count + c.Memory.Values.Sum(items => items.Count);
        body.Children.Add(Section("Memória",
            Hint(nmem + " lembranças locais · recuperação por relevância · histórico e fontes."),
            TextButton("Gerenciar memórias", ShowMemoryManager)));

        }

        if (_settingsCategory == "agents")
        {
            body.Children.Add(Hint("Cada agente tem seu próprio modelo, histórico, instruções e acesso às ferramentas. Ao abrir um agente, começa uma conversa nova."));
            var agents = new StackPanel { Spacing = 4 };
            foreach (var a in AgentNames.All(c))
            {
                var line = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 10) };
                line.Children.Add(Wrap(AgentNames.CallName(a, c), 15, UrbeTheme.Text, FontWeight.SemiBold));
                var model = c.AgentModels.GetValueOrDefault(a.Id);
                line.Children.Add(Wrap((model?.Model ?? "Modelo padrão") + " · " + PermissionSummary(a), 12, UrbeTheme.Text2));
                var actions = new WrapPanel { Orientation = Orientation.Horizontal };
                actions.Children.Add(TextButton("Configurar", () => EditAgentProfile(a)));
                actions.Children.Add(TextButton("Renomear", () => _ = RenameAgentAsync(a)));
                if (a.Id != "geral")
                {
                    actions.Children.Add(TextButton("Editar instruções", () => EditAgent(a)));
                    var rm = IconButton(UrbeTheme.Icons.Trash, "Excluir agente e histórico");
                    Avalonia.Automation.AutomationProperties.SetName(rm, "Excluir " + AgentNames.CallName(a, c));
                    rm.Click += async (_, _) => await DeleteAgentAsync(a);
                    actions.Children.Add(rm);
                }
                line.Children.Add(actions);
                agents.Children.Add(new Border
                {
                    Child = line, Background = Brushes.Transparent,
                    BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .55),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                });
            }
            body.Children.Add(agents);
            body.Children.Add(TextButton("+ Novo agente", () => EditAgent(null)));
        }

        if (_settingsCategory == "web")
        {
            body.Children.Add(Section("Pesquisa na internet",
                Hint(_cfg.Web.Enabled ? "Disponível para todos os agentes · " + (_cfg.Web.Provider == "brave" ? "Brave (opcional)" : "sem chave") : "Desativada pela preferência de privacidade"),
                TextButton("Privacidade e pesquisa", ShowWebSettings),
                Hint("Peça para pesquisar no chat. Sem configurar provedor nem permissão individual.")));
        }
        if (addPreset is not null) EditProvider(null, addPreset);
    }


    /// <summary>Separate memory workspace: no giant list in provider settings or implicit keyboard focus.</summary>
    private void ShowMemoryManager()
    {
        var c = _cfg;
        var body = OpenView("Memória", () => ShowSettings());
        var head = new StackPanel { Spacing = 6 };
        var state = Hint("As notas continuam sendo a fonte de verdade. Lembranças não são instruções provenientes de arquivos.");
        head.Children.Add(state);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(TextButton("+ Lembrança", () => _ = AddMemoryByHandAsync()));
        actions.Children.Add(TextButton("Exportar memória", () => _ = ExportAgentMemoryAsync()));
        actions.Children.Add(TextButton("Conflitos", () =>
        {
            var conflicts = AssistantMemoryEngine.PotentialConflicts(_cfg, VaultKey, _conv.AgentId);
            _ = _app.Dialogs.AlertAsync("Possíveis conflitos",
                conflicts.Count == 0 ? "Nenhum candidato encontrado. A detecção é heurística." :
                string.Join("\n\n", conflicts.Take(8).Select(p =>
                    p.Left.Text + "\n↔\n" + p.Right.Text)) +
                "\n\nSão candidatos, não uma decisão automática sobre a verdade.");
        }));
        head.Children.Add(actions);
        head.Children.Add(TextButton(c.AutoCaptureEpisodes ?
            "Registro de tarefas: ligado (desativar)" : "Registro de tarefas: desligado (ativar)", () =>
        {
            c.AutoCaptureEpisodes = !c.AutoCaptureEpisodes;
            SaveConfig();
            ShowMemoryManager();
        }));
        body.Children.Add(head);
        var search = new TextBox { PlaceholderText = "Pesquisar fatos, decisões, procedimentos ou fontes…",
            MinHeight = 44 };
        body.Children.Add(search);
        var list = new StackPanel { Spacing = 8 };
        body.Children.Add(list);
        var visibleRows = 80; // Only the UI page size; all memories remain searchable.
        void Refresh()
        {
            list.Children.Clear();
            // Includes the owner's other agents and archived records. Never share these across hosts.
            var entries = c.MemoryRecords.Where(m => m.Scope == "global" || m.Vault == VaultKey)
                .OrderByDescending(m => m.UpdatedAt).ToArray();
            var older = c.Memory.SelectMany(pair => (pair.Key == "*" || pair.Key == VaultKey)
                ? pair.Value.Select((text, i) => new AiMemoryRecord
                {
                    Id = "legacy:" + pair.Key + ":" + i, Text = text, Kind = "fact",
                    Scope = pair.Key == "*" ? "global" : "vault", Vault = pair.Key == "*" ? "" : pair.Key,
                    Origin = "legacy"
                })
                : Array.Empty<AiMemoryRecord>());
            var term = AssistantMemoryEngine.Normalize(search.Text?.Trim());
            var matching = entries.Concat(older).Where(m => term.Length == 0 ||
                AssistantMemoryEngine.Normalize(m.Text + " " + m.Kind + " " +
                    m.SourceReference + " " + string.Join(" ", m.RelatedPaths)).Contains(term, StringComparison.Ordinal))
                .ToArray();
            var filtered = matching.Take(visibleRows).ToArray();
            state.Text = c.MemoryRecords.Count + c.Memory.Values.Sum(a => a.Count) +
                " lembranças locais · " + filtered.Length + " de " + matching.Length + " exibidas.";
            if (filtered.Length == 0) list.Children.Add(Hint("Sem resultados. Você pode adicionar uma lembrança ou usar “lembre que…” no chat."));
            foreach (var m in filtered)
            {
                var info = m.Kind + " · " + (m.Scope == "agent" ? "agente " + m.AgentId :
                    m.Scope == "global" ? "geral" : "vault") +
                    " · " + (m.State == "active" ? "ativa" : m.State);
                var label = new StackPanel { Spacing = 4 };
                label.Children.Add(Label(info, 11, UrbeTheme.Text3));
                label.Children.Add(Wrap(m.Text, 13, UrbeTheme.Text));
                if (m.SourceReference.Length > 0 || m.RelatedPaths.Count > 0)
                    label.Children.Add(Hint("Origem: " + m.Origin + (m.SourceReference.Length > 0 ? " · " + m.SourceReference : "") +
                        (m.RelatedPaths.Count > 0 ? "\nNotas: " + string.Join(", ", m.RelatedPaths.Take(4)) : "")));
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
                var edit = TextButton("Editar", () => _ = EditMemoryAsync(m.Id));
                buttons.Children.Add(edit);
                var pin = TextButton(m.Pinned ? "Desafixar" : "Fixar", () =>
                {
                    var current = AssistantMemoryEngine.ResolveForEdit(_cfg, m.Id);
                    if (current is not null)
                    {
                        current.Pinned = !current.Pinned;
                        try { SaveMemoryConfig(); ShowMemoryManager(); }
                        catch (Exception ex) { _app.Notify(ex.Message); }
                    }
                    else ShowMemoryManager();
                });
                buttons.Children.Add(pin);
                buttons.Children.Add(TextButton("Esquecer", () => _ = DeleteMemoryAsync(m.Id)));
                label.Children.Add(buttons);
                list.Children.Add(Card(label));
            }
            if (matching.Length > filtered.Length)
                list.Children.Add(TextButton("Carregar mais " +
                    Math.Min(80, matching.Length - filtered.Length) + " lembranças", () =>
                {
                    visibleRows += 80;
                    Refresh();
                }));
        }
        search.TextChanged += (_, _) => { visibleRows = 80; Refresh(); };
        Refresh();
    }

    private async Task AddMemoryByHandAsync()
    {
        var kinds = new[] { "fact", "preference", "decision", "goal", "procedure", "observation" };
        var names = new[] { "Fato", "Preferência", "Decisão", "Objetivo", "Procedimento", "Observação" };
        var pick = await _app.Dialogs.ChooseAsync("Tipo de lembrança", names.Select(n => new DialogItem(n)).ToArray());
        if (pick is null) return;
        var scopePick = await _app.Dialogs.ChooseAsync("Quem pode utilizar esta memória?",
        [
            new DialogItem("Este vault · todos os agentes"),
            new DialogItem("Somente este agente neste vault"),
            new DialogItem("Geral · todos os vaults")
        ]);
        if (scopePick is null) return;
        var text = await _app.Dialogs.PromptAsync(new PromptOptions("Nova lembrança",
            "Conhecimento verificável ou preferência", Placeholder: "O que deve ser lembrado?"));
        if (text is null) return;
        var scope = scopePick.Value switch { 1 => "agent", 2 => "global", _ => "vault" };
        try
        {
            AssistantMemoryEngine.Remember(_cfg, text, VaultKey, _conv.AgentId, kinds[pick.Value],
                scope, "user", "manual", verified: true);
            SaveMemoryConfig();
            ShowMemoryManager();
        }
        catch (Exception ex) { await _app.Dialogs.AlertAsync("Não foi possível memorizar", ex.Message); }
    }

    private async Task EditMemoryAsync(string id)
    {
        var m = AssistantMemoryEngine.ResolveForEdit(_cfg, id);
        if (m is null) { ShowMemoryManager(); return; }
        var changed = await _app.Dialogs.PromptAsync(new PromptOptions("Editar lembrança", "Texto",
            Value: m.Text));
        if (changed is null) return;
        if (string.IsNullOrWhiteSpace(changed)) return;
        try
        {
            if (!AssistantMemoryEngine.Edit(_cfg, m.Id, changed)) return;
            SaveMemoryConfig();
            ShowMemoryManager();
        }
        catch (Exception ex) { await _app.Dialogs.AlertAsync("Erro na edição", ex.Message); }
    }

    private async Task DeleteMemoryAsync(string id)
    {
        if (!await _app.Dialogs.ConfirmAsync("Esquecer esta lembrança?",
                "Ela será removida da memória local. As notas do vault não serão alteradas.",
                "Esquecer", danger: true)) return;
        try
        {
            if (id.StartsWith("legacy:", StringComparison.Ordinal))
            {
                var parts = id.Split(':', 3);
                if (parts.Length == 3 && int.TryParse(parts[2], out var at) &&
                    _cfg.Memory.TryGetValue(parts[1], out var values) && at >= 0 && at < values.Count)
                    values.RemoveAt(at);
                else throw new InvalidOperationException("Lembrança antiga não localizada.");
                AssistantMemoryEngine.MigrateLegacy(_cfg);
            }
            else AssistantMemoryEngine.Forget(_cfg, id);
            SaveMemoryConfig();
            ShowMemoryManager();
        }
        catch (Exception ex) { await _app.Dialogs.AlertAsync("Erro ao esquecer", ex.Message); }
    }

    private async Task AddProviderAsync()
    {
        var i = await _app.Dialogs.ChooseAsync("Qual provedor?", PresetOrder.Select(k => new DialogItem(AssistantProviders.Presets[k].Name, UrbeTheme.Icons.Storage)).ToList());
        if (i is { } k) { if (PresetOrder[k] == "device") ShowDevice(); else EditProvider(null, PresetOrder[k]); }
    }

    private async Task TestProviderAsync(AiProviderConfig p, TextBlock output)
    {
        var pre = AssistantProviders.Presets.GetValueOrDefault(p.Preset);
        var m = p.Id == Provider()?.Id && ModelId().Length > 0 ? ModelId() : pre?.Models?.FirstOrDefault();
        output.Text = "Testando…";
        output.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        try
        {
            if (m is null)
            {
                var ms = await Providers.ListModelsAsync(p);
                output.Text = "Conectado · " + ms.Count + " modelos disponíveis";
            }
            else
            {
                var r = await Providers.TestAsync(p, m);
                output.Text = "Funcionando · " + r.Model;
            }
            output.Foreground = UrbeTheme.Brush("#3a9a5b");
        }
        catch (Exception e)
        {
            output.Text = e.Message;
            output.Foreground = UrbeTheme.Brush("#c0504d");
        }
    }

    /// <summary>editProvider(): name, API address and key; saved after the models list answers (a 401/403 stops it), then the model picker.</summary>
    private void EditProvider(AiProviderConfig? p, string preset)
    {
        if (preset == "device") { ShowDevice(); return; }
        var pre = AssistantProviders.Presets.GetValueOrDefault(preset) ?? AssistantProviders.Presets["custom"];
        bool isNew = p is null;
        p ??= new AiProviderConfig(AssistantAgent.NewId("p"), preset, pre.Name, "", "");
        var body = OpenView(isNew ? "Conectar " + pre.Name : p.Name ?? pre.Name, () => ShowSettings());
        var name = new TextBox { Text = p.Name ?? pre.Name };
        var url = new TextBox { Text = p.BaseUrl is { Length: > 0 } ? p.BaseUrl : pre.BaseUrl, PlaceholderText = "https://…/v1" };
        var key = new TextBox { Text = p.ApiKey ?? "", PasswordChar = '•', PlaceholderText = pre.KeyHint };
        Avalonia.Automation.AutomationProperties.SetName(key, "Chave da API");
        var output = Hint("");
        body.Children.Add(Section("Nome", name));
        body.Children.Add(Section("Endereço da API", url));
        body.Children.Add(Section("Chave da API" + (pre.NeedsKey ? "" : " (opcional)"), key));
        if (pre.Site is { } site) body.Children.Add(Hint("Crie uma chave em " + site.Replace("https://", "") + "."));
        if (preset == "ollama") body.Children.Add(Hint("Use um modelo com suporte a ferramentas (ex.: qwen2.5, llama3.1). No aparelho, o endereço precisa ser o do computador na rede, não localhost."));
        body.Children.Add(output);
        var save = TextButton("Salvar e escolher modelo", () => { }, primary: true);
        var orig = p;
        save.Click += async (_, _) =>
        {
            var typedUrl = (url.Text ?? "").Trim();
            var cfg = orig with
            {
                Name = (name.Text ?? "").Trim() is { Length: > 0 } n ? n : pre.Name,
                BaseUrl = typedUrl == pre.BaseUrl ? "" : typedUrl,
                ApiKey = (key.Text ?? "").Trim()
            };
            if (pre.NeedsKey && cfg.ApiKey!.Length == 0) { Fail("Cole a chave da API."); return; }
            if (AssistantProviders.Resolve(cfg).BaseUrl.Length == 0) { Fail("Informe o endereço da API."); return; }
            output.Text = "Conectando…";
            output.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
            try { _cfg.ModelCache[cfg.Id] = new AiModelCache(Now(), await Providers.ListModelsAsync(cfg)); }
            catch (AiProviderException e) when (e.Status is 401 or 403) { Fail(e.Message); return; }
            catch (Exception) { }
            int at = _cfg.Providers.FindIndex(x => x.Id == cfg.Id);
            if (at >= 0) _cfg.Providers[at] = cfg; else _cfg.Providers.Add(cfg);
            if (_cfg.ProviderId is null || isNew)
            {
                _cfg.ProviderId = cfg.Id;
                _cfg.Model = "";
                _conv.ProviderId = cfg.Id;
                _conv.Model = "";
            }
            SaveConfig();
            CloseView();
            RenderAll();
            await PickModelAsync();
        };
        void Fail(string text) { output.Text = text; output.Foreground = UrbeTheme.Brush("#c0504d"); }
        var cancel = TextButton("Cancelar", () => ShowSettings());
        cancel.Margin = new Thickness(0, 0, 8, 0);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } });
    }

    /// <summary>UC-81: a new call name for an agent (empty gives it back its own name).</summary>
    private async Task RenameAgentAsync(AiAgentDef a)
    {
        var typed = await _app.Dialogs.PromptAsync(new PromptOptions("Nome do agente", Label: "Como ele se chama", Value: AgentNames.CallName(a, _cfg),
            Placeholder: a.Name, Hint: "Vazio volta ao nome padrão (" + a.Name + ").", Confirm: "Salvar",
            Validate: v => v.Trim().Length == 0 || v.Trim() == a.Name ? null : AgentNames.Problem(v, a.Id, _cfg), Required: false));
        if (typed is null) return;
        if (AgentNames.Set(_cfg, a.Id, typed) is { } problem) { await _app.Dialogs.AlertAsync("Nome do agente", problem); return; }
        SaveConfig();
        RenderAll();
        ShowSettings();
    }

    /// <summary>
    /// CST-070: the instruction blocks (an optional title and the text), the same editor when the agent is created and
    /// when it is configured later; the function reads what is there now (blocks without text are left out).
    /// </summary>
    private (Control Editor, Func<List<AiInstructionBlock>> Read) InstructionBlocksEditor(IEnumerable<AiInstructionBlock> initial)
    {
        var blocksPanel = new StackPanel { Spacing = 10 };
        var rows = new List<(TextBox Title, TextBox Body, Control Card)>();
        void AddBlock(AiInstructionBlock block)
        {
            var title = new TextBox { Text = block.Title, PlaceholderText = "Nome do bloco (opcional), ex.: Estilo de escrita" };
            var details = new TextBox { Text = block.Content, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                MinHeight = 90, PlaceholderText = "Instruções detalhadas para este aspecto do trabalho." };
            var field = new StackPanel { Spacing = 6 };
            var card = Card(field);
            var rm = TextButton("Remover bloco", () =>
            {
                rows.RemoveAll(r => ReferenceEquals(r.Card, card));
                blocksPanel.Children.Remove(card);
            });
            field.Children.Add(title);
            field.Children.Add(details);
            field.Children.Add(rm);
            rows.Add((title, details, card));
            blocksPanel.Children.Add(card);
        }
        foreach (var block in initial) AddBlock(block);
        var editor = Section("Instruções por blocos", Hint("Você pode criar vários blocos. Todos são usados em conjunto com as instruções gerais, mantendo as permissões do agente."),
            blocksPanel, TextButton("+ Adicionar bloco", () => AddBlock(new AiInstructionBlock("", ""))));
        return (editor, () => [.. rows.Select(r => new AiInstructionBlock((r.Title.Text ?? "").Trim(), (r.Body.Text ?? "").Trim())).Where(b => b.Content.Length > 0)]);
    }

    private async Task PickLanguageAsync()
    {
        var choices = AgentLanguage.Choices;
        if (await _app.Dialogs.ChooseAsync("Idioma das respostas", [.. choices.Select(x => new DialogItem(x.Name, Detail: x.Code.Length > 0 ? x.Code : null))]) is not { } i) return;
        _cfg.Language = choices[i].Code;
        SaveConfig();
        ShowSettings();
    }

    private void EditAgent(AiAgentDef? a)
    {
        bool isNew = a is null;
        var body = OpenView(isNew ? "Novo agente" : a!.Name, () => ShowSettings());
        var name = new TextBox { Text = a?.Name ?? "", PlaceholderText = "Ex.: Revisor acadêmico" };
        var prompt = new TextBox { Text = a?.Prompt ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, PlaceholderText = "Como este agente deve pensar e trabalhar." };
        if (isNew)
        {
            var templates = new ComboBox { ItemsSource = new[] { "Em branco" }.Concat(AssistantAgent.Agents.Skip(1).Select(t => t.Name)).ToList(), SelectedIndex = 0 };
            templates.SelectionChanged += (_, _) =>
            {
                if (templates.SelectedIndex <= 0) { prompt.Text = ""; return; }
                var template = AssistantAgent.Agents[templates.SelectedIndex];
                name.Text = template.Name;
                prompt.Text = template.Prompt;
            };
            body.Children.Add(Section("Modelo de agente", templates));
        }
        body.Children.Add(Section("Nome", name));
        body.Children.Add(Section("Instruções", prompt));
        // CST-070: the blocks are there from the start, the same ones the agent's settings show later
        var (blocksEditor, readBlocks) = InstructionBlocksEditor(a is null ? [] : _cfg.AgentInstructionBlocks.GetValueOrDefault(a.Id) ?? []);
        body.Children.Add(blocksEditor);
        body.Children.Add(Hint("Acesso às ferramentas é configurado em Permissões, junto da autonomia do agente."));
        var save = TextButton("Salvar", () =>
        {
            var n = (name.Text ?? "").Trim();
            if (n.Length == 0) return;
            if (AgentNames.Problem(n, a?.Id ?? "", _cfg) is { } problem) { _app.Notify(problem); return; }
            var p = (prompt.Text ?? "").Trim();
            var t = a?.Tools ?? "read";
            var made = new AiAgentDef(a?.Id ?? AssistantAgent.NewId("a"), n, t == "read" ? "search" : "sparkle", p.Length > 80 ? p[..80] : p, t, t == "all", p);
            int at = _cfg.CustomAgents.FindIndex(x => x.Id == made.Id);
            if (at >= 0) _cfg.CustomAgents[at] = made; else _cfg.CustomAgents.Add(made);
            var blocks = readBlocks();
            if (blocks.Count == 0) _cfg.AgentInstructionBlocks.Remove(made.Id); else _cfg.AgentInstructionBlocks[made.Id] = blocks;
            SaveConfig();
            EditAgentProfile(made);
        }, primary: true);
        var cancel = TextButton("Cancelar", () => ShowSettings());
        cancel.Margin = new Thickness(0, 0, 8, 0);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } });
    }


    /// <summary>Provider/model picker for images and for any agent. Never changes the conversation's model.</summary>
    private async Task PickConfiguredModelAsync(string? agentId, string? imageAgentId = null)
    {
        if (_cfg.Providers.Count == 0) { ShowSettings("openrouter"); return; }
        bool image = agentId is null;
        var agent = image ? null : AssistantAgent.AgentById(agentId, _cfg.CustomAgents);
        void Back()
        {
            if (imageAgentId is not null) EditAgentProfile(AssistantAgent.AgentById(imageAgentId, _cfg.CustomAgents));
            else if (agent is null) ShowSettings();
            else EditAgentProfile(agent);
        }
        var choice = image
            ? _cfg.AgentImages.GetValueOrDefault(imageAgentId ?? _conv.AgentId) ?? new AiModelChoice("", "")
            : _cfg.AgentModels.GetValueOrDefault(agentId!) ?? new AiModelChoice("", "");
        var providers = _cfg.Providers.Where(p => !image || AssistantProviders.Resolve(p).Kind == "openai" &&
            p.Preset is "openrouter" or "openai" or "gemini" or "custom").ToList();
        if (providers.Count == 0) { ShowSettings(); return; }
        var selected = providers.FirstOrDefault(p => p.Id == choice.ProviderId) ?? providers[0];
        var body = OpenView(image ? "Modelo de imagem" : "Modelo de " + AgentNames.CallName(agent!, _cfg), Back);
        body.Children.Add(Hint(image
            ? "O modelo de imagem não altera o chat. Preços em US$ com unidade de cobrança e fonte; valores variáveis não são estimados como zero."
            : "Este modelo será usado somente pelo agente escolhido. Sem seleção, ele herda o modelo do Assistente."));
        var auto = TextButton(image ? "Sem modelo de imagem" : agentId == "geral" ? "Usar modelo básico do aparelho" : "Herdar modelo do Assistente", () =>
        {
            if (image) _cfg.AgentImages.Remove(imageAgentId ?? _conv.AgentId);
            else if (agentId == "geral" && LocalAi.Text is { } runtime)
            {
                var device = _cfg.Providers.FirstOrDefault(p => p.Preset == "device") ?? new AiProviderConfig("device", "device", "IA no aparelho", "", "");
                if (!_cfg.Providers.Any(p => p.Id == device.Id)) _cfg.Providers.Add(device);
                _cfg.AgentModels[agentId] = new AiModelChoice(device.Id, runtime.Recommended.Id);
            }
            else _cfg.AgentModels.Remove(agentId!);
            SaveConfig(); Back(); RenderAll();
        });
        body.Children.Add(auto);
        var tabs = new WrapPanel();
        var search = new TextBox { PlaceholderText = "Buscar modelo" };
        var manual = new TextBox { PlaceholderText = "Ou informe o ID exato do modelo" };
        var list = new StackPanel { Spacing = 7 };
        IReadOnlyList<AiModelInfo> models = [];
        string? loadError = null;
        // Quotes cached per picker session; concurrent requests for the same model are coalesced.
        var priceLoads = new Dictionary<string, Task<AiImageQuote?>>(StringComparer.Ordinal);
        var priceLimit = new SemaphoreSlim(4); // retained by asynchronous card updates after the picker opens
        var currentPaint = 0;
        async Task<AiImageQuote?> Quote(AiProviderConfig provider, string id)
        {
            var published = AssistantImagePricing.Published(provider.Preset, id);
            if (published is not null) return published;
            var key = provider.Id + ":" + id;
            if (!priceLoads.TryGetValue(key, out var pending))
            {
                async Task<AiImageQuote?> Fetch()
                {
                    await priceLimit.WaitAsync();
                    try { return await Providers.GetImageQuoteAsync(provider, id); }
                    finally { priceLimit.Release(); }
                }
                priceLoads[key] = pending = Fetch();
            }
            return await pending;
        }
        void Choose(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (image) _cfg.AgentImages[imageAgentId ?? _conv.AgentId] = new AiModelChoice(selected.Id, id.Trim());
            else _cfg.AgentModels[agentId!] = new AiModelChoice(selected.Id, id.Trim());
            SaveConfig(); Back(); RenderAll();
        }
        var use = TextButton("Usar ID", () => Choose(manual.Text ?? ""), primary: true);
        var manualRow = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
        DockPanel.SetDock(use, Dock.Right);
        manualRow.Children.Add(use);
        manualRow.Children.Add(manual);
        body.Children.Add(tabs);
        body.Children.Add(search);
        body.Children.Add(manualRow);
        var manualPrice = Hint("ID manual: preço indisponível até identificação no catálogo.");
        manual.TextChanged += (_, _) => manualPrice.Text = image ? "Preço indisponível · consultado ao concluir o ID" : ModelPrice(models.FirstOrDefault(m => m.Id == (manual.Text ?? "").Trim()));
        manual.LostFocus += (_, _) =>
        {
            if (image && !string.IsNullOrWhiteSpace(manual.Text)) _ = UpdatePrice(manual.Text.Trim(), manualPrice, currentPaint, selected);
        };
        body.Children.Add(manualPrice);
        body.Children.Add(list);
        void Paint()
        {
            list.Children.Clear();
            if (loadError is not null) list.Children.Add(Wrap(loadError, 12, "#c0504d"));
            var query = (search.Text ?? "").Trim();
            var filtered = models.Where(m => query.Length == 0 || (m.Id + " " + m.Name).Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(image ? 24 : 150).ToList();
            var painting = ++currentPaint;
            var providerSnapshot = selected;
            if (image && filtered.Count == 24)
                list.Children.Add(Hint("Exibindo 24 modelos. Use a busca para filtrar o catálogo completo."));
            if (filtered.Count == 0) list.Children.Add(Hint(image ? "Nenhum modelo de imagem encontrado. Informe o ID manualmente." : "Nenhum modelo encontrado."));
            foreach (var m in filtered)
            {
                string id = m.Id;
                var chosen = choice.ProviderId == selected.Id && choice.Model == id;
                var priceLine = Wrap(image ? "Consultando preço…" : ModelPrice(m), 12, UrbeTheme.Text2);
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    MinHeight = 68, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12),
                    Background = UrbeTheme.Brush(chosen ? UrbeTheme.Accent : UrbeTheme.Surface, chosen ? .14 : 1),
                    BorderBrush = UrbeTheme.Brush(chosen ? UrbeTheme.Accent : UrbeTheme.Line2),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Content = new StackPanel { Spacing = 3, Children = { Label(m.Name, 14, UrbeTheme.Text, FontWeight.SemiBold), Wrap(id, 11, UrbeTheme.Text3), priceLine } }
                };
                button.Click += (_, _) => Choose(id);
                list.Children.Add(button);
                if (image)
                {
                    _ = UpdatePrice(id, priceLine, painting, providerSnapshot);
                }
            }
        }
        async Task UpdatePrice(string id, TextBlock target, int painting, AiProviderConfig provider)
        {
            try
            {
                var quoted = await Quote(provider, id);
                if (painting != currentPaint) return;
                target.Text = quoted?.Rate ?? "Preço não informado · consulte o provedor";
                target.Foreground = UrbeTheme.Brush(quoted is null ? UrbeTheme.Text3 : UrbeTheme.Accent);
                if (quoted is not null) ToolTip.SetTip(target, quoted.Explanation + "\nFonte: " + quoted.SourceUrl);
                else ToolTip.SetTip(target, "O catálogo não fornece um preço verificável para este modelo. Não significa que seja gratuito.");
            }
            catch
            {
                if (painting != currentPaint) return;
                target.Text = "Preço indisponível · consulte o provedor";
                target.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
            }
        }

        int loadGeneration = 0;
        async Task Load()
        {
            int generation = ++loadGeneration;
            models = [];
            ++currentPaint; // invalidate in-flight quote labels from the previous provider
            var provider = selected;
            list.Children.Clear();
            list.Children.Add(Hint("Carregando modelos…"));
            loadError = null;
            try
            {
                IReadOnlyList<AiModelInfo> loaded;
                if (image) loaded = await Providers.ListImageModelsAsync(provider);
                else if (_cfg.ModelCache.TryGetValue(provider.Id, out var cached) && Now() - cached.At < 6 * 3600_000) loaded = cached.List;
                else
                {
                    loaded = await Providers.ListModelsAsync(provider);
                    _cfg.ModelCache[provider.Id] = new AiModelCache(Now(), loaded);
                    SaveConfig();
                }
                if (generation != loadGeneration) return;
                models = loaded;
            }
            catch (Exception e)
            {
                if (generation != loadGeneration) return;
                models = [];
                loadError = e.Message + " Você ainda pode informar um ID manualmente.";
            }
            if (image && models.Count == 0 && AssistantProviders.ImageModel(provider.Preset) is { Length: > 0 } defaultModel)
                models = [new AiModelInfo(defaultModel, defaultModel + " (padrão)", null, null, null)];
            Paint();
        }
        void Tabs()
        {
            tabs.Children.Clear();
            foreach (var p in providers)
            {
                var provider = p;
                var button = TextButton(provider.Name ?? provider.Id, () => { selected = provider; Tabs(); _ = Load(); }, primary: provider.Id == selected.Id);
                button.Margin = new Thickness(0, 0, 6, 6);
                tabs.Children.Add(button);
            }
        }
        search.TextChanged += (_, _) => Paint();
        Tabs();
        await Load();
    }

    /// <summary>Edit ordered instruction blocks and the optional provider/model override for built-in or custom agents.</summary>
    private void EditAgentProfile(AiAgentDef agent)
    {
        var body = OpenView("Configurar " + AgentNames.CallName(agent, _cfg), () => ShowSettings());
        body.Children.Add(Section("Acesso e colaboração", Hint(PermissionSummary(agent) + " · alterações valem nas próximas execuções"),
            TextButton("Alterar permissões", () => _ = PickPolicyAsync(agent)),
            Hint(_cfg.AgentDelegationAuto.GetValueOrDefault(agent.Id)
                ? "Consultar outros agentes: automaticamente quando seguro"
                : "Consultar outros agentes: pede sua aprovação"),
            TextButton("Definir autorização de consultas", () => _ = PickDelegationAsync(agent))));
        var image = _cfg.AgentImages.GetValueOrDefault(agent.Id);
        body.Children.Add(Section("Modelo de imagem", Hint(image?.Model ?? "Nenhum"),
            TextButton("Selecionar modelo de imagem", () => _ = PickConfiguredModelAsync(null, agent.Id))));
        if (image is not null && _cfg.Providers.FirstOrDefault(p => p.Id == image.ProviderId) is { } imageProvider)
        {
            var price = Hint("Consultando preço do modelo…");
            body.Children.Add(price);
            _ = PriceInSettingsAsync(imageProvider, image.Model, price);
        }
        var selected = _cfg.AgentModels.GetValueOrDefault(agent.Id);
        var provider = selected is null ? null : _cfg.Providers.FirstOrDefault(p => p.Id == selected.ProviderId);
        var choice = provider is null ? "Padrão do Assistente (herdado)" : (provider.Name ?? provider.Id) + " · " + selected!.Model;
        var (blocksEditor, readBlocks) = InstructionBlocksEditor(_cfg.AgentInstructionBlocks.GetValueOrDefault(agent.Id) ?? []);
        void SaveBlocks()
        {
            var values = readBlocks();
            if (values.Count == 0) _cfg.AgentInstructionBlocks.Remove(agent.Id);
            else _cfg.AgentInstructionBlocks[agent.Id] = values;
            SaveConfig();
        }
        body.Children.Add(Section("Modelo de conversa", Hint("Opcional: se não escolher, este agente continua usando o modelo padrão."),
            Wrap(choice, 13, UrbeTheme.Text, FontWeight.SemiBold),
            Wrap(ModelPriceFor(selected ?? _cfg.ModelFor(new AiConversation { AgentId = agent.Id })), 12, UrbeTheme.Text2),
            TextButton("Selecionar provedor e modelo", () => { SaveBlocks(); _ = PickConfiguredModelAsync(agent.Id); })));
        body.Children.Add(blocksEditor);
        var save = TextButton("Salvar instruções", () => { SaveBlocks(); ShowSettings(); }, primary: true);
        body.Children.Add(save);
    }

    /// <summary>pickModel(): the provider's models (cached 6 h), search, price, the "sem ferramentas" warning, and a typed id.</summary>
    public async Task PickModelAsync()
    {
        if (_cfg.Providers.Count == 0) { ShowSettings("openrouter"); return; }
        var prov = Provider()!;
        var body = OpenView("Modelo");
        body.Children.Add(Hint("Modelo deste agente. Você também pode configurá-lo em Configurações → Agentes."));
        var tabs = new WrapPanel();
        var search = new TextBox { PlaceholderText = "Buscar modelo (ex.: claude, gpt, gemini)" };
        var list = new StackPanel { Spacing = 6 };
        IReadOnlyList<AiModelInfo> models = [];
        var manual = new TextBox { PlaceholderText = "Ou digite o ID do modelo" };
        var use = TextButton("Usar", () => { var id = (manual.Text ?? "").Trim(); if (id.Length > 0) Choose(id); });
        var manualRow = new DockPanel();
        DockPanel.SetDock(use, Dock.Right);
        use.Margin = new Thickness(8, 0, 0, 0);
        manualRow.Children.Add(use);
        manualRow.Children.Add(manual);
        body.Children.Add(tabs);
        body.Children.Add(search);
        body.Children.Add(manualRow);
        var manualPrice = Hint("ID manual: preço indisponível até identificação no catálogo.");
        manual.TextChanged += (_, _) => manualPrice.Text = ModelPrice(models.FirstOrDefault(m => m.Id == (manual.Text ?? "").Trim()));
        body.Children.Add(manualPrice);
        body.Children.Add(list);

        void Tabs()
        {
            tabs.Children.Clear();
            foreach (var p in _cfg.Providers)
            {
                var b = TextButton(p.Name ?? p.Id, () => { prov = p; Tabs(); _ = Load(); }, primary: p.Id == prov.Id);
                b.Margin = new Thickness(0, 0, 6, 6);
                tabs.Children.Add(b);
            }
        }

        static int Rank(string id)
        {
            string[] s = ["claude", "gpt-5", "gpt-4", "gemini", "qwen", "llama", "mistral", "deepseek"];
            for (int i = 0; i < s.Length; i++) if (id.Contains(s[i], StringComparison.OrdinalIgnoreCase)) return i;
            return 99;
        }

        void Paint()
        {
            list.Children.Clear();
            var q = (search.Text ?? "").Trim().ToLowerInvariant();
            var hits = models.Where(m => q.Length == 0 || (m.Id + " " + m.Name).Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Tools == true ? 0 : 1).ThenBy(m => Rank(m.Id)).ThenBy(m => m.Name, StringComparer.CurrentCulture).Take(200).ToList();
            if (hits.Count == 0) { list.Children.Add(Hint("Nenhum modelo.")); return; }
            foreach (var m in hits)
            {
                var detail = m.Id + (m.Context is { } c ? " · " + Math.Round(c / 1000.0) + "k contexto" : "") +
                             " · " + ModelPrice(m) +
                             (m.Tools == false ? " · sem ferramentas" : "");
                bool on = m.Id == ModelId() && prov.Id == Provider()?.Id;
                var b = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12),
                    Background = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Surface, on ? .14 : 1), BorderThickness = new Thickness(0),
                    Content = new StackPanel { Spacing = 2, Children = { Label(m.Name, 14, UrbeTheme.Text, FontWeight.SemiBold), Wrap(detail, 12, m.Tools == false ? "#c0504d" : UrbeTheme.Text3) } }
                };
                var id = m.Id;
                b.Click += (_, _) => Choose(id);
                list.Children.Add(b);
            }
        }

        int loadGeneration = 0;
        async Task Load()
        {
            int generation = ++loadGeneration;
            models = [];
            var provider = prov;
            list.Children.Clear();
            list.Children.Add(Hint("Carregando modelos…"));
            try
            {
                IReadOnlyList<AiModelInfo> loaded;
                if (_cfg.ModelCache.TryGetValue(provider.Id, out var cache) && Now() - cache.At < 6 * 3600_000) loaded = cache.List;
                else
                {
                    loaded = await Providers.ListModelsAsync(provider);
                    _cfg.ModelCache[provider.Id] = new AiModelCache(Now(), loaded);
                    SaveConfig();
                }
                if (generation != loadGeneration) return;
                models = loaded;
                Paint();
            }
            catch (Exception e)
            {
                if (generation != loadGeneration) return;
                models = [];
                list.Children.Clear();
                list.Children.Add(Wrap(e.Message, 13, UrbeTheme.Danger));
            }
        }

        void Choose(string id)
        {
            _cfg.AgentModels[_conv.AgentId] = new AiModelChoice(prov.Id, id);
            _conv.ProviderId = prov.Id;
            _conv.Model = id;
            SaveConfig();
            CloseView();
            RenderAll();
        }

        search.TextChanged += (_, _) => Paint();
        Tabs();
        await Load();
    }
}
