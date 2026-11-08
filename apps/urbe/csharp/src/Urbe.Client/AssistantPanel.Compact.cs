using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// A small projection of the EXISTING conversation, never another agent/session.
/// The host may show this in a non-modal overlay while the editor stays interactive.
/// Approvals and questions delegate to the identical controls and callbacks of the full panel.
/// </summary>
public sealed partial class AssistantPanel
{
    public event Action? CompactChanged;

    private ScrollViewer? _compactChatScroll;
    private string? _compactChatScrollId;
    private double _compactChatScrollOffset;
    private bool _compactChatFollowTail = true;

    /// <summary>Only calls initiated from a note opt in to contextual presentation.</summary>
    public bool CompactActive { get; private set; }

    /// <summary>Exposed for UI regression tests and the floating launcher.</summary>
    public string CompactStatus
    {
        get
        {
            if (!CompactActive) return "closed";
            if (_pending is not null) return "approval";
            if (_running is not null || _inNote is not null && _conv.Runs.Count == 0) return "thinking";
            if (_conv.Runs.LastOrDefault()?.Stopped == "needs_input") return "question";
            if (_conv.Runs.LastOrDefault()?.Stopped == "no_progress") return "paused";
            if (_conv.Runs.LastOrDefault()?.Stopped == "error") return "error";
            if (_conv.Runs.LastOrDefault()?.Stopped == "aborted") return "cancelled";
            return "result";
        }
    }

    /// <summary>A stable key prevents rebuilding live approval editors and wiping partially typed responses.</summary>
    public string CompactRenderKey
    {
        get
        {
            var tool = _conv.Messages.AsEnumerable().Reverse()
                .SelectMany(m => m.Content.Where(b => b.Type == "tool_use"))
                .FirstOrDefault();
            // Never remount the input on every streamed token: that would steal focus
            // and the caret from someone writing while the assistant is answering.
            return _conv.Id + ":" + CompactStatus + ":" + (_pending?.Request.Call.Id ?? "") + ":" +
                (tool?.Id ?? "") + ":" + _conv.Runs.Count + ":" + _conv.Messages.Count;
        }
    }

    private void ActivateCompact()
    {
        CompactActive = true;
        NotifyCompactChanged();
    }

    /// <summary>Open the current conversation over the editor without changing destination or creating a chat.</summary>
    public void ShowCompact()
    {
        Load();
        if (!_loaded) return;
        _everOpened = true; // Expanding the popup must resume this conversation, not the agent picker.
        IsVisible = false;
        ActivateCompact();
    }

    public void DismissCompact()
    {
        CompactActive = false;
        NotifyCompactChanged();
    }

    /// <summary>Notify the host without touching focus or opening the assistant screen.</summary>
    private void NotifyCompactChanged()
    {
        if (Dispatcher.UIThread.CheckAccess()) CompactChanged?.Invoke();
        else Dispatcher.UIThread.Post(() => CompactChanged?.Invoke(), DispatcherPriority.Background);
    }

    public void StopCompact() => Stop();

    /// <summary>Builds only the surface required NOW: running, confirmation, question, or result.</summary>
    public Control CompactContent()
    {
        var status = CompactStatus;
        var agent = AgentNames.Named(AssistantAgent.AgentById(_conv.AgentId, _cfg.CustomAgents), _cfg).Name;
        var calls = _conv.Messages.Where(m => m.Role == "assistant")
            .SelectMany(m => m.Content.Where(b => b.Type == "tool_use")).ToList();
        var last = calls.LastOrDefault();
        var title = status switch
        {
            "approval" => "Precisa da sua aprovação",
            "question" => "O agente tem uma pergunta",
            "thinking" when last is not null => ActivityVerb(last.Name),
            "thinking" => "Analisando o pedido",
            "error" => "Não foi possível concluir",
            "cancelled" => "Execução interrompida",
            "paused" => "Pausado: operações sem progresso",
            _ when _conv.Runs.Count == 0 => "Converse sem sair do editor",
            _ => "Trabalho concluído"
        };

        // Fixed-height popup: only the transcript scrolls, never the composer.
        var body = new Grid { Height = 260, RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
        var heading = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 5) };
        heading.Children.Add(Label(agent, 11, UrbeTheme.Text3, FontWeight.SemiBold));
        heading.Children.Add(Wrap(title, 15, UrbeTheme.Text, FontWeight.SemiBold));
        Grid.SetRow(heading, 0);
        body.Children.Add(heading);

        var transcript = new StackPanel { Spacing = UrbeTokens.Space.S };

        if (status == "approval" && _pending is { } pending)
        {
            // Same diff, 'Edit', Approve / Reject / Approve All. No second permission path.
            transcript.Children.Add(Approval(pending.Request));
        }
        else if (status == "question" && _conv.Forms.Any(f => f.State == "pending"))
        {
            transcript.Children.Add(Wrap("Preencha o formulário para o agente continuar. Seus valores ficam salvos ao sair.", 13, UrbeTheme.Text2));
            transcript.Children.Add(TextButton("Preencher formulário", Open));
        }
        else if (status == "question" && calls.LastOrDefault(b => b.Name == "ask_user") is { } question)
        {
            transcript.Children.Add(Clarification(question));
        }
        else if (status == "thinking")
        {
            if (last is not null && _progress.TryGetValue(last.Id, out var progress))
                transcript.Children.Add(Wrap(progress, 12, UrbeTheme.Text2));
            if (!UrbeTokens.Motion.Reduced)
                transcript.Children.Add(new ProgressBar { Height = 2, IsIndeterminate = true,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Accent),
                    Background = UrbeTheme.Brush(UrbeTheme.Line2) });
        }
        else if (status == "error" && _conv.Runs.LastOrDefault()?.Error is { Length: > 0 } error)
        {
            transcript.Children.Add(Wrap(error, 12, UrbeTheme.Text2));
        }

        // Complete user-visible thread. Hidden instructions and internal tool
        // results do not appear as human conversation messages.
        foreach (var message in _conv.Messages)
        {
            if (message.Role is not ("user" or "assistant") ||
                (message.Role == "user" && message.Meta?.ToolResults == true))
                continue;

            var text = string.Join("\n\n", message.Content
                .Where(b => b.Type == "text" && !b.Hidden && !string.IsNullOrWhiteSpace(b.Text))
                .Select(b => b.Text));
            var files = message.Content.Where(b => b.Type == "attachment" && !string.IsNullOrWhiteSpace(b.FileName))
                .Select(b => b.FileName!);
            var attachments = string.Join(", ", files);
            if (text.Length == 0 && attachments.Length == 0) continue;

            var user = message.Role == "user";
            var bubble = new StackPanel
            {
                Spacing = 3, Margin = new Thickness(0, 3),
                HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Stretch
            };
            bubble.Children.Add(Label(user ? "Você" : agent, 11, UrbeTheme.Text3, FontWeight.SemiBold));
            var content = new StackPanel { Spacing = 4 };
            if (text.Length > 0)
            {
                var textBlock = new SelectableTextBlock
                {
                    Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text)
                };
                Ui.TextActions.Attach(textBlock);
                content.Children.Add(textBlock);
            }
            if (attachments.Length > 0)
                content.Children.Add(Wrap("Anexos: " + attachments, 11, UrbeTheme.Text2));
            bubble.Children.Add(new Border
            {
                Child = content, Padding = new Thickness(10, 8),
                CornerRadius = new CornerRadius(12),
                Background = UrbeTheme.Brush(user ? UrbeTheme.Accent : UrbeTheme.Surface, user ? .14 : 1)
            });
            transcript.Children.Add(bubble);
        }
        if (transcript.Children.Count == 0)
            transcript.Children.Add(Wrap("Envie uma mensagem para começar a conversa.", 13, UrbeTheme.Text2));

        // Preserve the reader's position during streaming. Auto-follow only if
        // the person was already at the end of the conversation.
        if (_compactChatScrollId == _conv.Id && _compactChatScroll is { } old)
        {
            _compactChatScrollOffset = old.Offset.Y;
            _compactChatFollowTail = old.Extent.Height - old.Viewport.Height - old.Offset.Y <= 24;
        }
        else
        {
            _compactChatScrollOffset = 0;
            _compactChatFollowTail = true;
        }
        var chatScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = transcript, Margin = new Thickness(0, 0, 0, 4)
        };
        Avalonia.Automation.AutomationProperties.SetName(chatScroll, "Conversa completa da IA — role para ler");
        _compactChatScroll = chatScroll;
        _compactChatScrollId = _conv.Id;
        var restored = false;
        chatScroll.LayoutUpdated += (_, _) =>
        {
            if (restored || !ReferenceEquals(_compactChatScroll, chatScroll) || chatScroll.Viewport.Height <= 0) return;
            restored = true;
            chatScroll.Offset = new Vector(0, _compactChatFollowTail
                ? Math.Max(0, chatScroll.Extent.Height - chatScroll.Viewport.Height)
                : Math.Min(_compactChatScrollOffset, Math.Max(0, chatScroll.Extent.Height - chatScroll.Viewport.Height)));
        };
        Grid.SetRow(chatScroll, 1);
        body.Children.Add(chatScroll);

        // A separate lightweight composer projects into the SAME underlying input and conversation.
        // Reconstruct only on a state transition: no duplicated chat, tool or storage authority.
        var compactInput = new TextBox
        {
            Text = _input.Text ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 44, MaxHeight = 104, FontSize = 14,
            PlaceholderText = "Pergunte ao Assistente…",
            CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 8)
        };
        EditorInput.ConfigureMultiline(compactInput);
        Avalonia.Automation.AutomationProperties.SetName(compactInput, "Mensagem rápida ao Assistente");
        compactInput.TextChanged += (_, _) =>
        {
            if (_input.Text != compactInput.Text) _input.Text = compactInput.Text;
        };
        void SendCompact()
        {
            _input.Text = compactInput.Text ?? "";
            if (_cfg.Providers.Count == 0 || ModelId().Length == 0)
            {
                // A first-time setup needs the full settings/model picker; never hide it under the editor.
                // Keep the draft intact rather than discarding the user's message.
                Open();
                if (_cfg.Providers.Count == 0)
                {
                    if (HasDeviceAi) ShowDevice(); else ShowSettings("openrouter");
                }
                else _ = PickModelAsync();
                return;
            }
            Submit();
            compactInput.Text = _input.Text;
        }
        compactInput.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift)
                && !OperatingSystem.IsAndroid())
            {
                e.Handled = true;
                SendCompact();
            }
        };
        var send = TextButton("Enviar", SendCompact);
        send.MinHeight = UrbeTokens.MinTouch;
        var composer = new DockPanel { Margin = new Thickness(0, 6, 0, 4) };
        DockPanel.SetDock(send, Dock.Right);
        composer.Children.Add(send);
        composer.Children.Add(compactInput);
        Grid.SetRow(composer, 2);
        body.Children.Add(composer);

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        if (status == "thinking")
            actions.Children.Add(TextButton("Parar", StopCompact));
        actions.Children.Add(LinkButton("Abrir conversa", Open));
        actions.Children.Add(LinkButton("Fechar", DismissCompact));
        Grid.SetRow(actions, 3);
        body.Children.Add(actions);
        return body;
    }
}
