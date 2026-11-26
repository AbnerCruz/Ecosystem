using System.Text.Json;

namespace Lunet.Core;

public enum WorkspaceTheme { Dark, Light, System }
public enum EditorFont { Monospace, Condensed, Serif }

/// <summary>Preferências do IDE, salvas em um arquivo JSON.</summary>
public sealed class EditorSettings
{
    public const int MinFontSize = 9;
    public const int MaxFontSize = 28;

    public WorkspaceTheme Theme { get; set; }
    public EditorFont Font { get; set; }
    public int FirstUseStep { get; set; }
    public bool UseSharedProjectFolder { get; set; }
    public string? ProjectTree { get; set; }
    public string? ProjectRevision { get; set; }
    public string? ProjectWorkingCopy { get; set; }

    public string? PreviousProjectWorkingCopy { get; set; }
    public string? PreviousProjectRevision { get; set; }

    public int FontSize { get; set; } = 14;
    public bool ShowLineNumbers { get; set; } = true;
    public bool ShowMinimap { get; set; } = true;
    public bool FormatOnRun { get; set; }
    public bool KeepScreenOn { get; set; }
    public bool HighRefreshRate { get; set; } = true;

    /// <summary>Roda o Preview num processo separado: um jogo que trava ou estoura a memória não derruba o IDE.</summary>
    public bool IsolatedPreview { get; set; }

    /// <summary>Corrige valores inválidos (fonte fora do intervalo).</summary>
    public EditorSettings Normalized()
    {
        if (!Enum.IsDefined(Theme)) Theme = WorkspaceTheme.Dark;
        if (!Enum.IsDefined(Font)) Font = EditorFont.Monospace;
        FirstUseStep = Math.Clamp(FirstUseStep, 0, 3);
        if (string.IsNullOrWhiteSpace(ProjectTree) || !Guid.TryParseExact(ProjectWorkingCopy, "N", out _))
        {
            (ProjectTree, ProjectRevision, ProjectWorkingCopy) = (null, null, null);
            UseSharedProjectFolder = false;
            PreviousProjectWorkingCopy = null;
        }
        if (!Guid.TryParseExact(PreviousProjectWorkingCopy, "N", out _))
            (PreviousProjectWorkingCopy, PreviousProjectRevision) = (null, null);
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
