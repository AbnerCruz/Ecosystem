using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;
using Urbe.Core.Pages;

namespace Urbe.Client;

/// <summary>What the studio needs from the app: the notes, saving the page file, rendering, the preview and the menus.</summary>
public interface IPageStudioHost
{
    UrbeDialogs Dialogs { get; }
    /// <summary>html(false): the page as the engine exports it, with the vault's notes.</summary>
    string Render(JsonObject spec, bool preview = false);
    /// <summary>The platform's viewer for this HTML, or null (then the studio shows the page's outline); the page's messages go to <paramref name="messages"/>.</summary>
    Task<Control?> PreviewAsync(string path, byte[] html, Action<string>? messages = null);
    /// <summary>Writes the page file (it exists) and verifies it.</summary>
    Task SaveAsync(string path, string text);
    /// <summary>Writes a note the page shows (it exists) and verifies it, like the editor (a comment written in the preview).</summary>
    Task SaveNoteAsync(string path, string text);
    /// <summary>The page file renamed (the title changed); returns the new path.</summary>
    Task<string> RenameAsync(string path, string newPath);
    bool Exists(string path);
    IReadOnlyList<(string Path, string Title)> Notes();
    IReadOnlyList<string> Folders();
    /// <summary>noteFor(path): the note by path, path + .md, or title.</summary>
    PageNote? Note(string path);
    IReadOnlyList<string> Tags();
    /// <summary>Páginas/Blocos/*.block.json: (path, name, description, section).</summary>
    IReadOnlyList<(string Path, string Name, string Description, JsonObject Section)> SavedBlocks();
    /// <summary>The person's templates (Páginas/Modelos/*.template.json): (path, name, description, spec).</summary>
    IReadOnlyList<(string Path, string Name, string Description, JsonObject Spec)> SavedTemplates();
    Task CreateFileAsync(string path, string text);
    Task ExportMenuAsync(string path);
    Task MoreMenuAsync(string path);
    /// <summary>An image chosen on the device, as a data: URL reduced to 1600 px (null when cancelled).</summary>
    Task<string?> PickImageAsync();
    void Closed();
    /// <summary>Something of the app is over the studio (the Assistente, a plugin panel, a file preview): a native preview would hide it.</summary>
    bool PreviewCovered => false;
}

/// <summary>
/// The page studio of the 1.8.4 (src/pages/studio.js), natively: the top bar (Voltar, the page's title, Salvo/Salvando…,
/// Desfazer, Refazer, JSON, Exportar, ⋯), the live preview (the same HTML the export writes, in the platform's viewer),
/// and the panels generated from each block's fields — Seções, Tema, Página and Editar — side by side from 1000 px, or
/// on a phone in a bottom sheet opened by the tab bar (Seções, Editar, ＋, Tema, Página). Every change is one undo step
/// (typing in a field is one), is saved 600 ms later and redraws the preview. <see cref="PageStudio"/> holds the model.
/// </summary>
public sealed partial class PageStudioView : Border
{
    private readonly IPageStudioHost _host;
    private PageStudio? _st;
    private string _path = "";
    private string _leftTab = "sections";
    private string? _sheet;
    private string _titleBefore = "";
    private readonly Dictionary<string, bool> _groups = new(StringComparer.Ordinal);

    private readonly TextBox _title = new();
    private readonly TextBlock _status = new();
    private readonly Button _undo, _redo;
    private readonly ContentControl _preview = new();
    private readonly Border _left = new(), _right = new();
    private readonly StackPanel _leftTabs = new() { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(10, 8, 10, 4) };
    private readonly Border _sheetBox = new();
    private readonly ContentControl _sheetBody = new();
    private readonly TextBlock _sheetTitle = new();
    private readonly Grid _tabbar = new();
    private readonly Grid _body = new();
    private readonly Grid _overlay = new() { IsVisible = false };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool _wideLast;
    private string? _previewPath;
    // the phone's preview is a platform view drawn over everything of the app: while something is shown over it
    // (the block library, JSON, a menu, the Assistente) it is hidden, else those cannot be seen nor touched (UC-48)
    private readonly DispatcherTimer _coverTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private string? _postedSelection;

    public PageStudioView(IPageStudioHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        _saveTimer.Tick += async (_, _) => { _saveTimer.Stop(); await SaveAsync(); };
        _previewTimer.Tick += async (_, _) => { _previewTimer.Stop(); await RenderPreviewAsync(); };
        _coverTimer.Tick += (_, _) => UpdateCover();

        var back = IconButton(UrbeTheme.Icons.Back, "Voltar");
        back.Click += async (_, _) => await CloseAsync();
        _title.FontFamily = UrbeTheme.UiFont;
        _title.FontSize = 16;
        _title.FontWeight = FontWeight.SemiBold;
        _title.MinWidth = 120;
        _title.Padding = new Thickness(8, 4);
        _title.Background = Brushes.Transparent;
        _title.BorderThickness = new Thickness(0);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _title.CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent);
        Avalonia.Automation.AutomationProperties.SetName(_title, "Título da página");
        _title.GotFocus += (_, _) => _titleBefore = _st is null ? "" : S(_st.Meta["title"]);
        _title.TextChanged += (_, _) =>
        {
            if (_st is null || _loading || _title.Text == S(_st.Meta["title"])) return;
            if (_st.Set("meta.title", _title.Text ?? "", key: "meta.title")) Changed(panels: false);
        };
        _title.LostFocus += async (_, _) => await RenameToTitleAsync();
        _status.FontSize = 11;
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _status.Margin = new Thickness(8, 0, 0, 0);
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _status } };
        _undo = IconText("↶", "Desfazer");
        _undo.Click += (_, _) => Undo();
        _redo = IconText("↷", "Refazer");
        _redo.Click += (_, _) => Redo();
        var json = IconText("{ }", "JSON");
        json.Click += (_, _) => OpenJson();
        var export = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { UrbeTheme.Icon(UrbeTheme.Icons.Download, 16, UrbeTheme.AccentInk), new TextBlock { Text = "Exportar", FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk), VerticalAlignment = VerticalAlignment.Center } } },
            Height = 38, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(10), Background = UrbeTheme.Brush(UrbeTheme.Accent), VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(export, "Exportar");
        export.Click += async (_, _) => { await FlushAsync(); await _host.ExportMenuAsync(_path); };
        var more = IconButton(UrbeTheme.Icons.More, "Mais opções");
        more.Click += async (_, _) => { await FlushAsync(); await _host.MoreMenuAsync(_path); };
        var acts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { _undo, _redo, json, export, more } };
        var top = new DockPanel { Height = 58, Margin = new Thickness(6, 0) };
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(acts, Dock.Right);
        top.Children.Add(back);
        top.Children.Add(acts);
        top.Children.Add(name);

        _preview.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _preview.VerticalContentAlignment = VerticalAlignment.Stretch;
        var stage = new Border { Background = UrbeTheme.Brush(UrbeTheme.Surface), Child = _preview, ClipToBounds = true };
        foreach (var (key, label) in new[] { ("sections", "Seções"), ("theme", "Tema"), ("page", "Página") })
        {
            var tab = TabChip(label, () => { _leftTab = key; RenderPanels(); });
            tab.Tag = key;
            _leftTabs.Children.Add(tab);
        }
        var leftDock = new DockPanel { Width = 330 };
        DockPanel.SetDock(_leftTabs, Dock.Top);
        leftDock.Children.Add(_leftTabs);
        leftDock.Children.Add(_left);
        var leftSide = new Border { Child = leftDock, Background = UrbeTheme.Brush(UrbeTheme.Bg), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 0, 1, 0) };
        var rightSide = new Border { Child = _right, Width = 350, Background = UrbeTheme.Brush(UrbeTheme.Bg), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(1, 0, 0, 0) };
        _body.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        Grid.SetColumn(leftSide, 0);
        Grid.SetColumn(stage, 1);
        Grid.SetColumn(rightSide, 2);
        _body.Children.Add(leftSide);
        _body.Children.Add(stage);
        _body.Children.Add(rightSide);

        // the phone's sheet and tab bar (studio.js .ps-sheet / .ps-tabbar)
        var close = IconButton(UrbeTheme.Icons.Close, "Fechar");
        close.Click += (_, _) => { _sheet = null; RenderPanels(); };
        _sheetTitle.FontWeight = FontWeight.Bold;
        _sheetTitle.FontSize = 15;
        _sheetTitle.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _sheetTitle.VerticalAlignment = VerticalAlignment.Center;
        var sheetHead = new DockPanel { Margin = new Thickness(14, 4, 6, 0), Height = 46 };
        DockPanel.SetDock(close, Dock.Right);
        sheetHead.Children.Add(close);
        sheetHead.Children.Add(_sheetTitle);
        var sheetDock = new DockPanel();
        DockPanel.SetDock(sheetHead, Dock.Top);
        sheetDock.Children.Add(sheetHead);
        sheetDock.Children.Add(_sheetBody);
        _sheetBox.Child = sheetDock;
        _sheetBox.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        _sheetBox.CornerRadius = new CornerRadius(18, 18, 0, 0);
        _sheetBox.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _sheetBox.BorderThickness = new Thickness(0, 1, 0, 0);
        _sheetBox.VerticalAlignment = VerticalAlignment.Stretch;
        _sheetBox.IsVisible = false;
        _tabbar.ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*");
        _tabbar.Height = 62;
        _tabbar.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        int col = 0;
        foreach (var (key, label, icon) in new[] { ("sections", "Seções", UrbeTheme.Icons.List), ("edit", "Editar", UrbeTheme.Icons.Edit), ("add", "", UrbeTheme.Icons.Plus), ("theme", "Tema", UrbeTheme.Icons.Brush), ("page", "Página", UrbeTheme.Icons.Page) })
        {
            Button b;
            if (key == "add")
            {
                b = new Button
                {
                    Content = UrbeTheme.Icon(icon, 24, UrbeTheme.AccentInk, 2.4), Width = 50, Height = 50, CornerRadius = new CornerRadius(25), Padding = new Thickness(0),
                    Background = UrbeTheme.Brush(UrbeTheme.Accent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
                };
                ToolTip.SetTip(b, "Adicionar bloco");
                Avalonia.Automation.AutomationProperties.SetName(b, "Adicionar bloco");
                b.Click += (_, _) => OpenLibrary();
            }
            else
            {
                b = new Button
                {
                    Content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, Children = { UrbeTheme.Icon(icon, 20, UrbeTheme.Text2), new TextBlock { Text = label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) } } },
                    Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
                };
                Avalonia.Automation.AutomationProperties.SetName(b, label);
                b.Click += (_, _) => OpenSheet(key);
            }
            Grid.SetColumn(b, col++);
            _tabbar.Children.Add(b);
        }

        // the phone's sheet sits under the preview (not over it: the preview is a platform view that covers the app)
        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
        Grid.SetRow(top, 0);
        Grid.SetRow(_body, 1);
        Grid.SetRow(_sheetBox, 2);
        Grid.SetRow(_tabbar, 3);
        main.Children.Add(top);
        main.Children.Add(_body);
        main.Children.Add(_sheetBox);
        main.Children.Add(_tabbar);
        var root = new Grid();
        root.Children.Add(main);
        root.Children.Add(_overlay);
        Child = root;
        PropertyChanged += (_, e) =>
        {
            if (e.Property != BoundsProperty || _st is null) return;
            if (Wide != _wideLast) { _wideLast = Wide; Layout(); RenderPanels(); }
        };
        KeyDown += OnKey;
    }

    /// <summary>The page open in the studio, or null.</summary>
    public string? Path => IsVisible && _st is not null ? _path : null;
    public PageStudio? Model => _st;
    public string Status => _status.Text ?? "";
    /// <summary>The HTML last shown in the preview (tests).</summary>
    public string LastHtml { get; private set; } = "";
    public bool Wide => Bounds.Width >= 1000;
    /// <summary>The panel shown in the phone's sheet ("sections", "edit", "theme", "page") or null.</summary>
    public string? Sheet => _sheet;
    public string LeftTab => _leftTab;

    private bool _loading;

    /// <summary>open(id): the page normalized, a fresh history, the preview drawn.</summary>
    public async Task OpenAsync(string path, string text)
    {
        _saveTimer.Stop();
        _path = path;
        // the Urbe's own fields too (UC-62: the vault's queries in Notas), the same engine options as the preview's
        _st = new PageStudio(new PageEngine { UrbeMarks = true }, text);
        _st.Changed += (_, _) => { };
        _sheet = null;
        IsVisible = true;
        _coverTimer.Start();
        _wideLast = Wide;
        Layout();
        RenderAll();
        if (_st.InvalidJson)
            await _host.Dialogs.AlertAsync("JSON inválido", "O arquivo " + path + " não é um JSON válido. Abra como texto para corrigir, ou use “JSON” para colar uma versão válida.");
        else if (_st.OpenProblems > 0) Toast(_st.OpenProblems + " problema(s) no arquivo foram corrigidos ao abrir.");
        await RenderPreviewAsync();
    }

    /// <summary>The page was changed from outside (the Assistente, another editor): one undo step back.</summary>
    public void ExternalUpdate(string text)
    {
        if (_st is null || text == _st.SavedText) return;
        _st.Replace(text);
        RenderAll();
        SchedulePreview();
        Toast("Página atualizada por fora (Assistente ou outro editor).");
    }

    /// <summary>The page file moved (renamed elsewhere).</summary>
    public void Renamed(string path) => _path = path;

    public async Task CloseAsync()
    {
        if (!IsVisible) return;
        await FlushAsync();
        IsVisible = false;
        _overlay.IsVisible = false;
        _overlay.Children.Clear();
        ReleasePreview();
        _st = null;
        _host.Closed();
    }

    /// <summary>The page left without saving (it went to the trash).</summary>
    public void Discard()
    {
        _saveTimer.Stop();
        IsVisible = false;
        ReleasePreview();
        _st = null;
    }

    /// <summary>The preview's browser let go when the studio closes: it never stays over the other screens.</summary>
    private void ReleasePreview()
    {
        _coverTimer.Stop();
        _preview.Content = null;
        _previewPath = null;
        _postedSelection = null;
    }

    private void UpdateCover()
    {
        if (_preview.Content is not NativeControlHost native) return;
        bool covered = !IsEffectivelyVisible || _overlay.IsVisible || _host.Dialogs.IsOpen || _host.PreviewCovered;
        if (native.IsVisible == !covered) return;
        native.IsVisible = !covered;
        if (!covered) _postedSelection = null;
    }

    /// <summary>The selected part marked in the preview (the 1.8.4 posts it to the iframe): a second tap on it writes in place.</summary>
    private void PostSelection()
    {
        if (_st is null || _preview.Content is not IHtmlReloadable live) return;
        var key = _st.Selected + "|" + _st.NodeSelected;
        if (key == _postedSelection) return;
        _postedSelection = key;
        live.Post(SelectionMessage(scroll: true));
    }

    private string SelectionMessage(bool scroll)
    {
        var m = new JsonObject { ["urbePage"] = "select", ["host"] = true, ["id"] = _st?.Selected, ["nid"] = _st?.NodeSelected, ["scroll"] = scroll, ["instant"] = true };
        return m.ToJsonString();
    }

    private void Layout()
    {
        bool wide = Wide;
        _body.Children[0].IsVisible = wide;
        _body.Children[2].IsVisible = wide;
        _tabbar.IsVisible = !wide;
        if (wide) _sheet = null;
    }

    private void RenderAll()
    {
        RenderChrome();
        RenderPanels();
    }

    private void RenderChrome()
    {
        if (_st is null) return;
        _loading = true;
        if (!_title.IsFocused) _title.Text = S(_st.Meta["title"]);
        _loading = false;
        _undo.IsEnabled = _st.CanUndo;
        _redo.IsEnabled = _st.CanRedo;
        if (_status.Text is null or "") _status.Text = "Salvo";
    }

    /// <summary>renderPanels: from 1000 px the left tab and Editar; on a phone the open sheet.</summary>
    public void RenderPanels()
    {
        if (_st is null) return;
        RenderChrome();
        Layout();
        foreach (var t in _leftTabs.Children.OfType<Button>()) MarkChip(t, (string?)t.Tag == _leftTab);
        PostSelection();
        if (Wide)
        {
            _left.Child = Panel(_leftTab);
            _right.Child = Panel("edit");
            _sheetBox.IsVisible = false;
        }
        else
        {
            _left.Child = null;
            _right.Child = null;
            _sheetBox.IsVisible = _sheet is not null;
            if (_sheet is not null)
            {
                _sheetTitle.Text = _sheet switch
                {
                    "sections" => "Seções", "theme" => "Tema", "page" => "Página",
                    _ => _st.SelectedSection is { } s ? _st.Label(s).Label : "Editar"
                };
                _sheetBox.Height = Math.Max(240, Bounds.Height * .5);
                _sheetBody.Content = Panel(_sheet);
            }
        }
    }

    private void OpenSheet(string name)
    {
        _sheet = _sheet == name ? null : name;
        RenderPanels();
    }

    private Control Panel(string name)
    {
        var content = name switch { "sections" => SectionsPanel(), "theme" => ThemePanel(), "page" => PagePanel(), _ => EditPanel() };
        return new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content };
    }

    // ---------------------------------------------------------------- changes, saving, preview

    /// <summary>After every commit: the panels (unless typing), the chrome, a save in 600 ms and a new preview.</summary>
    private void Changed(bool panels = true)
    {
        if (panels) RenderPanels(); else RenderChrome();
        _status.Text = "Salvando…";
        _saveTimer.Stop();
        _saveTimer.Start();
        SchedulePreview();
    }

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    /// <summary>save(): the page file written when its text changed; the error stays on the status.</summary>
    private async Task SaveAsync()
    {
        if (_st is null) return;
        var text = _st.Text();
        if (text == _st.SavedText) { _status.Text = "Salvo"; return; }
        try
        {
            await _host.SaveAsync(_path, text);
            _st.SavedText = text;
            _status.Text = "Salvo";
            ToolTip.SetTip(_status, null);
        }
        catch (Exception ex)
        {
            _status.Text = "Erro ao salvar";
            ToolTip.SetTip(_status, ex.Message);
        }
    }

    /// <summary>flush(): a pending save written now.</summary>
    public async Task FlushAsync()
    {
        if (_st is null) return;
        _saveTimer.Stop();
        await SaveAsync();
    }

    private async Task RenderPreviewAsync()
    {
        if (_st is null) return;
        var html = _host.Render(_st.Spec, preview: true);
        if (html == LastHtml && _preview.Content is not null) return;
        LastHtml = html;
        // the same browser shows the new page (creating one per change made the phone slow)
        // the selected part stays marked in the new page
        var shown = html + "<script>addEventListener('load',function(){postMessage(" + SelectionMessage(scroll: false) + ",'*')})</script>";
        _postedSelection = _st.Selected + "|" + _st.NodeSelected;
        if (_preview.Content is IHtmlReloadable live && _previewPath == _path) { live.Reload(shown); return; }
        _previewPath = _path;
        var viewer = await _host.PreviewAsync(_path, System.Text.Encoding.UTF8.GetBytes(shown), PreviewMessage);
        _preview.Content = viewer ?? Outline();
    }

    /// <summary>Without a platform viewer (desktop preview, tests): the page's outline.</summary>
    private Control Outline()
    {
        var list = new StackPanel { Spacing = 6, Margin = new Thickness(24), MaxWidth = 640 };
        list.Children.Add(new TextBlock { Text = (S(_st!.Meta["icon"]) is { Length: > 0 } i ? i + "  " : "") + S(_st.Meta["title"]), FontSize = 24, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextWrapping = TextWrapping.Wrap });
        list.Children.Add(new TextBlock { Text = "A prévia completa aparece no aparelho; Exportar → Baixar HTML abre a página no navegador.", FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap });
        foreach (var s in _st.Sections.OfType<JsonObject>())
        {
            var l = _st.Label(s);
            list.Children.Add(new Border
            {
                Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(10), Background = UrbeTheme.Brush(UrbeTheme.Surface2),
                Opacity = PageEngine.Truthy(s["style"]?["hidden"]) ? .5 : 1,
                Child = new TextBlock { Text = l.Icon + "  " + l.Label + (l.Title.Length > 0 ? " — " + l.Title : ""), Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextWrapping = TextWrapping.Wrap }
            });
        }
        return new ScrollViewer { Content = list, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    /// <summary>
    /// onFrameMessage: a part of the preview tapped selects its section (and the free element) and opens Editar;
    /// text written straight into a free element becomes its content (a heading stays on one line).
    /// </summary>
    public void PreviewMessage(string json)
    {
        if (_st is null) return;
        JsonObject? m;
        try { m = JsonNode.Parse(json) as JsonObject; }
        catch (System.Text.Json.JsonException) { return; }
        var kind = S(m?["urbePage"]);
        var id = S(m?["id"]);
        var nid = m?["nid"] is JsonValue nv && nv.TryGetValue<string>(out var ns) ? ns : null;
        if (kind == "select")
        {
            if (id != _st.Selected) _st.NodeSelected = null;
            _st.Select(id.Length > 0 ? id : null);
            if (nid is not null) _st.NodeSelected = nid;
            if (!Wide && _st.Selected is not null) _sheet = "edit";
            RenderPanels();
            return;
        }
        if (kind == "comment")
        {
            _ = CommentAsync(m!["field"] is JsonValue fv && fv.TryGetValue<string>(out var f) ? f : null,
                m["note"] is JsonValue nov && nov.TryGetValue<string>(out var note) ? note : null,
                m["n"] is JsonValue cv && cv.TryGetValue<int>(out var n) ? n : -1);
            return;
        }
        if (kind != "text" || nid is null || _st.Section(id) is not { } sec || (string?)sec["type"] != "free") return;
        if (PageEngine.FreeFind(sec["props"]?["root"] as JsonObject, nid) is not { } hit || hit.Node["content"] is not JsonObject content || content["text"] is null) return;
        var text = System.Text.RegularExpressions.Regex.Replace(S(m!["text"]).Replace('\u00a0', ' '), "\n{3,}", "\n\n").Trim();
        if ((string?)hit.Node["type"] == "heading") text = System.Text.RegularExpressions.Regex.Replace(text, @"\s*\n\s*", " ");
        _st.Break();
        if (_st.Set(PageStudio.FreePath(id, nid, "content", "text"), JsonValue.Create(text))) Changed();
    }

    /// <summary>
    /// "Comentar" in a 💬 of the preview (UC-49): its comments and a box for one more; the new one is written after them
    /// in the page's own text (one undo step, saved like any change) or in the note the page shows (saved and verified).
    /// </summary>
    private async Task CommentAsync(string? field, string? note, int n)
    {
        if (_st is null || n < 0) return;
        string? text = field is not null ? (_st.Get(field) is JsonValue v && v.TryGetValue<string>(out var s) ? s : null)
            : note is not null ? _host.Note(note)?.Content : null;
        if (PageComments.At(text, n) is not { } group) { Toast("O texto mudou; toque de novo no 💬."); return; }
        var added = await _host.Dialogs.CommentsAsync("Comentários", group.Comments);
        if (_st is null || string.IsNullOrWhiteSpace(added)) return;
        if (field is not null)
        {
            // read again: the page may have changed while the box was open
            var now = _st.Get(field) is JsonValue nv && nv.TryGetValue<string>(out var ns) ? ns : null;
            if (now != text || PageComments.Add(now, n, added) is not { } updated) { Toast("O texto mudou; o comentário não foi gravado."); return; }
            _st.Break();
            if (_st.Set(field, JsonValue.Create(updated))) Changed();
            return;
        }
        var current = _host.Note(note!);
        if (current is null || current.Content != text || PageComments.Add(text, n, added) is not { } written) { Toast("A nota mudou; o comentário não foi gravado."); return; }
        try
        {
            await _host.SaveNoteAsync(current.Path, written);
            SchedulePreview();
        }
        catch (Exception ex) { Toast("Não foi possível gravar o comentário: " + ex.Message); }
    }

    private void Undo()
    {
        if (_st is null) return;
        if (!_st.Undo()) { Toast("Nada para desfazer."); return; }
        RenderAll();
        Changed();
    }

    private void Redo()
    {
        if (_st is null) return;
        if (!_st.Redo()) { Toast("Nada para refazer."); return; }
        RenderAll();
        Changed();
    }

    /// <summary>renameToTitle: the file follows the title while its name was automatic or the previous title.</summary>
    private async Task RenameToTitleAsync()
    {
        if (_st is null) return;
        var target = PageStudio.RenameTarget(_path, S(_st.Meta["title"]), _titleBefore, _host.Exists);
        if (target is null) return;
        await FlushAsync();
        try
        {
            _path = await _host.RenameAsync(_path, target);
            Toast("Arquivo renomeado para " + _path[(_path.LastIndexOf('/') + 1)..] + ".");
        }
        catch (Exception ex) { Toast("Não foi possível renomear: " + ex.Message); }
    }

    // ---------------------------------------------------------------- keys (studio.js onKey)

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_st is null || _host.Dialogs.IsOpen) return;
        bool mod = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool typing = e.Source is TextBox;
        if (_overlay.IsVisible)
        {
            if (e.Key == Key.Escape) { CloseOverlay(); e.Handled = true; }
            return;
        }
        if (mod && e.Key == Key.Z) { if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) Redo(); else Undo(); e.Handled = true; return; }
        if (mod && e.Key == Key.Y) { Redo(); e.Handled = true; return; }
        if (mod && e.Key == Key.S) { _ = FlushAsync(); Toast("Página salva."); e.Handled = true; return; }
        if (mod && e.Key == Key.J) { OpenJson(); e.Handled = true; return; }
        if (mod && e.Key == Key.E) { _ = FlushAsync().ContinueWith(_ => Dispatcher.UIThread.Post(() => _ = _host.ExportMenuAsync(_path))); e.Handled = true; return; }
        if (mod && e.Key == Key.D && _st.Selected is { } d) { Duplicate(d); e.Handled = true; return; }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key is Key.Up or Key.Down && _st.Selected is { } m) { Move(m, e.Key == Key.Up ? -1 : 1); e.Handled = true; return; }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (typing) { Focus(); return; }
            if (_sheet is not null) { _sheet = null; RenderPanels(); return; }
            if (_st.Selected is not null) { _st.Select(null); RenderPanels(); return; }
            _ = CloseAsync();
            return;
        }
        if (typing || mod || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        if (e.Key is Key.OemQuestion or Key.Divide or Key.A) { OpenLibrary(); e.Handled = true; return; }
        if (e.Key is Key.Delete && _st.Selected is { } del) { Remove(del); e.Handled = true; return; }
        if (e.Key is Key.Up or Key.Down or Key.J or Key.K && _st.Sections.Count > 0)
        {
            int i = _st.Selected is { } sel ? _st.IndexOf(sel) : -1, dir = e.Key is Key.Up or Key.K ? -1 : 1;
            i = i < 0 ? (dir > 0 ? 0 : _st.Sections.Count - 1) : Math.Clamp(i + dir, 0, _st.Sections.Count - 1);
            _st.Select((string?)_st.Sections[i]!["id"]);
            RenderPanels();
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------- small pieces

    private static string S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n is null ? "" : LegacyCustomize.JsToString(n);

    /// <summary>toast(msg, action): a short notice at the bottom, with an action button (5 s) or alone (2.4 s).</summary>
    private void Toast(string text, string? actionLabel = null, Action? action = null)
    {
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        line.Children.Add(new TextBlock { Text = text, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextWrapping = TextWrapping.Wrap, MaxWidth = 460, VerticalAlignment = VerticalAlignment.Center });
        var t = new Border
        {
            Background = UrbeTheme.Brush(UrbeTheme.Surface3), CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10), Margin = new Thickness(16, 0, 16, Wide ? 76 : 6),   // phone: over the tab bar, not over the preview
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
            Child = line
        };
        LastToast = text;
        if (Child is not Grid g) return;
        if (actionLabel is not null && action is not null)
        {
            var b = new Button { Content = new TextBlock { Text = actionLabel, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) }, Background = Brushes.Transparent, Padding = new Thickness(6, 2) };
            b.Click += (_, _) => { action(); g.Children.Remove(t); };
            line.Children.Add(b);
        }
        foreach (var old in g.Children.OfType<Border>().Where(x => x.Tag as string == "toast").ToList()) g.Children.Remove(old);
        t.Tag = "toast";
        g.Children.Add(t);
        DispatcherTimer.RunOnce(() => g.Children.Remove(t), TimeSpan.FromMilliseconds(action is null ? 2400 : 5000));
    }

    /// <summary>The last toast (tests).</summary>
    public string? LastToast { get; private set; }

    private static Button IconButton(string icon, string tip)
    {
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 20, UrbeTheme.Text2, 1.8), Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        return b;
    }

    private static Button IconText(string glyph, string tip)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = glyph, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) },
            Width = 40, Height = 40, Padding = new Thickness(0), CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        return b;
    }

    private static Button TabChip(string label, Action click)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeight.SemiBold }, Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1)
        };
        b.Click += (_, _) => click();
        MarkChip(b, false);
        return b;
    }

    private static void MarkChip(Button b, bool on)
    {
        b.Background = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Surface2, on ? .18 : 1);
        b.BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2);
        if (b.Content is TextBlock t) t.Foreground = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Text2);
    }
}
