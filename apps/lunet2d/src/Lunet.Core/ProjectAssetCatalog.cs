namespace Lunet.Core;

/// <summary>Tipos de recursos visíveis no painel Assets. A pasta do projeto continua sendo a fonte de verdade.</summary>
public enum ProjectAssetKind { Sprite, TileMap, Image, Audio, Font, Data }

/// <summary>Projeção descartável de um arquivo de projeto; não cria banco de dados nem metadados persistentes.</summary>
public sealed record ProjectAsset(string Path, ProjectAssetKind Kind, string? PreviewPath, string? RuntimePath)
{
    /// <summary>Exemplo de acesso via as APIs públicas do framework Lunet, quando aplicável.</summary>
    public string UsageCode
    {
        get
        {
            if (RuntimePath is null) return "// Edite este recurso no Lunet Studio.";
            string path = RuntimePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return Kind switch
            {
                ProjectAssetKind.Sprite or ProjectAssetKind.Image => $"Content.LoadTexture(\"{path}\")",
                ProjectAssetKind.TileMap => $"Content.LoadTileMap(\"{path}\")",
                ProjectAssetKind.Audio when path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) => $"Content.LoadSound(\"{path}\")",
                ProjectAssetKind.Audio => $"Content.LoadMusic(\"{path}\")",
                ProjectAssetKind.Font => $"Content.LoadTrueTypeFont(\"{path}\", 24)",
                _ => $"Content.ReadText(\"{path}\")"
            };
        }
    }
}

/// <summary>
/// Catálogo apenas de leitura, derivado de LunetProject.ListFiles em cada abertura.
/// Vincula uma fonte .sprite.json à PNG gerada sem mostrar duas cópias do mesmo recurso.
/// Nunca altera arquivos, remove outros PNGs ou usa caminhos fora de Content/ e Assets/.
/// </summary>
public static class ProjectAssetCatalog
{
    public static IReadOnlyList<ProjectAsset> Build(IEnumerable<string> projectFiles)
    {
        ArgumentNullException.ThrowIfNull(projectFiles);
        var paths = new HashSet<string>(projectFiles, StringComparer.OrdinalIgnoreCase);
        var result = new List<ProjectAsset>();

        foreach (string path in paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (!(path.StartsWith("Content/", StringComparison.OrdinalIgnoreCase) ||
                  path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))) continue;
            if (path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
            {
                string png = path[..^".sprite.json".Length] + ".png";
                bool hasPng = paths.Contains(png);
                result.Add(new ProjectAsset(path, ProjectAssetKind.Sprite, hasPng ? png : null,
                    hasPng ? ContentPath(png) : null));
                continue;
            }
            if (path.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new ProjectAsset(path, ProjectAssetKind.TileMap, null, ContentPath(path)));
                continue;
            }
            if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                paths.Contains(path[..^".png".Length] + ".sprite.json")) continue;
            var kind = Classify(path);
            if (kind is null) continue;
            result.Add(new ProjectAsset(path, kind.Value,
                kind == ProjectAssetKind.Image ? path : null, ContentPath(path)));
        }
        return result;
    }

    private static ProjectAssetKind? Classify(string path)
    {
        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) return ProjectAssetKind.Image;
        if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) return ProjectAssetKind.Audio;
        if (path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)) return ProjectAssetKind.Font;
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return ProjectAssetKind.Data;
        return null;
    }

    private static string ContentPath(string path) => path.StartsWith("Content/", StringComparison.OrdinalIgnoreCase)
        ? path["Content/".Length..] : path;
}
