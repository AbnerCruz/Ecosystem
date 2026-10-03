using System.IO.Compression;

namespace Lunet.Core;

/// <summary>Importação local de projetos; o formato continua sendo o ZIP de LunetProject.ExportZip.</summary>
internal static class ProjectZipImporter
{
    internal const long MaxArchiveBytes = 256L * 1024 * 1024;
    internal const long MaxExpandedBytes = 512L * 1024 * 1024;
    internal const int MaxEntries = 10_000;

    internal static LunetProject Import(ProjectStore store, Stream source)
    {
        // A pasta intermediária não tem lunet.json: List nunca expõe importações incompletas.
        var staging = Path.Combine(store.RootDirectory, ".lunet-imports", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var archivePath = Path.Combine(staging, "source.zip");
            using (var copy = File.Create(archivePath)) CopyBounded(source, copy, MaxArchiveBytes);
            using var input = File.OpenRead(archivePath);
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);
            if (zip.Entries.Count > MaxEntries) throw new ProjectException("ZIP tem arquivos demais (máximo 10.000).");

            var entries = zip.Entries.Select(e => (Entry: e, Path: ValidatePath(e.FullName))).ToArray();
            var manifests = entries.Where(e => !e.Path.EndsWith('/') &&
                (e.Path == ProjectStore.ManifestFileName || e.Path.EndsWith("/" + ProjectStore.ManifestFileName, StringComparison.Ordinal))).ToArray();
            if (manifests.Length != 1) throw new ProjectException("ZIP precisa conter exatamente um lunet.json.");
            var manifestPath = manifests[0].Path;
            var prefix = manifestPath[..^ProjectStore.ManifestFileName.Length];
            if (prefix.Count(c => c == '/') > 1)
                throw new ProjectException("lunet.json deve ficar na raiz ou dentro de uma única pasta de projeto.");

            var extracted = Path.Combine(staging, "project");
            Directory.CreateDirectory(extracted);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var (entry, path) in entries)
            {
                if (path == prefix) continue; // entrada opcional da pasta que envolve o projeto
                if (!path.StartsWith(prefix, StringComparison.Ordinal))
                    throw new ProjectException("ZIP contém arquivos fora da pasta do projeto.");
                var relative = path[prefix.Length..];
                var directory = relative.EndsWith('/');
                var normalized = relative.TrimEnd('/');
                if (!paths.Add(normalized)) throw new ProjectException("ZIP contém caminhos duplicados.");
                // ZIP de projeto não pode criar links, dispositivos ou sockets.
                var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if (unixType != 0 && unixType != 0x8000 && unixType != 0x4000)
                    throw new ProjectException("ZIP contém um tipo de arquivo não permitido.");
                if (entry.Length > MaxExpandedBytes - total)
                    throw new ProjectException("Projeto excede o limite de 512 MiB descompactados.");
                var target = Path.Combine(extracted, normalized.Replace('/', Path.DirectorySeparatorChar));
                if (directory)
                {
                    if (entry.Length != 0) throw new ProjectException("Pasta inválida no ZIP.");
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var content = entry.Open();
                using var output = new FileStream(target, FileMode.CreateNew);
                total += CopyBounded(content, output, MaxExpandedBytes - total);
            }

            var manifestFile = Path.Combine(extracted, ProjectStore.ManifestFileName);
            if (new FileInfo(manifestFile).Length > 1024 * 1024)
                throw new ProjectException("lunet.json excede o limite de 1 MiB.");
            var manifest = ProjectManifest.Parse(File.ReadAllText(manifestFile));
            var originalName = ProjectStore.ValidateName(manifest.Name);
            var entryPoint = ValidatePath(manifest.EntryPoint);
            if (entryPoint.EndsWith('/') || !File.Exists(Path.Combine(extracted, entryPoint)))
                throw new ProjectException("Arquivo de entrada do projeto não encontrado.");

            var name = originalName;
            for (var suffix = 2; Exists(store, name); suffix++)
            {
                var tail = "_" + suffix;
                name = originalName[..Math.Min(originalName.Length, 60 - tail.Length)] + tail;
            }
            if (name != manifest.Name)
            {
                manifest.Name = name;
                AtomicFile.WriteAllText(manifestFile, manifest.ToJson());
            }
            var destination = Path.Combine(store.RootDirectory, name);
            // Move no mesmo filesystem: ou o projeto completo aparece, ou nada muda.
            Directory.Move(extracted, destination);
            return new LunetProject(destination, manifest);
        }
        catch (InvalidDataException ex)
        {
            throw new ProjectException("ZIP inválido: " + ex.Message);
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }

    private static bool Exists(ProjectStore store, string name) =>
        Directory.EnumerateFileSystemEntries(store.RootDirectory)
            .Any(p => Path.GetFileName(p).Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.StartsWith('/') ||
            path.Any(c => c == '\\' || c == ':' || char.IsControl(c)))
            throw new ProjectException("Caminho inválido no projeto ZIP.");
        var parts = path.TrimEnd('/').Split('/');
        if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.')))
            throw new ProjectException("Caminho inválido no projeto ZIP.");
        return path;
    }

    private static long CopyBounded(Stream source, Stream destination, long limit)
    {
        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (read > limit - copied) throw new ProjectException("ZIP excede o limite de tamanho permitido.");
            destination.Write(buffer, 0, read);
            copied += read;
        }
        return copied;
    }
}
