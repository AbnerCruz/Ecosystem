using System.Text.Json;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The pure rules of the 1.8.4-beta note editor, ported from the original code and checked against it
/// by tools/editor-oracle.mjs (LegacyEditorToolsOracleTests): the Markdown toolbar of the source mode
/// (app.js insertAtCursor and the .mdBtn handlers), the wikilink completion context
/// (getWikiContext / commitWikiSuggestion), the code tools (v21IndentSelection / v21FormatCurrent),
/// the status line (updateStats) and the "/" block menu of the Visual editor (visual-tools.js).
/// </summary>
public static partial class LegacyEditorTools
{
    public sealed record TextEdit(string Text, int SelectionStart, int SelectionEnd);

    /// <summary>One button of #mdToolbar (index.html), in order. Separators are not buttons.</summary>
    public sealed record ToolbarButton(string Id, string Title, string? Md = null, string? Wrap = null, string? Special = null, string Icon = "");

    public static IReadOnlyList<ToolbarButton> Toolbar { get; } =
    [
        new("h1", "Título 1", Md: "# "),
        new("h2", "Título 2", Md: "## "),
        new("h3", "Título 3", Md: "### "),
        new("bold", "Negrito", Wrap: "**"),
        new("italic", "Itálico", Wrap: "_"),
        new("code", "Código", Wrap: "`", Icon: "code"),
        new("list", "Lista", Md: "- ", Icon: "list"),
        new("task", "Tarefa", Md: "- [ ] ", Icon: "task"),
        new("quote", "Citação", Md: "> ", Icon: "quote"),
        new("noteLink", "Vincular nota", Special: "noteLink", Icon: "wikilink"),
        new("link", "Link externo", Wrap: "[]()", Special: "link", Icon: "link"),
        new("rule", "Linha horizontal", Md: "\n---\n", Icon: "rule"),
    ];

    /// <summary>Separators of the toolbar fall after these button ids.</summary>
    public static readonly string[] SeparatorAfter = ["h3", "code", "quote"];

    /// <summary>insertAtCursor(prefix, suffix): setRangeText(prefix + selection + suffix, start, end, "end").</summary>
    public static TextEdit InsertAtCursor(string text, int selectionStart, int selectionEnd, string prefix, string suffix = "")
    {
        var a = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var b = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        var replacement = prefix + text[a..b] + suffix;
        var next = text[..a] + replacement + text[b..];
        var caret = a + replacement.Length;
        return new TextEdit(next, caret, caret);
    }

    /// <summary>The Fonte-mode click handler of a toolbar button (noteLink is resolved by the caller: see <see cref="NoteLink"/>).</summary>
    public static TextEdit? ApplySource(ToolbarButton button, string text, int selectionStart, int selectionEnd)
    {
        if (button.Special == "link") return InsertAtCursor(text, selectionStart, selectionEnd, "[", "](url)");
        if (button.Special == "noteLink") return null;
        if (button.Wrap is { } wrap) return InsertAtCursor(text, selectionStart, selectionEnd, wrap, wrap);
        return InsertAtCursor(text, selectionStart, selectionEnd, button.Md ?? "");
    }

    /// <summary>After choosing the note to link: insertAtCursor("[[" + target + "]]") at the saved selection.</summary>
    public static TextEdit NoteLink(string text, int selectionStart, int selectionEnd, string target) =>
        InsertAtCursor(text, selectionStart, selectionEnd, "[[" + target + "]]");

    // ------------------------------------------------------------------ wikilink completion

    public sealed record WikiContext(int Start, string Query);

    /// <summary>getWikiContext(): the last "[[" before the caret, unless "]]" or a newline follows it.</summary>
    public static WikiContext? FindWikiContext(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var before = text[..caret];
        var open = before.LastIndexOf("[[", StringComparison.Ordinal);
        if (open < 0) return null;
        var afterOpen = before[(open + 2)..];
        if (afterOpen.Contains("]]", StringComparison.Ordinal)) return null;
        if (afterOpen.Contains('\n')) return null;
        return new WikiContext(open, afterOpen);
    }

    /// <summary>commitWikiSuggestion(): replaces [start, caret) with "[[name]]", caret after it.</summary>
    public static TextEdit? CommitWiki(string text, int caret, string name)
    {
        var ctx = FindWikiContext(text, caret);
        if (ctx is null) return null;
        caret = Math.Clamp(caret, 0, text.Length);
        var token = "[[" + name + "]]";
        return new TextEdit(text[..ctx.Start] + token + text[caret..], ctx.Start + token.Length, ctx.Start + token.Length);
    }

    // ------------------------------------------------------------------ code tools / status

    /// <summary>v21IndentSelection(): two spaces before every line of the selection; selection kept over the result.</summary>
    public static TextEdit IndentSelection(string text, int selectionStart, int selectionEnd)
    {
        var s = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        var e = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        var selected = string.Join('\n', text[s..e].Split('\n').Select(l => "  " + l));
        return new TextEdit(text[..s] + selected + text[e..], s, s + selected.Length);
    }

    /// <summary>v21FormatCurrent(): JSON re-indented with 2 spaces, CSS by the original regex chain. Message is the toast.</summary>
    public static (string? Text, string Message) Format(string text, string ext)
    {
        try
        {
            if (ext == "json")
                return (LegacyCustomize.Stringify(LegacyCustomize.ParseJs(text), 2), "JSON formatado.");
            if (ext == "css")
            {
                var css = CssOpen().Replace(text, " {\n  ");
                css = CssSemi().Replace(css, ";\n  ");
                css = CssClose().Replace(css, "\n}\n");
                css = CssBlank().Replace(css, "\n");
                return (css, "");
            }
            return (null, "Formatação automática disponível para JSON e CSS nesta versão.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException or InvalidOperationException)
        {
            return (null, "Não foi possível formatar: sintaxe inválida.");
        }
    }

    // JS \s = WhiteSpace + LineTerminator, which differs from .NET \s (U+0085, U+FEFF).
    private const string Ws = @"[\t\n\v\f\r    -     　﻿]";
    [GeneratedRegex(Ws + @"*\{" + Ws + "*")] private static partial Regex CssOpen();
    [GeneratedRegex(";" + Ws + "*")] private static partial Regex CssSemi();
    [GeneratedRegex(Ws + @"*\}" + Ws + "*")] private static partial Regex CssClose();
    [GeneratedRegex(@"\n" + Ws + @"+\n")] private static partial Regex CssBlank();
    [GeneratedRegex(Ws + "+")] private static partial Regex WsRun();

    /// <summary>updateStats(): "MD · {lines} linhas · {words} palavras" (kind in capitals).</summary>
    public static string Stats(string text, string kind)
    {
        var trimmed = LegacyCustomize.JsTrim(text);
        var words = trimmed.Length == 0 ? 0 : WsRun().Split(trimmed).Length;
        var lines = text.Split('\n').Length;
        return kind.ToUpperInvariant() + " · " + lines + " linhas · " + words + " palavras";
    }

    /// <summary>v21RefreshEditorChrome, when a note opens: "MD · {lines} linhas" (the words come with the first edit,
    /// updateStats).</summary>
    public static string ChromeStats(string text, string kind) =>
        kind.ToUpperInvariant() + " · " + text.Split('\n').Length + " linhas";

    public static (int Lines, int Words) Count(string text)
    {
        var trimmed = LegacyCustomize.JsTrim(text);
        return (text.Split('\n').Length, trimmed.Length == 0 ? 0 : WsRun().Split(trimmed).Length);
    }

    // ------------------------------------------------------------------ "/" block menu (visual-tools.js)

    public sealed record SlashItem(string Act, string Label, string Hint, string Keys, string? Icon = null, string? Text = null);

    public static IReadOnlyList<SlashItem> SlashItems { get; } =
    [
        new("h1", "Título 1", "Título grande", "titulo heading h1 grande", Text: "H1"),
        new("h2", "Título 2", "Seção", "titulo heading h2 secao", Text: "H2"),
        new("h3", "Título 3", "Subseção", "titulo heading h3 subsecao", Text: "H3"),
        new("list", "Lista", "Itens com marcador", "lista bullet itens", "list"),
        new("task", "Tarefa", "Caixa de seleção", "tarefa todo checkbox check", "task"),
        new("quote", "Citação", "Destaque de trecho", "citacao quote", "quote"),
        new("rule", "Divisória", "Linha horizontal", "divisoria linha separador hr", "rule"),
        new("noteLink", "Link para nota", "Cria uma rua na cidade", "link nota wiki rua", "wikilink"),
        new("link", "Link externo", "Endereço da web", "link url web externo", "link"),
    ];

    /// <summary>fold(): NFD, strip combining marks U+0300..U+036F, lower case.</summary>
    public static string Fold(string s)
    {
        var d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var c in d)
            if (c is < '̀' or > 'ͯ') sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }

    public static IReadOnlyList<SlashItem> FilterSlash(string query)
    {
        var q = Fold(query);
        return SlashItems.Where(x => q.Length == 0 || Fold(x.Label + " " + x.Keys).Contains(q, StringComparison.Ordinal)).ToArray();
    }

    /// <summary>The items math/editor.js adds to the menu after visual-tools.js (VT.addItem, in that order): Fórmula (∑) and
    /// Fórmula em destaque (∫).</summary>
    public static IReadOnlyList<SlashItem> MathSlashItems { get; } =
    [
        new("formula", "Fórmula", "LaTeX no meio do texto", "formula latex matematica equacao math", Text: "∑"),
        new("formulaBlock", "Fórmula em destaque", "Equação centralizada", "formula destaque bloco equacao latex math display", Text: "∫"),
    ];

    /// <summary>The menu the app shows: the visual-tools.js items and then the added ones, with the same filter.</summary>
    public static IReadOnlyList<SlashItem> FilterSlashWithAddons(string query)
    {
        var q = Fold(query);
        return SlashItems.Concat(MathSlashItems).Where(x => q.Length == 0 || Fold(x.Label + " " + x.Keys).Contains(q, StringComparison.Ordinal)).ToArray();
    }

    /// <summary>slashQuery(): the block text (U+200B removed) must be "/" + up to 24 characters without space or "/".</summary>
    public static string? SlashQuery(string blockText)
    {
        var t = blockText.Replace("​", "", StringComparison.Ordinal);
        var m = SlashRe().Match(t);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^/([^\t\n\v\f\r    -     　﻿/]{0,24})$")]
    private static partial Regex SlashRe();
}
