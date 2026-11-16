using System.Security.Cryptography;
using System.Text.Json;

namespace Lunet.Core;

/// <summary>Arquivo versionado da biblioteca, preservando a localização original dentro do jogo.</summary>
public sealed record LibraryAssetFile(string Path, string Sha256, long Length);

/// <summary>Manifesto imutável de uma versão; importação em jogos é sempre uma cópia, nunca um link global.</summary>
public sealed record LibraryAssetVersion(
    string Id, int Version, string Title, ProjectAssetKind Kind, string Author, string License,
    string Attribution, string[] Tags, LibraryAssetFile[] Files, DateTime CreatedUtc);

/// <summary>
/// Biblioteca local e versionada de assets do usuário. Não faz rede nem altera um projeto ao publicar.
/// Um projeto importador recebe cópias verificadas e nunca será afetado por revisões posteriores da Library.
/// </summary>
public sealed class ReusableAssetLibrary
{
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ReusableAssetLibrary(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public IReadOnlyList<LibraryAssetVersion> List()
    {
        if (!Directory.Exists(_root)) return [];
        return Directory.EnumerateDirectories(_root)
            .SelectMany(id => Directory.EnumerateDirectories(id, "v*"))
            .Where(dir => File.Exists(Path.Combine(dir, "asset.json")))
            .Select(dir => ReadManifest(dir))
            .OrderBy(v => v.Title, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(v => v.Version).ToArray();
    }

    public LibraryAssetVersion Find(string id, int version)
    {
        ValidateId(id);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        var dir = VersionDirectory(id, version);
        if (!File.Exists(Path.Combine(dir, "asset.json")))
            throw new FileNotFoundException("Versão do asset não encontrada.");
        return ReadManifest(dir);
    }

    public LibraryAssetVersion Publish(LunetProject project, string primaryPath, string title,
        string author, string license, string attribution = "", IEnumerable<string>? tags = null,
        string? existingAssetId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ValidatePath(primaryPath);
        var asset = ProjectAssetCatalog.Build(project.ListFiles())
            .SingleOrDefault(a => string.Equals(a.Path, primaryPath, StringComparison.Ordinal));
        if (asset is null) throw new ArgumentException("Selecione um asset real de Assets/ ou Content/.", nameof(primaryPath));

        var paths = new List<string> { asset.Path };
        if (asset.PreviewPath is { } preview && !paths.Contains(preview, StringComparer.Ordinal))
            paths.Add(preview);
        if (primaryPath.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase) ||
            primaryPath.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase))
        {
            var wave = primaryPath[..primaryPath.LastIndexOf('.', primaryPath.LastIndexOf('.') - 1)] + ".wav";
            if (project.ListFiles().Contains(wave, StringComparer.Ordinal)) paths.Add(wave);
        }

        var id = existingAssetId ?? Guid.NewGuid().ToString("D");
        ValidateId(id);
        int version = 1;
        if (existingAssetId is not null)
        {
            var versions = List().Where(a => a.Id == id).ToArray();
            if (versions.Length == 0) throw new InvalidOperationException("Asset de origem não encontrado.");
            if (versions[0].Kind != asset.Kind)
                throw new InvalidOperationException("Uma revisão não pode mudar o tipo original do asset.");
            version = versions.Max(a => a.Version) + 1;
        }

        var safeTags = (tags ?? []).Select(t => Bounded(t, 60, "Tag", true))
            .Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (safeTags.Length > 30) throw new ArgumentException("Até 30 tags por asset.");
        var manifest = new LibraryAssetVersion(id, version,
            Bounded(title, 160, "Título"), asset.Kind,
            Bounded(author, 120, "Autor", true), Bounded(license, 120, "Licença"),
            Bounded(attribution, 500, "Atribuição", true), safeTags, [], DateTime.UtcNow);

        var parent = Path.Combine(_root, id);
        Directory.CreateDirectory(parent);
        var target = VersionDirectory(id, version);
        if (Directory.Exists(target)) throw new IOException("Esta versão do asset já existe.");
        var staging = Path.Combine(parent, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var files = new List<LibraryAssetFile>();
            foreach (var path in paths)
            {
                ValidatePath(path);
                project.EnsureInsideProject(path);
                var original = Path.Combine(project.Directory, path.Replace('/', Path.DirectorySeparatorChar));
                EnsureNoLinks(project.Directory, path);
                if (!File.Exists(original)) throw new FileNotFoundException("Arquivo de origem não encontrado.", path);
                var before = HashFile(original);
                var copy = FilePath(Path.Combine(staging, "files"), path);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(original, copy, overwrite: false);
                var after = HashFile(copy);
                if (before != after || HashFile(original) != before)
                    throw new IOException("O asset foi alterado durante a publicação; tente novamente.");
                files.Add(new LibraryAssetFile(path, after, new FileInfo(copy).Length));
            }
            manifest = manifest with { Files = files.ToArray() };
            File.WriteAllText(Path.Combine(staging, "asset.json"), JsonSerializer.Serialize(manifest, JsonOptions));
            Directory.Move(staging, target);
            return manifest;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    /// <summary>
    /// Pré-valida a versão inteira; rejeita colisões sem sobrescrita. Staging e rollback impedem
    /// importação parcial em erros de IO. Uma edição concorrente nunca é apagada pelo rollback.
    /// </summary>
    public IReadOnlyList<string> ImportToProject(string assetId, int version, LunetProject destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var manifest = Find(assetId, version);
        var root = VersionDirectory(assetId, version);
        var checkedFiles = new List<(string Relative, string Source, string Target, string Hash)>();
        foreach (var item in manifest.Files)
        {
            ValidatePath(item.Path);
            destination.EnsureInsideProject(item.Path);
            EnsureNoLinks(destination.Directory, item.Path);
            var target = Path.Combine(destination.Directory, item.Path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(target) || Directory.Exists(target))
                throw new IOException("Conflito de importação: " + item.Path + " já existe. Nenhum arquivo foi alterado.");
            var source = FilePath(Path.Combine(root, "files"), item.Path);
            EnsureNoLinks(Path.Combine(root, "files"), item.Path);
            if (!File.Exists(source) || new FileInfo(source).Length != item.Length || HashFile(source) != item.Sha256)
                throw new InvalidDataException("Asset da biblioteca corrompido: " + item.Path);
            checkedFiles.Add((item.Path, source, target, item.Sha256));
        }

        EnsureNoLinks(destination.Directory, ".lunet");
        var staging = Path.Combine(destination.Directory, ".lunet", "import-" + Guid.NewGuid().ToString("N"));
        var created = new List<(string Path, string Hash)>();
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var item in checkedFiles)
            {
                var temporary = FilePath(staging, item.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
                File.Copy(item.Source, temporary, overwrite: false);
                if (HashFile(temporary) != item.Hash)
                    throw new IOException("Erro de integridade durante o preparo da importação.");
            }
            foreach (var item in checkedFiles)
            {
                EnsureNoLinks(destination.Directory, item.Relative);
                Directory.CreateDirectory(Path.GetDirectoryName(item.Target)!);
                File.Move(FilePath(staging, item.Relative), item.Target, overwrite: false);
                created.Add((item.Target, item.Hash));
            }
            return checkedFiles.Select(x => x.Relative).ToArray();
        }
        catch
        {
            foreach (var item in created)
                if (File.Exists(item.Path) && HashFile(item.Path) == item.Hash)
                    File.Delete(item.Path);
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private LibraryAssetVersion ReadManifest(string dir)
    {
        LibraryAssetVersion item;
        try
        {
            item = JsonSerializer.Deserialize<LibraryAssetVersion>(File.ReadAllText(Path.Combine(dir, "asset.json")))
                ?? throw new InvalidDataException("Manifesto de asset vazio.");
        }
        catch (JsonException ex) { throw new InvalidDataException("Manifesto de asset inválido; dados preservados.", ex); }
        ValidateId(item.Id);
        if (item.Version < 1 || VersionDirectory(item.Id, item.Version) != dir ||
            item.Files is null || item.Files.Length == 0 ||
            item.Files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new InvalidDataException("Manifesto de asset inconsistente, mantido sem alteração.");
        foreach (var file in item.Files)
        {
            ValidatePath(file.Path);
            if (file.Sha256.Length != 64 || file.Length < 0)
                throw new InvalidDataException("Hash ou tamanho inválido na biblioteca.");
        }
        return item;
    }

    private static string FilePath(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private string VersionDirectory(string id, int version) => Path.Combine(_root, id, "v" + version);

    private static string HashFile(string file)
    {
        using var input = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    private static void ValidateId(string id)
    {
        if (!Guid.TryParseExact(id, "D", out _)) throw new ArgumentException("ID de asset inválido.");
    }

    private static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':') ||
            path.StartsWith('/') || Path.IsPathRooted(path) ||
            !(path.StartsWith("Assets/", StringComparison.Ordinal) ||
              path.StartsWith("Content/", StringComparison.Ordinal)) ||
            path.Split('/').Any(part => part.Length == 0 || part is "." or ".." || part.StartsWith('.')))
            throw new InvalidDataException("Caminho de asset inválido: " + path);
    }

    private static void EnsureNoLinks(string root, string relative)
    {
        var current = root;
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Links simbólicos não podem participar da biblioteca.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static string Bounded(string? value, int max, string label, bool optional = false)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length > max || (!optional && trimmed.Length == 0))
            throw new ArgumentException(label + " inválido.");
        return trimmed;
    }
}
