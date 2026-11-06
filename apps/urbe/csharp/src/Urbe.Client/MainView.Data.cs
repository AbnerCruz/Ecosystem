using System.Text;

namespace Urbe.Client;

public sealed partial class MainView
{
    /// <summary>The biggest data file a ```dados block reads (a CSV of hundreds of thousands of rows fits).</summary>
    public const long MaxDataBytes = 64L * 1024 * 1024;

    /// <summary>
    /// CST-207: the text of a ```dados source — a note of the vault, else a file (CSV, JSON) read from the vault. A file
    /// that is not valid UTF-8 is read as Latin-1 (spreadsheets exported in Brazil often are).
    /// </summary>
    public async Task<string?> ReadDataSourceAsync(string path)
    {
        var clean = path.Replace('\\', '/').Trim().TrimStart('/');
        if (clean.Length == 0 || clean.Split('/').Any(p => p == "..")) return null;
        // a note is read as the editor has it; a data file from its bytes (the text store may hold it decoded as UTF-8)
        var isNote = clean.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || clean.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);
        if (isNote && _store?.Get(clean) is { } note) return note.Content;
        if (_vault is null) return _store?.Get(clean)?.Content;
        try
        {
            var read = await _vault.ReadFileAsync(clean, MaxDataBytes);
            if (read?.Bytes is not { } bytes) return null;
            return Decode(bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { return null; }
    }

    /// <summary>The ```dados sources of a publication's notes, read before an export (the export itself is synchronous).</summary>
    private async Task<Func<string, string?>> PublicationDataAsync(Urbe.Core.PublicationProject project, Urbe.Core.PublicationSourceCatalog catalog)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var piece in project.Sections)
        {
            if (catalog.Read(piece.Source) is not { } source) continue;
            foreach (var path in Urbe.Core.Data.DataBlock.Sources(source.Markdown))
            {
                if (map.ContainsKey(path)) continue;
                map[path] = await ReadDataSourceAsync(path);
                if (map[path] is null && !Path.HasExtension(path)) map[path + ".md"] = await ReadDataSourceAsync(path + ".md");
            }
        }
        return p => map.GetValueOrDefault(p);
    }

    internal static string Decode(byte[] bytes)
    {
        try { return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
    }
}
