using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The host of the native plugins (ADD-0021): which ones are on is kept on this device (as the 1.8.4 kept its
/// approvals), the safe mode turns all off, and each running plugin gets an <see cref="IPluginApi"/> that checks its
/// manifest on every call and remembers everything it registered, so that turning it off undoes it all.
/// </summary>
public sealed partial class MainView
{
    private const string NativePluginsKey = "urbe.plugins.native.v1";
    private readonly Dictionary<string, (IUrbePlugin Plugin, PluginApi Api)> _plugins = new(StringComparer.Ordinal);
    /// <summary>The plugins the app has (a new instance is made each time one is turned on).</summary>
    private readonly List<(PluginManifest Manifest, Func<IUrbePlugin> Make)> _available = BuiltInPlugins.All.Select(f => (f().Manifest, f)).ToList();
    private StackPanel? _topButtons;
    private readonly StackPanel _pluginPanels = new()
    {
        Spacing = 10, Margin = new Thickness(12, 64, 12, 0),
        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top
    };

    /// <summary>The native plugins this app has, with whether each is on.</summary>
    public IReadOnlyList<(PluginManifest Manifest, bool On)> NativePlugins() =>
        _available.Select(p => (p.Manifest, _plugins.ContainsKey(p.Manifest.Id))).ToList();

    /// <summary>Offers one more plugin (first-party code; never a file from the vault).</summary>
    public void AddPlugin(Func<IUrbePlugin> make)
    {
        var m = make().Manifest;
        if (_available.Any(p => p.Manifest.Id == m.Id)) throw new InvalidOperationException("Plugin repetido: " + m.Id);
        _available.Add((m, make));
    }

    /// <summary>The running instance of a plugin, or null (tests).</summary>
    public IUrbePlugin? RunningPlugin(string id) => _plugins.TryGetValue(id, out var r) ? r.Plugin : null;

    private HashSet<string> PluginsOnHere()
    {
        try { return Customization.Device.Get(NativePluginsKey) is { } t ? JsonSerializer.Deserialize<HashSet<string>>(t) ?? [] : []; }
        catch (JsonException) { return []; }
    }

    /// <summary>Turns a native plugin on or off on this device (and starts or stops it now).</summary>
    public void SetNativePlugin(string id, bool on)
    {
        var set = PluginsOnHere();
        if (on) set.Add(id); else set.Remove(id);
        Customization.Device.Set(NativePluginsKey, JsonSerializer.Serialize(set.Order(StringComparer.Ordinal)));
        SyncPlugins();
    }

    /// <summary>Starts the plugins that are on and stops the others (and all of them in the safe mode).</summary>
    public void SyncPlugins()
    {
        var on = Customization.SafeMode ? [] : PluginsOnHere();
        foreach (var id in _plugins.Keys.Where(id => !on.Contains(id)).ToList()) StopPlugin(id);
        foreach (var (manifest, make) in _available)
            if (on.Contains(manifest.Id) && !_plugins.ContainsKey(manifest.Id))
            {
                var p = make();
                var api = new PluginApi(this, manifest);
                _plugins[manifest.Id] = (p, api);
                try { p.Start(api); }
                catch (Exception ex)
                {
                    StopPlugin(manifest.Id);
                    ShowLifeNotice("O plugin " + manifest.Name + " falhou ao ligar: " + ex.Message);
                }
            }
    }

    private void StopPlugin(string id)
    {
        if (!_plugins.Remove(id, out var running)) return;
        try { running.Plugin.Stop(); } catch (Exception) { /* a plugin's failure never keeps its leftovers */ }
        running.Api.CleanUp();
    }

    private event Action<string>? NoteOpened;
    private event Action<string>? NoteSaved;

    /// <summary>The API given to one plugin: every call checks the manifest; what it adds is undone by <see cref="CleanUp"/>.</summary>
    private sealed class PluginApi(MainView host, PluginManifest manifest) : IPluginApi
    {
        private readonly List<Action> _undo = [];

        public string PluginId => manifest.Id;

        private void Need(string permission)
        {
            if (!manifest.Permissions.Contains(permission))
                throw new UnauthorizedAccessException($"O plugin {manifest.Name} não declarou a permissão “{PluginPermissions.Labels.GetValueOrDefault(permission, permission)}”.");
        }

        public void Notice(string text)
        {
            Need(PluginPermissions.Notices);
            host.ShowLifeNotice(text);
        }

        public void Button(string title, string icon, Func<Task> run)
        {
            Need(PluginPermissions.Buttons);
            if (host._topButtons is not { } bar) return;
            var b = RoundButton(icon, title);
            Avalonia.Automation.AutomationProperties.SetName(b, title);
            b.Click += async (_, _) =>
            {
                try { await run(); }
                catch (Exception ex) { host.ShowLifeNotice(manifest.Name + ": " + ex.Message); }
            };
            bar.Children.Insert(Math.Max(0, bar.Children.Count - 1), b);
            _undo.Add(() => bar.Children.Remove(b));
        }

        public void Command(string title, Func<Task> run)
        {
            Need(PluginPermissions.Commands);
            // the palette's "Comandos", as plugin.<id>.<slug> under "Plugin · name" (plugins.js api.comando)
            // slug(): lower case, accents dropped, anything else a dash
            var plain = new string(title.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            var slug = System.Text.RegularExpressions.Regex.Replace(plain, "[^a-z0-9]+", "-").Trim('-');
            var remove = host.Search.AddCommand("plugin." + manifest.Id + "." + (slug.Length > 0 ? slug : "comando"), title, "Plugin · " + manifest.Name, () =>
            {
                _ = Run();
                async Task Run()
                {
                    try { await run(); }
                    catch (Exception ex) { host.ShowLifeNotice(manifest.Name + ": " + ex.Message); }
                }
            });
            _undo.Add(remove);
        }

        public IReadOnlyList<string> Notes(string? folder = null)
        {
            Need(PluginPermissions.ReadNotes);
            var prefix = string.IsNullOrEmpty(folder) ? "" : folder.TrimEnd('/') + "/";
            return host._contents.Keys.Where(p => p.StartsWith(prefix, StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToList();
        }

        public string? ReadNote(string path)
        {
            Need(PluginPermissions.ReadNotes);
            return host._contents.GetValueOrDefault(path);
        }

        public async Task WriteNoteAsync(string path, string content)
        {
            Need(PluginPermissions.WriteNotes);
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith(".urbe", StringComparison.OrdinalIgnoreCase) || path.StartsWith(LegacyPlugins.Folder, StringComparison.Ordinal)
                || path.Split('/').Any(s => s is "" or "." or ".."))
                throw new InvalidOperationException("Caminho inválido para um plugin: " + path);
            if (!host.VaultWritable || host._vault is not { } vault) throw new InvalidOperationException(NoVaultMessage);
            if (host._contents.ContainsKey(path)) await vault.SaveExistingTextFileAsync(path, content);
            else await vault.CreateTextFileAsync(path, content);
            host._contents[path] = content;
            host.RefreshNotes();
        }

        public void On(string evt, Action<string> handler)
        {
            Need(PluginPermissions.Events);
            void Safe(string p) { try { handler(p); } catch (Exception ex) { host.ShowLifeNotice(manifest.Name + ": " + ex.Message); } }
            Action<string> h = Safe;
            switch (evt)
            {
                case "nota:aberta": host.NoteOpened += h; _undo.Add(() => host.NoteOpened -= h); break;
                case "nota:salva": host.NoteSaved += h; _undo.Add(() => host.NoteSaved -= h); break;
                default: throw new ArgumentException("Evento desconhecido: " + evt);
            }
        }

        public IPluginPanel Panel(string title, Control content)
        {
            Need(PluginPermissions.Panels);
            var panel = new PluginPanel(title, content);
            host._pluginPanels.Children.Add(panel);
            _undo.Add(() => host._pluginPanels.Children.Remove(panel));
            return panel;
        }

        private string DataKey(string key) => "urbe.plugin." + manifest.Id + "." + key;

        public string? GetData(string key)
        {
            Need(PluginPermissions.Data);
            return host.Customization.Device.Get(DataKey(key));
        }

        public void SetData(string key, string? value)
        {
            Need(PluginPermissions.Data);
            host.Customization.Device.Set(DataKey(key), value);
        }

        public void CleanUp()
        {
            for (int i = _undo.Count - 1; i >= 0; i--) _undo[i]();
            _undo.Clear();
        }
    }

    /// <summary>A plugin's floating card, with its title and ×.</summary>
    private sealed class PluginPanel : Border, IPluginPanel
    {
        public PluginPanel(string title, Control content)
        {
            IsVisible = false;
            Width = 300;
            Background = UrbeTheme.Brush(UrbeTheme.Surface);
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(16);
            Padding = new Thickness(16, 10, 10, 16);
            BoxShadow = BoxShadows.Parse("0 6 20 0 #47000000");
            var close = new Button
            {
                Content = UrbeTheme.Icon(UrbeTheme.Icons.CloseUi, 16, UrbeTheme.Text2, 1.8), Width = 32, Height = 32, Padding = new Thickness(0),
                CornerRadius = new CornerRadius(16), Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(close, "Fechar");
            close.Click += (_, _) => Hide();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center });
            Child = new StackPanel { Children = { head, content } };
        }

        public bool IsOpen => IsVisible;
        public void Show() => IsVisible = true;
        public void Hide() => IsVisible = false;
        public void Toggle() => IsVisible = !IsVisible;
    }
}
