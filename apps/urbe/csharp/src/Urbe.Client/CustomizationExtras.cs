using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>Per-device settings that never travel with the vault (the localStorage of the original: safe mode, plugin approvals).</summary>
public interface IDeviceStore
{
    string? Get(string key);
    void Set(string key, string? value);
    /// <summary>Every key and value (collectState of the export).</summary>
    IReadOnlyDictionary<string, string> All() => new Dictionary<string, string>();
}

public sealed class MemoryDeviceStore : IDeviceStore
{
    private readonly Dictionary<string, string> _values = [];
    public string? Get(string key) => _values.GetValueOrDefault(key);
    public void Set(string key, string? value) { if (value is null) _values.Remove(key); else _values[key] = value; }
    public IReadOnlyDictionary<string, string> All() => new Dictionary<string, string>(_values);
}

/// <summary>A small JSON file in the app's local data folder; a failure to read or write only costs persistence.</summary>
public sealed class FileDeviceStore(string? path = null) : IDeviceStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Urbe", "device-settings.json");
    private Dictionary<string, string>? _values;

    private Dictionary<string, string> Values()
    {
        if (_values is not null) return _values;
        try { _values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path)) ?? []; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { _values = []; }
        return _values;
    }

    public string? Get(string key) => Values().GetValueOrDefault(key);

    public IReadOnlyDictionary<string, string> All() => new Dictionary<string, string>(Values());

    public void Set(string key, string? value)
    {
        var values = Values();
        if (value is null) values.Remove(key); else values[key] = value;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(values));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}

/// <summary>The remaining customize.js / plugins.js services: vault-backed lists (themes, styles, plugins) and safe mode.</summary>
public sealed partial class Customization
{
    private const string PluginKey = "urbe.plugins.v1", SafeKey = "urbe.modoSeguro";
    private IReadOnlyDictionary<string, string> _documents = new Dictionary<string, string>();

    public IDeviceStore Device { get; set; } = new FileDeviceStore();
    /// <summary>The vault documents (path → text) this service was loaded from.</summary>
    public IReadOnlyDictionary<string, string> Documents => _documents;

    public static string ThemeFolder => LegacyCustomize.Folder + "/temas/";

    // ---------------------------------------------------------------- colours

    public LegacyCustomizeTheme.SavedTheme? SavedThemeOf(string name)
    {
        var path = LegacyCustomize.ThemePath(name);
        return _documents.TryGetValue(path, out var text) ? LegacyCustomizeTheme.ParseSavedTheme(path, Title(path), text) is { Valid: true } t ? t : null : null;
    }

    public LegacyCustomizeTheme.Palette Palette => LegacyCustomizeTheme.PaletteOf(Config, SavedThemeOf);
    public LegacyCustomizeTheme.Variables Variables => LegacyCustomizeTheme.VariablesOf(Config, SavedThemeOf);

    private static string Title(string path) => new DocumentStore().Upsert(new DocumentInput { Path = path, Content = "" }).Title;

    public sealed record ThemeEntry(string Id, string Name, bool Light, IReadOnlyDictionary<string, string> Colors);

    /// <summary>temasDoVault(): the valid saved themes.</summary>
    public IReadOnlyList<ThemeEntry> Themes() => _documents
        .Where(d => d.Key.StartsWith(ThemeFolder, StringComparison.Ordinal) && d.Key.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        .OrderBy(d => d.Key, StringComparer.InvariantCulture)
        .Select(d => LegacyCustomizeTheme.ParseSavedTheme(d.Key, Title(d.Key), d.Value))
        .Where(t => t is { Valid: true })
        .Select(t => new ThemeEntry(t!.Path, t.Name, t.Light, t.Colors)).ToList();

    /// <summary>salvarTema(nome): writes the current palette as Personalização/temas/&lt;nome&gt;.json and returns its path.</summary>
    public async Task<string> SaveThemeAsync(string name)
    {
        var (path, text) = LegacyCustomizeTheme.ThemeFile(Config, name, SavedThemeOf);
        await UpsertAsync(path, text);
        return path;
    }

    // ---------------------------------------------------------------- styles

    public sealed record StyleEntry(string Path, string Name, bool On, int Size);

    /// <summary>estilos(): the .css files of Personalização.</summary>
    public IReadOnlyList<StyleEntry> Styles()
    {
        var on = Config["estilos"] as JsonObject;
        return _documents.Where(d => d.Key.EndsWith(".css", StringComparison.OrdinalIgnoreCase) && d.Key.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal))
            .OrderBy(d => d.Key, StringComparer.InvariantCulture)
            .Select(d => new StyleEntry(d.Key, Title(d.Key), on?[d.Key] is JsonValue v && v.TryGetValue<bool>(out var b) && b, d.Value.Length)).ToList();
    }

    /// <summary>Writes a Personalização file (docs.upsert of a style, theme or plugin) and reloads the configuration.</summary>
    public async Task UpsertAsync(string path, string content)
    {
        if (Writer is null) throw new InvalidOperationException("Selecione uma pasta real para gravar.");
        bool exists = _documents.ContainsKey(path);
        if (exists && _documents[path] == content) return;
        await Writer(path, content, exists);
        Reload();
    }

    /// <summary>recarregar(): reads tema.json and the folder again.</summary>
    public void Reload() => Load(_documents);

    // ---------------------------------------------------------------- safe mode, plugins

    /// <summary>modoSeguro(): all styles and plugins off, nothing deleted; kept on this device.</summary>
    /// <summary>
    /// CST-025: Personalização → Editor → "Horário de cada bloco". A display preference of this device, so the
    /// 1.8.4's personalizacao.json keeps its format.
    /// </summary>
    public bool BlockTimes
    {
        get => Device.Get(BlockTimesKey) == "1";
        set { Device.Set(BlockTimesKey, value ? "1" : null); Applied?.Invoke(); }
    }

    private const string BlockTimesKey = "urbe.editor.horarios";

    public bool SafeMode
    {
        get => Device.Get(SafeKey) == "1";
        set { Device.Set(SafeKey, value ? "1" : null); Applied?.Invoke(); }
    }

    public sealed record PluginEntry(string Path, string Name, string Version, string Description, string Status, string? Error);

    private JsonObject Approvals()
    {
        try { return Device.Get(PluginKey) is { } text && JsonNode.Parse(text) is JsonObject o ? o : []; }
        catch (JsonException) { return []; }
    }

    private static string ApprovalKey(string path) => "Urbe::" + path;

    /// <summary>lista(): the plugin files with their state. The native app cannot run them: "aprovado" only records the owner's approval of this exact code.</summary>
    public IReadOnlyList<PluginEntry> Plugins()
    {
        var approvals = Approvals();
        return _documents.Where(d => d.Key.StartsWith(LegacyPlugins.Folder, StringComparison.Ordinal)
                && (d.Key.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || Urbe.Core.Plugins.VaultPluginSpec.IsPluginPath(d.Key)))
            .OrderBy(d => d.Key, StringComparer.InvariantCulture)
            .Select(d =>
            {
                string status = "desligado"; string? error = null;
                LegacyPlugins.Info info;
                if (Urbe.Core.Plugins.VaultPluginSpec.IsPluginPath(d.Key))
                {
                    // a plugin of the native API: checked before it can be turned on
                    var (spec, errors) = Urbe.Core.Plugins.VaultPluginSpec.Parse(d.Key, d.Value);
                    info = new LegacyPlugins.Info(d.Key, spec?.Name ?? d.Key.Split('/').Last(), spec?.Version ?? "", spec?.Description ?? "", d.Value.Length);
                    if (spec is null)
                        return new PluginEntry(d.Key, info.Name, "", "", "invalido", "O plugin tem erros e não pode ser ligado:\n" + string.Join("\n", errors.Take(6)) + (errors.Count > 6 ? "\n…" : ""));
                }
                else info = LegacyPlugins.Read(d.Key, d.Key.Split('/').Last(), d.Value);
                if (approvals[ApprovalKey(d.Key)] is JsonObject ap)
                {
                    if ((string?)ap["hash"] != LegacyPlugins.Fingerprint(d.Value)) { status = "mudou"; error = "O código mudou desde que você ligou este plugin. Revise e ligue de novo."; }
                    else status = SafeMode ? "seguro" : "aprovado";
                }
                return new PluginEntry(d.Key, info.Name, info.Version, info.Description, status, error);
            }).ToList();
    }

    /// <summary>ligar(path): records the approval (fingerprint of the code). Nothing runs natively.</summary>
    public void EnablePlugin(string path)
    {
        if (!_documents.TryGetValue(path, out var text)) throw new InvalidOperationException("Plugin não encontrado: " + path);
        var approvals = Approvals();
        approvals[ApprovalKey(path)] = new JsonObject { ["hash"] = LegacyPlugins.Fingerprint(text), ["em"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        Device.Set(PluginKey, approvals.ToJsonString());
        PluginsChanged?.Invoke();
    }

    public void DisablePlugin(string path)
    {
        var approvals = Approvals();
        approvals.Remove(ApprovalKey(path));
        Device.Set(PluginKey, approvals.ToJsonString());
        PluginsChanged?.Invoke();
    }

    /// <summary>A plugin was turned on or off here (the host starts or stops it).</summary>
    public event Action? PluginsChanged;

    /// <summary>The vault plugins (.plugin.json) approved on this device for exactly their current text, valid, outside the safe mode.</summary>
    public IReadOnlyList<(string Path, Urbe.Core.Plugins.VaultPluginSpec Spec, string Hash)> ApprovedVaultPlugins()
    {
        if (SafeMode) return [];
        var approvals = Approvals();
        var list = new List<(string, Urbe.Core.Plugins.VaultPluginSpec, string)>();
        foreach (var (path, text) in _documents.Where(d => Urbe.Core.Plugins.VaultPluginSpec.IsPluginPath(d.Key)).OrderBy(d => d.Key, StringComparer.InvariantCulture))
        {
            var hash = LegacyPlugins.Fingerprint(text);
            if (approvals[ApprovalKey(path)] is not JsonObject ap || (string?)ap["hash"] != hash) continue;
            if (Urbe.Core.Plugins.VaultPluginSpec.Parse(path, text).Spec is { } spec) list.Add((path, spec, hash));
        }
        return list;
    }

    /// <summary>A native plugin (.plugin.json) from the guide's example, with the given name (disabled).</summary>
    public async Task<string> CreateVaultPluginAsync(string name)
    {
        name = name.Trim();
        var safe = System.Text.RegularExpressions.Regex.Replace(name, @"[\\/:*?""<>|]", "-");
        var path = LegacyPlugins.Folder + safe + Urbe.Core.Plugins.VaultPluginSpec.Extension;
        for (int n = 2; _documents.ContainsKey(path); n++) path = LegacyPlugins.Folder + safe + " " + n + Urbe.Core.Plugins.VaultPluginSpec.Extension;
        var o = (JsonObject)JsonNode.Parse(Urbe.Core.Plugins.VaultPluginSpec.ExampleTimer)!;
        o["nome"] = name;
        await UpsertAsync(path, o.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        return path;
    }

    /// <summary>novo(nome, modelo): writes a plugin from a template (disabled) and returns its path.</summary>
    public async Task<string> CreatePluginAsync(string name, string template)
    {
        var (path, content) = LegacyPlugins.Create(name, template, _documents.ContainsKey);
        await UpsertAsync(path, content);
        return path;
    }
}
