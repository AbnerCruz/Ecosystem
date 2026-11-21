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

    /// <summary>Why <paramref name="tool"/> must not run for <paramref name="request"/>; null when it may.</summary>
    public static string? Refusal(string? request, string tool, bool nativeFlashcards)
    {
        if (!PluginTools.Contains(tool) || !nativeFlashcards || !AsksForFlashcards(request)) return null;
        if (AssistantTools.Fold(request ?? "").Contains("plugin", StringComparison.Ordinal)) return null;   // the person asked for a plugin
        return "Recusado: o pedido é de flashcards, e o Numa tem flashcards nativos. Use create_flashcards (uma chamada cria o conjunto inteiro na biblioteca de Estudo). " +
               "Não crie plugin, nota ou pasta para isso; um plugin só se a pessoa pedir um plugin.";
    }
}
