using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunet.Core;

/// <summary>Conteúdo de <c>lunet.json</c>.</summary>
public sealed class ProjectManifest
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public string Name { get; set; } = "";
    public string GameId { get; set; } = Guid.NewGuid().ToString("D");
    public string PackageId { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string FrameworkVersion { get; set; } = "0.1.0";
    public string EntryPoint { get; set; } = "Game.cs";
    public string Orientation { get; set; } = "portrait";
    public int VirtualWidth { get; set; } = 360;
    public int VirtualHeight { get; set; } = 640;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ProjectManifest Parse(string json)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<ProjectManifest>(json, JsonOptions)
                ?? throw new ProjectException("lunet.json está vazio.");
            if (manifest.FormatVersion > CurrentFormatVersion)
                throw new ProjectException($"lunet.json usa o formato {manifest.FormatVersion}, mais novo que este Lunet ({CurrentFormatVersion}). Atualize o aplicativo.");
            if (string.IsNullOrWhiteSpace(manifest.Name)) throw new ProjectException("lunet.json precisa de um \"name\".");
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new ProjectException("lunet.json inválido: " + ex.Message);
        }
    }
}

public sealed class ProjectException(string message) : Exception(message);
