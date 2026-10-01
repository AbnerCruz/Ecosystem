using System.IO.Compression;

namespace Lunet.Core;

/// <summary>Um projeto Lunet aberto: pasta comum com <c>lunet.json</c> e arquivos do usuário.</summary>
public sealed class LunetProject
{
    private static readonly string[] IgnoredDirectories = [".lunet", "bin", "obj", ".git"];
    private const string TemporarySuffix = ".lunet-tmp";

    internal LunetProject(string directory, ProjectManifest manifest)
    {
        Directory = directory;
        Manifest = manifest;
    }

    public string Directory { get; }
    public ProjectManifest Manifest { get; }
    public string Name => Manifest.Name;

    /// <summary>Caminhos relativos (com '/') de todos os arquivos do projeto, exceto pastas de cache/build.</summary>
    public IReadOnlyList<string> ListFiles()
    {
        var result = new List<string>();
        Walk(Directory, "", result);
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static void Walk(string absolute, string relative, List<string> result)
    {
        foreach (var file in System.IO.Directory.EnumerateFiles(absolute))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(TemporarySuffix, StringComparison.Ordinal)) continue;
            result.Add(relative + name);
        }
        foreach (var directory in System.IO.Directory.EnumerateDirectories(absolute))
        {
            var name = Path.GetFileName(directory);
            if (IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            Walk(directory, relative + name + "/", result);
        }
    }

    public string ReadText(string relativePath) => File.ReadAllText(Resolve(relativePath));

    public void WriteText(string relativePath, string text)
    {
        var path = Resolve(relativePath);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.WriteAllText(path, text);
    }

    public void CreateFile(string relativePath, string text = "")
    {
        if (File.Exists(Resolve(relativePath))) throw new ProjectException("Esse arquivo já existe.");
        WriteText(relativePath, text);
    }

    /// <summary>Copia um arquivo binário (imagem, áudio) para o projeto de forma atômica.</summary>
    public string Import(string relativeDirectory, string fileName, Stream source)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName.StartsWith('.')) throw new ProjectException("Nome de arquivo inválido.");
        var relative = relativeDirectory.TrimEnd('/') + "/" + safeName;
        var path = Resolve(relative);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + TemporarySuffix;
        using (var output = File.Create(temporary)) source.CopyTo(output);
        File.Move(temporary, path, overwrite: true);
        return relative;
    }

    /// <summary>Árvore de pastas e arquivos para o Explorer (pastas de cache/build ocultas), pastas primeiro.</summary>
    public IReadOnlyList<ExplorerEntry> ListTree()
    {
        var result = new List<ExplorerEntry>();
        WalkTree(Directory, "", 0, result);
        return result;
    }

    private static void WalkTree(string absolute, string relative, int depth, List<ExplorerEntry> result)
    {
        foreach (var directory in System.IO.Directory.EnumerateDirectories(absolute).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(directory);
            if (IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(new ExplorerEntry(relative + name, name, depth, true));
            WalkTree(directory, relative + name + "/", depth + 1, result);
        }
        foreach (var file in System.IO.Directory.EnumerateFiles(absolute).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(TemporarySuffix, StringComparison.Ordinal)) continue;
            result.Add(new ExplorerEntry(relative + name, name, depth, false));
        }
    }

    public void CreateDirectory(string relativePath)
    {
        var path = Resolve(relativePath);
        if (System.IO.Directory.Exists(path) || File.Exists(path)) throw new ProjectException("Já existe algo com esse nome.");
        System.IO.Directory.CreateDirectory(path);
    }

    /// <summary>Renomeia ou move um arquivo/pasta dentro do projeto.</summary>
    public void Rename(string fromRelative, string toRelative)
    {
        if (IsProtected(fromRelative)) throw new ProjectException("Este item não pode ser renomeado.");
        var from = Resolve(fromRelative);
        var to = Resolve(toRelative);
        if (File.Exists(to) || System.IO.Directory.Exists(to)) throw new ProjectException("Já existe algo com esse nome.");
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        if (System.IO.Directory.Exists(from)) System.IO.Directory.Move(from, to);
        else if (File.Exists(from)) File.Move(from, to);
        else throw new ProjectException("Item não encontrado.");
    }

    /// <summary>Apaga arquivo ou pasta (recursivamente). <c>lunet.json</c> e a entrada do projeto raiz não podem ser apagados.</summary>
    public void Delete(string relativePath)
    {
        if (IsProtected(relativePath)) throw new ProjectException("Este item não pode ser apagado.");
        var path = Resolve(relativePath);
        if (System.IO.Directory.Exists(path)) System.IO.Directory.Delete(path, recursive: true);
        else if (File.Exists(path)) File.Delete(path);
        else throw new ProjectException("Item não encontrado.");
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
        File.Delete(Resolve(relativePath));
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
            using var input = File.OpenRead(Resolve(relative));
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }

    /// <summary>Lança <see cref="ProjectException"/> se o caminho sair da pasta do projeto.</summary>
    internal void EnsureInsideProject(string relativePath) => Resolve(relativePath);

    /// <summary>Resolve um caminho relativo garantindo que ele fique dentro da pasta do projeto.</summary>
    private string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new ProjectException("Caminho inválido.");
        var full = Path.GetFullPath(Path.Combine(Directory, relativePath));
        var root = Directory.EndsWith(Path.DirectorySeparatorChar) ? Directory : Directory + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.Ordinal)) throw new ProjectException("Caminho fora do projeto.");
        return full;
    }
}

/// <summary>Arquivo C# do projeto: caminho relativo e conteúdo.</summary>
public sealed record ProjectSource(string Path, string Text);

/// <summary>Item do Explorer: caminho relativo com '/', nome, profundidade na árvore e se é pasta.</summary>
public sealed record ExplorerEntry(string Path, string Name, int Depth, bool IsDirectory);
