namespace Lunet.Content;

/// <summary>Origem dos arquivos de conteúdo do jogo (pasta do projeto, assets do APK exportado...).</summary>
public interface IContentSource
{
    /// <summary>Diz se o arquivo existe.</summary>
    /// <param name="path">Caminho relativo a Content.</param>
    /// <returns>Verdadeiro se existe.</returns>
    bool Exists(string path);

    /// <summary>Abre o arquivo para leitura; caminhos usam '/' e são relativos a <c>Content/</c>.</summary>
    Stream Open(string path);
}

/// <summary>Conteúdo lido de uma pasta. Recusa caminhos que saiam dela.</summary>
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
