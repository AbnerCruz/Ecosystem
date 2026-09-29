namespace Lunet.Editor;

/// <summary>Comandos do editor acionáveis por teclado físico (e reutilizados por menus e botões).</summary>
public enum EditorCommand
{
    None, Save, Run, Find, FindInProject, Undo, Redo, Format, DuplicateLine, DeleteLine, MoveLineUp, MoveLineDown,
    ToggleComment, Indent, Outdent, GoToDefinition, Documentation, Rename, QuickFix, Outline, SelectNextOccurrence,
    SelectAllOccurrences, ToggleFold, ToggleExplorer, ShowSymbolInfo,
}

/// <summary>Mapa de atalhos (estilo VS Code). As teclas são nomes independentes de plataforma: "S", "F5", "Enter", "Slash"…</summary>
public static class Shortcuts
{
    private static readonly Dictionary<(string Key, bool Ctrl, bool Shift, bool Alt), EditorCommand> Map = new()
    {
        [("S", true, false, false)] = EditorCommand.Save,
        [("F5", false, false, false)] = EditorCommand.Run,
        [("Enter", true, false, false)] = EditorCommand.Run,
        [("F", true, false, false)] = EditorCommand.Find,
        [("F", true, true, false)] = EditorCommand.FindInProject,
        [("Z", true, false, false)] = EditorCommand.Undo,
        [("Y", true, false, false)] = EditorCommand.Redo,
        [("Z", true, true, false)] = EditorCommand.Redo,
        [("L", true, false, true)] = EditorCommand.Format,
        [("F", false, true, true)] = EditorCommand.Format,
        [("D", true, false, false)] = EditorCommand.DuplicateLine,
        [("K", true, true, false)] = EditorCommand.DeleteLine,
        [("Up", false, false, true)] = EditorCommand.MoveLineUp,
        [("Down", false, false, true)] = EditorCommand.MoveLineDown,
        [("Slash", true, false, false)] = EditorCommand.ToggleComment,
        [("RightBracket", true, false, false)] = EditorCommand.Indent,
        [("LeftBracket", true, false, false)] = EditorCommand.Outdent,
        [("F12", false, false, false)] = EditorCommand.GoToDefinition,
        [("F1", false, false, false)] = EditorCommand.Documentation,
        [("F2", false, false, false)] = EditorCommand.Rename,
        [("Period", true, false, false)] = EditorCommand.QuickFix,
        [("O", true, true, false)] = EditorCommand.Outline,
        [("G", true, false, false)] = EditorCommand.SelectNextOccurrence,
        [("L", true, true, false)] = EditorCommand.SelectAllOccurrences,
        [("LeftBracket", true, true, false)] = EditorCommand.ToggleFold,
        [("B", true, false, false)] = EditorCommand.ToggleExplorer,
        [("I", true, false, false)] = EditorCommand.ShowSymbolInfo,
    };

    public static EditorCommand Resolve(string key, bool ctrl, bool shift, bool alt) =>
        Map.TryGetValue((key, ctrl, shift, alt), out var command) ? command : EditorCommand.None;

    /// <summary>Lista para a tela de ajuda de atalhos: descrição do comando e teclas.</summary>
    public static IReadOnlyList<(EditorCommand Command, string Keys)> All() =>
        Map.GroupBy(kv => kv.Value).Select(g => (g.Key, string.Join(" / ", g.Select(kv => Describe(kv.Key))))).OrderBy(x => x.Key.ToString(), StringComparer.Ordinal).ToList();

    private static string Describe((string Key, bool Ctrl, bool Shift, bool Alt) k)
    {
        var parts = new List<string>();
        if (k.Ctrl) parts.Add("Ctrl");
        if (k.Alt) parts.Add("Alt");
        if (k.Shift) parts.Add("Shift");
        parts.Add(k.Key switch { "Slash" => "/", "Period" => ".", "LeftBracket" => "[", "RightBracket" => "]", _ => k.Key });
        return string.Join('+', parts);
    }
}
