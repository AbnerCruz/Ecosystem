using System.Buffers.Binary;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>
/// CST-082: where a generated image came from, written inside the image itself as XMP (IPTC's recommendation for
/// AI-made media: <c>Iptc4xmpExt:DigitalSourceType = trainedAlgorithmicMedia</c>). The file is its only authority:
/// moved, copied into a publication or exported, it carries what made it. PNG keeps it in an iTXt chunk
/// <c>XML:com.adobe.xmp</c> and JPEG in an APP1 segment, as the XMP specification (part 3) says; any other format is
/// left as it is.
/// </summary>
public sealed record ImageProvenance(string Model, string Provider, string Prompt, DateTimeOffset Created)
{
    public const string TrainedAlgorithmicMedia = "http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia";
    private static readonly XNamespace X = "adobe:ns:meta/", Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#",
        XmpNs = "http://ns.adobe.com/xap/1.0/", Dc = "http://purl.org/dc/elements/1.1/", Ext = "http://iptc.org/std/Iptc4xmpExt/2008-02-29/",
        Urbe = "https://urbe.app/ns/1.0/";
    private const string PngKeyword = "XML:com.adobe.xmp";
    private static readonly byte[] JpegXmpId = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");

    /// <summary>The XMP packet.</summary>
    public string Xmp()
    {
        var description = new XElement(Rdf + "Description", new XAttribute(Rdf + "about", ""),
            new XAttribute(XNamespace.Xmlns + "xmp", XmpNs), new XAttribute(XNamespace.Xmlns + "dc", Dc),
            new XAttribute(XNamespace.Xmlns + "Iptc4xmpExt", Ext), new XAttribute(XNamespace.Xmlns + "urbe", Urbe),
            new XElement(Ext + "DigitalSourceType", TrainedAlgorithmicMedia),
            new XElement(XmpNs + "CreatorTool", "Urbe"),
            new XElement(XmpNs + "CreateDate", Created.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(Dc + "description", new XElement(Rdf + "Alt", new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), Prompt))),
            new XElement(Urbe + "model", Model),
            new XElement(Urbe + "provider", Provider));
        var meta = new XElement(X + "xmpmeta", new XAttribute(XNamespace.Xmlns + "x", X),
            new XElement(Rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", Rdf), description));
        return "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>" + meta.ToString(SaveOptions.DisableFormatting) + "<?xpacket end=\"w\"?>";
    }

    /// <summary>The image with this provenance inside (PNG, JPEG); other formats, or a damaged file, come back unchanged.</summary>
    public byte[] Embed(byte[] image)
    {
        var xmp = Encoding.UTF8.GetBytes(Xmp());
        if (image.Length > 33 && image.AsSpan(0, 8).SequenceEqual(PngSignature) && Encoding.ASCII.GetString(image, 12, 4) == "IHDR")
        {
            // after IHDR (8 signature + 4 length + 4 type + 13 data + 4 crc)
            const int at = 33;
            var data = Encoding.ASCII.GetBytes(PngKeyword).Concat(new byte[] { 0, 0, 0, 0, 0 }).Concat(xmp).ToArray();
            var chunk = new byte[12 + data.Length];
            BinaryPrimitives.WriteInt32BigEndian(chunk, data.Length);
            Encoding.ASCII.GetBytes("iTXt").CopyTo(chunk, 4);
            data.CopyTo(chunk, 8);
            BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), Crc32(chunk.AsSpan(4, 4 + data.Length)));
            return [.. image.AsSpan(0, at), .. chunk, .. image.AsSpan(at)];
        }
        if (image.Length > 4 && image[0] == 0xFF && image[1] == 0xD8 && JpegXmpId.Length + xmp.Length + 2 <= ushort.MaxValue)
        {
            int at = 2;
            // after a JFIF APP0, where readers expect it
            if (image[2] == 0xFF && image[3] == 0xE0 && image.Length > 6) at = 4 + BinaryPrimitives.ReadUInt16BigEndian(image.AsSpan(4));
            if (at > image.Length) return image;
            var segment = new byte[4 + JpegXmpId.Length + xmp.Length];
            segment[0] = 0xFF; segment[1] = 0xE1;
            BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(segment.Length - 2));
            JpegXmpId.CopyTo(segment, 4);
            xmp.CopyTo(segment, 4 + JpegXmpId.Length);
            return [.. image.AsSpan(0, at), .. segment, .. image.AsSpan(at)];
        }
        return image;
    }

    /// <summary>
    /// The provenance an image carries, or null when it says nothing of having been generated. Only the start of the
    /// file is read: the packet goes there (after IHDR, after APP0), and a big photo is not decoded as text to know it.
    /// </summary>
    public static ImageProvenance? Read(byte[] image)
    {
        var text = Encoding.UTF8.GetString(image, 0, Math.Min(image.Length, 1 << 20));
        int start = text.IndexOf("<x:xmpmeta", StringComparison.Ordinal), end = text.IndexOf("</x:xmpmeta>", StringComparison.Ordinal);
        if (start < 0 || end < start) return null;
        XElement d;
        try { d = XElement.Parse(text[start..(end + "</x:xmpmeta>".Length)]).Descendants(Rdf + "Description").First(); }
        catch (Exception e) when (e is System.Xml.XmlException or InvalidOperationException) { return null; }
        if ((string?)d.Element(Ext + "DigitalSourceType") != TrainedAlgorithmicMedia) return null;
        DateTimeOffset.TryParse((string?)d.Element(XmpNs + "CreateDate"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var created);
        return new ImageProvenance((string?)d.Element(Urbe + "model") ?? "", (string?)d.Element(Urbe + "provider") ?? "",
            d.Element(Dc + "description")?.Descendants(Rdf + "li").FirstOrDefault()?.Value ?? "", created);
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        uint c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    internal static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint c = 0xFFFFFFFFu;
        foreach (var b in bytes) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
