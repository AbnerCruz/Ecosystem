using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The customize.js service in the native app: the configuration read from
/// Personalização/tema.json (recarregar), set(patch) / replace / reset, which write the file
/// through the vault only when it changes and a vault folder is connected (podeGravar), and
/// apply it (aplicar). Rules and file text come from Urbe.Core LegacyCustomize.
/// </summary>
public sealed partial class Customization
{
    private Func<string, bool> _savedTheme = _ => false;

    public JsonObject Config { get; private set; } = LegacyCustomize.Default();
    public IReadOnlyList<LegacyCustomize.Issue> Errors { get; private set; } = [];
    public IReadOnlyList<LegacyCustomize.Issue> Warnings { get; private set; } = [];
    /// <summary>The current text of tema.json in the vault, or null when it does not exist.</summary>
    public string? FileContent { get; private set; }

    /// <summary>Writes one vault text file (path, content, file already exists); null without a writable vault.</summary>
    public Func<string, string, bool, Task>? Writer { get; set; }

    /// <summary>aplicar(c): raised after every applied configuration.</summary>
    public event Action? Applied;

    /// <summary>recarregar(): from the vault documents (path → text).</summary>
    public void Load(IReadOnlyDictionary<string, string> documents)
    {
        _documents = documents;
        _savedTheme = path => documents.TryGetValue(path, out var t) && IsObject(t);
        FileContent = documents.TryGetValue(LegacyCustomize.FilePath, out var text) ? text : null;
        _tagsText = documents.TryGetValue(TagStyles.FilePath, out var tags) ? tags : null;
        TagColors = TagStyles.Parse(_tagsText);
        UrbeTheme.TagOverrides = TagColors;
        var n = LegacyCustomize.Normalize(FileContent);
        Errors = n.Errors;
        Warnings = n.Warnings;
        Apply(n.Config);
    }

    private string? _tagsText;

    /// <summary>The colours chosen for the tags (Personalização/tags.json, DEC-0045, UC-43).</summary>
    public IReadOnlyDictionary<string, string> TagColors { get; private set; } = new Dictionary<string, string>();

    /// <summary>Chooses the colour of a tag, or (null) goes back to the app's own; writes tags.json and applies it.</summary>
    public async Task SetTagColorAsync(string tag, string? color)
    {
        if (!TagStyles.IsTag(tag) || (color is not null && !TagStyles.IsColor(color))) return;
        var next = new Dictionary<string, string>(TagColors, StringComparer.OrdinalIgnoreCase);
        if (color is null) next.Remove(tag); else next[tag] = color.ToLowerInvariant();
        var text = TagStyles.Text(next);
        if (Writer is { } write && text != _tagsText)
        {
            await write(TagStyles.FilePath, text, _tagsText is not null);
            _tagsText = text;
        }
        TagColors = next;
        UrbeTheme.TagOverrides = next;
        Applied?.Invoke();
    }

    public LegacyCustomize.Result Normalize(string text) => LegacyCustomize.Normalize(text, _savedTheme);

    /// <summary>set(patch) = gravar(merge(atual, patch)).</summary>
    public Task<LegacyCustomize.Result> SetAsync(JsonObject patch) => SaveAsync(LegacyCustomize.Merge(Config, patch));

    /// <summary>True while a live preview (a colour or slider being dragged) is being applied: nothing is written.</summary>
    public bool IsPreview { get; private set; }

    /// <summary>preview(patch): applies merge(atual, patch) on screen without writing.</summary>
    public LegacyCustomize.Result Preview(JsonObject patch)
    {
        var n = LegacyCustomize.Normalize(LegacyCustomize.Merge(Config, patch), _savedTheme);
        if (n.Errors.Count > 0) return n;
        var keep = Config;
        Config = n.Config;
        IsPreview = true;
        try { Applied?.Invoke(); } finally { IsPreview = false; }
        _previewBase ??= keep;
        return n;
    }

    private JsonObject? _previewBase;

    /// <summary>Drops an uncommitted preview: the saved configuration is applied again.</summary>
    public void EndPreview()
    {
        if (_previewBase is null) return;
        var back = _previewBase; _previewBase = null;
        if (LegacyCustomize.Stringify(back) != LegacyCustomize.Stringify(Config)) Apply(back);
    }

    /// <summary>reset(secao): that section back to the default, the rest kept.</summary>
    public Task<LegacyCustomize.Result> ResetSectionAsync(string section)
    {
        var o = (JsonObject)Config.DeepClone();
        o[section] = LegacyCustomize.Default()[section]!.DeepClone();
        return SaveAsync(o);
    }

    /// <summary>reset(): gravar(clone(DEFAULT)).</summary>
    public Task<LegacyCustomize.Result> ResetAsync() => SaveAsync(LegacyCustomize.Default());

    /// <summary>gravar(c): normalize; with errors nothing changes; else write (if changed) and apply.</summary>
    public async Task<LegacyCustomize.Result> SaveAsync(JsonNode config)
    {
        _previewBase = null;
        var n = LegacyCustomize.Normalize(config, _savedTheme);
        if (n.Errors.Count > 0) return n;
        if (Writer is { } write)
        {
            var text = LegacyCustomize.FileText(n.Config);
            if (FileContent != text)
            {
                await write(LegacyCustomize.FilePath, text, FileContent is not null);
                FileContent = text;
            }
        }
        Apply(n.Config);
        return n;
    }

    /// <summary>Arquivo → Aplicar: the typed JSON text becomes tema.json, then recarregar().</summary>
    public async Task WriteFileAsync(string text)
    {
        if (Writer is { } write && FileContent != text)
        {
            await write(LegacyCustomize.FilePath, text, FileContent is not null);
            FileContent = text;
        }
        var n = LegacyCustomize.Normalize(text, _savedTheme);
        Errors = n.Errors;
        Warnings = n.Warnings;
        Apply(n.Config);
    }

    private void Apply(JsonObject config)
    {
        Config = config;
        UpdateGround();
        Applied?.Invoke();
    }

    private static bool IsObject(string text)
    {
        try { return LegacyCustomize.ParseJs(text) is JsonObject; }
        catch (System.Text.Json.JsonException) { return false; }
    }

    // ---------------------------------------------------------------- reading the configuration

    public bool City(string key) => Config["cidade"] is JsonObject c && c[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    public string Ambient => Config["cidade"] is JsonObject c && c["ambiente"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : "ciclo";
}
