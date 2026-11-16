using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Urbe.Core;

/// <summary>
/// CST-191: every output is opened and checked before it is saved, so a broken file never reaches the person as if it
/// were done. Returns what is wrong, or null when the file opens as its format.
/// </summary>
public static class PublicationOutputCheck
{
    private static readonly Dictionary<string, string[]> Required = new(StringComparer.OrdinalIgnoreCase)
    {
        ["docx"] = ["[Content_Types].xml", "word/document.xml"],
        ["pptx"] = ["[Content_Types].xml", "ppt/presentation.xml"],
        ["odt"] = ["mimetype", "content.xml"],
        ["epub"] = ["mimetype", "META-INF/container.xml"],
        ["zip"] = []
    };

    private static readonly Dictionary<string, string> Mimetype = new(StringComparer.OrdinalIgnoreCase)
    {
        ["odt"] = "application/vnd.oasis.opendocument.text",
        ["epub"] = "application/epub+zip"
    };

    public static string? Problem(string extension, byte[] bytes)
    {
        if (bytes.Length == 0) return "O arquivo saiu vazio.";
        if (extension.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return StartsWith(bytes, "%PDF-") && Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 1024), Math.Min(1024, bytes.Length)).Contains("%%EOF", StringComparison.Ordinal)
                ? null : "O PDF saiu incompleto.";
        if (!Required.TryGetValue(extension, out var required)) return null;
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (required.FirstOrDefault(name => zip.GetEntry(name) is null) is { } missing) return "Falta “" + missing + "” dentro do arquivo.";
            if (Mimetype.TryGetValue(extension, out var mime))
            {
                if (zip.Entries[0].FullName != "mimetype") return "“mimetype” precisa ser o primeiro item do arquivo.";
                using var reader = new StreamReader(zip.Entries[0].Open());
                if (reader.ReadToEnd() != mime) return "O “mimetype” não é " + mime + ".";
            }
            foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
                         || e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)))
            {
                try { using var xml = XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore }); while (xml.Read()) { } }
                catch (XmlException e) { return "“" + entry.FullName + "” está malformado: " + e.Message; }
            }
            return null;
        }
        catch (InvalidDataException) { return "O arquivo não abre como " + extension.ToUpperInvariant() + "."; }
    }

    private static bool StartsWith(byte[] bytes, string ascii) =>
        bytes.Length >= ascii.Length && Encoding.ASCII.GetString(bytes, 0, ascii.Length) == ascii;
}
