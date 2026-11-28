using System.IO.Compression;

namespace Lunet.Core;

/// <summary>
/// Um projeto Lunet aberto: a pasta do jogo com <c>lunet.json</c> e os arquivos do usuário. Toda leitura e escrita
/// passa por <see cref="Files"/> — o disco no computador, a pasta escolhida (SAF) no Android — sem cópia.
/// </summary>
public sealed class LunetProject
{
    private static readonly string[] IgnoredDirectories = [".lunet", "bin", "obj", ".git"];
    private static readonly object TextWrites = new();
    private const string TemporarySuffix = ".lunet-tmp";
    private static readonly System.Text.UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    internal LunetProject(string directory, ProjectManifest manifest) : this(new DiskProjectFiles(directory), manifest) { }

    internal LunetProject(IProjectFiles files, ProjectManifest manifest)
    {
        Files = files;
        Manifest = manifest;
    }

    /// <summary>Where the game's files are, read and written directly.</summary>
    public IProjectFiles Files { get; }

    /// <summary>The game's folder on this device's disk; empty for a folder reached only through SAF.</summary>
    public string Directory => Files.LocalDirectory ?? "";

    public ProjectManifest Manifest { get; }
    public string Name => Manifest.Name;

    /// <summary>Caminhos relativos (com '/') de todos os arquivos do projeto, exceto pastas de cache/build.</summary>
    public IReadOnlyList<string> ListFiles()
    {
        var result = new List<string>();
        Walk("", result);
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private void Walk(string relative, List<string> result)
    {
        foreach (var entry in Files.List(relative.TrimEnd('/')))
        {
            if (entry.IsDirectory)
            {
                if (IgnoredDirectories.Contains(entry.Name, StringComparer.OrdinalIgnoreCase)) continue;
                Walk(relative + entry.Name + "/", result);
            }
            else if (!entry.Name.EndsWith(TemporarySuffix, StringComparison.Ordinal)) result.Add(relative + entry.Name);
        }
    }

    public string ReadText(string relativePath) => Decode(ReadBytes(relativePath));

    /// <summary>The bytes of a file of the game.</summary>
    public byte[] ReadBytes(string relativePath) => Files.Read(Relative(relativePath));

    public bool Exists(string relativePath) => Files.FileExists(Relative(relativePath));

    private static string Decode(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public void WriteText(string relativePath, string text)
    {
        var path = Relative(relativePath);
        lock (TextWrites) Files.Write(path, Utf8.GetBytes(text));
    }

    /// <summary>Writes the bytes of a file of the game (atomic or verified by the storage).</summary>
    public void WriteBytes(string relativePath, byte[] bytes) => Files.Write(Relative(relativePath), bytes);

    /// <summary>Atomically checks the observed text against other in-process text authors before writing.</summary>
    public bool WriteTextIfUnchanged(string relativePath, string? expected, string text, out string? actual)
    {
        var path = Relative(relativePath);
        lock (TextWrites)
        {
            actual = Files.FileExists(path) ? Decode(Files.Read(path)) : null;
            if (!string.Equals(actual, expected, StringComparison.Ordinal)) return false;
            Files.Write(path, Utf8.GetBytes(text));
            return true;
        }
    }

    public void CreateFile(string relativePath, string text = "")
    {
        if (!WriteTextIfUnchanged(relativePath, null, text, out _))
            throw new ProjectException("Esse arquivo já existe.");
    }

    /// <summary>Copia um arquivo binário (imagem, áudio) para o projeto de forma atômica.</summary>
    public string Import(string relativeDirectory, string fileName, Stream source)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName.StartsWith('.')) throw new ProjectException("Nome de arquivo inválido.");
        var relative = Relative(relativeDirectory.TrimEnd('/') + "/" + safeName);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        Files.Write(relative, buffer.ToArray());
        return relative;
    }

    /// <summary>Árvore de pastas e arquivos para o Explorer (pastas de cache/build ocultas), pastas primeiro.</summary>
    public IReadOnlyList<ExplorerEntry> ListTree()
    {
        var result = new List<ExplorerEntry>();
        WalkTree("", 0, result);
        return result;
    }

    private void WalkTree(string relative, int depth, List<ExplorerEntry> result)
    {
        var entries = Files.List(relative.TrimEnd('/'));
        foreach (var directory in entries.Where(e => e.IsDirectory).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (IgnoredDirectories.Contains(directory.Name, StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(new ExplorerEntry(relative + directory.Name, directory.Name, depth, true));
            WalkTree(relative + directory.Name + "/", depth + 1, result);
        }
        foreach (var file in entries.Where(e => !e.IsDirectory).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (file.Name.EndsWith(TemporarySuffix, StringComparison.Ordinal)) continue;
            result.Add(new ExplorerEntry(relative + file.Name, file.Name, depth, false));
        }
    }

    public void CreateDirectory(string relativePath)
    {
        var path = Relative(relativePath);
        if (Files.DirectoryExists(path) || Files.FileExists(path)) throw new ProjectException("Já existe algo com esse nome.");
        Files.CreateDirectory(path);
    }

    /// <summary>Renomeia ou move um arquivo/pasta dentro do projeto.</summary>
    public void Rename(string fromRelative, string toRelative)
    {
        if (IsProtected(fromRelative)) throw new ProjectException("Este item não pode ser renomeado.");
        var from = Relative(fromRelative);
        var to = Relative(toRelative);
        if (Files.FileExists(to) || Files.DirectoryExists(to)) throw new ProjectException("Já existe algo com esse nome.");
        if (!Files.FileExists(from) && !Files.DirectoryExists(from)) throw new ProjectException("Item não encontrado.");
        Files.Move(from, to);
    }

    /// <summary>Apaga arquivo ou pasta (recursivamente). <c>lunet.json</c> e a entrada do projeto raiz não podem ser apagados.</summary>
    public void Delete(string relativePath)
    {
        if (IsProtected(relativePath)) throw new ProjectException("Este item não pode ser apagado.");
        var path = Relative(relativePath);
        if (!Files.FileExists(path) && !Files.DirectoryExists(path)) throw new ProjectException("Item não encontrado.");
        Files.Delete(path);
    }

    private bool IsProtected(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim('/');
        return normalized.Equals(ProjectStore.ManifestFileName, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(Manifest.EntryPoint, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".lunet", StringComparison.OrdinalIgnoreCase);
    }

    public void DeleteFile(string relativePath)
    {
        if (relativePath.Equals(ProjectStore.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            throw new ProjectException("lunet.json não pode ser apagado.");
        var path = Relative(relativePath);
        if (Files.FileExists(path)) Files.Delete(path);
    }

    /// <summary>Todos os arquivos .cs do projeto (exceto <c>Tests/</c>).</summary>
    public IReadOnlyList<ProjectSource> LoadSources() => ListFiles()
        .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !f.StartsWith("Tests/", StringComparison.OrdinalIgnoreCase))
        .Select(f => new ProjectSource(f, ReadText(f)))
        .ToList();

    /// <summary>Grava o projeto inteiro (sem cache) como ZIP.</summary>
    public void ExportZip(Stream destination)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var relative in ListFiles())
        {
            var entry = archive.CreateEntry(Name + "/" + relative, CompressionLevel.Optimal);
            using var output = entry.Open();
            var bytes = Files.Read(relative);
            output.Write(bytes, 0, bytes.Length);
        }
    }

    /// <summary>Lança <see cref="ProjectException"/> se o caminho sair da pasta do projeto.</summary>
    internal void EnsureInsideProject(string relativePath) => Relative(relativePath);

    /// <summary>
    /// A path relative to the game's folder, with '/', that cannot leave it: no root, no drive, no "..", no empty part.
    /// </summary>
    public static string Relative(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
            throw new ProjectException("Caminho inválido.");
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ProjectException("Caminho inválido.");
        foreach (var part in parts)
            if (part is "." or "..") throw new ProjectException("Caminho fora do projeto.");
        return string.Join('/', parts);
    }
}

/// <summary>Arquivo C# do projeto: caminho relativo e conteúdo.</summary>
public sealed record ProjectSource(string Path, string Text);

/// <summary>Item do Explorer: caminho relativo com '/', nome, profundidade na árvore e se é pasta.</summary>
public sealed record ExplorerEntry(string Path, string Name, int Depth, bool IsDirectory);
