using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// Recursos (the workspace's resources tab): everything the game uses, in one place. Create tiles open the Studios;
/// files are grouped (Arte, Mapas, Sons, Partículas, Dados, Fontes), with thumbnails and a search; a tap edits what a
/// Studio edits and shows the rest in a bottom sheet (preview, size, play, the path to use in code). Importar brings
/// files from the device into the right Content/ folder. The editable source hides its generated twin (.png of a
/// sprite, .wav of a sound or song, .schema.json of a table).
/// </summary>
public sealed class ResourcesPage : UserControl
{
    private readonly LunetShell _shell;
    private readonly WorkspaceView _workspace;
    private readonly StackPanel _list = new() { Spacing = LunetTokens.Space.Xxs };
    private readonly Grid _root = new();
    private readonly Dictionary<string, Bitmap?> _thumbs = new(StringComparer.Ordinal);
    private Control? _sheet;
    private string _query = "";
    private string? _notice;

    public ResourcesPage(LunetShell shell, WorkspaceView workspace)
    {
        _shell = shell;
        _workspace = workspace;
        var search = new TextBox { PlaceholderText = "Buscar recursos", MinHeight = LunetTokens.MinTouch, VerticalContentAlignment = VerticalAlignment.Center, Name = "ResourceSearch" };
        AutomationProperties.SetName(search, "Buscar recursos");
        search.TextChanged += (_, _) => { _query = search.Text ?? ""; Fill(); };
        var page = new StackPanel { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.M, LunetTokens.Space.M, 96) };
        page.Children.Add(LunetUi.Title("Recursos"));
        page.Children.Add(CreateTiles());
        page.Children.Add(search);
        page.Children.Add(_list);
        _root.Children.Add(new ScrollViewer { Content = new Border { Child = page, MaxWidth = 900 } });
        Content = _root;
        Fill();
    }

    /// <summary>The path a file has in the game's code (relative to Content/).</summary>
    public static string CodePath(string file) => file.StartsWith("Content/", StringComparison.Ordinal) ? file["Content/".Length..] : file;

    public string? Open { get; private set; }
    public IReadOnlyList<string> Shown { get; private set; } = [];

    /// <summary>Copies a file into the folder its kind belongs to (never over another file). Returns its project path.</summary>
    public string Import(string fileName, byte[] bytes)
    {
        var name = Path.GetFileName(fileName);
        if (name.Length == 0) throw new ArgumentException("Arquivo sem nome.");
        var ext = Path.GetExtension(name).ToLowerInvariant();
        var folder = ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif" or ".aseprite" => "Content/Textures",
            ".wav" or ".ogg" or ".mp3" => "Content/Audio",
            ".ttf" or ".otf" => "Content/Fonts",
            ".json" or ".csv" or ".txt" => "Content/Data",
            _ => "Content",
        };
        var files = _shell.Workspace.Files().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stem = Path.GetFileNameWithoutExtension(name);
        var path = $"{folder}/{name}";
        for (var n = 2; files.Contains(path); n++) path = $"{folder}/{stem}-{n}{ext}";
        // Verified write, as the Studios' files (StudioAssetWorkspace).
        new Lunet.Core.StudioAssetWorkspace(_shell.Workspace.Project!.Directory).WriteBytes(path, bytes);
        _notice = $"Importado: {path}";
        Fill();
        return path;
    }

    private async Task PickAndImport()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var picked = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Importar para o jogo", AllowMultiple = true });
        var count = 0;
        foreach (var file in picked)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                Import(file.Name, buffer.ToArray());
                count++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _notice = $"Não foi possível importar {file.Name}: {e.Message}";
            }
        }
        if (count > 1) _notice = $"{count} arquivos importados.";
        Fill();
    }

    private Control CreateTiles()
    {
        var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
        void Tile(string icon, string label, Action act, string name, bool primary = false)
        {
            var content = new StackPanel
            {
                Spacing = LunetTokens.Space.Xs,
                Children = { LunetIcons.Create(icon, primary ? LunetTokens.Color.OnPrimary : LunetTokens.Color.Primary, 22), LunetUi.Text(label, LunetTokens.Type.Label, primary ? LunetTokens.Color.OnPrimary : LunetTokens.Color.TextPrimary, FontWeight.SemiBold) },
            };
            var b = new Button
            {
                Content = content, Width = 116, Height = 76, Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xs),
                Padding = new Thickness(LunetTokens.Space.S), CornerRadius = new CornerRadius(LunetTokens.Radius.L), BorderThickness = new Thickness(0),
                Background = LunetTokens.Brush(primary ? LunetTokens.Color.Primary : LunetTokens.Color.Surface),
                HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top, Name = name,
            };
            AutomationProperties.SetName(b, label);
            b.Click += (_, _) => act();
            tiles.Children.Add(b);
        }
        Tile(LunetIcons.Edit, "Sprite", () => _workspace.NewSprite(), "NewSprite", primary: true);
        Tile(LunetIcons.Map, "Mapa", () => _workspace.NewMap(), "NewMap");
        Tile(LunetIcons.Sound, "Efeito", () => _workspace.NewSound(), "NewSound");
        Tile(LunetIcons.Sound, "Música", () => _workspace.NewMusic(), "NewMusic");
        Tile(LunetIcons.Pulse, "Partículas", () => _workspace.NewParticles(), "NewParticles");
        Tile(LunetIcons.File, "Tabela", () => _workspace.NewData(), "NewData");
        Tile(LunetIcons.Art, "Paleta", () => _workspace.OpenPalette(), "OpenPalette");
        Tile(LunetIcons.Download, "Importar", () => _ = PickAndImport(), "ImportFiles");
        return tiles;
    }

    private static bool IsFont(string f) => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase);
    private static bool IsImage(string f) => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
    private static bool IsParticles(string f) => f.EndsWith(".particles.json", StringComparison.OrdinalIgnoreCase);

    /// <summary>The groups and their files, without the generated twins of editable sources.</summary>
    public static IReadOnlyList<(string Label, string Icon, IReadOnlyList<string> Files)> Groups(IReadOnlyList<string> all)
    {
        var set = all.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Twin(string f)
        {
            if (f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && set.Contains(f[..^4] + ".sprite.json")) return true;
            if (f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) && (set.Contains(f[..^4] + ".sfx.json") || set.Contains(f[..^4] + ".song.json"))) return true;
            return false;
        }
        var visible = all.Where(f => !Twin(f)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        return
        [
            ("Arte", LunetIcons.Art, visible.Where(ProjectFiles.IsArt).ToArray()),
            ("Mapas", LunetIcons.Map, visible.Where(ProjectFiles.IsMap).ToArray()),
            ("Sons", LunetIcons.Sound, visible.Where(f => ProjectFiles.IsSound(f) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)).ToArray()),
            ("Partículas", LunetIcons.Pulse, visible.Where(IsParticles).ToArray()),
            ("Dados", LunetIcons.File, visible.Where(DataView.IsTable).ToArray()),
            ("Fontes", LunetIcons.File, visible.Where(IsFont).ToArray()),
        ];
    }

    private void Fill()
    {
        _list.Children.Clear();
        if (_notice is { } notice)
        {
            _list.Children.Add(new Border
            {
                Child = LunetUi.Text(notice, LunetTokens.Type.Label), Padding = new Thickness(LunetTokens.Space.S),
                Background = LunetTokens.Brush(LunetTokens.Color.Surface), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
                BorderBrush = LunetTokens.Brush(notice.StartsWith("Não", StringComparison.Ordinal) ? LunetTokens.Color.Danger : LunetTokens.Color.Success), BorderThickness = new Thickness(3, 0, 0, 0),
            });
        }
        var shown = new List<string>();
        foreach (var (label, icon, files) in Groups(_shell.Workspace.Files()))
        {
            var match = files.Where(f => _query.Length == 0 || f.Contains(_query.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
            if (match.Length == 0 && (_query.Length > 0 || label == "Fontes")) continue;
            var heading = LunetUi.Text($"{label.ToUpperInvariant()} · {match.Length}", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
            heading.Margin = new Thickness(0, LunetTokens.Space.M, 0, LunetTokens.Space.Xxs);
            _list.Children.Add(heading);
            if (match.Length == 0) _list.Children.Add(LunetUi.Text("Nada ainda.", LunetTokens.Type.Label, LunetTokens.Color.TextTertiary));
            foreach (var f in match) { _list.Children.Add(Row(f, icon)); shown.Add(f); }
        }
        if (_query.Length > 0 && shown.Count == 0) _list.Children.Add(LunetUi.Text("Nenhum recurso com esse nome.", LunetTokens.Type.Label, LunetTokens.Color.TextTertiary));
        Shown = shown;
    }

    private bool Editable(string f) =>
        f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) || ProjectFiles.IsMap(f) || f.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase)
        || f.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase) || IsParticles(f) || DataView.IsTable(f);

    /// <summary>A tap on a file: the Studio that edits it, or the details sheet.</summary>
    public void Activate(string f)
    {
        if (f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)) _workspace.OpenSprite(f);
        else if (ProjectFiles.IsMap(f)) _workspace.OpenMap(f);
        else if (f.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase)) _workspace.OpenSound(f);
        else if (f.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase)) _workspace.OpenMusic(f);
        else if (IsParticles(f)) _workspace.OpenParticles(f);
        else if (DataView.IsTable(f)) _workspace.OpenData(f);
        else ShowDetails(f);
    }

    private Control Row(string f, string icon)
    {
        var editable = Editable(f);
        Control lead = Thumb(f) is { } thumb
            ? new Border
            {
                Width = 44, Height = 44, CornerRadius = new CornerRadius(LunetTokens.Radius.S), ClipToBounds = true, Background = LunetTokens.Brush(LunetTokens.Color.Stage),
                Child = Crisp(new Image { Source = thumb, Stretch = Stretch.Uniform, Margin = new Thickness(4) }),
            }
            : new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(LunetTokens.Radius.S), Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised), Child = LunetIcons.Create(icon, LunetTokens.Color.TextSecondary, 20) };
        var name = Path.GetFileName(f);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        text.Children.Add(LunetUi.Text(name, LunetTokens.Type.Callout, weight: FontWeight.SemiBold));
        text.Children.Add(LunetUi.Text((Path.GetDirectoryName(f) ?? "").Replace('\\', '/') + (editable ? " · editar" : " · ver"), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var row = new DockPanel();
        DockPanel.SetDock(lead, Dock.Left);
        lead.Margin = new Thickness(0, 0, LunetTokens.Space.S, 0);
        row.Children.Add(lead);
        row.Children.Add(text);
        var b = new Button
        {
            Content = row, MinHeight = 56, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0), Padding = new Thickness(0, LunetTokens.Space.Xxs),
            CornerRadius = new CornerRadius(LunetTokens.Radius.M), Name = "Resource-" + f,
        };
        AutomationProperties.SetName(b, (editable ? "Editar " : "Ver ") + f);
        var path = f;
        b.Click += (_, _) => Activate(path);
        return b;
    }

    private static Image Crisp(Image image) { RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None); return image; }

    /// <summary>A small picture of an image or sprite (its sheet), read once per page.</summary>
    private Bitmap? Thumb(string f)
    {
        var png = f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) ? f[..^".sprite.json".Length] + ".png" : IsImage(f) ? f : null;
        if (png is null) return null;
        if (_thumbs.TryGetValue(png, out var cached)) return cached;
        Bitmap? bitmap = null;
        try
        {
            var full = Path.Combine(_shell.Workspace.Project!.Directory, png.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full) && new FileInfo(full).Length < 16 * 1024 * 1024)
            {
                using var stream = File.OpenRead(full);
                bitmap = new Bitmap(stream);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException) { bitmap = null; }
        return _thumbs[png] = bitmap;
    }

    // ---------- details sheet ----------

    public bool CloseSheet()
    {
        if (_sheet is null) return false;
        _root.Children.Remove(_sheet);
        _sheet = null;
        Open = null;
        return true;
    }

    public void ShowDetails(string f)
    {
        CloseSheet();
        Open = f;
        var full = Path.Combine(_shell.Workspace.Project!.Directory, f.Replace('/', Path.DirectorySeparatorChar));
        var size = File.Exists(full) ? new FileInfo(full).Length : 0;
        var body = new StackPanel { Spacing = LunetTokens.Space.S };
        body.Children.Add(new Border { Width = 40, Height = 4, CornerRadius = new CornerRadius(2), Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong), HorizontalAlignment = HorizontalAlignment.Center });
        body.Children.Add(LunetUi.Text(Path.GetFileName(f), LunetTokens.Type.Title, weight: FontWeight.SemiBold));
        var facts = $"{f} · {(size >= 1024 * 1024 ? $"{size / 1024.0 / 1024:0.0} MB" : $"{Math.Max(1, size / 1024)} KB")}";
        if (Thumb(f) is { } image)
        {
            facts += $" · {image.PixelSize.Width}×{image.PixelSize.Height} px";
            body.Children.Add(new Border
            {
                Background = LunetTokens.Brush(LunetTokens.Color.Stage), CornerRadius = new CornerRadius(LunetTokens.Radius.M), Padding = new Thickness(LunetTokens.Space.S), MaxHeight = 260,
                Child = Crisp(new Image { Source = image, Stretch = Stretch.Uniform, MaxHeight = 240 }),
            });
        }
        body.Children.Add(LunetUi.Text(facts, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        void Act(Button b, string name) { b.Name = name; b.Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xs); actions.Children.Add(b); }
        if (f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) && _shell.PlaySound is { } play && File.Exists(full))
            Act(LunetUi.Button("Ouvir", ButtonKind.Primary, LunetIcons.Play, () => play(File.ReadAllBytes(full))), "PlayResource");
        Act(LunetUi.Button("Copiar caminho", ButtonKind.Secondary, LunetIcons.Copy, () => _ = CopyPath(f)), "CopyResourcePath");
        Act(LunetUi.Button("Fechar", ButtonKind.Ghost, null, () => CloseSheet()), "CloseResource");
        body.Children.Add(actions);
        body.Children.Add(LunetUi.Text($"No código: \"{CodePath(f)}\"", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        var sheet = new Border
        {
            Child = body, VerticalAlignment = VerticalAlignment.Bottom, MaxWidth = 640, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S, LunetTokens.Space.M, LunetTokens.Space.L),
            Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised), CornerRadius = new CornerRadius(LunetTokens.Radius.L, LunetTokens.Radius.L, 0, 0), Name = "ResourceSheet",
        };
        var scrim = new Border { Background = LunetTokens.Brush("#99000000") };
        scrim.PointerPressed += (_, e) => { CloseSheet(); e.Handled = true; };
        _sheet = new Grid { Children = { scrim, sheet } };
        _root.Children.Add(_sheet);
    }

    private async Task CopyPath(string f)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, CodePath(f));
        _notice = $"Caminho copiado: {CodePath(f)}";
        CloseSheet();
        Fill();
    }
}
