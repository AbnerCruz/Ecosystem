namespace Lunet.Core;

/// <summary>Opções reais da criação: dimensões e execução são refletidas também no Game.cs.</summary>
public sealed record ProjectCreationSettings(
    string Name,
    string Orientation = "portrait",
    int VirtualWidth = 360,
    int VirtualHeight = 640,
    int UpdatesPerSecond = 60,
    string ViewportScaling = "Fit",
    string Version = "0.1.0",
    string? PackageId = null);


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

    /// <summary>
    /// Importa um ZIP exportado pelo Lunet (ou com lunet.json na raiz).
    /// Mantém o stream aberto e cria uma cópia com sufixo se o nome já existir.
    /// Nenhum projeto fica visível antes de todos os arquivos serem validados.
    /// </summary>
    public LunetProject ImportZip(Stream source) => ProjectZipImporter.Import(this, source);

    /// <summary>Nomes de pastas que contêm um <c>lunet.json</c>, em ordem alfabética.</summary>
    public IReadOnlyList<string> List() => Directory.EnumerateDirectories(RootDirectory)
        .Where(d => File.Exists(Path.Combine(d, ManifestFileName)))
        .Select(d => Path.GetFileName(d)!)
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>Compatibilidade com amostras internas: projetos do launcher usam Create(ProjectCreationSettings).</summary>
    public LunetProject Create(string rawName, ProjectTemplate template = ProjectTemplate.Blank, string orientation = "portrait")
    {
        var width = orientation == "landscape" ? 640 : 360;
        var height = orientation == "landscape" ? 360 : 640;
        return CreateCore(new ProjectCreationSettings(rawName, orientation, width, height), template);
    }

    /// <summary>Cria um projeto novo configurado, sem incluir protótipos ou demos.</summary>
    public LunetProject Create(ProjectCreationSettings settings) => CreateCore(settings, ProjectTemplate.Blank);

    private LunetProject CreateCore(ProjectCreationSettings settings, ProjectTemplate template)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var name = ValidateName(settings.Name);
        if (settings.Orientation is not ("portrait" or "landscape"))
            throw new ProjectException("Orientação inválida: use portrait ou landscape.");
        if (settings.VirtualWidth is < 1 or > 8192 || settings.VirtualHeight is < 1 or > 8192)
            throw new ProjectException("Resolução virtual: largura e altura entre 1 e 8192.");
        if (settings.UpdatesPerSecond is < 1 or > 1000)
            throw new ProjectException("Atualizações por segundo: escolha de 1 a 1000.");
        if (settings.ViewportScaling is not ("Fit" or "Fill"))
            throw new ProjectException("Ajuste de tela inválido: use Fit ou Fill.");
        if (string.IsNullOrWhiteSpace(settings.Version) || settings.Version.Length > 40 ||
            settings.Version.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+')))
            throw new ProjectException("Versão inválida: use algo como 0.1.0 ou 1.0.0-beta.");

        var directory = Path.Combine(RootDirectory, name);
        if (Directory.Exists(directory)) throw new ProjectException("Esse projeto já existe.");

        var manifest = new ProjectManifest
        {
            Name = name,
            Orientation = settings.Orientation,
            VirtualWidth = settings.VirtualWidth,
            VirtualHeight = settings.VirtualHeight,
            UpdatesPerSecond = settings.UpdatesPerSecond,
            ViewportScaling = settings.ViewportScaling,
            Version = settings.Version,
        };
        // Cada novo projeto possui uma identidade única; não altere IDs existentes.
        manifest.PackageId = string.IsNullOrWhiteSpace(settings.PackageId)
            ? PackageIdFor(name, manifest.GameId)
            : settings.PackageId.Trim();
        if (!OfflineGameApkBuilder.ValidPackage(manifest.PackageId))
            throw new ProjectException("ID Android inválido: use algo como com.exemplo.meujogo.");
        string? conflict = FindIdentityConflict(manifest);
        if (conflict is not null)
            throw new ProjectException($"O ID Android já pertence ao projeto \"{conflict}\".");

        Directory.CreateDirectory(directory);
        try
        {
            AtomicFile.WriteAllText(Path.Combine(directory, ManifestFileName), manifest.ToJson());
            var className = ClassName(name);
            AtomicFile.WriteAllText(Path.Combine(directory, manifest.EntryPoint),
                template switch
                {
                    ProjectTemplate.CoinCatcher => ProjectTemplates.CoinCatcherSource(className),
                    ProjectTemplate.Lab => ProjectTemplates.LabSource(className),
                    ProjectTemplate.Animation => ProjectTemplates.AnimationSource(className),
                    ProjectTemplate.Particles => ProjectTemplates.ParticlesSource(className),
                    _ => ProjectTemplates.BlankGameSource(className, manifest.VirtualWidth, manifest.VirtualHeight,
                        manifest.UpdatesPerSecond, manifest.ViewportScaling),
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

    /// <summary>Apaga a pasta inteira do projeto. Só apaga pastas que são projetos Lunet (com lunet.json).</summary>
    public void Delete(string name)
    {
        var directory = Path.Combine(RootDirectory, ValidateName(name));
        if (!File.Exists(Path.Combine(directory, ManifestFileName))) throw new ProjectException($"Projeto \"{name}\" não encontrado.");
        Directory.Delete(directory, recursive: true);
    }

    public static string ValidateName(string rawName)
    {
        var name = (rawName ?? "").Trim();
        if (name.Length is < 1 or > 60 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ProjectException("Use de 1 a 60 caracteres: letras, números, _ ou -.");
        return name;
    }

    /// <summary>Stable, globally unique package for a newly created or explicitly forked game.</summary>
    internal static string PackageIdFor(string name, string gameId)
    {
        var id = Guid.Parse(gameId);
        return "com.lunet.games." + PackageSegment(name) + ".g" + id.ToString("N");
    }

    /// <summary>Find another local project that would overwrite this game's APK or reuse its signing key.</summary>
    public string? FindIdentityConflict(ProjectManifest candidate, string? exceptDirectory = null)
    {
        foreach (var directory in System.IO.Directory.EnumerateDirectories(RootDirectory))
        {
            if (exceptDirectory is not null &&
                Path.GetFullPath(directory).Equals(Path.GetFullPath(exceptDirectory), StringComparison.OrdinalIgnoreCase))
                continue;
            var path = Path.Combine(directory, ManifestFileName);
            if (!File.Exists(path)) continue;
            ProjectManifest other;
            try { other = ProjectManifest.Parse(File.ReadAllText(path)); }
            catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
            { continue; }
            bool duplicatePackage = !string.IsNullOrWhiteSpace(candidate.PackageId) &&
                candidate.PackageId.Equals(other.PackageId, StringComparison.OrdinalIgnoreCase);
            bool duplicateKey = Guid.TryParse(candidate.GameId, out var first) &&
                Guid.TryParse(other.GameId, out var second) && first == second;
            if (duplicatePackage || duplicateKey) return Path.GetFileName(directory);
        }
        return null;
    }

    /// <summary>
    /// Explicitly fork a conflicting legacy project; do not silently break the
    /// installed package's update identity. The caller must obtain user consent.
    /// </summary>
    public void ForkIdentity(LunetProject project)
    {
        if (FindIdentityConflict(project.Manifest, project.Directory) is null)
            throw new ProjectException("Este projeto não possui conflito de identidade.");
        string oldGameId = project.Manifest.GameId;
        string oldPackageId = project.Manifest.PackageId;
        try
        {
            project.Manifest.GameId = Guid.NewGuid().ToString("D");
            project.Manifest.PackageId = PackageIdFor(project.Name, project.Manifest.GameId);
            project.WriteText(ManifestFileName, project.Manifest.ToJson());
        }
        catch
        {
            project.Manifest.GameId = oldGameId;
            project.Manifest.PackageId = oldPackageId;
            throw;
        }
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
