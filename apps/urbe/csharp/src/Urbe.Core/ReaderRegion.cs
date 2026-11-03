using System.Globalization;
namespace Urbe.Core;

/// <summary>Normalized source rectangle, independent of display DPI/zoom. Source revision must match before restoration.</summary>
public sealed record ReaderRegion(double X, double Y, double Width, double Height)
{
    public string Fragment => string.Join(",",new[] { X,Y,Width,Height }.Select(n=>n.ToString("0.######",CultureInfo.InvariantCulture)));
    public static ReaderRegion? Parse(string? text)
    {
        if (text is null) return null;
        var fields=text.Split(','); if(fields.Length!=4)return null;
        var values=new double[4];
        for(int i=0;i<4;i++)if(!double.TryParse(fields[i],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out values[i])||!double.IsFinite(values[i]))return null;
        return values[0]>=0&&values[1]>=0&&values[2]>0&&values[3]>0&&values[0]+values[2]<=1.000001&&values[1]+values[3]<=1.000001
            ?new(values[0],values[1],values[2],values[3]):null;
    }
}
