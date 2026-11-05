using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Urbe.Core;

/// <summary>A text file decoded for reading, with what the reader must say about how it was read.</summary>
public sealed record DecodedText(string Text, string? Notice);

/// <summary>
/// The plain text family's bytes as text (CST-238): UTF-8 (with or without BOM), UTF-16 with BOM, and a file that is
/// not UTF-8 read as Windows-1252, saying so. A binary (NUL bytes, many control bytes) is not text: null.
/// </summary>
public static class TextDecoding
{
    /// <summary>True when the bytes look like text: no NUL, almost no control bytes (a UTF-16 BOM counts as text).</summary>
    public static bool LooksLikeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return true;
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]) || bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) return true;
        var sample = bytes[..Math.Min(bytes.Length, 64 * 1024)];
        int control = 0;
        foreach (var b in sample)
        {
            if (b == 0) return false;
            if (b < 0x20 && b is not ((byte)'\t' or (byte)'\n' or (byte)'\r' or 0x0C or 0x1B)) control++;
        }
        return control * 100 <= sample.Length;
    }

    public static DecodedText? Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])) return Utf16(bytes, Encoding.Unicode);
        if (bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) return Utf16(bytes, Encoding.BigEndianUnicode);
        if (!LooksLikeText(bytes) || Array.IndexOf(bytes, (byte)0) >= 0) return null;
        try { return new(new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'), null); }
        catch (DecoderFallbackException) { }
        return new(Windows1252(bytes), "O arquivo não está em UTF-8; foi lido como Windows-1252 (acentos podem aparecer trocados).");
    }

    private static DecodedText? Utf16(byte[] bytes, Encoding encoding)
    {
        try { return new(encoding.GetString(bytes, 2, bytes.Length - 2 & ~1), null); }
        catch (DecoderFallbackException) { return null; }
    }

    // 0x80..0x9F of Windows-1252 (the rest is Latin-1); an unassigned byte stays its Latin-1 control
    private const string Cp1252High = "€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f" +
                                      "\u0090‘’“”•–—˜™š›œ\u009džŸ";

    public static char Windows1252(byte b) => b is >= 0x80 and <= 0x9F ? Cp1252High[b - 0x80] : (char)b;

    public static string Windows1252(byte[] bytes) => string.Create(bytes.Length, bytes, (span, source) =>
    {
        for (int i = 0; i < source.Length; i++) span[i] = Windows1252(source[i]);
    });
}

/// <summary>
/// An RTF document as plain text (CST-238): paragraphs, tabs, \'hh and \u escapes, fields' results; headers, fonts,
/// pictures and objects are skipped. The formatting is not kept, and the reader says so.
/// </summary>
public static class RtfText
{
    public const string Notice = "A formatação do RTF não é mostrada: só o texto (sem fontes, cores, tabelas ou imagens).";

    private static readonly HashSet<string> SkippedDestinations = new(StringComparer.Ordinal)
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "header", "headerl", "headerr", "headerf", "footer", "footerl", "footerr",
        "footerf", "fldinst", "themedata", "colorschememapping", "datastore", "latentstyles", "xmlnstbl", "listtable", "listoverridetable",
        "rsidtbl", "generator", "mmathPr", "filetbl", "revtbl", "pgdsctbl", "bkmkstart", "bkmkend", "nonshppict", "shppict", "xe", "tc"
    };

    /// <summary>The text of an RTF file; throws <see cref="InvalidDataException"/> when the bytes are not RTF.</summary>
    public static string Convert(ReadOnlySpan<byte> rtf)
    {
        if (!rtf.StartsWith("{\\rtf"u8)) throw new InvalidDataException("O arquivo não começa como um documento RTF.");
        var output = new StringBuilder(rtf.Length / 2);
        // the group stack, iterative so a deeply nested file can't overflow the call stack
        var skip = new Stack<(bool Skip, int Uc)>();
        bool skipping = false;
        int uc = 1, pendingSkip = 0;
        int i = 0;
        while (i < rtf.Length)
        {
            byte c = rtf[i];
            if (c == '{')
            {
                skip.Push((skipping, uc));
                i++;
                // "{\*\dest …}": an optional destination the reader doesn't know is skipped
                if (i + 1 < rtf.Length && rtf[i] == '\\' && rtf[i + 1] == '*') skipping = true;
                continue;
            }
            if (c == '}')
            {
                if (skip.Count > 0) (skipping, uc) = skip.Pop();
                i++;
                continue;
            }
            if (c == '\\')
            {
                i++;
                if (i >= rtf.Length) break;
                byte n = rtf[i];
                if (n is (byte)'\\' or (byte)'{' or (byte)'}')
                {
                    i++;
                    Emit((char)n);
                    continue;
                }
                if (n == '\'')
                {
                    if (i + 2 < rtf.Length && byte.TryParse(Encoding.ASCII.GetString(rtf.Slice(i + 1, 2)), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        Emit(TextDecoding.Windows1252(code));
                    i += 3;
                    continue;
                }
                if (n == '~') { Emit(' '); i++; continue; }
                if (n == '_') { Emit('‑'); i++; continue; }
                if (n is (byte)'-' or (byte)'*' or (byte)':' or (byte)'|') { i++; continue; }
                if (n is (byte)'\n' or (byte)'\r') { Emit('\n', raw: true); i++; continue; }
                if (!char.IsAsciiLetter((char)n)) { i++; continue; }
                int start = i;
                while (i < rtf.Length && char.IsAsciiLetter((char)rtf[i])) i++;
                string word = Encoding.ASCII.GetString(rtf[start..i]);
                int? parameter = null;
                int numberStart = i;
                if (i < rtf.Length && (rtf[i] == '-' || char.IsAsciiDigit((char)rtf[i])))
                {
                    i++;
                    while (i < rtf.Length && char.IsAsciiDigit((char)rtf[i]) && i - numberStart < 11) i++;
                    if (int.TryParse(Encoding.ASCII.GetString(rtf[numberStart..i]), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)) parameter = value;
                }
                if (i < rtf.Length && rtf[i] == ' ') i++;
                Word(word, parameter, ref i, rtf);
                continue;
            }
            if (c is (byte)'\r' or (byte)'\n') { i++; continue; }
            Emit((char)c);
            i++;
        }
        return output.ToString();

        void Emit(char ch, bool raw = false)
        {
            if (skipping) return;
            // the ANSI characters after a \u escape stand in for it in old readers
            if (pendingSkip > 0 && !raw) { pendingSkip--; return; }
            output.Append(ch);
        }

        void Word(string word, int? parameter, ref int at, ReadOnlySpan<byte> source)
        {
            if (word == "bin" && parameter is int length and > 0) { at = (int)Math.Min(source.Length, (long)at + length); return; }
            if (SkippedDestinations.Contains(word)) { skipping = true; return; }
            if (skipping) return;
            switch (word)
            {
                case "par" or "line" or "row" or "sect" or "page": pendingSkip = 0; output.Append('\n'); break;
                case "tab" or "cell": output.Append('\t'); break;
                case "emdash": output.Append('—'); break;
                case "endash": output.Append('–'); break;
                case "bullet": output.Append('•'); break;
                case "lquote": output.Append('‘'); break;
                case "rquote": output.Append('’'); break;
                case "ldblquote": output.Append('“'); break;
                case "rdblquote": output.Append('”'); break;
                case "uc": uc = Math.Clamp(parameter ?? 1, 0, 8); break;
                case "u" when parameter is int u:
                    output.Append((char)(u < 0 ? u + 65536 : u));
                    pendingSkip = uc;
                    break;
            }
        }
    }
}

/// <summary>
/// A CSV/TSV file as a table without parsing it all (CST-238): the start of every row is indexed once (quotes
/// respected, a newline inside quotes stays in the cell), and a row's cells are parsed only when it is shown.
/// </summary>
public sealed class CsvTable
{
    private readonly string _text;
    private readonly List<int> _rowStarts = [];
    public char Delimiter { get; }
    public int RowCount => _rowStarts.Count;

    public CsvTable(string text, char? delimiter = null)
    {
        _text = text;
        Delimiter = delimiter ?? Guess(text);
        bool quoted = false;
        int rowStart = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') quoted = !quoted;
            else if (c == '\n' && !quoted)
            {
                if (i > rowStart && !(i - rowStart == 1 && text[rowStart] == '\r')) _rowStarts.Add(rowStart);
                rowStart = i + 1;
            }
        }
        if (rowStart < text.Length && text.AsSpan(rowStart).Trim().Length > 0) _rowStarts.Add(rowStart);
    }

    /// <summary>The delimiter of the first line: tab, ';' (Brazilian spreadsheets) or ','.</summary>
    public static char Guess(string text)
    {
        var first = text.AsSpan(0, Math.Min(text.Length, 64 * 1024));
        int nl = first.IndexOf('\n');
        if (nl >= 0) first = first[..nl];
        int tabs = first.Count('\t'), semis = first.Count(';'), commas = first.Count(',');
        return tabs > 0 && tabs >= semis && tabs >= commas ? '\t' : semis > commas ? ';' : ',';
    }

    /// <summary>The offset in the text where row <paramref name="index"/> starts.</summary>
    public int RowStart(int index) => _rowStarts[index];

    /// <summary>The row containing a text offset (a search match).</summary>
    public int RowAt(int offset)
    {
        int found = _rowStarts.BinarySearch(offset);
        return found >= 0 ? found : Math.Max(0, ~found - 1);
    }

    public string[] Row(int index)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = _rowStarts[index]; i < _text.Length; i++)
        {
            char c = _text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < _text.Length && _text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quoted = false;
                }
                else cell.Append(c);
                continue;
            }
            if (c == '"') { quoted = true; continue; }
            if (c == Delimiter) { cells.Add(cell.ToString()); cell.Clear(); continue; }
            if (c == '\n') break;
            if (c == '\r' && (i + 1 == _text.Length || _text[i + 1] == '\n')) continue;
            cell.Append(c);
        }
        cells.Add(cell.ToString());
        return cells.ToArray();
    }
}

/// <summary>A JSON file formatted for reading; an invalid one is shown as it is, with the error (CST-238).</summary>
public static class JsonPretty
{
    public static DecodedText Format(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 512 });
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 512 }))
                document.WriteTo(writer);
            bool comments = text.Contains("//", StringComparison.Ordinal) || text.Contains("/*", StringComparison.Ordinal);
            return new(Encoding.UTF8.GetString(output.ToArray()), comments ? "JSON formatado para leitura; comentários do arquivo não aparecem nesta vista." : "JSON formatado para leitura.");
        }
        catch (JsonException ex)
        {
            return new(text, "JSON inválido" + (ex.LineNumber is long line ? " (linha " + (line + 1) + ")" : "") + ": mostrado como texto.");
        }
    }
}

/// <summary>
/// Search inside a text without regard to case or accents ("ação" finds "ACAO"): each character is folded on its own,
/// so a match's offset and length are the original text's.
/// </summary>
public static class TextSearch
{
    /// <summary>Where the search stops counting; the reader says "10000+".</summary>
    public const int MaxMatches = 10000;

    public static string Fold(string text) => string.Create(text.Length, text, (span, source) =>
    {
        for (int i = 0; i < source.Length; i++) span[i] = Fold(source[i]);
    });

    public static char Fold(char c)
    {
        c = char.ToLowerInvariant(c);
        return c switch
        {
            'à' or 'á' or 'â' or 'ã' or 'ä' or 'å' => 'a',
            'ç' => 'c',
            'è' or 'é' or 'ê' or 'ë' => 'e',
            'ì' or 'í' or 'î' or 'ï' => 'i',
            'ñ' => 'n',
            'ò' or 'ó' or 'ô' or 'õ' or 'ö' => 'o',
            'ù' or 'ú' or 'û' or 'ü' => 'u',
            'ý' or 'ÿ' => 'y',
            _ => c
        };
    }

    /// <summary>The offsets of <paramref name="query"/> in an already folded text (<see cref="Fold(string)"/>).</summary>
    public static IReadOnlyList<int> FindAll(string folded, string query, CancellationToken cancellationToken = default)
    {
        var results = new List<int>();
        if (string.IsNullOrEmpty(query)) return results;
        var needle = Fold(query);
        int at = 0;
        while (results.Count < MaxMatches && (at = folded.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            if ((results.Count & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            results.Add(at);
            at += needle.Length;
        }
        return results;
    }
}
