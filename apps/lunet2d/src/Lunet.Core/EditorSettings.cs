using System.Text.Json;

namespace Lunet.Core;

/// <summary>Preferências do IDE, salvas em um arquivo JSON. Valores fora do intervalo são ajustados ao carregar.</summary>
public sealed class EditorSettings
{
    public const int MinFontSize = 9;
    public const int MaxFontSize = 28;

    public int FontSize { get; set; } = 14;
    public bool ShowLineNumbers { get; set; } = true;
    public bool ShowMinimap { get; set; } = true;
    public bool FormatOnRun { get; set; }
    public bool KeepScreenOn { get; set; }
    public bool HighRefreshRate { get; set; } = true;

    /// <summary>Corrige valores inválidos (fonte fora do intervalo).</summary>
    public EditorSettings Normalized()
    {
        FontSize = Math.Clamp(FontSize, MinFontSize, MaxFontSize);
        return this;
    }

    public EditorSettings Clone() => (EditorSettings)MemberwiseClone();
}

/// <summary>Leitura e gravação atômica das preferências. Arquivo ausente ou corrompido vira as preferências padrão.</summary>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public string Path { get; } = path;

    public EditorSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new EditorSettings();
            return (JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(Path), Options) ?? new EditorSettings()).Normalized();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new EditorSettings();
        }
    }

    public void Save(EditorSettings settings)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
        AtomicFile.WriteAllText(Path, JsonSerializer.Serialize(settings.Normalized(), Options));
    }
}
