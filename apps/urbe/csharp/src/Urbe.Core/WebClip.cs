using System.Globalization;
using Urbe.Core.Assistant;

namespace Urbe.Core;

/// <summary>
/// CST-235: a web page as a person reads or keeps it — its main text (the same extractor the agents' research uses,
/// <see cref="HtmlMainText"/>) with the title, and, for a note, where it came from and when. Built only when the
/// person asks (Modo leitura, Salvar página como nota); nothing is kept by itself.
/// </summary>
public static class WebClip
{
    public sealed record Page(string Title, string Markdown);

    public static Page Read(string url, string html)
    {
        var uri = new Uri(url);
        var title = HtmlMainText.Title(html) ?? uri.Host;
        return new(title, HtmlMainText.Extract(html, uri));
    }

    /// <summary>The note's text: the source and the day in properties, then the page's text.</summary>
    public static string Note(string url, Page page, DateTime when) =>
        "---\nsource: " + url + "\ncaptured: " + when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\n---\n\n" + page.Markdown.Trim() + "\n";
}
