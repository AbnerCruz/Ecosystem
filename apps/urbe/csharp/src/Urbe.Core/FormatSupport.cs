using System.Text;

namespace Urbe.Core;

/// <summary>
/// How far the Urbe goes with a format (CST-238): "completo" inside the app, "parcial" with what is left out said in
/// the reader, or "fallback externo" (the file opens in another app, untouched). Never a promise the reader can't keep.
/// </summary>
public enum SupportLevel { Complete, Partial, External }

/// <summary>One row of the support matrix: open, search inside, select text, works offline, and where.</summary>
public sealed record FormatSupport(string Name, IReadOnlyList<string> Extensions, SupportLevel Open, SupportLevel Search, SupportLevel Selection,
    bool Offline, string Platforms, string Note);

/// <summary>
/// The one support matrix (NN-001) the readers and the info panel speak from. A format that is not here is unknown:
/// its name, size and type are shown, and "Abrir em outro app".
/// </summary>
public static class FormatSupportMatrix
{
    public const string BothPlatforms = "Android e Windows";

    public static readonly IReadOnlyList<FormatSupport> All =
    [
        new("Nota Markdown", [".md", ".markdown"], SupportLevel.Complete, SupportLevel.Complete, SupportLevel.Complete, true, BothPlatforms, "Abre no editor do Urbe."),
        new("Texto simples", [".txt", ".text", ".log"], SupportLevel.Complete, SupportLevel.Complete, SupportLevel.Complete, true, BothPlatforms,
            "Arquivos grandes são mostrados em partes; a busca percorre o arquivo inteiro."),
        new("RTF", [".rtf"], SupportLevel.Partial, SupportLevel.Complete, SupportLevel.Complete, true, BothPlatforms,
            "Só o texto é mostrado; a formatação do RTF (fontes, cores, tabelas, imagens) não é mostrada."),
        new("Tabela CSV", [".csv", ".tsv"], SupportLevel.Complete, SupportLevel.Complete, SupportLevel.Partial, true, BothPlatforms,
            "Mostrada como tabela; a seleção é por linha."),
        new("JSON", [".json", ".jsonc"], SupportLevel.Complete, SupportLevel.Complete, SupportLevel.Complete, true, BothPlatforms,
            "Mostrado formatado; um JSON inválido aparece como texto, com o erro."),
        new("PDF", [".pdf"], SupportLevel.Complete, SupportLevel.Partial, SupportLevel.Partial, true, BothPlatforms,
            "Busca e seleção dependem da camada de texto; página digitalizada usa OCR por região."),
        new("EPUB", [".epub"], SupportLevel.Complete, SupportLevel.Complete, SupportLevel.Complete, true, BothPlatforms, "EPUB sem DRM."),
        new("Documento Word/ODF", [".docx", ".odt"], SupportLevel.Partial, SupportLevel.Partial, SupportLevel.Partial, true, BothPlatforms,
            "Leitura semântica: paginação e objetos complexos podem diferir do aplicativo de origem."),
        new("Apresentação PowerPoint", [".pptx"], SupportLevel.Partial, SupportLevel.Partial, SupportLevel.Partial, true, BothPlatforms,
            "Leitura, não edição: slides desenhados no aparelho com texto, formas simples, imagens, tabelas e notas do apresentador, na proporção " +
            "original. Animações, transições, gráficos, SmartArt, equações, vídeo, macros e fontes incorporadas não são reproduzidos; cada slide " +
            "diz o que ficou de fora. A busca cobre o texto dos slides e das notas, não o de imagens; a seleção é nas notas."),
        new("Apresentação ODF", [".odp"], SupportLevel.Partial, SupportLevel.Partial, SupportLevel.Partial, true, BothPlatforms,
            "Leitura, não edição: slides desenhados no aparelho com texto, retângulos, elipses, linhas, imagens, tabelas e notas do apresentador, " +
            "com as decorações do slide mestre. Animações, transições, gráficos, equações, formas livres e mídia não são reproduzidos; cada slide diz o que ficou de fora."),
        new("PowerPoint 97–2003", [".ppt"], SupportLevel.External, SupportLevel.External, SupportLevel.External, true, BothPlatforms,
            "Formato binário antigo: o Urbe não o lê. Abra em outro app, ou salve como .pptx para ler aqui; o original não é modificado."),
        new("Documento legado do Office", [".doc", ".xls", ".xlsx", ".ods"], SupportLevel.External, SupportLevel.External, SupportLevel.External, true, BothPlatforms,
            "O Urbe não lê este formato; o original é aberto em outro app sem conversão."),
        new("Quadrinhos CBZ", [".cbz"], SupportLevel.Complete, SupportLevel.External, SupportLevel.External, true, BothPlatforms,
            "Páginas em ordem natural; o texto dos balões não é pesquisável."),
        new("Quadrinhos CBR/CB7", [".cbr", ".cb7", ".cbt"], SupportLevel.External, SupportLevel.External, SupportLevel.External, true, BothPlatforms,
            "RAR, 7z e TAR não são abertos pelo Urbe; converta para CBZ ou abra em outro app."),
        new("Imagem", [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"], SupportLevel.Complete, SupportLevel.Partial, SupportLevel.Partial, true, BothPlatforms,
            "Busca e seleção por OCR de uma região."),
        new("Imagem SVG", [".svg"], SupportLevel.Partial, SupportLevel.Complete, SupportLevel.Complete, true, BothPlatforms, "Mostrada como código-fonte."),
        new("Áudio", [".mp3", ".wav", ".ogg", ".m4a", ".aac", ".flac"], SupportLevel.Complete, SupportLevel.External, SupportLevel.External, true, BothPlatforms,
            "Tocado pelo reprodutor do sistema; formatos que o aparelho não decodifica abrem em outro app."),
        new("Vídeo", [".mp4", ".m4v", ".webm", ".mov", ".mkv", ".avi"], SupportLevel.Complete, SupportLevel.External, SupportLevel.External, true, BothPlatforms,
            "Tocado pelo reprodutor do sistema; formatos que o aparelho não decodifica abrem em outro app."),
        new("Página HTML", [".html", ".htm", ".xhtml"], SupportLevel.Complete, SupportLevel.Partial, SupportLevel.Partial, true, BothPlatforms,
            "Mostrada isolada, sem rede; a busca é na fonte."),
        new("Arquivo ZIP", [".zip"], SupportLevel.Partial, SupportLevel.Complete, SupportLevel.Complete, true, BothPlatforms, "Lista as entradas; nada é extraído."),
        new("Arquivo compactado", [".7z", ".rar", ".tar", ".gz"], SupportLevel.External, SupportLevel.External, SupportLevel.External, true, BothPlatforms,
            "O Urbe não abre este formato; use outro app."),
    ];

    private static readonly Dictionary<string, FormatSupport> ByExtension =
        All.SelectMany(f => f.Extensions.Select(e => (e, f))).ToDictionary(p => p.e, p => p.f, StringComparer.OrdinalIgnoreCase);

    /// <summary>The row of a file by its extension, or null when the format is unknown.</summary>
    public static FormatSupport? For(string path) => ByExtension.GetValueOrDefault(Path.GetExtension(path));

    /// <summary>"completo", "parcial" or "fallback externo".</summary>
    public static string Label(SupportLevel level) => level switch
    {
        SupportLevel.Complete => "completo",
        SupportLevel.Partial => "parcial",
        _ => "fallback externo"
    };
}

/// <summary>What a file's first bytes say it is, whatever its name (a renamed binary must never crash a reader).</summary>
public enum FileSignature { Unknown, Png, Jpeg, Gif, WebP, Bmp, Pdf, Zip, Rar, SevenZip, Gzip, Rtf, Mp3, Ogg, Flac, Wav, Mp4, Matroska, Executable, Text }

public static class FileSignatures
{
    public static FileSignature Detect(ReadOnlySpan<byte> b)
    {
        if (b.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G'])) return FileSignature.Png;
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return FileSignature.Jpeg;
        if (b.StartsWith("GIF8"u8)) return FileSignature.Gif;
        if (b.Length >= 12 && b.StartsWith("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8)) return FileSignature.WebP;
        if (b.Length >= 12 && b.StartsWith("RIFF"u8) && b[8..12].SequenceEqual("WAVE"u8)) return FileSignature.Wav;
        if (b.Length >= 26 && b.StartsWith("BM"u8)) return FileSignature.Bmp;
        if (b.StartsWith("%PDF"u8)) return FileSignature.Pdf;
        if (b.StartsWith("PK\u0003\u0004"u8) || b.StartsWith("PK\u0005\u0006"u8)) return FileSignature.Zip;
        if (b.StartsWith("Rar!\u001a\u0007"u8)) return FileSignature.Rar;
        if (b.StartsWith((ReadOnlySpan<byte>)[(byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C])) return FileSignature.SevenZip;
        if (b.Length >= 2 && b[0] == 0x1F && b[1] == 0x8B) return FileSignature.Gzip;
        if (b.StartsWith("{\\rtf"u8)) return FileSignature.Rtf;
        if (b.StartsWith("ID3"u8) || b.Length >= 2 && b[0] == 0xFF && (b[1] & 0xE0) == 0xE0 && b[1] != 0xFE) return FileSignature.Mp3;
        if (b.StartsWith("OggS"u8)) return FileSignature.Ogg;
        if (b.StartsWith("fLaC"u8)) return FileSignature.Flac;
        if (b.Length >= 12 && b[4..8].SequenceEqual("ftyp"u8)) return FileSignature.Mp4;
        if (b.StartsWith((ReadOnlySpan<byte>)[0x1A, 0x45, 0xDF, 0xA3])) return FileSignature.Matroska;
        if (b.StartsWith("MZ"u8) || b.StartsWith((ReadOnlySpan<byte>)[0x7F, (byte)'E', (byte)'L', (byte)'F'])) return FileSignature.Executable;
        return TextDecoding.LooksLikeText(b) ? FileSignature.Text : FileSignature.Unknown;
    }

    public static bool IsImage(FileSignature signature) => signature is FileSignature.Png or FileSignature.Jpeg or FileSignature.Gif or FileSignature.WebP or FileSignature.Bmp;

    /// <summary>A short name of the detected content, for the info panel ("parece ser um PDF").</summary>
    public static string Describe(FileSignature signature) => signature switch
    {
        FileSignature.Png => "imagem PNG", FileSignature.Jpeg => "imagem JPEG", FileSignature.Gif => "imagem GIF", FileSignature.WebP => "imagem WebP",
        FileSignature.Bmp => "imagem BMP", FileSignature.Pdf => "PDF", FileSignature.Zip => "arquivo ZIP", FileSignature.Rar => "arquivo RAR",
        FileSignature.SevenZip => "arquivo 7z", FileSignature.Gzip => "arquivo GZIP", FileSignature.Rtf => "documento RTF", FileSignature.Mp3 => "áudio MP3",
        FileSignature.Ogg => "áudio ou vídeo Ogg", FileSignature.Flac => "áudio FLAC", FileSignature.Wav => "áudio WAV", FileSignature.Mp4 => "mídia MP4",
        FileSignature.Matroska => "vídeo Matroska/WebM", FileSignature.Executable => "programa executável", FileSignature.Text => "texto",
        _ => "conteúdo binário desconhecido"
    };
}
