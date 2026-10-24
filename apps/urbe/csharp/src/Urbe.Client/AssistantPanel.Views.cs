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
            stack.Children.Add(new Border { Child = bubble, Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(16, 16, 4, 16), Background = UrbeTheme.Brush(UrbeTheme.Accent, .16) });
            return stack;
        }
        var view = new StackPanel { Spacing = 8 };
        var meta = m.Meta;
        if (meta?.Reasoning is { Length: > 0 } reasoning)
            view.Children.Add(Lazy(Label(meta.Streaming == true && m.Content.Count == 0 ? "Pensando…" : "Raciocínio", 12, UrbeTheme.Text3), () => Wrap(reasoning, 12, UrbeTheme.Text3)));
        foreach (var b in m.Content)
        {
            if (b.Type == "text")
            {
                var md = Markdown(b.Text ?? "");
                // an answer given before finishing the reading: folded, the revised one comes next
                view.Children.Add(meta?.Draft == true
                    ? new Expander { Header = Label("Resposta preliminar (substituída pela revisada abaixo)", 12, UrbeTheme.Text3), Content = md, HorizontalAlignment = HorizontalAlignment.Stretch }
                    : md);
            }
            else if (b.Type == "tool_use") view.Children.Add(ToolCard(b, m));
        }
        if (meta?.Streaming == true && m.Content.Count == 0 && string.IsNullOrEmpty(meta.Reasoning))
            view.Children.Add(Label("•••", 18, UrbeTheme.Text3));
        // UC-80: an answer to a request made in a note can be kept there, as a quote under the line that asked it
        var answer = string.Join("\n\n", m.Content.Where(b => b.Type == "text" && !string.IsNullOrWhiteSpace(b.Text)).Select(b => b.Text!.Trim()));
        if (meta?.Streaming != true && meta?.Draft != true && answer.Length > 0 && AskedInNote() is { } asked)
        {
            Button? pin = null;
            pin = LinkButton("Pôr na nota", () => { pin!.IsEnabled = false; _ = PinAnswerAsync(asked.Path, asked.Agent, asked.Request, answer, meta?.Model ?? ModelId()); });
            Avalonia.Automation.AutomationProperties.SetName(pin, "Pôr a resposta na nota");
            view.Children.Add(pin);
        }
        return view;
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
        if (b.Name == "update_plan" && !b.Pending && b.Id == LatestPlanId() && b.Input?["steps"] is JsonArray steps) card.Children.Add(Plan(steps));
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
            MinHeight = 150, MaxHeight = 325, CornerRadius = new CornerRadius(12),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
            Background = UrbeTheme.Brush(UrbeTheme.Surface2),
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
        var actions = new DockPanel();
        DockPanel.SetDock(open, Dock.Right);
        actions.Children.Add(open);
        actions.Children.Add(info);
        frame.Children.Add(actions);

        async Task Load()
        {
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
                    MaxHeight = 320, MaxWidth = 392, HorizontalAlignment = HorizontalAlignment.Center
                };
                picture.DetachedFromVisualTree += (_, _) => bitmap.Dispose();
                visual.Child = picture;
            }
            catch (Exception)
            {
                visual.Child = Wrap("Não foi possível exibir a prévia. Use Abrir para ver a imagem no vault.", 12, UrbeTheme.Text3);
            }
        }
        _ = Load();
        return frame;
    }

    private Control Clarification(AiChatBlock call)
    {
        var info = call.Input as JsonObject ?? new JsonObject();
        var question = (string?)info["question"] ?? "O que você prefere?";
        var reason = (string?)info["reason"];
        int total = Math.Max(1, (int)LegacyCustomize.JsToNumber(info["total"]));
        int done = Math.Clamp((int)LegacyCustomize.JsToNumber(info["resolved"]), 0, total);
        var input = new TextBox { AcceptsReturn = true, MinHeight = 64, MaxHeight = 180,
            TextWrapping = TextWrapping.Wrap, PlaceholderText = "Responda aqui; uma resposta pode resolver várias dúvidas." };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Wrap(question, 14, UrbeTheme.Text, FontWeight.SemiBold));
        if (!string.IsNullOrWhiteSpace(reason)) panel.Children.Add(Hint(reason));
        panel.Children.Add(Label(done + " de " + total + " dúvidas esclarecidas (estimativa)", 12, UrbeTheme.Text3));
        panel.Children.Add(new ProgressBar { Minimum = 0, Maximum = total, Value = done, Height = 5 });
        panel.Children.Add(input);
        panel.Children.Add(TextButton("Responder e continuar", () =>
        {
            var answer = (input.Text ?? "").Trim();
            if (answer.Length == 0 || _running is not null) return;
            Send(answer);
        }, primary: true));
        return panel;
    }

    private static string ClipText(string s, int n) => s.Length > n ? s[..n] : s;

    private static string SafeLabel(AiTool t, JsonNode? input)
    {
        try { return t.Label(input as JsonObject ?? new JsonObject()); }
        catch (Exception) { return t.Name; }
    }

    private static Control Pre(string text, bool error) => new Border
    {
        Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(8), Background = UrbeTheme.Brush(error ? "#c0504d" : UrbeTheme.Bg, error ? .12 : 1),
        Child = new SelectableTextBlock { Text = text, FontFamily = UrbeTheme.MonoFont, FontSize = 11.5, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) }
    };

    private static Control Plan(JsonArray steps)
    {
        var list = new StackPanel { Spacing = 6, Margin = new Thickness(2, 2, 0, 2) };
        foreach (var x in steps)
        {
            var s = (string?)x?["status"] ?? "pending";
            var mark = s switch { "done" => "✓", "in_progress" => "◐", _ => "○" };
            var description = new StackPanel { Spacing = 2 };
            description.Children.Add(Wrap((string?)x?["step"] ?? "", 13, s == "done" ? UrbeTheme.Text3 : UrbeTheme.Text));
            if ((string?)x?["details"] is { Length: > 0 } details) description.Children.Add(Wrap(details, 12, UrbeTheme.Text2));
            if ((string?)x?["acceptance"] is { Length: > 0 } acceptance)
                description.Children.Add(Wrap("Concluído quando: " + acceptance, 11, UrbeTheme.Text3));
            list.Children.Add(Row(Label(mark, 13, s == "done" ? UrbeTheme.Accent : UrbeTheme.Text3), description));
        }
        return list;
    }

    // ---------------------------------------------------------------- approval with a preview

    private Control Approval(AiApprovalRequest p)
    {
        var pv = p.Preview;
        bool danger = p.Access == "destructive", plugin = p.Access == "plugin";
        var box = new StackPanel { Spacing = 8 };
        box.Children.Add(Wrap((danger ? "Confirmar exclusão" : plugin ? "Criar e ligar plugin" : "Aprovar alteração") + (pv is { Path.Length: > 0 } ? " · " + pv.Path : ""), 13, UrbeTheme.Text, FontWeight.Bold));
        if (pv?.Note is { Length: > 0 } note) box.Children.Add(Wrap(note, 13, UrbeTheme.Text2));
        Control? diff = pv is not null && (pv.Before is not null || pv.After is not null) ? Diff(pv.Before, pv.After) : null;
        if (diff is not null) box.Children.Add(diff);
        // UC-51: the proposed text can be edited before approving; what is written is the person's version
        TextBox? editor = null;
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        if (AssistantTools.CanEdit(p.Tool, pv))
        {
            var edit = TextButton("Editar", () => { });
            edit.Margin = new Thickness(0, 0, 8, 0);
            edit.Click += (_, _) =>
            {
                if (editor is not null) return;
                editor = new TextBox
                {
                    Text = pv!.After, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, MaxHeight = 360,
                    FontFamily = UrbeTheme.MonoFont, FontSize = 13, Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
                    CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent)
                };
                Avalonia.Automation.AutomationProperties.SetName(editor, "Texto proposto");
                var at = diff is not null ? box.Children.IndexOf(diff) : box.Children.Count - 1;
                if (diff is not null) box.Children.Remove(diff);
                box.Children.Insert(Math.Max(0, at), editor);
                edit.IsVisible = false;
                editor.Focus();
            };
            actions.Children.Add(edit);
        }
        var reject = TextButton("Recusar", () => Answer(AiApproval.Reject()));
        reject.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(reject);
        if (!danger && !plugin)
        {
            var all = TextButton("Aprovar todas", () => Answer(AiApproval.ApproveAll));
            ToolTip.SetTip(all, "Aprovar esta e as próximas alterações desta resposta");
            all.Margin = new Thickness(0, 0, 8, 0);
            actions.Children.Add(all);
        }
        actions.Children.Add(TextButton(danger ? "Excluir" : plugin ? "Ligar plugin" : "Aprovar",
            () => Answer(editor is not null && editor.Text != pv?.After ? AiApproval.ApproveEdited(editor.Text ?? "") : AiApproval.Approve), primary: !danger, danger: danger));
        box.Children.Add(actions);
        return new Border { Child = box, Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Accent, .07) };
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
        else if (r.Stopped == "loop") tail.Children.Add(Note("Parei: o agente repetia uma ação que falhava.", "Continuar", () => Send("Continue de onde parou.")));
        else if (r.Stopped == "max_tokens") tail.Children.Add(Note("A resposta foi cortada pelo limite do modelo.", "Continuar", () => Send("Continue de onde parou.")));
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
            var sources = _conv.WebSources.ToArray();
            var read = sources.Count(s => s.Status == "página acessada");
            var summary = Row(UrbeTheme.Icon(UrbeTheme.Icons.Search, 14, UrbeTheme.Text2),
                Label("Fontes públicas · " + sources.Length + " (" + read + " páginas lidas)", 12, UrbeTheme.Text2, FontWeight.SemiBold));
            tail.Children.Add(Card(Lazy(summary, () =>
            {
                var list = new StackPanel { Spacing = 8 };
                foreach (var source in sources)
                {
                    // Exact source URLs are persisted, clickable via the existing MarkdownReader link launcher.
                    var safeTitle = source.Title.Replace("[", "").Replace("]", "").Replace("\n", " ").Trim();
                    var url = source.FinalUrl;
                    try { AssistantWebSession.ValidateUrl(url); }
                    catch (AiToolException) { continue; }
                    list.Children.Add(Markdown("[" + source.Id + "] [" + (safeTitle.Length > 120 ? safeTitle[..120] : safeTitle) +
                        "](" + url + ")"));
                    list.Children.Add(Hint(source.Domain + " · " +
                        (source.Status == "página acessada" ? "Página consultada" : "Somente resultado de busca") +
                        " · " + (source.Published ?? "data desconhecida") + " · acesso " + source.Accessed));
                }
                return list;
            })));
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
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(14),
                Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
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
        var desc = Wrap(a.Description, 14, UrbeTheme.Text2);
        desc.TextAlignment = TextAlignment.Center;
        box.Children.Add(desc);
        foreach (var s in sug.Take(5))
        {
            var b = new Button
            {
                Content = Wrap(s, 13, UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12, 9),
                CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1)
            };
            b.Click += (_, _) => { _input.Text = s; Submit(); };
            box.Children.Add(b);
        }
        return box;
    }

    // ---------------------------------------------------------------- pickers

    private async Task PickAgentAsync()
    {
        if (_running is not null) return;
        var list = AssistantAgent.Agents.Concat(_cfg.CustomAgents).ToList();
        var i = await _app.Dialogs.ChooseAsync("Agente", list.Select(a => new DialogItem(AgentNames.CallName(a, _cfg) + (a.Id == _conv.AgentId ? "  ✓" : ""), UrbeTheme.Icons.Assistant,
            a.Description.Length > 0 ? a.Description : a.Tools == "read" ? "Só leitura" : "Personalizado")).ToList());
        if (i is not { } k) return;
        _conv.AgentId = list[k].Id;
        _cfg.AgentId = list[k].Id;
        SaveConfig();
        RenderAll();
    }

    private async Task PickPolicyAsync()
    {
        if (_running is not null) return;
        string[] values = ["ask", "auto", "readonly"];
        var i = await _app.Dialogs.ChooseAsync("Permissões nesta conversa",
        [
            new DialogItem("Pede aprovação" + (_conv.Policy == "ask" ? "  ✓" : ""), UrbeTheme.Icons.Check, "Lê à vontade; mostra cada alteração antes de aplicar"),
            new DialogItem("Edita sozinho" + (_conv.Policy == "auto" ? "  ✓" : ""), UrbeTheme.Icons.Edit, "Aplica alterações direto (dá para desfazer). Exclusões ainda pedem aprovação"),
            new DialogItem("Só leitura" + (_conv.Policy == "readonly" ? "  ✓" : ""), UrbeTheme.Icons.Search, "Nunca altera notas")
        ]);
        if (i is not { } k) return;
        _conv.Policy = values[k];
        _cfg.Policy = values[k];
        SaveConfig();
        RenderChrome();
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
        var list = _store.ListConversations();
        var body = OpenView("Conversas");
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
                if (_running is not null) return;
                _conv = _store.GetConversation(c.Id) ?? c;
                CloseView();
                RenderAll();
            };
            var del = IconButton(UrbeTheme.Icons.Trash, "Excluir conversa");
            del.Click += async (_, _) =>
            {
                if (!await _app.Dialogs.ConfirmAsync("Excluir esta conversa?", null, "Excluir", danger: true)) return;
                _store.DeleteConversation(c.Id);
                if (_conv.Id == c.Id) _conv = NewConversation();
                ShowHistory();
                RenderAll();
            };
            var row = new DockPanel();
            DockPanel.SetDock(del, Dock.Right);
            row.Children.Add(del);
            row.Children.Add(open);
            body.Children.Add(row);
        }
    }

    private static Control Section(string title, params Control[] items)
    {
        var s = new StackPanel { Spacing = 8, Children = { Label(title, 15, UrbeTheme.Text, FontWeight.Bold) } };
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
        body.Children.Add(Wrap("Personalize como seus agentes trabalham", 15, UrbeTheme.Text2));
        var choices = new (string Id, string Title, string Description)[]
        {
            ("models", "Modelos", "Provedores e imagens"),
            ("agents", "Agentes", "Identidade e perfis"),
            ("web", "Pesquisa", "Internet e fontes"),
            ("context", "Contexto", "Instruções e memória"),
            ("execution", "Execução", "Autonomia e segurança")
        };
        var navigation = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 150,
            ItemHeight = 66 };
        foreach (var choice in choices)
        {
            var selected = choice.Id == _settingsCategory;
            var item = new Button
            {
                Width = 146, MinHeight = 60, Padding = new Thickness(11, 8),
                CornerRadius = new CornerRadius(12), HorizontalContentAlignment = HorizontalAlignment.Left,
                BorderThickness = new Thickness(1),
                BorderBrush = UrbeTheme.Brush(selected ? UrbeTheme.Accent : UrbeTheme.Line2),
                Background = UrbeTheme.Brush(selected ? UrbeTheme.Accent : UrbeTheme.Surface, selected ? .13 : 1),
                Content = new StackPanel { Spacing = 2, Children =
                {
                    Label(choice.Title, 13, selected ? UrbeTheme.Accent : UrbeTheme.Text, FontWeight.SemiBold),
                    Wrap(choice.Description, 11, UrbeTheme.Text3)
                } }
            };
            Avalonia.Automation.AutomationProperties.SetName(item, "Configurações: " + choice.Title);
            item.Click += (_, _) => { _settingsCategory = choice.Id; ShowSettings(); };
            navigation.Children.Add(item);
        }
        body.Children.Add(navigation);
        body.Children.Add(new Border { Height = 1, Background = UrbeTheme.Brush(UrbeTheme.Line2),
            Margin = new Thickness(0, 4, 0, 2) });

        if (_settingsCategory == "models")
        {
        body.Children.Add(Section("Conexões e provedores", Hint("Gerencie credenciais, modelos e testes de conexão em um só lugar.")));
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
                foreach (var id in c.AgentModels.Where(x => x.Value.ProviderId == p.Id).Select(x => x.Key).ToList()) c.AgentModels.Remove(id);
                SaveConfig();
                ShowSettings();
                RenderChrome();
            };
            actions.Children.Add(rm);
            provs.Children.Add(Card(new StackPanel { Spacing = 8, Children = { info, actions } }));
        }
        var add = TextButton("+ Adicionar provedor", () => _ = AddProviderAsync());
        body.Children.Add(Section("Provedores de modelo", Hint("As chaves ficam salvas só neste aparelho e vão direto para o provedor."), provs, add));

        var imageProvider = c.Providers.FirstOrDefault(p => p.Id == c.ImageProviderId);
        var imageChoice = imageProvider is null ? "Automático (provedor de imagens disponível)"
            : (imageProvider.Name ?? imageProvider.Id) + " · " + (c.ImageModel.Length > 0 ? c.ImageModel : AssistantProviders.ImageModel(imageProvider.Preset) ?? "Escolher modelo");
        var imagePrice = Hint(imageProvider is null ? "O custo depende do provedor que for escolhido automaticamente." : "Consultando preço do modelo…");
        body.Children.Add(Section("Geração de imagens",
            Hint("Modelo visual independente do chat. Preços em US$, segundo a unidade de cobrança do provedor."),
            Label(imageChoice, 13, UrbeTheme.Text, FontWeight.SemiBold),
            imagePrice,
            TextButton("Ver modelos e preços", () => _ = PickConfiguredModelAsync(null))));
        if (imageProvider is not null)
        {
            var id = c.ImageModel.Length > 0 ? c.ImageModel : AssistantProviders.ImageModel(imageProvider.Preset) ?? "";
            if (id.Length == 0) imagePrice.Text = "Selecione o ID do modelo para consultar o preço.";
            else _ = PriceInSettingsAsync(imageProvider, id, imagePrice);
        }

        }

        if (_settingsCategory == "context")
        {
        body.Children.Add(Section("Preferências de contexto", Hint("Estas informações orientam as respostas; você mantém o controle sobre o que é lembrado.")));
        var instr = new TextBox { Text = c.Instructions, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, PlaceholderText = "Ex.: escreva em português do Brasil, frases curtas; use a pasta Diário para registros do dia." };
        instr.LostFocus += (_, _) => { c.Instructions = instr.Text ?? ""; SaveConfig(); };
        instr.TextChanged += (_, _) => c.Instructions = instr.Text ?? "";
        body.Children.Add(Section("Instruções para todos os agentes", instr));

        var nmem = c.MemoryRecords.Count + c.Memory.Values.Sum(items => items.Count);
        body.Children.Add(Section("Memória",
            Hint(nmem + " lembranças locais · recuperação por relevância · histórico e fontes."),
            TextButton("Gerenciar memórias", ShowMemoryManager)));

        }

        if (_settingsCategory == "agents")
        {
        body.Children.Add(Section("Equipe de agentes", Hint("Modelos, nomes, instruções individuais e capacidades de cada agente.")));
        // UC-81: each agent's call name, on this device (the chip, the prompt and &Nome&: in a note use it)
        var names = new StackPanel { Spacing = 6 };
        foreach (var a in AgentNames.All(c))
        {
            var rename = TextButton("Renomear", () => _ = RenameAgentAsync(a));
            var configure = TextButton("Configurar", () => EditAgentProfile(a));
            var row = new DockPanel();
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { rename, configure } };
            DockPanel.SetDock(actions, Dock.Right);
            row.Children.Add(actions);
            var call = AgentNames.CallName(a, c);
            row.Children.Add(new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { Label(call, 14, UrbeTheme.Text, FontWeight.SemiBold), Hint(call == a.Name ? "Nome padrão" : a.Name) } });
            names.Children.Add(row);
        }
        body.Children.Add(Section("Nomes dos agentes", Hint("Como cada agente se chama. Numa conversa, chame-o por um nome novo (“Jarvis, …”) e ele pergunta se deve passar a se chamar assim."), names));

        var custom = new StackPanel { Spacing = 8 };
        if (c.CustomAgents.Count == 0) custom.Children.Add(Hint("Crie agentes com instruções próprias, ex.: “Revisor acadêmico”."));
        foreach (var a in c.CustomAgents)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            actions.Children.Add(TextButton("Editar", () => EditAgent(a)));
            actions.Children.Add(TextButton("Modelo e blocos", () => EditAgentProfile(a)));
            var rm = IconButton(UrbeTheme.Icons.Trash, "Remover");
            rm.Click += (_, _) =>
            {
                c.CustomAgents.RemoveAll(x => x.Id == a.Id);
                c.AgentModels.Remove(a.Id);
                c.AgentInstructionBlocks.Remove(a.Id);
                SaveConfig(); ShowSettings();
            };
            actions.Children.Add(rm);
            custom.Children.Add(Card(new StackPanel
            {
                Spacing = 6,
                Children = { Label(a.Name, 14, UrbeTheme.Text, FontWeight.SemiBold), Hint(a.Tools switch { "all" => "Tudo", "write" => "Lê e escreve (sem excluir)", _ => "Só leitura" }), actions }
            }));
        }
        body.Children.Add(Section("Agentes personalizados", custom, TextButton("+ Novo agente", () => EditAgent(null))));

        }

        if (_settingsCategory == "execution")
        {
            body.Children.Add(Section("Autonomia e proteção",
                Hint("O modo de aprovação de alterações pode ser trocado também na conversa."),
                TextButton("Alterar permissões da conversa", () => _ = PickPolicyAsync())));
            body.Children.Add(Section("Profundidade da investigação",
                Hint("O agente define quantas perguntas e etapas são necessárias a partir do objetivo, das respostas e das evidências. Você continua controlando aprovações e pode interromper a execução a qualquer momento.")));
            body.Children.Add(Section("Transparência",
                Hint("Atividades ficam agrupadas no chat; entradas, saídas, decisões e aprovações podem ser examinadas por etapa.")));
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

    private void EditAgent(AiAgentDef? a)
    {
        bool isNew = a is null;
        var body = OpenView(isNew ? "Novo agente" : a!.Name, () => ShowSettings());
        var name = new TextBox { Text = a?.Name ?? "", PlaceholderText = "Ex.: Revisor acadêmico" };
        var prompt = new TextBox { Text = a?.Prompt ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, PlaceholderText = "Como este agente deve pensar e trabalhar." };
        string[] values = ["all", "write", "read"];
        var tools = new ComboBox { ItemsSource = new[] { "Tudo (inclusive excluir, com aprovação)", "Ler e escrever, sem excluir", "Só ler" }, SelectedIndex = Math.Max(0, Array.IndexOf(values, a?.Tools ?? "all")) };
        body.Children.Add(Section("Nome", name));
        body.Children.Add(Section("Instruções", prompt));
        body.Children.Add(Section("O que pode fazer", tools));
        var save = TextButton("Salvar", () =>
        {
            var n = (name.Text ?? "").Trim();
            if (n.Length == 0) return;
            var p = (prompt.Text ?? "").Trim();
            var t = values[Math.Max(0, tools.SelectedIndex)];
            var made = new AiAgentDef(a?.Id ?? AssistantAgent.NewId("a"), n, t == "read" ? "search" : "sparkle", p.Length > 80 ? p[..80] : p, t, t == "all", p);
            int at = _cfg.CustomAgents.FindIndex(x => x.Id == made.Id);
            if (at >= 0) _cfg.CustomAgents[at] = made; else _cfg.CustomAgents.Add(made);
            SaveConfig();
            EditAgentProfile(made);
        }, primary: true);
        var cancel = TextButton("Cancelar", () => ShowSettings());
        cancel.Margin = new Thickness(0, 0, 8, 0);
        body.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } });
    }


    /// <summary>Provider/model picker for images and for any agent. Never changes the conversation's model.</summary>
    private async Task PickConfiguredModelAsync(string? agentId)
    {
        if (_cfg.Providers.Count == 0) { ShowSettings("openrouter"); return; }
        bool image = agentId is null;
        var agent = image ? null : AssistantAgent.AgentById(agentId, _cfg.CustomAgents);
        void Back()
        {
            if (agent is null) ShowSettings();
            else EditAgentProfile(agent);
        }
        var choice = image
            ? new AiModelChoice(_cfg.ImageProviderId ?? "", _cfg.ImageModel)
            : _cfg.AgentModels.GetValueOrDefault(agentId!) ?? new AiModelChoice("", "");
        var providers = _cfg.Providers.Where(p => !image || AssistantProviders.Resolve(p).Kind == "openai" &&
            p.Preset is "openrouter" or "openai" or "gemini" or "custom").ToList();
        if (providers.Count == 0) { ShowSettings(); return; }
        var selected = providers.FirstOrDefault(p => p.Id == choice.ProviderId) ?? providers[0];
        var body = OpenView(image ? "Modelo de imagem" : "Modelo de " + AgentNames.CallName(agent!, _cfg), Back);
        body.Children.Add(Hint(image
            ? "O modelo de imagem não altera o chat. Preços em US$ com unidade de cobrança e fonte; valores variáveis não são estimados como zero."
            : "Este modelo será usado somente pelo agente escolhido. Sem seleção, ele herda o modelo do Assistente."));
        var auto = TextButton(image ? "Usar escolha automática" : "Herdar modelo do Assistente", () =>
        {
            if (image) { _cfg.ImageProviderId = null; _cfg.ImageModel = ""; }
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
            if (image) { _cfg.ImageProviderId = selected.Id; _cfg.ImageModel = id.Trim(); }
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
                var priceLine = Label(image ? "Consultando preço…" : "", 12, UrbeTheme.Text2);
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    MinHeight = 68, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12),
                    Background = UrbeTheme.Brush(chosen ? UrbeTheme.Accent : UrbeTheme.Surface, chosen ? .14 : 1),
                    BorderBrush = UrbeTheme.Brush(chosen ? UrbeTheme.Accent : UrbeTheme.Line2),
                    BorderThickness = new Thickness(1),
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

        async Task Load()
        {
            list.Children.Clear();
            list.Children.Add(Hint("Carregando modelos…"));
            loadError = null;
            try
            {
                if (image) models = await Providers.ListImageModelsAsync(selected);
                else if (_cfg.ModelCache.TryGetValue(selected.Id, out var cached) && Now() - cached.At < 6 * 3600_000) models = cached.List;
                else
                {
                    models = await Providers.ListModelsAsync(selected);
                    _cfg.ModelCache[selected.Id] = new AiModelCache(Now(), models);
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                models = [];
                loadError = e.Message + " Você ainda pode informar um ID manualmente.";
            }
            if (image && models.Count == 0 && AssistantProviders.ImageModel(selected.Preset) is { Length: > 0 } defaultModel)
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
        body.Children.Add(Hint(agent.Description.Length > 0 ? agent.Description : "Agente personalizado."));
        var selected = _cfg.AgentModels.GetValueOrDefault(agent.Id);
        var provider = selected is null ? null : _cfg.Providers.FirstOrDefault(p => p.Id == selected.ProviderId);
        var choice = provider is null ? "Padrão do Assistente (herdado)" : (provider.Name ?? provider.Id) + " · " + selected!.Model;
        var blocksPanel = new StackPanel { Spacing = 10 };
        var rows = new List<(TextBox Title, TextBox Body, Control Card)>();

        void AddBlock(AiInstructionBlock block)
        {
            var title = new TextBox { Text = block.Title, PlaceholderText = "Nome do bloco, ex.: Estilo de escrita" };
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
        void SaveBlocks()
        {
            var values = rows.Select(r => new AiInstructionBlock((r.Title.Text ?? "").Trim(), (r.Body.Text ?? "").Trim()))
                .Where(b => b.Content.Length > 0).ToList();
            if (values.Count == 0) _cfg.AgentInstructionBlocks.Remove(agent.Id);
            else _cfg.AgentInstructionBlocks[agent.Id] = values;
            SaveConfig();
        }
        body.Children.Add(Section("Modelo de conversa", Hint("Opcional: se não escolher, este agente continua usando o modelo padrão."),
            Label(choice, 13, UrbeTheme.Text, FontWeight.SemiBold),
            TextButton("Selecionar provedor e modelo", () => { SaveBlocks(); _ = PickConfiguredModelAsync(agent.Id); })));
        body.Children.Add(Section("Instruções por blocos", Hint("Você pode criar vários blocos. Todos são usados em conjunto com as instruções gerais, mantendo as permissões do agente."),
            blocksPanel, TextButton("+ Adicionar bloco", () => AddBlock(new AiInstructionBlock("", "")))));
        if (_cfg.AgentInstructionBlocks.TryGetValue(agent.Id, out var existing))
            foreach (var block in existing) AddBlock(block);
        var save = TextButton("Salvar instruções", () => { SaveBlocks(); ShowSettings(); }, primary: true);
        body.Children.Add(save);
    }

    /// <summary>pickModel(): the provider's models (cached 6 h), search, price, the "sem ferramentas" warning, and a typed id.</summary>
    public async Task PickModelAsync()
    {
        if (_cfg.Providers.Count == 0) { ShowSettings("openrouter"); return; }
        var prov = Provider()!;
        var body = OpenView("Modelo");
        body.Children.Add(Hint("Modelo padrão desta conversa; cada agente pode ter um modelo próprio em Configurações → Nomes dos agentes → Configurar."));
        var tabs = new WrapPanel();
        var search = new TextBox { PlaceholderText = "Buscar modelo (ex.: claude, gpt, gemini)" };
        var list = new StackPanel { Spacing = 6 };
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
        body.Children.Add(list);
        IReadOnlyList<AiModelInfo> models = [];

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
                             (m.Price is { } pr ? " · US$ " + pr.Input.ToString("0.00", CultureInfo.InvariantCulture) + "/" + pr.Output.ToString("0.00", CultureInfo.InvariantCulture) + " por 1M" : "") +
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

        async Task Load()
        {
            list.Children.Clear();
            list.Children.Add(Hint("Carregando modelos…"));
            if (_cfg.ModelCache.TryGetValue(prov.Id, out var cache) && Now() - cache.At < 6 * 3600_000) models = cache.List;
            else
            {
                try
                {
                    models = await Providers.ListModelsAsync(prov);
                    _cfg.ModelCache[prov.Id] = new AiModelCache(Now(), models);
                    SaveConfig();
                }
                catch (Exception e)
                {
                    list.Children.Clear();
                    list.Children.Add(Wrap(e.Message, 13, "#c0504d"));
                    models = [];
                    return;
                }
            }
            Paint();
        }

        void Choose(string id)
        {
            _cfg.ProviderId = prov.Id;
            _cfg.Model = id;
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
