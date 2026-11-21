namespace Urbe.Core.Assistant;

/// <summary>
/// #1005 (beta.60: "dois flashcards com a ferramenta nativa" became a plugin, and the review called it native): a
/// deterministic check of the request against the tool about to run. When the person asked for flashcards and the app
/// has them natively, a plugin tool is refused with the tool to use — unless the person asked for a plugin. The result
/// class the person asked for is never swapped silently.
/// </summary>
public static class AssistantIntent
{
    private static readonly string[] Flashcards =
        ["flashcard", "flash card", "cartoes de memoriz", "cartao de memoriz", "cards de estudo", "card de estudo", "cartoes de estudo", "cartao de estudo", "cartoes para revis", "anki"];

    private static readonly HashSet<string> PluginTools = new(StringComparer.Ordinal) { "write_plugin", "plugin_guide" };

    /// <summary>True when the request asks for flashcards (any of their usual names).</summary>
    public static bool AsksForFlashcards(string? request)
    {
        var text = AssistantTools.Fold(request ?? "");
        return Flashcards.Any(f => text.Contains(f, StringComparison.Ordinal));
    }

    /// <summary>
    /// What the app does natively, by the words a request uses for it, and the tool that does it: a plugin is never the
    /// answer to these unless the person asked for a plugin (#1005: the same mistake as beta.60, for every native kind).
    /// </summary>
    private static readonly (string What, string[] Words, string Tool)[] Native =
    [
        ("flashcards", Flashcards, "create_flashcards"),
        // only words that name the object asked for; loose ones ("imagem", "livro") also appear in requests for plugins
        ("vídeo", ["faca um video", "faz um video", "crie um video", "cria um video", "gere um video", "gera um video", "monte um video", "motion graphic"], "create_video"),
        ("grafo", ["grafo", "mapa mental", "mapa de conceitos"], "edit_graph"),
    ];

    /// <summary>Why <paramref name="tool"/> must not run for <paramref name="request"/>; null when it may.</summary>
    /// <param name="available">The tools this run has (a native kind counts only when its tool is there).</param>
    public static string? Refusal(string? request, string tool, IReadOnlySet<string> available)
    {
        if (!PluginTools.Contains(tool)) return null;
        var text = AssistantTools.Fold(request ?? "");
        if (text.Contains("plugin", StringComparison.Ordinal)) return null;   // the person asked for a plugin
        foreach (var (what, words, native) in Native)
            if (available.Contains(native) && words.Any(w => text.Contains(AssistantTools.Fold(w), StringComparison.Ordinal)))
                return "Recusado: o pedido é de " + what + ", e o Numa faz isso nativamente. Use " + native +
                       ". Não crie plugin, nota ou pasta para isso; um plugin só se a pessoa pedir um plugin.";
        return null;
    }

    /// <summary>The flashcards-only form (kept for callers that know only that).</summary>
    public static string? Refusal(string? request, string tool, bool nativeFlashcards) =>
        Refusal(request, tool, nativeFlashcards ? new HashSet<string> { "create_flashcards" } : new HashSet<string>());
}
