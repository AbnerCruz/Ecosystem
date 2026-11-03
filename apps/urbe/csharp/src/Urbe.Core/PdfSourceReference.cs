namespace Urbe.Core;

/// <summary>A PDF location in an ordinary Markdown wikilink. No parallel annotation store.</summary>
public sealed record PdfSourceReference(int Page, string? Sha256, ReaderRegion? Region = null, int? TextOffset = null, int? TextLength = null)
{
    public string Fragment => "page=" + Page.ToString(System.Globalization.CultureInfo.InvariantCulture) +
        (Sha256 is null ? "" : "&sha256=" + Sha256) + (Region is null ? "" : "&region=" + Region.Fragment) + (TextOffset is { } offset ? "&text=" + offset + "&length=" + (TextLength ?? 0) : "");
    public static PdfSourceReference? Parse(string? fragment)
    {
        if (fragment is null) return null;
        var parts = fragment.Split('&');
        if (parts.Length is < 1 or > 5 || !parts[0].StartsWith("page=",StringComparison.Ordinal) ||
            !int.TryParse(parts[0].AsSpan(5),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int page) || page<1) return null;
        string? hash=null; ReaderRegion? region=null;int? offset=null,length=null;
        foreach(var part in parts.Skip(1))
        {
            if(part.StartsWith("sha256=",StringComparison.Ordinal)&&hash is null)
            { hash=part[7..]; if(hash.Length!=64||hash.Any(c=>!Uri.IsHexDigit(c)))return null; }
            else if(part.StartsWith("region=",StringComparison.Ordinal)&&region is null&&ReaderRegion.Parse(part[7..]) is { } parsed)region=parsed;
            else if(part.StartsWith("text=",StringComparison.Ordinal)&&offset is null&&int.TryParse(part.AsSpan(5),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int at))offset=at;
            else if(part.StartsWith("length=",StringComparison.Ordinal)&&length is null&&int.TryParse(part.AsSpan(7),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int count))length=count;
            else return null;
        }
        if((offset is null)!=(length is null))return null;
        return new(page,hash,region,offset,length);
    }
}
