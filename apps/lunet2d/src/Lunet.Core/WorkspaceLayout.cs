using System.Text.Json;

namespace Lunet.Core;

/// <summary>Onde fica o painel inferior (Problemas/Console). <see cref="Auto"/> usa a direita em paisagem e embaixo em retrato.</summary>
public enum PanelDock { Auto, Bottom, Right }

/// <summary>Disposição dos painéis do workspace. Salva com nome para reutilizar.</summary>
public sealed class WorkspaceLayout
{
    public const double MinPanelFraction = 0.12;
    public const double MaxPanelFraction = 0.7;
    public const int MinExplorerWidth = 200;
    public const int MaxExplorerWidth = 480;

    public bool ShowPanel { get; set; } = true;
    public PanelDock Dock { get; set; } = PanelDock.Auto;

    /// <summary>Fração da área (altura ou largura, conforme o encaixe) ocupada pelo painel.</summary>
    public double PanelFraction { get; set; } = 0.3;

    /// <summary>Largura do Explorer, em dp.</summary>
    public int ExplorerWidthDp { get; set; } = 300;

    /// <summary>Aba visível do painel inferior: "problems" ou "console".</summary>
    public string PanelTab { get; set; } = "problems";

    public WorkspaceLayout Normalized()
    {
        PanelFraction = Math.Clamp(double.IsNaN(PanelFraction) ? 0.3 : PanelFraction, MinPanelFraction, MaxPanelFraction);
        ExplorerWidthDp = Math.Clamp(ExplorerWidthDp, MinExplorerWidth, MaxExplorerWidth);
        if (!Enum.IsDefined(Dock)) Dock = PanelDock.Auto;
        if (PanelTab is not ("problems" or "console")) PanelTab = "problems";
        return this;
    }

    public WorkspaceLayout Clone() => (WorkspaceLayout)MemberwiseClone();

    /// <summary>Encaixe efetivo para a orientação atual da tela.</summary>
    public PanelDock EffectiveDock(bool landscape) => Dock == PanelDock.Auto ? (landscape ? PanelDock.Right : PanelDock.Bottom) : Dock;
}

/// <summary>Layout atual e layouts salvos com nome, num arquivo JSON. Os predefinidos não podem ser apagados nem sobrescritos.</summary>
public sealed class LayoutStore(string path)
{
    private sealed class Data
    {
        public WorkspaceLayout Current { get; set; } = new();
        public Dictionary<string, WorkspaceLayout> Saved { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, AllowTrailingCommas = true };

    private Data _data = new();

    /// <summary>Layouts que acompanham o app.</summary>
    public static IReadOnlyDictionary<string, WorkspaceLayout> Presets { get; } = new Dictionary<string, WorkspaceLayout>(StringComparer.OrdinalIgnoreCase)
    {
        ["Padrão"] = new WorkspaceLayout(),
        ["Foco no código"] = new WorkspaceLayout { ShowPanel = false },
        ["Depuração"] = new WorkspaceLayout { PanelFraction = 0.5, PanelTab = "console" },
    };

    public WorkspaceLayout Current
    {
        get => _data.Current;
        set => _data.Current = value.Normalized();
    }

    /// <summary>Nomes disponíveis: predefinidos primeiro, depois os salvos pelo usuário.</summary>
    public IReadOnlyList<string> Names => Presets.Keys.Concat(_data.Saved.Keys.Where(k => !Presets.ContainsKey(k)).OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase)).ToList();

    public bool IsPreset(string name) => Presets.ContainsKey(name);

    public void Load()
    {
        try
        {
            if (!File.Exists(path)) { _data = new Data(); return; }
            _data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), Options) ?? new Data();
            _data.Current = (_data.Current ?? new WorkspaceLayout()).Normalized();
            _data.Saved = new Dictionary<string, WorkspaceLayout>((_data.Saved ?? []).ToDictionary(kv => kv.Key, kv => kv.Value.Normalized()), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _data = new Data();
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(_data, Options));
    }

    /// <summary>Guarda o layout atual com um nome. Recusa nomes vazios e os predefinidos.</summary>
    public bool SaveAs(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 40 || IsPreset(name)) return false;
        _data.Saved[name] = _data.Current.Clone();
        return true;
    }

    /// <summary>Torna o layout de <paramref name="name"/> o atual.</summary>
    public bool Apply(string name)
    {
        if (Presets.TryGetValue(name, out var preset)) _data.Current = preset.Clone();
        else if (_data.Saved.TryGetValue(name, out var saved)) _data.Current = saved.Clone();
        else return false;
        return true;
    }

    public bool Delete(string name) => !IsPreset(name) && _data.Saved.Remove(name);
}
