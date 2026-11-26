using System.Globalization;
using System.Text;

namespace Lunet.Client.Shell;

/// <summary>One game on the Home: folder name, stable identity (GameId) and last edit.</summary>
public sealed record HomeGame(string Name, string? GameId, DateTime LastEditedUtc);

/// <summary>
/// How the Home arranges the games (anexo A §4/§6.1): "Continuar" is the last edited game; then the pinned ones (by
/// GameId, so a rename keeps the pin); then the others, most recently edited first. A search matches names without
/// caring about case or accents and hides the Continuar card. Pure, so the rules are tested without a screen.
/// </summary>
public static class HomeOrder
{
    public sealed record Arrangement(HomeGame? Continue, IReadOnlyList<HomeGame> Pinned, IReadOnlyList<HomeGame> Others);

    public static Arrangement Arrange(IEnumerable<HomeGame> games, IReadOnlyCollection<string> pinnedIds, string? query)
    {
        var all = games.OrderByDescending(g => g.LastEditedUtc).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = Fold(query.Trim());
            all = all.Where(g => Fold(g.Name).Contains(q, StringComparison.Ordinal)).ToList();
        }
        bool Pinned(HomeGame g) => g.GameId is { } id && pinnedIds.Contains(id);
        var pinned = all.Where(Pinned).ToList();
        var others = all.Where(g => !Pinned(g)).ToList();
        var resume = string.IsNullOrWhiteSpace(query) && all.Count > 1 && all[0].LastEditedUtc > DateTime.MinValue ? all[0] : null;
        return new Arrangement(resume, pinned, others);
    }

    /// <summary>Lower case without diacritics ("Fase Água" → "fase agua").</summary>
    public static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(char.ToLowerInvariant(c));
        return builder.ToString();
    }

    /// <summary>Pins or unpins a GameId, keeping the order of the others; returns the new list.</summary>
    public static List<string> Toggle(IReadOnlyList<string> pinned, string gameId) =>
        pinned.Contains(gameId) ? pinned.Where(p => p != gameId).ToList() : [.. pinned, gameId];
}
