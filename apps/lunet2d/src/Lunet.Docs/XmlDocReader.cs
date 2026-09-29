using System.Text;
using System.Xml.Linq;

namespace Lunet.Docs;

/// <summary>Conteúdo dos comentários XML de um membro, já convertido para texto simples (Markdown leve).</summary>
public sealed record XmlDocEntry(
    string Summary,
    string Remarks,
    string Returns,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Related,
    string? Since);

/// <summary>Lê o arquivo XML de documentação gerado pelo compilador.</summary>
public sealed class XmlDocReader
{
    private readonly Dictionary<string, XElement> _members = new(StringComparer.Ordinal);

    public XmlDocReader(Stream xml)
    {
        var doc = XDocument.Load(xml);
        foreach (var member in doc.Descendants("member"))
        {
            var name = (string?)member.Attribute("name");
            if (name is not null) _members[name] = member;
        }
    }

    public static XmlDocReader Empty() => new(new MemoryStream("<doc><members/></doc>"u8.ToArray()));

    public int Count => _members.Count;

    public XmlDocEntry? Get(string documentationId)
    {
        if (!_members.TryGetValue(documentationId, out var element)) return null;
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in element.Elements("param"))
        {
            var name = (string?)p.Attribute("name");
            if (name is not null) parameters[name] = Render(p);
        }
        var related = new List<string>();
        foreach (var see in element.Descendants().Where(e => e.Name == "seealso" || e.Name == "see"))
        {
            var cref = (string?)see.Attribute("cref");
            if (cref is not null && !related.Contains(cref)) related.Add(cref);
        }
        return new XmlDocEntry(
            Render(element.Element("summary")),
            Render(element.Element("remarks")),
            Render(element.Element("returns")),
            parameters,
            element.Elements("example").Select(Render).Where(e => e.Length > 0).ToList(),
            related,
            element.Element("since") is { } since ? Render(since) : null);
    }

    /// <summary>Converte o conteúdo XML em texto: <c>&lt;c&gt;</c> vira `código`, <c>&lt;code&gt;</c> vira bloco, <c>&lt;see cref&gt;</c> vira o nome curto.</summary>
    internal static string Render(XElement? element)
    {
        if (element is null) return "";
        var builder = new StringBuilder();
        Append(builder, element);
        return Tidy(builder.ToString());
    }

    private static void Append(StringBuilder builder, XNode node)
    {
        switch (node)
        {
            case XText text:
                builder.Append(CollapseWhitespace(text.Value));
                break;
            case XElement element:
                switch (element.Name.LocalName)
                {
                    case "c":
                        builder.Append('`').Append(element.Value.Trim()).Append('`');
                        break;
                    case "code":
                        builder.Append("\n\n```\n").Append(DedentCode(element.Value)).Append("\n```\n\n");
                        break;
                    case "para":
                        builder.Append("\n\n");
                        foreach (var child in element.Nodes()) Append(builder, child);
                        builder.Append("\n\n");
                        break;
                    case "see":
                    case "seealso":
                        builder.Append(SeeText(element));
                        break;
                    case "paramref":
                    case "typeparamref":
                        builder.Append('`').Append((string?)element.Attribute("name")).Append('`');
                        break;
                    case "list":
                        foreach (var item in element.Elements("item"))
                            builder.Append("\n- ").Append(Tidy(Render(item.Element("description") ?? item)));
                        builder.Append('\n');
                        break;
                    default:
                        foreach (var child in element.Nodes()) Append(builder, child);
                        break;
                }
                break;
        }
    }

    private static string SeeText(XElement see)
    {
        if ((string?)see.Attribute("langword") is { } keyword) return $"`{keyword}`";
        if ((string?)see.Attribute("cref") is { } cref) return "`" + ShortName(cref) + "`";
        return see.Value;
    }

    /// <summary>De "M:Ns.Type.Method(System.Int32)" para "Type.Method".</summary>
    public static string ShortName(string cref)
    {
        var kind = cref.Length > 2 && cref[1] == ':' ? cref[0] : '?';
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var paren = name.IndexOf('(');
        if (paren >= 0) name = name[..paren];
        var parts = name.Split('.');
        // Tipos: só o nome. Membros: Tipo.Membro.
        return kind is 'T' || parts.Length < 2 ? parts[^1] : parts[^2] + "." + parts[^1];
    }

    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousSpace) builder.Append(' ');
                previousSpace = true;
            }
            else
            {
                builder.Append(c);
                previousSpace = false;
            }
        }
        return builder.ToString();
    }

    private static string DedentCode(string code)
    {
        var lines = code.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0])) lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1])) lines.RemoveAt(lines.Count - 1);
        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
    }

    private static string Tidy(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => l.StartsWith("    ") ? l : l.Trim()).ToList();
        var joined = string.Join("\n", lines);
        while (joined.Contains("\n\n\n")) joined = joined.Replace("\n\n\n", "\n\n");
        return joined.Trim();
    }
}
