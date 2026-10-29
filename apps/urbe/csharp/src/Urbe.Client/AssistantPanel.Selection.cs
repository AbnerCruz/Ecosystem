using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Urbe.Client;

/// <summary>
/// Text the person selects in the chat survives the updates (FeaturesCheck, "Atualização visual da conversa de IA",
/// and CST-008). Nothing waits for a selection in general: only a view that is about to leave the log while its
/// selection is still being made (moved in the last <see cref="SelectionSettleMs"/>, or a finger down in the log)
/// holds that update back for a moment, and a view rebuilt with text selected passes the selection to its new text.
/// </summary>
public sealed partial class AssistantPanel
{
    private const int SelectionSettleMs = 1500;
    private long _selectionMovedAt = long.MinValue / 2;
    private bool _pointerInLog, _carrying;
    private IDisposable? _selectionWatch;
    private DispatcherTimer? _selectionWait;

    private void WatchSelections()
    {
        _log.AddHandler(PointerPressedEvent, (_, _) => _pointerInLog = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        _log.AddHandler(PointerReleasedEvent, (_, _) => _pointerInLog = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        _log.AddHandler(PointerCaptureLostEvent, (_, _) => _pointerInLog = false, RoutingStrategies.Bubble, handledEventsToo: true);
        _log.AttachedToVisualTree += (_, _) =>
        {
            void Moved(SelectableTextBlock block)
            {
                if (!_carrying && _log.IsVisualAncestorOf(block)) _selectionMovedAt = Environment.TickCount64;
            }
            _selectionWatch ??= new CompositeWatch(
                SelectableTextBlock.SelectionStartProperty.Changed.AddClassHandler<SelectableTextBlock>((b, _) => Moved(b)),
                SelectableTextBlock.SelectionEndProperty.Changed.AddClassHandler<SelectableTextBlock>((b, _) => Moved(b)));
        };
        _log.DetachedFromVisualTree += (_, _) =>
        {
            _selectionWait?.Stop();
            _selectionWatch?.Dispose();
            _selectionWatch = null;
        };
    }

    private sealed class CompositeWatch(params IDisposable[] parts) : IDisposable
    {
        public void Dispose() { foreach (var p in parts) p.Dispose(); }
    }

    private bool SelectionActive() => HasSelection(_log);

    private static bool HasSelection(Visual root) => Selectables(root).Any(t => t.SelectionStart != t.SelectionEnd) ||
        root.GetVisualDescendants().OfType<TextBox>().Any(box => box.SelectionStart != box.SelectionEnd);

    private static IEnumerable<SelectableTextBlock> Selectables(Visual root) =>
        (root is SelectableTextBlock self ? [self] : Enumerable.Empty<SelectableTextBlock>())
            .Concat(root.GetLogicalDescendants().OfType<SelectableTextBlock>());

    /// <summary>The selection in <paramref name="view"/> is still being made: replacing it now would pull it from under the finger.</summary>
    private bool SelectionBusy(Visual view) => HasSelection(view) &&
        (_pointerInLog || Environment.TickCount64 - _selectionMovedAt < SelectionSettleMs);

    /// <summary>Message views are rebuilt (a full render, a run that ended), except those holding a selection.</summary>
    private void ForgetViewsKeepingSelections()
    {
        foreach (var (message, view) in _views.ToList())
            if (!HasSelection(view)) _views.Remove(message);
    }

    /// <summary>Renders again once the selection in the way is let go.</summary>
    private void WaitForSelection()
    {
        if (_selectionWait is null)
        {
            _selectionWait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _selectionWait.Tick += (_, _) =>
            {
                _selectionWait.Stop();
                bool near = _followChatTail && NearEnd();
                RenderLog();
                NotifyCompactChanged();
                if (near) ScrollEnd();
            };
        }
        _selectionWait.Start();
    }

    private static string TextOf(SelectableTextBlock block) =>
        block.Text ?? string.Concat(block.Inlines?.Select(i => i switch { Run r => r.Text, LineBreak => "\n", _ => "" }) ?? []);

    private sealed record HeldSelection(string Text, int Start, int End);

    private static List<HeldSelection> SelectionsIn(IEnumerable<Control> views) =>
        views.SelectMany(v => Selectables(v)).Where(t => t.SelectionStart != t.SelectionEnd)
            .Select(t => new HeldSelection(TextOf(t), Math.Min(t.SelectionStart, t.SelectionEnd), Math.Max(t.SelectionStart, t.SelectionEnd)))
            .ToList();

    /// <summary>
    /// Each selection goes to the first new text that still has the same words up to its end (a message that grew,
    /// was rebuilt, or became the final answer).
    /// </summary>
    private void CarrySelections(List<HeldSelection> held, IEnumerable<Control> arrived)
    {
        if (held.Count == 0) return;
        var candidates = arrived.SelectMany(v => Selectables(v)).Where(t => t.SelectionStart == t.SelectionEnd).ToList();
        _carrying = true;
        try
        {
            foreach (var sel in held)
            {
                var prefix = sel.Text[..Math.Min(sel.End, sel.Text.Length)];
                var target = candidates.FirstOrDefault(t => TextOf(t).StartsWith(prefix, StringComparison.Ordinal));
                if (target is null) continue;
                candidates.Remove(target);
                target.SelectionStart = sel.Start;
                target.SelectionEnd = sel.End;
            }
        }
        finally { _carrying = false; }
    }
}
