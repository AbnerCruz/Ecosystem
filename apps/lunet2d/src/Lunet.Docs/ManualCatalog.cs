using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Lunet.Docs;

public sealed record ManualEntry(string Id, string Title, string Category, string[] Terms, string Since);
public sealed record ManualCatalog(ManualEntry[] Entries, Dictionary<string, string> Redirects)
{
    public static ManualCatalog Read(string json) =>
        JsonSerializer.Deserialize<ManualCatalog>(json, JsonSerializerOptions.Web)
        ?? throw new InvalidDataException("Catálogo do manual inválido.");

    public static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant();

    public static string Anchor(string text) =>
        string.Join('-', Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => string.Concat(word.Where(char.IsLetterOrDigit))).Where(word => word.Length > 0));
}
