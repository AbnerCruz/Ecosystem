using System.Globalization;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// The kinds of the vault's files as the 1.8.4 tells them apart for the preview (app.js EXT_IMG … EXT_ZIP,
/// classificarArquivo, rotuloClasse) and how it writes a size (formatBytes).
/// </summary>
public static partial class VaultFileKinds
{
    public const string Image = "image", Audio = "audio", Video = "video", Pdf = "pdf", Html = "html", Zip = "zip", Other = "other";

    /// <summary>classificarArquivo(rel).</summary>
    public static string Classify(string path) =>
        ImageRe().IsMatch(path) ? Image : AudioRe().IsMatch(path) ? Audio : VideoRe().IsMatch(path) ? Video
        : PdfRe().IsMatch(path) ? Pdf : HtmlRe().IsMatch(path) ? Html : ZipRe().IsMatch(path) ? Zip : Other;

    /// <summary>rotuloClasse(c).</summary>
    public static string Label(string kind) => kind switch
    {
        Image => "Imagem", Audio => "Áudio", Video => "Vídeo", Pdf => "PDF", Html => "HTML", Zip => "Arquivo compactado", _ => "Arquivo"
    };

    /// <summary>formatBytes(n): "812 B", "1.5 KB", "12 MB" (one decimal below 10, from KB up).</summary>
    public static string FormatBytes(long n)
    {
        string[] u = ["B", "KB", "MB", "GB"];
        int i = 0;
        double v = n;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        var number = v < 10 && i > 0 ? v.ToString("0.0", CultureInfo.InvariantCulture) : LegacyJsMath.Round(v).ToString(CultureInfo.InvariantCulture);
        return number + " " + u[i];
    }

    /// <summary>The MIME type a file of this name is opened with outside the app.</summary>
    public static string Mime(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml",
        ".mp3" => "audio/mpeg", ".wav" => "audio/wav", ".ogg" => "audio/ogg", ".m4a" => "audio/mp4", ".aac" => "audio/aac", ".flac" => "audio/flac",
        ".mp4" or ".m4v" => "video/mp4", ".webm" => "video/webm", ".mov" => "video/quicktime", ".mkv" => "video/x-matroska", ".avi" => "video/x-msvideo",
        ".pdf" => "application/pdf", ".html" or ".htm" or ".xhtml" => "text/html", ".zip" => "application/zip",
        ".txt" or ".md" or ".markdown" or ".csv" or ".css" or ".js" or ".json" or ".yaml" or ".yml" => "text/plain",
        _ => "application/octet-stream"
    };

    [GeneratedRegex(@"\.(png|jpe?g|gif|webp|svg)$", RegexOptions.IgnoreCase)] private static partial Regex ImageRe();
    [GeneratedRegex(@"\.(mp3|wav|ogg|m4a|aac|flac)$", RegexOptions.IgnoreCase)] private static partial Regex AudioRe();
    [GeneratedRegex(@"\.(mp4|webm|mov|mkv|avi|m4v)$", RegexOptions.IgnoreCase)] private static partial Regex VideoRe();
    [GeneratedRegex(@"\.pdf$", RegexOptions.IgnoreCase)] private static partial Regex PdfRe();
    [GeneratedRegex(@"\.(html?|xhtml)$", RegexOptions.IgnoreCase)] private static partial Regex HtmlRe();
    [GeneratedRegex(@"\.(zip|7z|rar|tar|gz)$", RegexOptions.IgnoreCase)] private static partial Regex ZipRe();
}
