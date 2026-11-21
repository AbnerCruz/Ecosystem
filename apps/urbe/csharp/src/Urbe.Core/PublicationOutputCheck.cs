using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>What the check of an exported file found: what was confirmed, what it could not confirm, and the problem.</summary>
public sealed record OutputReceipt(IReadOnlyList<string> Checked, IReadOnlyList<string> NotChecked, string? Problem)
{
    /// <summary>"verificado" when every check ran and passed; "parcial" when some could not run.</summary>
    public string Status => Problem is not null ? "com problema" : NotChecked.Count == 0 ? "verificado" : "parcial";

    /// <summary>One line for the person: status and evidence.</summary>
    public string Summary => Status + ": " + string.Join(", ", Checked) + (NotChecked.Count == 0 ? "" : "; não conferido: " + string.Join(", ", NotChecked));
}

/// <summary>
/// CST-191/213: every output is opened and checked before it is saved, so a broken file never reaches the person as if it
/// were done. Beyond the parts and well-formed XML: OOXML relationships that point to missing parts and parts without
/// a content type (what makes Word and PowerPoint say "unreadable content"), ODF and EPUB manifests whose files are
/// missing, an EPUB spine pointing to no item, a PDF with no page and fonts not embedded.
/// </summary>
public static partial class PublicationOutputCheck
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

    // the PDF standard 14 fonts every reader has: they need no embedding
    private static readonly HashSet<string> Standard14 =
    [
        "Times-Roman", "Times-Bold", "Times-Italic", "Times-BoldItalic", "Helvetica", "Helvetica-Bold", "Helvetica-Oblique",
        "Helvetica-BoldOblique", "Courier", "Courier-Bold", "Courier-Oblique", "Courier-BoldOblique", "Symbol", "ZapfDingbats"
    ];

    public static string? Problem(string extension, byte[] bytes) => Receipt(extension, bytes).Problem;

    public static OutputReceipt Receipt(string extension, byte[] bytes)
    {
        var done = new List<string>();
        var missing = new List<string>();
        OutputReceipt Fail(string problem) => new(done, missing, problem);
        if (bytes.Length == 0) return Fail("O arquivo saiu vazio.");
        if (extension.Equals("pdf", StringComparison.OrdinalIgnoreCase)) return Pdf(bytes, done, missing);
        if (!Required.TryGetValue(extension, out var required)) return new(done, ["formato sem verificador"], null);
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (required.FirstOrDefault(name => zip.GetEntry(name) is null) is { } absent) return Fail("Falta “" + absent + "” dentro do arquivo.");
            if (Mimetype.TryGetValue(extension, out var mime))
            {
                if (zip.Entries[0].FullName != "mimetype") return Fail("“mimetype” precisa ser o primeiro item do arquivo.");
                using var reader = new StreamReader(zip.Entries[0].Open());
                if (reader.ReadToEnd() != mime) return Fail("O “mimetype” não é " + mime + ".");
            }
            var xml = new Dictionary<string, XDocument>(StringComparer.Ordinal);
            foreach (var entry in zip.Entries.Where(e => Regex.IsMatch(e.FullName, @"\.(xml|rels|opf|xhtml|ncx)$", RegexOptions.IgnoreCase)))
            {
                try { using var s = entry.Open(); xml[entry.FullName] = XDocument.Load(XmlReader.Create(s, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore })); }
                catch (XmlException e) { return Fail("“" + entry.FullName + "” está malformado: " + e.Message); }
            }
            done.Add(zip.Entries.Count + " partes, XML válido");
            var names = new HashSet<string>(zip.Entries.Select(e => e.FullName), StringComparer.Ordinal);
            var problem = extension.ToLowerInvariant() switch
            {
                "docx" or "pptx" => Ooxml(xml, names, done),
                "odt" => Odf(xml, names, done),
                "epub" => Epub(xml, names, done),
                _ => null
            };
            if (problem is not null) return Fail(problem);
            if (extension.Equals("pptx", StringComparison.OrdinalIgnoreCase))
            {
                int slides = names.Count(n => Regex.IsMatch(n, @"^ppt/slides/slide\d+\.xml$"));
                if (slides == 0) return Fail("A apresentação saiu sem slides.");
                done.Add(slides + (slides == 1 ? " slide" : " slides"));
            }
            if (extension is "docx" or "pptx" or "odt" or "epub") missing.Add("aparência no programa de destino");
            return new(done, missing, null);
        }
        catch (InvalidDataException) { return Fail("O arquivo não abre como " + extension.ToUpperInvariant() + "."); }
    }

    /// <summary>Every internal relationship points to a part that exists, and every part has a content type.</summary>
    private static string? Ooxml(Dictionary<string, XDocument> xml, HashSet<string> names, List<string> done)
    {
        XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships", ct = "http://schemas.openxmlformats.org/package/2006/content-types";
        int links = 0;
        foreach (var (path, doc) in xml.Where(x => x.Key.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
        {
            // "word/_rels/document.xml.rels" relates "word/document.xml": targets are relative to "word/"
            var folder = path.Contains("_rels/") ? path[..path.IndexOf("_rels/", StringComparison.Ordinal)] : "";
            foreach (var r in doc.Descendants(rel + "Relationship"))
            {
                if ((string?)r.Attribute("TargetMode") == "External") continue;
                var target = Uri.UnescapeDataString((string?)r.Attribute("Target") ?? "");
                var full = Normalize(target.StartsWith('/') ? target[1..] : folder + target);
                links++;
                if (!names.Contains(full)) return "Uma ligação interna aponta para “" + full + "”, que não existe no arquivo (o programa diria que o conteúdo está ilegível).";
            }
        }
        var types = xml["[Content_Types].xml"];
        var defaults = types.Descendants(ct + "Default").Select(d => ((string?)d.Attribute("Extension"))?.ToLowerInvariant()).ToHashSet();
        var overrides = types.Descendants(ct + "Override").Select(o => ((string?)o.Attribute("PartName"))?.TrimStart('/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names.Where(n => n != "[Content_Types].xml" && !n.EndsWith('/')))
            if (!overrides.Contains(name) && !defaults.Contains(Path.GetExtension(name).TrimStart('.').ToLowerInvariant()))
                return "A parte “" + name + "” não tem tipo de conteúdo declarado.";
        done.Add(links + " ligações internas íntegras");
        return null;
    }

    private static string? Odf(Dictionary<string, XDocument> xml, HashSet<string> names, List<string> done)
    {
        if (!xml.TryGetValue("META-INF/manifest.xml", out var manifest)) return "Falta “META-INF/manifest.xml” dentro do arquivo.";
        XNamespace m = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
        foreach (var path in manifest.Descendants(m + "file-entry").Select(e => (string?)e.Attribute(m + "full-path") ?? "").Where(p => p is not ("" or "/") && !p.EndsWith('/')))
            if (!names.Contains(path)) return "O manifesto lista “" + path + "”, que não existe no arquivo.";
        done.Add("manifesto completo");
        return null;
    }

    private static string? Epub(Dictionary<string, XDocument> xml, HashSet<string> names, List<string> done)
    {
        XNamespace c = "urn:oasis:names:tc:opendocument:xmlns:container", opf = "http://www.idpf.org/2007/opf";
        var root = xml["META-INF/container.xml"].Descendants(c + "rootfile").Select(r => (string?)r.Attribute("full-path")).FirstOrDefault();
        if (root is null || !xml.TryGetValue(root, out var package)) return "O container não aponta para o pacote (OPF) do livro.";
        var folder = root.Contains('/') ? root[..(root.LastIndexOf('/') + 1)] : "";
        var items = package.Descendants(opf + "item").ToList();
        foreach (var href in items.Select(i => (string?)i.Attribute("href") ?? ""))
            if (!names.Contains(Normalize(folder + Uri.UnescapeDataString(href.Split('#')[0]))))
                return "O pacote do livro lista “" + href + "”, que não existe no arquivo.";
        var ids = items.Select(i => (string?)i.Attribute("id")).ToHashSet();
        var spine = package.Descendants(opf + "itemref").Select(r => (string?)r.Attribute("idref")).ToList();
        if (spine.Count == 0) return "O livro saiu sem capítulos na ordem de leitura.";
        if (spine.FirstOrDefault(id => !ids.Contains(id)) is { } lost) return "A ordem de leitura cita “" + lost + "”, que não está no pacote.";
        done.Add(spine.Count + (spine.Count == 1 ? " item" : " itens") + " na ordem de leitura, manifesto completo");
        return null;
    }

    private static OutputReceipt Pdf(byte[] bytes, List<string> done, List<string> missing)
    {
        var tail = Encoding.ASCII.GetString(bytes, Math.Max(0, bytes.Length - 1024), Math.Min(1024, bytes.Length));
        if (!StartsWith(bytes, "%PDF-") || !tail.Contains("%%EOF", StringComparison.Ordinal)) return new(done, missing, "O PDF saiu incompleto.");
        int pages;
        // opened strictly by a PDF reader: a broken cross-reference or object is caught here, not by the person's reader
        try { using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes, new UglyToad.PdfPig.ParsingOptions { UseLenientParsing = false }); pages = pdf.NumberOfPages; }
        catch (Exception e) when (e is not OutOfMemoryException) { return new(done, missing, "O PDF não abre num leitor de PDF: " + e.Message); }
        if (pages == 0) return new(done, missing, "O PDF saiu sem páginas.");
        done.Add(pages + (pages == 1 ? " página" : " páginas") + ", estrutura válida");
        var text = Encoding.Latin1.GetString(bytes);
        if (text.Contains("/ObjStm", StringComparison.Ordinal)) missing.Add("fontes (objetos comprimidos)");
        else if (FontProblem(text) is { } font) return new(done, missing, font);
        else done.Add("fontes embutidas");
        missing.Add("aparência das páginas");
        return new(done, missing, null);
    }

    /// <summary>A font that is not one of the standard 14 and has no FontFile: other devices draw its text with a substitute.</summary>
    internal static string? FontProblem(string pdf)
    {
        foreach (Match font in FontRegex().Matches(pdf))
        {
            var name = font.Groups["name"].Value;
            var plain = name.Contains('+') ? name[(name.IndexOf('+') + 1)..] : name;
            if (Standard14.Contains(plain) || font.Groups["sub"].Value is "Type0" or "Type3") continue;   // Type0 embeds through its descendant
            var described = Regex.IsMatch(pdf, @"/FontName\s*/" + Regex.Escape(name) + @"\b[^>]*?/FontFile[23]?\b", RegexOptions.Singleline) ||
                            Regex.IsMatch(pdf, @"/FontFile[23]?\s+\d+\s+\d+\s+R[^>]*?/FontName\s*/" + Regex.Escape(name) + @"\b", RegexOptions.Singleline);
            if (!described) return "A fonte “" + plain + "” não foi embutida no PDF: em outro aparelho o texto sai com outra letra.";
        }
        return null;
    }

    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else parts.Add(part);
        }
        return string.Join("/", parts);
    }

    private static bool StartsWith(byte[] bytes, string ascii) =>
        bytes.Length >= ascii.Length && Encoding.ASCII.GetString(bytes, 0, ascii.Length) == ascii;

    [GeneratedRegex(@"/Type\s*/Font\b(?:(?!>>).)*?/Subtype\s*/(?<sub>\w+)(?:(?!>>).)*?/BaseFont\s*/(?<name>[^\s/>\[]+)|/Type\s*/Font\b(?:(?!>>).)*?/BaseFont\s*/(?<name>[^\s/>\[]+)(?:(?!>>).)*?/Subtype\s*/(?<sub>\w+)", RegexOptions.Singleline)]
    private static partial Regex FontRegex();
}
