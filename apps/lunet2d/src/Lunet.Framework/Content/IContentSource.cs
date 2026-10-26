namespace Lunet.Content;

/// <summary>Origem dos arquivos de conteúdo do jogo (pasta do projeto, assets do APK exportado...).</summary>
/// <example>
/// <code>
/// if (source.Exists("Data/level.json"))
/// {
///     using Stream stream = source.Open("Data/level.json");
///     log.Info("level.json tem " + stream.Length + " bytes");
/// }
/// </code>
/// </example>
public interface IContentSource
{
    /// <summary>Diz se o arquivo existe.</summary>
    /// <param name="path">Caminho relativo a Content.</param>
    /// <returns>Verdadeiro se existe.</returns>
    bool Exists(string path);

    /// <summary>Abre o arquivo para leitura; caminhos usam '/' e são relativos a <c>Content/</c>.</summary>
    /// <param name="path">Caminho do arquivo dentro de `Content/`, por exemplo `Textures/hero.png`.</param>
    /// <returns>Um fluxo de leitura; quem chama deve fechá-lo.</returns>
    Stream Open(string path);
}

/// <summary>Conteúdo lido de uma pasta. Recusa caminhos que saiam dela.</summary>
/// <example>
/// <code>
/// IContentSource files = new DirectoryContentSource("/data/user/0/app/Content");
/// using Stream stream = files.Open("Textures/hero.png");
/// </code>
/// </example>
public sealed class DirectoryContentSource : IContentSource
{
    private readonly string _root;

    /// <summary>Lê conteúdo de uma pasta.</summary>
    /// <param name="directory">Pasta raiz do conteúdo.</param>
    public DirectoryContentSource(string directory)
    {
        var full = Path.GetFullPath(directory);
        _root = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
    }

    /// <summary>Diz se o arquivo existe; caminhos que saem da pasta dão falso.</summary>
    /// <param name="path">Caminho relativo.</param>
    /// <returns>Verdadeiro se existe.</returns>
    public bool Exists(string path) => TryResolve(path, out var full) && File.Exists(full);

    /// <summary>Abre o arquivo para leitura.</summary>
    /// <param name="path">Caminho relativo.</param>
    /// <returns>Um fluxo de leitura.</returns>
    public Stream Open(string path)
    {
        if (!TryResolve(path, out var full)) throw new FileNotFoundException($"Caminho de conteúdo inválido: {path}");
        if (!File.Exists(full)) throw new FileNotFoundException($"Arquivo de conteúdo não encontrado: {path}");
        return File.OpenRead(full);
    }

    private bool TryResolve(string path, out string full)
    {
        full = "";
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
        full = Path.GetFullPath(Path.Combine(_root, path));
        return full.StartsWith(_root, StringComparison.Ordinal);
    }
}


/// <summary>
/// Fonte de conteúdo para o Preview de um projeto Lunet.
/// Mantém Content/ como origem principal e torna legíveis os arquivos que os
/// Studios já salvam em Assets/, sem mover/copiar nem alterar o contrato de Game.Content.
/// </summary>
/// <remarks>
/// Os caminhos no código continuam relativos a Content/; somente caminhos com
/// prefixo Assets/ têm fallback para a pasta Assets/ da raiz do projeto.
/// Content/Assets/ tem prioridade quando existe o mesmo caminho nas duas pastas.
/// </remarks>
public sealed class ProjectContentSource : IContentSource
{
    private const string AssetsPrefix = "Assets/";
    private readonly DirectoryContentSource _content;
    private readonly DirectoryContentSource _assets;

    /// <summary>Recebe a raiz do projeto (onde existe lunet.json), não a pasta Content/.</summary>
    public ProjectContentSource(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
            throw new ArgumentException("Pasta do projeto inválida.", nameof(projectDirectory));
        string root = Path.GetFullPath(projectDirectory);
        _content = new DirectoryContentSource(Path.Combine(root, "Content"));
        _assets = new DirectoryContentSource(Path.Combine(root, "Assets"));
    }

    public bool Exists(string path) =>
        _content.Exists(path) || (TryAssetPath(path, out var relative) && _assets.Exists(relative));

    public Stream Open(string path)
    {
        if (_content.Exists(path)) return _content.Open(path);
        if (TryAssetPath(path, out var relative) && _assets.Exists(relative)) return _assets.Open(relative);
        throw new FileNotFoundException($"Arquivo de conteúdo não encontrado: {path}");
    }

    private static bool TryAssetPath(string path, out string relative)
    {
        relative = "";
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
            !path.StartsWith(AssetsPrefix, StringComparison.Ordinal))
            return false;

        relative = path[AssetsPrefix.Length..];
        return relative.Length > 0;
    }
}
