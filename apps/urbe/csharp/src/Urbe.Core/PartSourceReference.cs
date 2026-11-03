namespace Urbe.Core;

public sealed record PartSourceReference(string Unit, int Index, string? Sha256, int? TextOffset = null, int? TextLength = null, ReaderRegion? Region = null)
{
    public string Fragment => Unit + "=" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) + (Sha256 is null ? "" : "&sha256=" + Sha256) +
        (TextOffset is { } offset ? "&text=" + offset.ToString(System.Globalization.CultureInfo.InvariantCulture) + "&length=" + (TextLength ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture) : "") + (Region is null ? "" : "&region=" + Region.Fragment);
    public static PartSourceReference? Parse(string? fragment)
    {
        if (fragment is null) return null;
        var fields = fragment.Split('&');
        if (fields.Length is < 1 or > 5) return null;
        var unit = fields[0].Split('=', 2);
        if (unit.Length != 2 || unit[0] is not ("chapter" or "slide" or "image" or "time") ||
            !int.TryParse(unit[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index) || index < (unit[0] == "time" ? 0 : 1)) return null;
        string? hash = null; int? offset = null, length = null; ReaderRegion? region=null;
        foreach (string field in fields.Skip(1))
        {
            var pair = field.Split('=', 2); if (pair.Length != 2) return null;
            if (pair[0] == "sha256" && hash is null) { hash = pair[1]; if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c))) return null; }
            else if (pair[0] == "text" && offset is null && int.TryParse(pair[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int at)) offset = at;
            else if (pair[0] == "length" && length is null && int.TryParse(pair[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int count)) length = count;
            else if(pair[0]=="region"&&region is null&&ReaderRegion.Parse(pair[1]) is { } parsed)region=parsed;
            else return null;
        }
        if ((offset is null) != (length is null)) return null;
        return new(unit[0], index, hash, offset, length, region);
    }
}
