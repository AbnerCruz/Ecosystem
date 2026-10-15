using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core.Plugins;

namespace Urbe.Client.Plugins;

/// <summary>
/// A plugin written in the vault (.plugin.json, <see cref="VaultPluginSpec"/>) running on the native plugin API: its
/// panel is drawn from its description, its buttons and commands run its actions, its clock ticks while on, and every
/// call goes through the same <see cref="IPluginApi"/> (and permission checks) as the built-in plugins.
/// </summary>
public sealed class VaultPluginAdapter(VaultPluginSpec spec, string hash) : IUrbePlugin
{
    private static readonly Dictionary<string, string> IconData = new(StringComparer.Ordinal)
    {
        ["clock"] = UrbeTheme.Icons.Clock, ["star"] = UrbeTheme.Icons.Star, ["check"] = UrbeTheme.Icons.Check, ["edit"] = UrbeTheme.Icons.Edit,
        ["search"] = UrbeTheme.Icons.Search, ["file"] = UrbeTheme.Icons.File, ["folder"] = UrbeTheme.Icons.Folder, ["list"] = UrbeTheme.Icons.List,
        ["book"] = UrbeTheme.Icons.Book, ["pin"] = UrbeTheme.Icons.Pin, ["sparkle"] = UrbeTheme.Icons.Assistant, ["plus"] = UrbeTheme.Icons.Plus,
        ["layers"] = UrbeTheme.Icons.Layers, ["flower"] = UrbeTheme.Icons.Flower, ["house"] = UrbeTheme.Icons.House
    };

    public PluginManifest Manifest { get; } = new(spec.Id, spec.Name, spec.Version, spec.Description, spec.Declared);

    /// <summary>The approved text's fingerprint (a new approval of a changed file restarts the plugin).</summary>
    public string Hash { get; } = hash;

    public VaultPluginRuntime? Runtime { get; private set; }
    public IPluginPanel? Panel { get; private set; }

    private IPluginApi? _api;
    private DispatcherTimer? _clock;
    private readonly List<Action> _refresh = [];
    private bool _stopped;

    public void Start(IPluginApi api)
    {
        _api = api;
        var host = new Host(api, this);
        var rt = new VaultPluginRuntime(spec, host);
        Runtime = rt;
        rt.Changed += () => Dispatcher.UIThread.Post(Refresh);
        if (spec.Panel is { } panel)
        {
            Panel = api.Panel(rt.Text(VaultPluginSpec.Str(panel["titulo"]) is { Length: > 0 } t ? t : spec.Name), new StackPanel { Spacing = 8, Children = { Items(panel["itens"] as JsonArray ?? [], null) } });
            if (rt.PanelOpen) Panel.Show();
        }
        foreach (var b in spec.Buttons.OfType<JsonObject>())
        {
            var title = VaultPluginSpec.Str(b["titulo"]);
            var icon = IconData.GetValueOrDefault(VaultPluginSpec.Str(b["icone"]) is { Length: > 0 } i ? i : spec.Icon, UrbeTheme.Icons.Assistant);
            Func<Task> run = b["acoes"] is JsonArray acts ? () => rt.RunAsync(acts) : () => { TogglePanel(); return Task.CompletedTask; };
            if (VaultPluginSpec.Str(b["onde"]) == "editor") api.EditorButton(title, icon, run);
            else api.Button(title, icon, run);
        }
        foreach (var c in spec.Commands.OfType<JsonObject>())
        {
            var acts = c["acoes"] as JsonArray ?? [];
            api.Command(VaultPluginSpec.Str(c["titulo"]), () => rt.RunAsync(acts));
        }
        if (spec.Handlers.Count > 0)
            foreach (var evt in VaultPluginSpec.Events.Where(e => spec.Handlers.ContainsKey(e)))
                api.On(evt, path => _ = rt.EventAsync(evt, path));
        if (spec.Clock is not null)
        {
            _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(100, rt.ClockInterval)) };
            _clock.Tick += async (_, _) => { if (rt.ClockOn && !_stopped) await rt.TickAsync(); };
            _clock.Start();
        }
        _ = rt.StartAsync();
        Refresh();
    }

    public void Stop()
    {
        _stopped = true;
        _clock?.Stop();
        _refresh.Clear();
        _api = null;
    }

    private void TogglePanel()
    {
        if (Panel is null || Runtime is null) return;
        Panel.Toggle();
        Runtime.PanelOpen = Panel.IsOpen;
        Refresh();
    }

    private void Refresh()
    {
        if (_stopped) return;
        foreach (var r in _refresh.ToList()) r();
    }

    /// <summary>The bridge from the runtime to the plugin API (each call checks the permission again).</summary>
    private sealed class Host(IPluginApi api, VaultPluginAdapter owner) : IVaultPluginHost
    {
        public DateTime Now => DateTime.Now;
        public IReadOnlyList<string> Notes(string? folder) => api.Notes(folder);
        public string? ReadNote(string path) => api.ReadNote(path);
        public void Notice(string text) => api.Notice(text);
        public void Panel(string how)
        {
            if (owner.Panel is not { } p) return;
            if (how == "mostrar") p.Show(); else if (how == "ocultar") p.Hide(); else p.Toggle();
        }
        public async Task WriteNoteAsync(string path, string content, bool append)
        {
            if (append && api.ReadNote(path) is { } old) content = old + (old.Length == 0 || old.EndsWith('\n') ? "" : "\n") + content;
            await api.WriteNoteAsync(path, content);
        }
        public void InsertText(string text) => api.InsertText(text);
        public void OpenNote(string path) => api.OpenNote(path);
        public string? OpenNotePath => api.OpenNotePath;
        public string? LoadData(string key) => api.GetData(key);
        public void SaveData(string key, string json) => api.SetData(key, json);
    }

    // ---------------------------------------------------------------- the panel's items

    private Control Items(JsonArray items, Dictionary<string, object?>? extra)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var it in items.OfType<JsonObject>())
            if (Item(it, extra) is { } c) panel.Children.Add(c);
        return panel;
    }

    private Control? Item(JsonObject it, Dictionary<string, object?>? extra)
    {
        var rt = Runtime!;
        var kind = it.Select(p => p.Key).FirstOrDefault(k => VaultPluginSpec.WidgetKinds.Contains(k));
        switch (kind)
        {
            case "texto":
            {
                var tb = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text),
                    FontSize = VaultPluginSpec.Str(it["tamanho"]) switch { "pequeno" => 12, "grande" => 20, "enorme" => 30, _ => 14 },
                    FontWeight = PluginScriptTruthy(it["negrito"]) || VaultPluginSpec.Str(it["tamanho"]) is "grande" or "enorme" ? FontWeight.Bold : FontWeight.Normal
                };
                if (PluginScriptTruthy(it["mono"])) tb.FontFamily = UrbeTheme.MonoFont;
                var src = VaultPluginSpec.Str(it["texto"]);
                Live(() => tb.Text = rt.Text(src, extra), extra);
                return tb;
            }
            case "botao":
            {
                var primary = PluginScriptTruthy(it["destaque"]);
                var b = new Button
                {
                    Height = 36, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(10), FontSize = 13, FontWeight = FontWeight.SemiBold,
                    Background = UrbeTheme.Brush(primary ? UrbeTheme.Accent : UrbeTheme.Surface2), Foreground = UrbeTheme.Brush(primary ? "#ffffff" : UrbeTheme.Text),
                    BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(primary ? 0 : 1), VerticalContentAlignment = VerticalAlignment.Center
                };
                var src = VaultPluginSpec.Str(it["botao"]);
                var acts = it["acoes"] as JsonArray ?? [];
                b.Click += async (_, _) => await rt.RunAsync(acts, extra);
                Live(() => b.Content = rt.Text(src, extra), extra);
                return b;
            }
            case "linha":
            {
                var row = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var c in (it["linha"] as JsonArray ?? []).OfType<JsonObject>())
                    if (Item(c, extra) is { } ctl) { ctl.Margin = new Thickness(0, 0, 6, 6); row.Children.Add(ctl); }
                return row;
            }
            case "campo":
            {
                var name = VaultPluginSpec.Str(it["campo"]);
                var number = VaultPluginSpec.Str(it["tipo"]) == "numero";
                var box = new TextBox { PlaceholderText = VaultPluginSpec.Str(it["dica"]), FontSize = 14 };
                box.TextChanged += (_, _) =>
                {
                    if (!box.IsFocused) return;
                    rt.Set(name, number ? PluginScript.Num(box.Text) : box.Text ?? "");
                };
                Live(() => { if (!box.IsFocused) box.Text = PluginScript.Text(rt.Vars.GetValueOrDefault(name)); }, null);
                var label = VaultPluginSpec.Str(it["rotulo"]);
                return label.Length == 0 ? box : new StackPanel { Spacing = 4, Children = { Small(label), box } };
            }
            case "marcar":
            {
                var name = VaultPluginSpec.Str(it["marcar"]);
                var check = new CheckBox { Content = VaultPluginSpec.Str(it["rotulo"]), Foreground = UrbeTheme.Brush(UrbeTheme.Text) };
                check.IsCheckedChanged += (_, _) => { if (PluginScript.Truthy(rt.Vars.GetValueOrDefault(name)) != (check.IsChecked == true)) rt.Set(name, check.IsChecked == true); };
                Live(() => check.IsChecked = PluginScript.Truthy(rt.Vars.GetValueOrDefault(name)), null);
                return check;
            }
            case "lista":
            {
                var host = new StackPanel { Spacing = 4 };
                var listExpr = VaultPluginSpec.Str(it["lista"]);
                var itemSrc = VaultPluginSpec.Str(it["item"]) is { Length: > 0 } s ? s : "{item}";
                var acts = it["acoes"] as JsonArray;
                Live(() =>
                {
                    host.Children.Clear();
                    var list = rt.Value(listExpr, extra) as List<object?> ?? [];
                    for (int i = 0; i < Math.Min(list.Count, 500); i++)
                    {
                        var vars = new Dictionary<string, object?>(extra ?? [], StringComparer.Ordinal) { ["item"] = list[i], ["indice"] = (double)i };
                        var text = new TextBlock { Text = rt.Text(itemSrc, vars), TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
                        if (acts is null) { host.Children.Add(text); continue; }
                        var b = new Button { Content = text, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0, 2), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
                        b.Click += async (_, _) => await rt.RunAsync(acts, vars);
                        host.Children.Add(b);
                    }
                }, extra);
                return host;
            }
            case "se":
            {
                // both branches are built once; the condition only shows one or the other
                var test = VaultPluginSpec.Str(it["se"]);
                var yes = Items(it["itens"] as JsonArray ?? [], extra);
                var no = Items(it["senao"] as JsonArray ?? [], extra);
                Live(() => { var on = PluginScript.Truthy(rt.Value(test, extra)); yes.IsVisible = on; no.IsVisible = !on; }, extra);
                return new StackPanel { Children = { yes, no } };
            }
            case "progresso":
            {
                var bar = new ProgressBar { Minimum = 0, Maximum = 1, Height = 8, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) };
                var src = VaultPluginSpec.Str(it["progresso"]);
                Live(() => bar.Value = Math.Clamp(PluginScript.Num(rt.Value(src, extra)), 0, 1), extra);
                return bar;
            }
            case "separador":
                return new Border { Height = 1, Background = UrbeTheme.Brush(UrbeTheme.Line2), Margin = new Thickness(0, 4) };
        }
        return null;
    }

    private void Live(Action update, Dictionary<string, object?>? _)
    {
        _refresh.Add(update);
        update();
    }

    private static bool PluginScriptTruthy(JsonNode? n) => PluginScript.Truthy(PluginScript.FromJson(n));

    private static TextBlock Small(string text) => new() { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) };
}
