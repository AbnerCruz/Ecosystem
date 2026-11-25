using System.Reflection;
using Lunet.Runtime.Inspection;

namespace Lunet.Client.Shell;

/// <summary>
/// The Inspector of the game running in the shell (M6), without UI: the rows of the live <c>Game</c> (or the project's
/// own <c>Inspector&lt;T&gt;</c>), and edits written on the game's dispatcher, as the previous screens did. Like Godot's
/// Remote tree, the values are those of the running game, not of the code; a change lasts until the game stops.
/// </summary>
public sealed class GameInspection
{
    private readonly GameSession _session;
    private Game? _inspected;
    private IReadOnlyList<IInspector> _customs = [];

    public GameInspection(GameSession session) => _session = session;

    /// <summary>The game being inspected, or null when nothing runs.</summary>
    public Game? Game => _session.State is GameState.Running or GameState.Paused ? _session.Game?.Game : null;

    /// <summary>The rows of the running game; empty when nothing runs.</summary>
    public IReadOnlyList<InspectorItem> Items()
    {
        if (Game is not { } game) { _inspected = null; return []; }
        if (!ReferenceEquals(game, _inspected))
        {
            _inspected = game;
            _customs = ObjectInspector.FindCustomInspectors(game.GetType().Assembly);
        }
        try { return ObjectInspector.Build(game, _customs); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or TargetInvocationException or NullReferenceException)
        {
            return []; // the game changes its own data; the next refresh usually works
        }
    }

    /// <summary>A key of the row list: when it changes, the view rebuilds its rows instead of refreshing values.</summary>
    public static string Signature(IReadOnlyList<InspectorItem> items) =>
        string.Join('|', items.Select(i => $"{(int)i.Kind}:{(int)i.FieldKind}:{i.Label}:{i.Depth}:{i.IsEditable}"));

    /// <summary>The value of a row as the Inspector shows it.</summary>
    public static string Text(InspectorItem item)
    {
        if (item.ValueType is null || item.Getter is null) return "";
        try { return InspectorValue.Format(item.ValueType, item.Getter()); }
        catch (Exception ex) when (ex is InvalidOperationException or TargetInvocationException or NullReferenceException) { return "—"; }
    }

    /// <summary>Writes what the person typed. False, changing nothing, when the text is not valid for the type.</summary>
    public bool TryApplyText(InspectorItem item, string text)
    {
        if (!item.IsEditable || item.ValueType is not { } type) return false;
        if (!InspectorValue.TryParse(type, text, out var value)) return false;
        Apply(item, value);
        return true;
    }

    /// <summary>Writes a value (toggle, choice, slider) on the game's dispatcher; while paused, the frame is drawn again.</summary>
    public void Apply(InspectorItem item, object? value)
    {
        if (!item.IsEditable || item.ValueType is not { } type) return;
        object? coerced;
        try { coerced = InspectorValue.Coerce(type, value); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException) { return; }
        Post(item.Label, () => item.Setter!(coerced));
    }

    /// <summary>Runs a button of a custom Inspector in the game.</summary>
    public void Press(InspectorItem item)
    {
        if (item.Kind != InspectorItemKind.Button || item.Action is not { } action) return;
        Post(item.Label, action);
    }

    private void Post(string label, Action action)
    {
        if (Game is not { } game) return;
        game.Dispatcher.Post(() =>
        {
            try { action(); }
            catch (Exception ex) when (ex is InvalidCastException or ArgumentException or TargetInvocationException or FormatException or OverflowException)
            {
                game.Log.Error($"Inspector: não foi possível gravar {label}: {ex.Message}");
            }
        });
        // A running game applies it on its next frame; a paused one would wait, so draw it now.
        if (_session.State == GameState.Paused) Changed?.Invoke(_session.Redraw());
    }

    /// <summary>Raised after an edit while paused; true when a new frame was drawn (the view blits it).</summary>
    public event Action<bool>? Changed;
}
