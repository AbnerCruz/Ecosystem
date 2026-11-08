using Avalonia;
using Avalonia.Controls;
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
            return CompactStatus + ":" + (_pending?.Request.Call.Id ?? "") + ":" +
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

        var body = new StackPanel { Spacing = UrbeTokens.Space.S };
        var heading = new StackPanel { Spacing = 2 };
        heading.Children.Add(Label(agent, 11, UrbeTheme.Text3, FontWeight.SemiBold));
        heading.Children.Add(Wrap(title, 15, UrbeTheme.Text, FontWeight.SemiBold));
        body.Children.Add(heading);

        if (status == "approval" && _pending is { } pending)
        {
            // Same diff, 'Edit', Approve / Reject / Approve All. No second permission path.
            body.Children.Add(Approval(pending.Request));
        }
        else if (status == "question" && _conv.Forms.Any(f => f.State == "pending"))
        {
            body.Children.Add(Wrap("Preencha o formulário para o agente continuar. Seus valores ficam salvos ao sair.", 13, UrbeTheme.Text2));
            body.Children.Add(TextButton("Preencher formulário", Open));
        }
        else if (status == "question" && calls.LastOrDefault(b => b.Name == "ask_user") is { } question)
        {
            body.Children.Add(Clarification(question));
        }
        else if (status == "thinking")
        {
            if (last is not null && _progress.TryGetValue(last.Id, out var progress))
                body.Children.Add(Wrap(progress, 12, UrbeTheme.Text2));
            if (!UrbeTokens.Motion.Reduced)
                body.Children.Add(new ProgressBar { Height = 2, IsIndeterminate = true,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Accent),
                    Background = UrbeTheme.Brush(UrbeTheme.Line2) });
        }
        else
        {
            var run = _conv.Runs.LastOrDefault();
            if (status == "error" && run?.Error is { Length: > 0 } error)
                body.Children.Add(Wrap(error, 12, UrbeTheme.Text2));
            else
            {
                var reply = _conv.Messages.LastOrDefault(m => m.Role == "assistant"
                    && m.Content.Any(b => b.Type == "text" && !b.Hidden && !string.IsNullOrWhiteSpace(b.Text)));
                var answer = reply?.Content.LastOrDefault(b => b.Type == "text" && !b.Hidden)?.Text;
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    // The popup is a reading surface, not a 320-character teaser.
                    // Keep the entire answer in a bounded, independently scrollable area.
                    // Scrolling it must not push the composer or "Abrir conversa" off-screen.
                    var replyScroll = new ScrollViewer
                    {
                        MaxHeight = 144,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = Wrap(answer, 13, UrbeTheme.Text2)
                    };
                    Avalonia.Automation.AutomationProperties.SetName(replyScroll, "Resposta da IA — role para ler");
                    body.Children.Add(replyScroll);
                }
                else
                {
                    body.Children.Add(Wrap(
                        run?.Changes.Count > 0 ? "A alteração está registrada na nota."
                        : _conv.Runs.Count == 0 ? "Faça uma pergunta. A conversa continuará aberta quando voltar."
                        : "Consulte os detalhes da execução na conversa.",
                        13, UrbeTheme.Text2));
                }
            }
        }

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
        body.Children.Add(composer);

        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        if (status == "thinking")
            actions.Children.Add(TextButton("Parar", StopCompact));
        actions.Children.Add(LinkButton("Abrir conversa", Open));
        actions.Children.Add(LinkButton("Fechar", DismissCompact));
        body.Children.Add(actions);
        return body;
    }
}
