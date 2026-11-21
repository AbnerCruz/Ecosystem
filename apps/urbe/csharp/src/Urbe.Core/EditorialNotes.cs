using System.Security.Cryptography;
using System.Text;

namespace Urbe.Core;

/// <summary>Where an adaptation came from, as its note's properties say.</summary>
public sealed record EditorialLink(string OriginId, string Kind, string OriginPrint);

/// <summary>
/// CST-155: a summary, script, speech, translation or adapted text of a source is a real note of the vault, never text
/// hidden in a publication. Its properties name the source (a link a person reads in any editor, the stable id the app
/// follows across renames), the kind of adaptation and a print of the source's text when it was adapted or last
/// reviewed, so a change to the source shows up without copying anything.
/// </summary>
public static class EditorialNotes
{
    public static readonly IReadOnlyList<string> Kinds = ["Resumo", "Roteiro", "Fala", "Tradução", "Adaptação"];

    /// <summary>A print of the source's text (its body; properties and line endings do not count).</summary>
    public static string Print(string markdown) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(MarkdownEngine.SplitFrontmatter(markdown).Body.Replace("\r", "").Trim())))[..16].ToLowerInvariant();

    /// <summary>The new note: its link to the source, then the source's text to start from.</summary>
    public static string Create(string originId, string originPath, string originMarkdown, string kind)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(originPath);
        var body = MarkdownEngine.SplitFrontmatter(originMarkdown).Body.Trim();
        return NoteProperties.Parse(body + "\n")
            .Set("adaptacao_de", PropertyKind.Text, "[[" + name + "]]")
            .Set("adaptacao_id", PropertyKind.Text, originId)
            .Set("adaptacao_tipo", PropertyKind.Text, kind)
            .Set("adaptacao_origem", PropertyKind.Text, Print(originMarkdown))
            .Render();
    }

    public static EditorialLink? Read(string markdown)
    {
        var p = NoteProperties.Parse(markdown);
        string? Get(string key) => p.Items.FirstOrDefault(i => i.Key == key)?.Value is { Length: > 0 } v ? v : null;
        return Get("adaptacao_id") is { } id ? new(id, Get("adaptacao_tipo") ?? "Adaptação", Get("adaptacao_origem") ?? "") : null;
    }

    /// <summary>The source's text changed since the adaptation was made or last reviewed.</summary>
    public static bool IsBehind(EditorialLink link, string originMarkdown) => link.OriginPrint != Print(originMarkdown);

    /// <summary>The adaptation, marked as reviewed against the source as it is now.</summary>
    public static string Reviewed(string markdown, string originMarkdown) =>
        NoteProperties.Parse(markdown).Set("adaptacao_origem", PropertyKind.Text, Print(originMarkdown)).Render();
}
