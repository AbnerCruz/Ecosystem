using System.Globalization;
using System.IO.Compression;
using System.Text;
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

    /// <summary>Busca offline por título, tipo, autoria, licença, atribuição e tags; termos combinados.</summary>
    public IReadOnlyList<LibraryAssetVersion> Search(string? query)
    {
        var entries = List();
        if (string.IsNullOrWhiteSpace(query)) return entries;
        var terms = NormalizeSearch(query).Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return entries.Where(asset =>
        {
            var searchable = NormalizeSearch(string.Join(" ", new[] { asset.Title, asset.Kind.ToString(),
                asset.Author, asset.License, asset.Attribution }.Concat(asset.Tags)));
            return terms.All(term => searchable.Contains(term, StringComparison.Ordinal));
        }).ToArray();
    }

    // Keep the original metadata untouched; normalize only the search index and query.
    // This also matches canonically decomposed Unicode typed by some mobile keyboards.
    private static string NormalizeSearch(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(char.ToLowerInvariant(character));
        return result.ToString().Normalize(NormalizationForm.FormC);
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
    /// Previews destination path collisions without writing files. The actual import
    /// repeats every check because another editor may change the project meanwhile.
    /// </summary>
    public IReadOnlyList<string> FindImportConflicts(string assetId, int version, LunetProject destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var manifest = Find(assetId, version);
        var conflicts = new List<string>();
        foreach (var file in manifest.Files)
        {
            ValidatePath(file.Path);
            destination.EnsureInsideProject(file.Path);
            EnsureNoLinks(destination.Directory, file.Path);
            var target = Path.Combine(destination.Directory,
                file.Path.Replace('/', Path.DirectorySeparatorChar));
            // A file in an ancestor directory also prevents import.
            var parent = Path.GetDirectoryName(target);
            var blockedByParent = false;
            while (parent is not null && parent.Length >= destination.Directory.Length)
            {
                if (File.Exists(parent)) { blockedByParent = true; break; }
                if (string.Equals(parent, destination.Directory, StringComparison.Ordinal)) break;
                parent = Path.GetDirectoryName(parent);
            }
            if (blockedByParent || File.Exists(target) || Directory.Exists(target))
                conflicts.Add(file.Path);
        }
        return conflicts;
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

    /// <summary>
    /// Verifies all stored files before a user imports or exports a version.
    /// This is read-only: damaged files remain available for manual recovery.
    /// </summary>
    public void VerifyIntegrity(string assetId, int version)
    {
        var manifest = Find(assetId, version);
        var root = Path.Combine(VersionDirectory(assetId, version), "files");
        foreach (var file in manifest.Files)
        {
            ValidatePath(file.Path);
            EnsureNoLinks(root, file.Path);
            var source = FilePath(root, file.Path);
            if (!File.Exists(source) || new FileInfo(source).Length != file.Length ||
                !HashFile(source).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Arquivo ausente ou corrompido: " + file.Path);
        }
    }

    /// <summary>
    /// Exports a self-contained immutable version with its manifest and source/derived files.
    /// The destination is never overwritten; a failed export leaves no partial ZIP behind.
    /// </summary>
    public void ExportZip(string assetId, int version, string destinationZip)
    {
        var manifest = Find(assetId, version);
        var sourceRoot = Path.Combine(VersionDirectory(assetId, version), "files");
        var verified = new List<(string Path, string Source)>();
        foreach (var file in manifest.Files)
        {
            ValidatePath(file.Path);
            EnsureNoLinks(sourceRoot, file.Path);
            var source = FilePath(sourceRoot, file.Path);
            if (!File.Exists(source) || new FileInfo(source).Length != file.Length ||
                HashFile(source) != file.Sha256)
                throw new InvalidDataException("Asset da biblioteca corrompido: " + file.Path);
            verified.Add((file.Path, source));
        }

        var destination = Path.GetFullPath(destinationZip);
        if (File.Exists(destination) || Directory.Exists(destination))
            throw new IOException("O ZIP de destino já existe.");
        var parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        var temporary = Path.Combine(parent, ".lunet-export-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                var metadata = archive.CreateEntry("asset.json", CompressionLevel.Optimal);
                using (var writer = new StreamWriter(metadata.Open()))
                    writer.Write(JsonSerializer.Serialize(manifest, JsonOptions));
                foreach (var file in verified)
                {
                    // Hash the actual bytes written into the archive, not a separate pre-copy read:
                    // a source can change between preflight and compression.
                    var expected = manifest.Files.Single(f => f.Path == file.Path);
                    var entry = archive.CreateEntry("files/" + file.Path, CompressionLevel.Optimal);
                    using var input = File.OpenRead(file.Source);
                    using var output = entry.Open();
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    long copied = 0;
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        hash.AppendData(buffer, 0, count);
                        output.Write(buffer, 0, count);
                        copied += count;
                    }
                    if (copied != expected.Length ||
                        !Convert.ToHexString(hash.GetHashAndReset()).Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Asset alterado durante a exportação: " + file.Path);
                }
            }
            File.Move(temporary, destination, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
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
        if (item.Id is null || !Guid.TryParseExact(item.Id, "D", out _))
            throw new InvalidDataException("ID inválido no manifesto de asset; dados preservados.");
        if (item.Version < 1 || VersionDirectory(item.Id, item.Version) != dir ||
            !Enum.IsDefined(item.Kind) || item.CreatedUtc == default ||
            item.Files is null || item.Files.Length == 0 ||
            item.Files.Any(f => f is null || f.Path is null || f.Sha256 is null) ||
            item.Files.GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw new InvalidDataException("Manifesto de asset inconsistente, mantido sem alteração.");
        // The manifest is local persisted input, not trusted code. Reject malformed metadata
        // before Android renders it or the import workflow reads it.
        if (item.Title is null || item.License is null || item.Author is null ||
            item.Attribution is null || item.Tags is null || item.Tags.Any(t => t is null))
            throw new InvalidDataException("Metadados incompletos no manifesto de asset; dados preservados.");
        try
        {
            Bounded(item.Title, 160, "Título");
            Bounded(item.Author, 120, "Autor", true);
            Bounded(item.License, 120, "Licença");
            Bounded(item.Attribution, 500, "Atribuição", true);
            if (item.Tags.Length > 30)
                throw new ArgumentException("Até 30 tags por asset.");
            foreach (var tag in item.Tags) Bounded(tag, 60, "Tag", true);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("Metadados inválidos no manifesto de asset; dados preservados.", ex);
        }
        foreach (var file in item.Files)
        {
            ValidatePath(file.Path);
            if (file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit) || file.Length < 0)
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
