namespace Lunet.Core;

/// <summary>Pasta que contém os projetos do usuário (uma subpasta por projeto).</summary>
public sealed class ProjectStore
{
    public const string ManifestFileName = "lunet.json";

    public ProjectStore(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(RootDirectory);
    }

    public string RootDirectory { get; }

    /// <summary>Nomes de pastas que contêm um <c>lunet.json</c>, em ordem alfabética.</summary>
    public IReadOnlyList<string> List() => Directory.EnumerateDirectories(RootDirectory)
        .Where(d => File.Exists(Path.Combine(d, ManifestFileName)))
        .Select(d => Path.GetFileName(d)!)
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public LunetProject Create(string rawName, ProjectTemplate template = ProjectTemplate.Blank)
    {
        var name = ValidateName(rawName);
        var directory = Path.Combine(RootDirectory, name);
        if (Directory.Exists(directory)) throw new ProjectException("Esse projeto já existe.");

        Directory.CreateDirectory(directory);
        try
        {
            var manifest = new ProjectManifest
            {
                Name = name,
                PackageId = "com.lunet.games." + PackageSegment(name),
            };
            AtomicFile.WriteAllText(Path.Combine(directory, ManifestFileName), manifest.ToJson());
            var className = ClassName(name);
            AtomicFile.WriteAllText(Path.Combine(directory, manifest.EntryPoint),
                template switch
                {
                    ProjectTemplate.CoinCatcher => ProjectTemplates.CoinCatcherSource(className),
                    ProjectTemplate.Lab => ProjectTemplates.LabSource(className),
                    _ => ProjectTemplates.BlankGameSource(className),
                });
            Directory.CreateDirectory(Path.Combine(directory, "Content"));
            if (template is ProjectTemplate.CoinCatcher or ProjectTemplate.Lab)
            {
                Directory.CreateDirectory(Path.Combine(directory, "Content", "Audio"));
                File.WriteAllBytes(Path.Combine(directory, "Content", "Audio", "beep.wav"), WavGenerator.Beep(880, 0.12));
            }
            if (template == ProjectTemplate.Lab)
                File.WriteAllBytes(Path.Combine(directory, "Content", "Audio", "loop.wav"), WavGenerator.ArpeggioLoop());
        }
        catch
        {
            Directory.Delete(directory, recursive: true);
            throw;
        }
        return Open(name);
    }

    public LunetProject Open(string name)
    {
        var directory = Path.Combine(RootDirectory, ValidateName(name));
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath)) throw new ProjectException($"Projeto \"{name}\" não encontrado.");
        return new LunetProject(directory, ProjectManifest.Parse(File.ReadAllText(manifestPath)));
    }

    public static string ValidateName(string rawName)
    {
        var name = (rawName ?? "").Trim();
        if (name.Length is < 1 or > 60 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ProjectException("Use de 1 a 60 caracteres: letras, números, _ ou -.");
        return name;
    }

    private static string PackageSegment(string name)
    {
        var segment = new string(name.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        if (segment.Length == 0 || char.IsAsciiDigit(segment[0])) segment = "g" + segment;
        return segment;
    }

    private static string ClassName(string name)
    {
        var parts = name.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var identifier = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        if (identifier.Length == 0 || char.IsAsciiDigit(identifier[0])) identifier = "Game" + identifier;
        return identifier;
    }
}
