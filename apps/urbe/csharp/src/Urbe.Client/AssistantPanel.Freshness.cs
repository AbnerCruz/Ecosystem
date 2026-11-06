using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// CST-145: above the composer, a calm line when this conversation is long for the model's window, or when the person
/// comes back after a long pause and asks about something else: "começar uma nova com um resumo?". The action opens a
/// new chat with a short summary of this one (and its id) in the composer, to read, change and send; nothing is sent and
/// nothing is switched by itself. The old conversation stays in Conversas. "×" hides the line for this conversation.
/// </summary>
public sealed partial class AssistantPanel
{
    private readonly Border _freshness = new() { IsVisible = false, Margin = new Thickness(0, 0, 0, 6) };
    private readonly HashSet<string> _freshnessDismissed = new(StringComparer.Ordinal);
    private (string Id, int Count, AiChatMessage? Last, long Chars) _freshnessSize;
    private ConversationHint? _freshnessShown;

    /// <summary>The line shown above the composer now, or null (tests).</summary>
    public ConversationHint? ConversationHint => _freshness.IsVisible ? _freshnessShown : null;

    private void UpdateFreshness()
    {
        var hint = CurrentFreshness();
        if (hint is null) { _freshness.IsVisible = false; _freshnessShown = null; return; }
        if (_freshness.IsVisible && _freshnessShown == hint) return;
        _freshnessShown = hint;
        var close = IconButton(UrbeTheme.Icons.Close, "Continuar nesta conversa");
        close.Width = close.Height = 36;
        close.Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 14, UrbeTheme.Text3);
        close.Click += (_, _) => DismissFreshness();
        var start = LinkButton("Nova com resumo", StartFromSummary);
        Avalonia.Automation.AutomationProperties.SetName(start, "Começar uma nova conversa com um resumo desta");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { start, close } };
        DockPanel.SetDock(actions, Dock.Right);
        var text = Wrap(hint.Text, 13, UrbeTheme.Text2);
        text.VerticalAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(text, hint.Reason == ConversationHintReason.Long
            ? "A conversa já ocupa cerca de " + Math.Round(hint.Fill * 100) + "% do que o modelo recebe por pedido; perto do limite, o começo dela passa a ser resumido ou deixado de fora."
            : "Faz algumas horas desde a última mensagem e o pedido não tem relação com o que foi conversado.");
        _freshness.Child = new DockPanel { Children = { actions, text } };
        _freshness.Padding = new Thickness(12, 4, 4, 4);
        _freshness.CornerRadius = new CornerRadius(12);
        _freshness.Background = UrbeTheme.Brush(UrbeTheme.Accent, .08);
        _freshness.BorderBrush = Brushes.Transparent;
        _freshness.IsVisible = true;
    }

    private ConversationHint? CurrentFreshness()
    {
        if (!_loaded || _running is not null || _pending is not null || _conv.Messages.Count == 0) return null;
        var hint = ConversationFreshness.Evaluate(_conv, ModelMeta()?.Context, Now(), _input.Text, FreshnessChars());
        return hint is null || _freshnessDismissed.Contains(_conv.Id + ":" + hint.Reason) ? null : hint;
    }

    // measured again only when the conversation changed (a keystroke does not serialize the whole history)
    private long FreshnessChars()
    {
        var last = _conv.Messages.Count > 0 ? _conv.Messages[^1] : null;
        if (_freshnessSize.Id != _conv.Id || _freshnessSize.Count != _conv.Messages.Count || !ReferenceEquals(_freshnessSize.Last, last))
            _freshnessSize = (_conv.Id, _conv.Messages.Count, last, ConversationFreshness.HistoryChars(_conv));
        return _freshnessSize.Chars;
    }

    private void DismissFreshness()
    {
        if (_freshnessShown is { } shown) _freshnessDismissed.Add(_conv.Id + ":" + shown.Reason);
        UpdateFreshness();
        _input.Focus();
    }

    /// <summary>A new chat, with the same agent and model, whose composer holds a summary of this one and what was being typed.</summary>
    private void StartFromSummary()
    {
        if (_running is not null || _conv.Messages.Count == 0) return;
        var old = _conv;
        var seed = ConversationFreshness.Seed(old, _input.Text);
        Persist();
        _conv = NewConversation();
        _conv.AgentId = old.AgentId;
        _conv.ProviderId = old.ProviderId;
        _conv.Model = old.Model;
        _conv.Policy = old.Policy;
        SyncActiveConversation();
        _views.Clear();
        RenderAll();
        _input.Text = seed;
        _input.CaretIndex = seed.Length;
        _input.Focus();
    }
}
