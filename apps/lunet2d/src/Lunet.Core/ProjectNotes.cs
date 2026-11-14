using System.Globalization;
using System.Text;

namespace Lunet.Core;

public enum NoteKind { Text, Sketch, Image, File }

public sealed record ProjectNote(string Path, string Title, NoteKind Kind, DateTime ModifiedUtc);

/// <summary>
/// Notes and sketches of a project (LUNET-485): Markdown and PNG files in <c>Notes/</c>, ordinary project files
/// that travel with ZIP export and Git, are readable by the AI and never compiled.
/// </summary>
public static class ProjectNotes
{
    public const string Folder = "Notes";

    public static IReadOnlyList<ProjectNote> List(LunetProject project)
    {
        var result = new List<ProjectNote>();
        foreach (var path in project.ListFiles().Where(p => p.StartsWith(Folder + "/", StringComparison.Ordinal)))
        {
            var full = System.IO.Path.Combine(project.Directory, path);
            var modified = File.GetLastWriteTimeUtc(full);
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                string head;
                try { head = ReadHead(full); } catch (IOException) { head = ""; }
                result.Add(new ProjectNote(path, TitleOf(path, head), NoteKind.Text, modified));
            }
            else if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                result.Add(new ProjectNote(path, System.IO.Path.GetFileNameWithoutExtension(path), NoteKind.Sketch, modified));
            else if (ImageExtensions.Contains(System.IO.Path.GetExtension(path)))
                result.Add(new ProjectNote(path, System.IO.Path.GetFileNameWithoutExtension(path), NoteKind.Image, modified));
            else
                result.Add(new ProjectNote(path, System.IO.Path.GetFileName(path), NoteKind.File, modified));
        }
        return result.OrderByDescending(n => n.ModifiedUtc).ThenBy(n => n.Path, StringComparer.Ordinal).ToArray();
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".webp", ".gif", ".bmp" };
    public const long MaxImportBytes = 64L * 1024 * 1024;

    public static bool IsImage(string path) =>
        path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || ImageExtensions.Contains(System.IO.Path.GetExtension(path));

    /// <summary>
    /// Unique, safe destination in Notes/ for an imported file, keeping a sanitized name and its extension
    /// (e.g. "Meu Desenho (1).JPG" → "Notes/meu-desenho-1.jpg").
    /// </summary>
    public static string ImportPath(LunetProject project, string? originalName)
    {
        var name = System.IO.Path.GetFileName(originalName ?? "") ?? "";
        var extension = System.IO.Path.GetExtension(name).ToLowerInvariant();
        if (extension.Length > 8 || extension.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '.')) extension = "";
        var stem = Slug(System.IO.Path.GetFileNameWithoutExtension(name));
        if (stem == "nota") stem = "anexo";
        return Unique(project, stem, extension.Length == 0 ? ".bin" : extension);
    }

    public static string CreateNote(LunetProject project, string title)
    {
        title = string.IsNullOrWhiteSpace(title) ? "Nova nota" : title.Trim();
        var path = Unique(project, Slug(title), ".md");
        project.WriteText(path, "# " + title + "\n\n");
        return path;
    }

    public static string NewSketchPath(LunetProject project, string title) =>
        Unique(project, Slug(string.IsNullOrWhiteSpace(title) ? "esboco" : title), ".png");

    public static string TitleOf(string path, string content)
    {
        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith("# ", StringComparison.Ordinal)) return trimmed[2..].Trim();
            break;
        }
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    public static string Slug(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }
        var slug = builder.ToString().Trim('-');
        if (slug.Length > 48) slug = slug[..48].TrimEnd('-');
        return slug.Length == 0 ? "nota" : slug;
    }

    private static string Unique(LunetProject project, string slug, string extension)
    {
        var candidate = $"{Folder}/{slug}{extension}";
        for (int i = 2; File.Exists(System.IO.Path.Combine(project.Directory, candidate)); i++)
            candidate = $"{Folder}/{slug}-{i}{extension}";
        return candidate;
    }

    private static string ReadHead(string path)
    {
        using var reader = new StreamReader(path);
        var buffer = new char[512];
        var read = reader.Read(buffer, 0, buffer.Length);
        return new string(buffer, 0, read);
    }
}
