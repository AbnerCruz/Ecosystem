using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using IoDirectory = System.IO.Directory;

namespace Lunet.Core;

/// <summary>A private, disposable snapshot of the exact source, manifest and assets used by a local launch.</summary>
public sealed class PlayableRevision : IDisposable
{
    private PlayableRevision(string directory, string key, Guid gameId, ProjectManifest manifest, IReadOnlyList<ProjectSource> sources)
    {
        Directory = directory;
        Key = key;
        GameId = gameId;
        Manifest = manifest;
        Sources = sources;
    }

    public string Directory { get; }
    public string Key { get; }
    public Guid GameId { get; }
    public ProjectManifest Manifest { get; }
    public IReadOnlyList<ProjectSource> Sources { get; }

    private static bool IsSource(string path) => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("Tests/", StringComparison.OrdinalIgnoreCase);

    private static bool IsInput(string path) => IsSource(path) ||
        path.Equals(ProjectStore.ManifestFileName, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("Content/", StringComparison.Ordinal) || path.StartsWith("Assets/", StringComparison.Ordinal);

    /// <summary>Capture bytes once; compile and load content from this same snapshot, never from the mutable project.</summary>
    public static PlayableRevision Capture(LunetProject project, string stagingRoot, string toolchainIdentity, CancellationToken cancellation = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolchainIdentity);
        ArgumentNullException.ThrowIfNull(project);
        var root = project.Files.LocalDirectory is { } disk ? Path.GetFullPath(disk) : null;
        var staging = Path.GetFullPath(stagingRoot);
        if (root is not null && (staging.Equals(root, StringComparison.Ordinal) ||
            staging.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            throw new ProjectException("A captura deve ficar fora do projeto.");
        string[] Inputs() => (root is null ? project.ListFiles() : Enumerate(root, "", cancellation))
            .Where(IsInput).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        Stream Open(string path) => root is null
            ? new MemoryStream(project.ReadBytes(path), writable: false)
            : File.OpenRead(Path.Combine(root, path));
        var folder = Path.Combine(staging, Guid.NewGuid().ToString("N"));
        IoDirectory.CreateDirectory(folder);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Append(hash, Encoding.UTF8.GetBytes("lunet-playable-v1"));
            Append(hash, Encoding.UTF8.GetBytes(toolchainIdentity));
            var inputs = Inputs();
            var checksums = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            // Content source expects a directory even for a blank project.
            IoDirectory.CreateDirectory(Path.Combine(folder, "Content"));
            IoDirectory.CreateDirectory(Path.Combine(folder, "Assets"));
            foreach (var relative in inputs)
            {
                cancellation.ThrowIfCancellationRequested();
                var target = Path.Combine(folder, relative);
                IoDirectory.CreateDirectory(Path.GetDirectoryName(target)!);
                using (var input = Open(relative))
                using (var output = File.Create(target))
                    input.CopyTo(output);
                using var captured = File.OpenRead(target);
                Append(hash, Encoding.UTF8.GetBytes(relative));
                var checksum = SHA256.HashData(captured);
                checksums.Add(relative, checksum);
                Append(hash, checksum);
            }
            // Reject a changed file list or bytes during capture; never silently combine revisions.
            var currentInputs = Inputs();
            if (!inputs.SequenceEqual(currentInputs, StringComparer.Ordinal))
                throw new ProjectException("Os arquivos do jogo mudaram durante a captura. Tente novamente.");
            foreach (var relative in currentInputs)
            {
                cancellation.ThrowIfCancellationRequested();
                using var current = Open(relative);
                if (!CryptographicOperations.FixedTimeEquals(checksums[relative], SHA256.HashData(current)))
                    throw new ProjectException("O jogo mudou durante a captura. Tente novamente.");
            }
            var manifest = ProjectManifest.Parse(File.ReadAllText(Path.Combine(folder, ProjectStore.ManifestFileName)));
            if (!Guid.TryParse(manifest.GameId, out var gameId) || gameId == Guid.Empty ||
                gameId != (Guid.TryParse(project.Manifest.GameId, out var expected) ? expected : Guid.Empty))
                throw new ProjectException("A identidade do jogo é inválida ou mudou durante a captura.");
            if (manifest.Orientation is not ("portrait" or "landscape"))
                throw new ProjectException("Orientação inválida em lunet.json.");
            var sources = inputs.Where(IsSource).Select(p => new ProjectSource(p, File.ReadAllText(Path.Combine(folder, p)))).ToArray();
            cancellation.ThrowIfCancellationRequested();
            return new PlayableRevision(folder, Convert.ToHexString(hash.GetHashAndReset()), gameId, manifest, Array.AsReadOnly(sources));
        }
        catch
        {
            Delete(folder);
            throw;
        }
    }

    private static IEnumerable<string> Enumerate(string root, string relative, CancellationToken cancellation)
    {
        var folder = Path.Combine(root, relative);
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
            throw new ProjectException("Links de diretório não são aceitos na captura do jogo.");
        foreach (var file in IoDirectory.EnumerateFiles(folder))
        {
            cancellation.ThrowIfCancellationRequested();
            var name = Path.GetFileName(file);
            if (name.EndsWith(".lunet-tmp", StringComparison.Ordinal)) continue;
            var path = relative + name;
            if (!IsInput(path)) continue;
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new ProjectException("Links de arquivo não são aceitos na captura do jogo.");
            yield return path;
        }
        foreach (var directory in IoDirectory.EnumerateDirectories(folder))
        {
            cancellation.ThrowIfCancellationRequested();
            var name = Path.GetFileName(directory);
            if (name.Equals(".lunet", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var path in Enumerate(root, relative + name + "/", cancellation))
                yield return path;
        }
    }

    private static void Append(IncrementalHash hash, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, data.Length);
        hash.AppendData(length);
        hash.AppendData(data);
    }

    public void Dispose() => Delete(Directory);

    private static void Delete(string directory)
    {
        if (!IoDirectory.Exists(directory)) return;
        try { IoDirectory.Delete(directory, recursive: true); }
        catch (IOException) { /* A private, unreferenced staging directory is disposable. */ }
        catch (UnauthorizedAccessException) { /* Cleanup can be retried by the caller's cache maintenance. */ }
    }
}

/// <summary>Disposable build artifacts, scoped by GameId and exact revision. A single atomic file carries its checksum.</summary>
public sealed class PlayableBuildCache(string root)
{
    private string PathFor(Guid gameId, string key)
    {
        if (gameId == Guid.Empty || key.Length != 64 || !key.All(Uri.IsHexDigit))
            throw new ArgumentException("Identidade de compilação inválida.");
        return Path.Combine(Path.GetFullPath(root), gameId.ToString("N"), key.ToUpperInvariant() + ".bin");
    }

    public byte[]? TryRead(Guid gameId, string key)
    {
        var path = PathFor(gameId, key);
        try
        {
            var envelope = File.ReadAllBytes(path);
            if (envelope.Length <= 32) return null;
            var bytes = envelope.AsSpan(32).ToArray();
            return CryptographicOperations.FixedTimeEquals(envelope.AsSpan(0, 32), Checksum(gameId, key, bytes)) ? bytes : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    private static byte[] Checksum(Guid gameId, string key, byte[] assembly)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(gameId.ToByteArray());
        hash.AppendData(Encoding.ASCII.GetBytes(key.ToUpperInvariant()));
        hash.AppendData(assembly);
        return hash.GetHashAndReset();
    }

    public void Publish(Guid gameId, string key, byte[] assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (assembly.Length == 0) throw new ArgumentException("Assembly vazio.", nameof(assembly));
        var path = PathFor(gameId, key);
        IoDirectory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temp))
            {
                stream.Write(Checksum(gameId, key, assembly));
                stream.Write(assembly);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
