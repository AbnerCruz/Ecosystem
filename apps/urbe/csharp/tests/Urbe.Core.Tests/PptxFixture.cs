using System.IO.Compression;
using System.Text;

namespace Urbe.Tests.Fixtures;

/// <summary>One slide of a test deck: raw shapes for its spTree, notes, what follows cSld (transition, timing), extra relationships and media.</summary>
public sealed class FixtureSlide
{
    public string Shapes { get; set; } = "";
    public string? Notes { get; set; }
    public string After { get; set; } = "";
    public string Attributes { get; set; } = "";
    public string Background { get; set; } = "";
    public List<(string Id, string Type, string Target)> Rels { get; } = [];
}

/// <summary>
/// CST-237: builds small but real PowerPoint packages in the tests (content types, presentation, theme, master with a
/// decoration and text styles, a layout with title and body placeholders, slides, notes, media), so the reader is
/// checked against the structure PowerPoint writes without binary fixtures.
/// </summary>
public sealed class PptxFixture
{
    public const string Ns = "xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"";
    private const string RelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string Office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";

    public long Width { get; set; } = 12192000;
    public long Height { get; set; } = 6858000;
    public List<FixtureSlide> Slides { get; } = [];
    public Dictionary<string, byte[]> Media { get; } = [];
    public bool Macros { get; set; }
    public bool EmbeddedFonts { get; set; }
    /// <summary>The master's decoration (a green bar at the bottom), drawn under every slide.</summary>
    public bool MasterDecoration { get; set; } = true;

    public FixtureSlide Add(string shapes = "", string? notes = null)
    {
        var slide = new FixtureSlide { Shapes = shapes, Notes = notes };
        Slides.Add(slide);
        return slide;
    }

    // ---- shape helpers (EMU positions)

    public static string Xfrm(long x, long y, long w, long h, int rotDegrees = 0, bool flipH = false) =>
        $"<a:xfrm{(rotDegrees != 0 ? $" rot=\"{rotDegrees * 60000}\"" : "")}{(flipH ? " flipH=\"1\"" : "")}><a:off x=\"{x}\" y=\"{y}\"/><a:ext cx=\"{w}\" cy=\"{h}\"/></a:xfrm>";

    public static string Run(string text, int size = 0, bool bold = false, string? color = null) =>
        $"<a:r><a:rPr lang=\"pt-BR\"{(size > 0 ? $" sz=\"{size * 100}\"" : "")}{(bold ? " b=\"1\"" : "")}>{(color is null ? "" : $"<a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill>")}</a:rPr><a:t>{System.Security.SecurityElement.Escape(text)}</a:t></a:r>";

    public static string Paragraph(string runs, string? align = null) => $"<a:p>{(align is null ? "" : $"<a:pPr algn=\"{align}\"/>")}{runs}</a:p>";

    public static string TextBox(int id, string xfrm, params string[] paragraphs) =>
        $"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Texto {id}\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr><p:spPr>{xfrm}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/></p:spPr>" +
        $"<p:txBody><a:bodyPr wrap=\"square\"/><a:lstStyle/>{string.Concat(paragraphs)}</p:txBody></p:sp>";

    public static string Shape(int id, string xfrm, string geometry, string fill, string? text = null) =>
        $"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Forma {id}\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr><p:spPr>{xfrm}<a:prstGeom prst=\"{geometry}\"><a:avLst/></a:prstGeom>" +
        $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill><a:ln><a:noFill/></a:ln></p:spPr>" +
        (text is null ? "" : $"<p:txBody><a:bodyPr anchor=\"ctr\"/><a:lstStyle/>{Paragraph(Run(text), "ctr")}</p:txBody>") + "</p:sp>";

    /// <summary>A placeholder with no position of its own (the layout's is used).</summary>
    public static string Placeholder(int id, string type, string? idx, params string[] paragraphs) =>
        $"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"Espaço {id}\"/><p:cNvSpPr><a:spLocks noGrp=\"1\"/></p:cNvSpPr><p:nvPr><p:ph{(type == "obj" ? "" : $" type=\"{type}\"")}{(idx is null ? "" : $" idx=\"{idx}\"")}/></p:nvPr></p:nvSpPr>" +
        $"<p:spPr/><p:txBody><a:bodyPr/><a:lstStyle/>{string.Concat(paragraphs)}</p:txBody></p:sp>";

    public static string Picture(int id, string relId, string xfrm) =>
        $"<p:pic><p:nvPicPr><p:cNvPr id=\"{id}\" name=\"Imagem {id}\"/><p:cNvPicPr/><p:nvPr/></p:nvPicPr><p:blipFill><a:blip r:embed=\"{relId}\"/><a:stretch><a:fillRect/></a:stretch></p:blipFill>" +
        $"<p:spPr>{xfrm}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:pic>";

    public static string Table(int id, string xfrm, string[][] cells, long columnWidth, long rowHeight) =>
        $"<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"{id}\" name=\"Tabela {id}\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>{xfrm.Replace("a:xfrm", "p:xfrm")}" +
        "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/table\"><a:tbl><a:tblPr firstRow=\"1\"/><a:tblGrid>" +
        string.Concat(cells[0].Select(_ => $"<a:gridCol w=\"{columnWidth}\"/>")) + "</a:tblGrid>" +
        string.Concat(cells.Select((row, r) => $"<a:tr h=\"{rowHeight}\">" + string.Concat(row.Select(c =>
            $"<a:tc><a:txBody><a:bodyPr/><a:lstStyle/>{Paragraph(Run(c, 14))}</a:txBody><a:tcPr>{(r == 0 ? "<a:solidFill><a:srgbClr val=\"FFC000\"/></a:solidFill>" : "")}</a:tcPr></a:tc>")) + "</a:tr>")) +
        "</a:tbl></a:graphicData></a:graphic></p:graphicFrame>";

    public static string Chart(int id, string xfrm) =>
        $"<p:graphicFrame><p:nvGraphicFramePr><p:cNvPr id=\"{id}\" name=\"Gráfico {id}\"/><p:cNvGraphicFramePr/><p:nvPr/></p:nvGraphicFramePr>{xfrm.Replace("a:xfrm", "p:xfrm")}" +
        "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" r:id=\"rIdChart\"/></a:graphicData></a:graphic></p:graphicFrame>";

    public const string Transition = "<p:transition spd=\"med\"><p:fade/></p:transition>";
    public const string Animation = "<p:timing><p:tnLst><p:par><p:cTn id=\"1\" dur=\"indefinite\" restart=\"never\" nodeType=\"tmRoot\"><p:childTnLst><p:par><p:cTn id=\"2\" presetID=\"10\" presetClass=\"entr\" fill=\"hold\"/></p:par><p:par><p:cTn id=\"3\" presetID=\"10\" presetClass=\"entr\" fill=\"hold\"/></p:par></p:childTnLst></p:cTn></p:par></p:tnLst></p:timing>";

    /// <summary>A real PNG of one colour (no imaging library: stored with zlib and CRC by hand).</summary>
    public static byte[] Png(int width, int height, byte r, byte g, byte b)
    {
        static uint Crc(ReadOnlySpan<byte> data)
        {
            uint c = 0xFFFFFFFF;
            foreach (byte x in data) { c ^= x; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; }
            return c ^ 0xFFFFFFFF;
        }
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        void Chunk(string type, byte[] data)
        {
            var length = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length); png.Write(length);
            var body = Encoding.ASCII.GetBytes(type).Concat(data).ToArray(); png.Write(body);
            var crc = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(body)); png.Write(crc);
        }
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; header[9] = 2;
        Chunk("IHDR", header);
        var raw = new byte[height * (width * 3 + 1)];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { int o = y * (width * 3 + 1) + 1 + x * 3; raw[o] = r; raw[o + 1] = g; raw[o + 2] = b; }
        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
        Chunk("IDAT", compressed.ToArray());
        Chunk("IEND", []);
        return png.ToArray();
    }

    // ---- package

    public byte[] Build()
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Put(string name, string text) { using var s = zip.CreateEntry(name).Open(); s.Write(Encoding.UTF8.GetBytes(text)); }
            void PutBytes(string name, byte[] bytes) { using var s = zip.CreateEntry(name).Open(); s.Write(bytes); }
            string Rels(params (string Id, string Type, string Target)[] rels) =>
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"{RelNs}\">" + string.Concat(rels.Select(r => $"<Relationship Id=\"{r.Id}\" Type=\"{(r.Type.StartsWith("http", StringComparison.Ordinal) ? r.Type : Office + r.Type)}\" Target=\"{r.Target}\"/>")) + "</Relationships>";
            Put("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/ppt/presentation.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml\"/>" +
                string.Concat(Slides.Select((_, i) => $"<Override PartName=\"/ppt/slides/slide{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>")) +
                "<Override PartName=\"/ppt/slideLayouts/slideLayout1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml\"/><Override PartName=\"/ppt/slideMasters/slideMaster1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml\"/><Override PartName=\"/ppt/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/>" +
                string.Concat(Slides.Select((s, i) => s.Notes is null ? "" : $"<Override PartName=\"/ppt/notesSlides/notesSlide{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml\"/>")) + "</Types>");
            Put("_rels/.rels", Rels(("rId1", "officeDocument", "ppt/presentation.xml")));
            Put("ppt/presentation.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:presentation {Ns}><p:sldMasterIdLst><p:sldMasterId id=\"2147483648\" r:id=\"rIdM\"/></p:sldMasterIdLst><p:sldIdLst>" +
                string.Concat(Slides.Select((_, i) => $"<p:sldId id=\"{256 + i}\" r:id=\"rIdS{i + 1}\"/>")) + $"</p:sldIdLst><p:sldSz cx=\"{Width}\" cy=\"{Height}\"/><p:notesSz cx=\"6858000\" cy=\"9144000\"/>" +
                (EmbeddedFonts ? "<p:embeddedFontLst><p:embeddedFont><p:font typeface=\"Fonte Própria\"/><p:regular r:id=\"rIdF\"/></p:embeddedFont></p:embeddedFontLst>" : "") +
                "<p:defaultTextStyle><a:lvl1pPr><a:defRPr sz=\"1800\"><a:solidFill><a:schemeClr val=\"tx1\"/></a:solidFill></a:defRPr></a:lvl1pPr></p:defaultTextStyle></p:presentation>");
            Put("ppt/_rels/presentation.xml.rels", Rels([("rIdM", "slideMaster", "slideMasters/slideMaster1.xml"), ("rIdT", "theme", "theme/theme1.xml"),
                .. Slides.Select((_, i) => ($"rIdS{i + 1}", "slide", $"slides/slide{i + 1}.xml"))]));
            Put("ppt/theme/theme1.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><a:theme {Ns} name=\"Teste\"><a:themeElements><a:clrScheme name=\"Teste\">" +
                "<a:dk1><a:sysClr val=\"windowText\" lastClr=\"000000\"/></a:dk1><a:lt1><a:sysClr val=\"window\" lastClr=\"FFFFFF\"/></a:lt1><a:dk2><a:srgbClr val=\"1F3864\"/></a:dk2><a:lt2><a:srgbClr val=\"EEECE1\"/></a:lt2>" +
                "<a:accent1><a:srgbClr val=\"00B050\"/></a:accent1><a:accent2><a:srgbClr val=\"C00000\"/></a:accent2><a:accent3><a:srgbClr val=\"A5A5A5\"/></a:accent3><a:accent4><a:srgbClr val=\"FFC000\"/></a:accent4><a:accent5><a:srgbClr val=\"5B9BD5\"/></a:accent5><a:accent6><a:srgbClr val=\"70AD47\"/></a:accent6>" +
                "<a:hlink><a:srgbClr val=\"0563C1\"/></a:hlink><a:folHlink><a:srgbClr val=\"954F72\"/></a:folHlink></a:clrScheme><a:fontScheme name=\"Teste\"><a:majorFont><a:latin typeface=\"DejaVu Sans\"/></a:majorFont><a:minorFont><a:latin typeface=\"DejaVu Sans\"/></a:minorFont></a:fontScheme>" +
                "<a:fmtScheme name=\"Teste\"><a:fillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:fillStyleLst><a:lnStyleLst><a:ln w=\"6350\"/></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements></a:theme>");
            long w = Width, h = Height;
            string decoration = MasterDecoration
                ? $"<p:sp><p:nvSpPr><p:cNvPr id=\"10\" name=\"Faixa\"/><p:cNvSpPr/><p:nvPr userDrawn=\"1\"/></p:nvSpPr><p:spPr>{Xfrm(0, h - h / 20, w, h / 20)}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:solidFill><a:schemeClr val=\"accent1\"/></a:solidFill></p:spPr></p:sp>"
                : "";
            Put("ppt/slideMasters/slideMaster1.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sldMaster {Ns}><p:cSld><p:bg><p:bgRef idx=\"1001\"><a:schemeClr val=\"bg1\"/></p:bgRef></p:bg><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
                $"<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Título\"/><p:cNvSpPr/><p:nvPr><p:ph type=\"title\"/></p:nvPr></p:nvSpPr><p:spPr>{Xfrm(w / 12, h / 24, w * 10 / 12, h / 6)}</p:spPr><p:txBody><a:bodyPr anchor=\"ctr\"/><a:lstStyle/><a:p><a:r><a:t>Título mestre</a:t></a:r></a:p></p:txBody></p:sp>" +
                $"<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Corpo\"/><p:cNvSpPr/><p:nvPr><p:ph type=\"body\" idx=\"1\"/></p:nvPr></p:nvSpPr><p:spPr>{Xfrm(w / 12, h / 4, w * 10 / 12, h * 2 / 3)}</p:spPr><p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Texto mestre</a:t></a:r></a:p></p:txBody></p:sp>" +
                decoration + "</p:spTree></p:cSld><p:clrMap bg1=\"lt1\" tx1=\"dk1\" bg2=\"lt2\" tx2=\"dk2\" accent1=\"accent1\" accent2=\"accent2\" accent3=\"accent3\" accent4=\"accent4\" accent5=\"accent5\" accent6=\"accent6\" hlink=\"hlink\" folHlink=\"folHlink\"/>" +
                "<p:sldLayoutIdLst><p:sldLayoutId id=\"2147483649\" r:id=\"rIdL\"/></p:sldLayoutIdLst><p:txStyles>" +
                "<p:titleStyle><a:lvl1pPr algn=\"l\"><a:defRPr sz=\"4400\" b=\"1\"><a:solidFill><a:schemeClr val=\"tx2\"/></a:solidFill></a:defRPr></a:lvl1pPr></p:titleStyle>" +
                "<p:bodyStyle><a:lvl1pPr marL=\"228600\" indent=\"-228600\"><a:buChar char=\"•\"/><a:defRPr sz=\"2800\"><a:solidFill><a:schemeClr val=\"tx1\"/></a:solidFill></a:defRPr></a:lvl1pPr><a:lvl2pPr marL=\"685800\" indent=\"-228600\"><a:buChar char=\"–\"/><a:defRPr sz=\"2400\"/></a:lvl2pPr></p:bodyStyle>" +
                "<p:otherStyle><a:lvl1pPr><a:defRPr sz=\"1800\"/></a:lvl1pPr></p:otherStyle></p:txStyles></p:sldMaster>");
            Put("ppt/slideMasters/_rels/slideMaster1.xml.rels", Rels(("rIdL", "slideLayout", "../slideLayouts/slideLayout1.xml"), ("rIdT", "theme", "../theme/theme1.xml")));
            Put("ppt/slideLayouts/slideLayout1.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sldLayout {Ns} type=\"obj\"><p:cSld name=\"Título e conteúdo\"><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
                $"<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Título\"/><p:cNvSpPr/><p:nvPr><p:ph type=\"title\"/></p:nvPr></p:nvSpPr><p:spPr>{Xfrm(w / 10, h / 20, w * 8 / 10, h / 5)}</p:spPr><p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Clique para editar o título</a:t></a:r></a:p></p:txBody></p:sp>" +
                $"<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Conteúdo\"/><p:cNvSpPr/><p:nvPr><p:ph idx=\"1\"/></p:nvPr></p:nvSpPr><p:spPr>{Xfrm(w / 10, h * 3 / 10, w * 8 / 10, h * 6 / 10)}</p:spPr><p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:r><a:t>Clique para editar o texto</a:t></a:r></a:p></p:txBody></p:sp>" +
                "</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr></p:sldLayout>");
            Put("ppt/slideLayouts/_rels/slideLayout1.xml.rels", Rels(("rIdM", "slideMaster", "../slideMasters/slideMaster1.xml")));
            for (int i = 0; i < Slides.Count; i++)
            {
                var slide = Slides[i];
                Put($"ppt/slides/slide{i + 1}.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:sld {Ns}{slide.Attributes}><p:cSld>{slide.Background}<p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
                    slide.Shapes + $"</p:spTree></p:cSld><p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>{slide.After}</p:sld>");
                var rels = new List<(string, string, string)> { ("rIdL", "slideLayout", "../slideLayouts/slideLayout1.xml") };
                if (slide.Notes is not null)
                {
                    rels.Add(("rIdN", "notesSlide", $"../notesSlides/notesSlide{i + 1}.xml"));
                    Put($"ppt/notesSlides/notesSlide{i + 1}.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><p:notes {Ns}><p:cSld><p:spTree><p:nvGrpSpPr><p:cNvPr id=\"1\" name=\"\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr/>" +
                        "<p:sp><p:nvSpPr><p:cNvPr id=\"2\" name=\"Imagem do slide\"/><p:cNvSpPr/><p:nvPr><p:ph type=\"sldImg\"/></p:nvPr></p:nvSpPr><p:spPr/></p:sp>" +
                        "<p:sp><p:nvSpPr><p:cNvPr id=\"3\" name=\"Notas\"/><p:cNvSpPr/><p:nvPr><p:ph type=\"body\" idx=\"1\"/></p:nvPr></p:nvSpPr><p:spPr/><p:txBody><a:bodyPr/><a:lstStyle/>" +
                        string.Concat(slide.Notes.Split('\n').Select(line => Paragraph(Run(line)))) + "</p:txBody></p:sp></p:spTree></p:cSld></p:notes>");
                    Put($"ppt/notesSlides/_rels/notesSlide{i + 1}.xml.rels", Rels(("rIdS", "slide", $"../slides/slide{i + 1}.xml")));
                }
                rels.AddRange(slide.Rels);
                Put($"ppt/slides/_rels/slide{i + 1}.xml.rels", Rels([.. rels]));
            }
            foreach (var (name, bytes) in Media) PutBytes("ppt/media/" + name, bytes);
            if (Macros) PutBytes("ppt/vbaProject.bin", [0xD0, 0xCF, 0x11, 0xE0, 1, 2, 3]);
        }
        return output.ToArray();
    }
}
