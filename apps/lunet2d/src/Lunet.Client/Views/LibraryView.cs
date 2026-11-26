using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// The Biblioteca in the shell (A6, owner 25/11: "Biblioteca tá com design péssimo"): your assets, reusable in any
/// game, over the existing <see cref="ReusableAssetLibrary"/> (same folder as the previous screen, nothing lost).
/// <list type="bullet">
/// <item>One list, searched as you type, a picture per asset, the newest version of each.</item>
/// <item>An asset opens its page: the picture, who made it and its license, its versions, and one clear action —
/// <b>Usar num jogo</b> — with the rest (Nova versão, Exportar ZIP, Verificar) below it.</item>
/// <item>Publishing is three steps on one page: the game, the file, the name and license.</item>
/// </list>
/// Importing never overwrites a game's file: a name already used blocks it and says which.
/// </summary>
public sealed class LibraryView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly ScrollViewer _scroll = new();
    private readonly TextBlock _title = LunetUi.Title("Biblioteca");
    private readonly Stack<Action> _history = new();
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly TextBox _search = new()
    {
        PlaceholderText = "Buscar por nome, tag, autor ou licença", MinHeight = LunetTokens.MinTouch,
        FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Callout, VerticalContentAlignment = VerticalAlignment.Center,
    };
    private readonly StackPanel _results = new() { Spacing = 2 };
    private readonly ReusableAssetLibrary _library;
    private Action _current;

    public LibraryView(LunetShell shell, Action close)
    {
        _shell = shell;
        _close = close;
        _library = new ReusableAssetLibrary(shell.LibraryPath);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        AutomationProperties.SetName(_search, "Buscar na biblioteca");
        _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); FillResults(); };
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Back());
        _title.VerticalAlignment = VerticalAlignment.Center;
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        bar.Children.Add(_title);
        _scroll.Content = new Border { Child = _page, MaxWidth = 760, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, 96) };
        var publish = LunetUi.Button("Publicar", ButtonKind.Primary, LunetIcons.Plus, () => Go(ShowPublishGames));
        publish.Name = "Publish";
        publish.MinHeight = 56;
        publish.HorizontalAlignment = HorizontalAlignment.Right;
        publish.VerticalAlignment = VerticalAlignment.Bottom;
        publish.Margin = new Thickness(LunetTokens.Space.M);
        _publish = publish;
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new Grid { Children = { _scroll, publish } });
        Content = root;
        _current = ShowList;
        ShowList();
    }

    private readonly Button _publish;

    /// <summary>What is on screen: "list", an asset id, "publish"… (tests and screen readers).</summary>
    public string Showing { get; private set; } = "list";

    /// <summary>The assets listed now (newest version of each).</summary>
    public IReadOnlyList<LibraryAssetVersion> Listed { get; private set; } = [];

    /// <summary>Back inside the Biblioteca; at the list it closes it. Always consumed.</summary>
    public bool Back()
    {
        if (_history.Count == 0) { _close(); return true; }
        _current = _history.Pop();
        _current();
        return true;
    }

    private void Go(Action page)
    {
        _history.Push(_current);
        _current = page;
        page();
    }

    private void Begin(string title, string showing, bool publishButton = false)
    {
        _title.Text = title;
        Showing = showing;
        _publish.IsVisible = publishButton;
        _page.Children.Clear();
        _scroll.Offset = default;
    }

    // ---- list ----

    public void ShowList()
    {
        Begin("Biblioteca", "list", publishButton: true);
        _page.Children.Add(LunetUi.Secondary("Seus recursos para usar em qualquer jogo. Usar num jogo copia os arquivos; o jogo não muda se você publicar uma versão nova depois."));
        if (_search.Parent is Panel p) p.Children.Remove(_search);
        _page.Children.Add(_search);
        _page.Children.Add(_results);
        FillResults();
    }

    /// <summary>Fills the list for the search text (the timer calls it after a pause; tests call it directly).</summary>
    public void FillResults()
    {
        _results.Children.Clear();
        IReadOnlyList<LibraryAssetVersion> all;
        try { all = _library.Search(_search.Text); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _results.Children.Add(Problem("A biblioteca não pôde ser lida: " + e.Message + " Nenhum arquivo foi apagado."));
            Listed = [];
            return;
        }
        Listed = all.GroupBy(a => a.Id).Select(g => g.MaxBy(a => a.Version)!).OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (Listed.Count == 0)
        {
            _results.Children.Add(string.IsNullOrWhiteSpace(_search.Text)
                ? LunetUi.EmptyState(LunetIcons.Folder, "Biblioteca vazia", "Publique um sprite, mapa, som ou outro recurso de um jogo para usá-lo em outros.")
                : LunetUi.Secondary("Nada encontrado. Tente outra palavra."));
            return;
        }
        foreach (var asset in Listed)
        {
            var a = asset;
            var versions = all.Count(v => v.Id == asset.Id);
            var body = new DockPanel();
            var picture = Picture(asset, 56);
            picture.Margin = new Thickness(0, 0, LunetTokens.Space.M, 0);
            DockPanel.SetDock(picture, Dock.Left);
            body.Children.Add(picture);
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            var title = LunetUi.Text(asset.Title, LunetTokens.Type.Body, weight: FontWeight.SemiBold);
            title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(title);
            text.Children.Add(LunetUi.Text($"{KindName(asset.Kind)} · {(versions > 1 ? versions + " versões" : "v" + asset.Version)} · {asset.License}",
                LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
            body.Children.Add(text);
            var row = new Button
            {
                Content = body, MinHeight = 72, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs), Background = LunetTokens.Brush("#00000000"),
                BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.L), Name = "Asset-" + asset.Id,
            };
            AutomationProperties.SetName(row, asset.Title);
            row.Click += (_, _) => Go(() => ShowAsset(a.Id, a.Version));
            _results.Children.Add(row);
        }
    }

    // ---- asset page ----

    public void ShowAsset(string id, int version)
    {
        LibraryAssetVersion asset;
        IReadOnlyList<LibraryAssetVersion> versions;
        try
        {
            asset = _library.Find(id, version);
            versions = _library.List().Where(v => v.Id == id).OrderByDescending(v => v.Version).ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            Begin("Biblioteca", id);
            _page.Children.Add(Problem(e.Message));
            return;
        }
        Begin(asset.Title, id);
        var picture = Picture(asset, 160);
        picture.HorizontalAlignment = HorizontalAlignment.Left;
        _page.Children.Add(picture);
        _page.Children.Add(LunetUi.Text($"{KindName(asset.Kind)} · versão {asset.Version} · {asset.Files.Length} arquivo(s)", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary));

        var use = LunetUi.Button("Usar num jogo", ButtonKind.Primary, LunetIcons.Download, () => Go(() => ShowUse(asset)));
        use.Name = "UseInGame";
        use.HorizontalAlignment = HorizontalAlignment.Stretch;
        use.MinHeight = 52;
        _page.Children.Add(use);
        var secondary = new WrapPanel { Orientation = Orientation.Horizontal };
        void Action(string label, string icon, Action act, string name)
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, icon, act);
            b.Name = name;
            secondary.Children.Add(b);
        }
        Action("Nova versão", LunetIcons.Plus, () => Go(() => ShowPublishGames(asset.Id)), "NewVersion");
        Action("Exportar ZIP", LunetIcons.Share, () => _ = Export(asset), "ExportAsset");
        Action("Verificar", LunetIcons.Check, () => Verify(asset), "VerifyAsset");
        _page.Children.Add(secondary);

        _page.Children.Add(Section("Sobre"));
        Fact("Autor", asset.Author);
        Fact("Licença", asset.License);
        Fact("Atribuição", asset.Attribution);
        Fact("Tags", string.Join(", ", asset.Tags));
        Fact("Publicado", asset.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        _page.Children.Add(Section("Arquivos"));
        foreach (var file in asset.Files)
            _page.Children.Add(LunetUi.Text(file.Path, LunetTokens.Type.Label, LunetTokens.Color.TextSecondary));
        if (versions.Count > 1)
        {
            _page.Children.Add(Section("Versões"));
            foreach (var v in versions)
            {
                var row = LunetUi.Button($"v{v.Version} · {v.CreatedUtc.ToLocalTime():dd/MM/yyyy}" + (v.Version == asset.Version ? " · esta" : ""),
                    ButtonKind.Ghost, null, () => { _current = () => ShowAsset(v.Id, v.Version); _current(); });
                row.HorizontalAlignment = HorizontalAlignment.Left;
                row.IsEnabled = v.Version != asset.Version;
                _page.Children.Add(row);
            }
        }
    }

    private void Fact(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = new StackPanel { Spacing = 0 };
        row.Children.Add(LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        row.Children.Add(LunetUi.Text(value, LunetTokens.Type.Body));
        _page.Children.Add(row);
    }

    /// <summary>Usar num jogo: pick the game; a file name already used there blocks the copy and is named.</summary>
    private void ShowUse(LibraryAssetVersion asset)
    {
        Begin("Usar em qual jogo?", asset.Id + ":use");
        var games = _shell.Workspace.Projects();
        if (games.Count == 0) { _page.Children.Add(LunetUi.Secondary("Crie um jogo antes.")); return; }
        foreach (var name in games)
        {
            var n = name;
            var b = Row(name, "", () => Use(asset, n));
            b.Name = "UseIn-" + name;
            _page.Children.Add(b);
        }
    }

    /// <summary>Copies a version into a game (tests use the same path as a tap).</summary>
    public void Use(LibraryAssetVersion asset, string game)
    {
        try
        {
            var project = _shell.Workspace.Store.Open(game);
            var conflicts = _library.FindImportConflicts(asset.Id, asset.Version, project);
            if (conflicts.Count > 0)
            {
                _page.Children.Add(Problem($"\"{game}\" já tem: {string.Join(", ", conflicts)}. Nada foi copiado; renomeie ou mova esses arquivos no jogo e tente de novo."));
                return;
            }
            var copied = _library.ImportToProject(asset.Id, asset.Version, project);
            _history.Clear();
            _current = () => ShowAsset(asset.Id, asset.Version);
            ShowAsset(asset.Id, asset.Version);
            _page.Children.Insert(2, Done($"Copiado para \"{game}\": {string.Join(", ", copied)}"));
            _history.Push(ShowList);
            Haptics.Confirm?.Invoke();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or ProjectException or UnauthorizedAccessException or InvalidOperationException)
        {
            _page.Children.Add(Problem("Não foi possível copiar: " + e.Message));
        }
    }

    private void Verify(LibraryAssetVersion asset)
    {
        try
        {
            _library.VerifyIntegrity(asset.Id, asset.Version);
            _page.Children.Insert(2, Done("Os arquivos desta versão conferem com o que foi publicado."));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _page.Children.Insert(2, Problem("Integridade comprometida: " + e.Message + " A biblioteca não foi alterada."));
        }
    }

    private async Task Export(LibraryAssetVersion asset)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage) return;
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Exportar " + asset.Title, SuggestedFileName = $"lunet-asset-{asset.Id}-v{asset.Version}.zip", DefaultExtension = "zip",
        });
        if (file is null) return;
        var temp = Path.Combine(Path.GetTempPath(), "lunet-asset-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await Task.Run(() => _library.ExportZip(asset.Id, asset.Version, temp));
            await using (var output = await file.OpenWriteAsync())
            await using (var input = File.OpenRead(temp))
                await input.CopyToAsync(output);
            _page.Children.Insert(2, Done("Exportado como ZIP."));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _page.Children.Insert(2, Problem("Não foi possível exportar: " + e.Message));
        }
        finally { try { File.Delete(temp); } catch (IOException) { } }
    }

    // ---- publish ----

    /// <summary>Publicar, step 1: the game the file comes from.</summary>
    public void ShowPublishGames() => ShowPublishGames(null);

    private void ShowPublishGames(string? existingId)
    {
        Begin(existingId is null ? "Publicar" : "Nova versão", "publish");
        _page.Children.Add(LunetUi.Secondary("De qual jogo vem o recurso?"));
        var games = _shell.Workspace.Projects();
        if (games.Count == 0) _page.Children.Add(LunetUi.Secondary("Crie um jogo antes."));
        foreach (var name in games)
        {
            var n = name;
            var b = Row(name, "", () => Go(() => ShowPublishFiles(n, existingId)));
            b.Name = "PublishFrom-" + name;
            _page.Children.Add(b);
        }
        _page.Children.Add(LunetUi.Text("Criações dos Studios sem jogo (rascunhos) se publicam pelo Studio.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
    }

    /// <summary>Step 2: the file (sprites, maps, images, sounds, fonts and data of Content/ and Assets/).</summary>
    private void ShowPublishFiles(string game, string? existingId)
    {
        Begin(game, "publish:" + game);
        IReadOnlyList<ProjectAsset> assets;
        try { assets = ProjectAssetCatalog.Build(_shell.Workspace.Store.Open(game).ListFiles()); }
        catch (Exception e) when (e is IOException or ProjectException or InvalidDataException or UnauthorizedAccessException)
        {
            _page.Children.Add(Problem(e.Message));
            return;
        }
        if (assets.Count == 0) { _page.Children.Add(LunetUi.Secondary("Este jogo ainda não tem recursos em Content/ ou Assets/.")); return; }
        _page.Children.Add(LunetUi.Secondary("Qual recurso?"));
        foreach (var asset in assets)
        {
            var a = asset;
            var studio = NeedsStudio(a.Path);
            var b = Row(Path.GetFileName(a.Path), KindName(a.Kind) + " · " + a.Path + (studio ? " · publique pelo Studio" : ""),
                studio ? null : () => Go(() => ShowPublishForm(game, a.Path, existingId)));
            b.Name = "PublishFile-" + a.Path;
            _page.Children.Add(b);
        }
    }

    /// <summary>Studio sources whose playable file is rendered by the Studio (sprite sheets, music, effects).</summary>
    private static bool NeedsStudio(string path) =>
        path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase);

    /// <summary>Step 3: name, author, license, attribution and tags; then publish.</summary>
    private void ShowPublishForm(string game, string path, string? existingId) =>
        ShowPublishForm(game, path, existingId, (title, author, license, attribution, tags, existing) =>
            _library.Publish(_shell.Workspace.Store.Open(game), path, title, author, license, attribution, tags, existing));

    /// <summary>Publicar a creation of the standalone Studios (Home → Studios), no game needed.</summary>
    public void ShowPublishStudio(string workspaceRoot, string path)
    {
        _history.Clear();
        _history.Push(ShowList);
        ShowPublishForm("Studios", path, null, (title, author, license, attribution, tags, existing) =>
            _library.PublishWorkspace(workspaceRoot, path, title, author, license, attribution, tags, existing));
    }

    private void ShowPublishForm(string game, string path, string? existingId,
        Func<string, string, string, string, string[], string?, LibraryAssetVersion> publishWith)
    {
        Begin(existingId is null ? "Publicar" : "Nova versão", "publish-form");
        _page.Children.Add(LunetUi.Text(game + " · " + path, LunetTokens.Type.Caption, LunetTokens.Color.Primary));
        TextBox Field(string label, string value, string hint, string name)
        {
            _page.Children.Add(LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary, FontWeight.SemiBold));
            var box = new TextBox
            {
                Text = value, PlaceholderText = hint, MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.UiFont,
                FontSize = LunetTokens.Type.Body, VerticalContentAlignment = VerticalAlignment.Center, Name = name,
            };
            AutomationProperties.SetName(box, label);
            _page.Children.Add(box);
            return box;
        }
        var title = Field("Nome", Path.GetFileNameWithoutExtension(path), "Ex.: Dragão voador", "AssetTitle");
        var author = Field("Autor", "", "Seu nome ou a autoria original", "AssetAuthor");
        var license = Field("Licença", "Uso próprio", "Ex.: CC0, CC-BY ou uso próprio", "AssetLicense");
        var attribution = Field("Atribuição e origem", "", "De onde veio e as condições de uso", "AssetAttribution");
        var tags = Field("Tags", "", "Separadas por vírgula: personagem, voo, pixel art", "AssetTags");
        var error = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.Danger);
        error.IsVisible = false;
        _page.Children.Add(error);
        var publish = LunetUi.Button(existingId is null ? "Publicar na biblioteca" : "Publicar nova versão", ButtonKind.Primary, LunetIcons.Plus, () =>
        {
            try
            {
                var published = publishWith(title.Text ?? "", author.Text ?? "", license.Text ?? "", attribution.Text ?? "",
                    (tags.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), existingId);
                _history.Clear();
                _history.Push(ShowList);
                _current = () => ShowAsset(published.Id, published.Version);
                _current();
                _page.Children.Insert(2, Done("Publicado na biblioteca."));
                Haptics.Confirm?.Invoke();
            }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or ProjectException or UnauthorizedAccessException or InvalidOperationException)
            {
                error.Text = "Não foi possível publicar: " + e.Message;
                error.IsVisible = true;
            }
        });
        publish.Name = "PublishNow";
        publish.MinHeight = 52;
        publish.HorizontalAlignment = HorizontalAlignment.Stretch;
        publish.Margin = new Thickness(0, LunetTokens.Space.S, 0, 0);
        _page.Children.Add(publish);
    }

    // ---- components ----

    /// <summary>The asset's picture (its PNG) or its kind's icon on a tinted tile.</summary>
    private Control Picture(LibraryAssetVersion asset, double size)
    {
        Control child;
        string? file = null;
        try { file = _library.PreviewFile(asset); } catch (Exception e) when (e is IOException or ArgumentException) { }
        Bitmap? bitmap = null;
        if (file is not null)
        {
            try { using var stream = File.OpenRead(file); bitmap = Bitmap.DecodeToWidth(stream, (int)(size * 2)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { bitmap = null; }
        }
        if (bitmap is not null)
        {
            child = new Image { Source = bitmap, Stretch = Stretch.Uniform, Width = size - 8, Height = size - 8 };
            RenderOptions.SetBitmapInterpolationMode(child, Avalonia.Media.Imaging.BitmapInterpolationMode.None); // pixel art stays crisp
        }
        else child = LunetIcons.Create(KindIcon(asset.Kind), LunetTokens.Color.Primary, size * 0.4);
        return new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(LunetTokens.Radius.M), ClipToBounds = true,
            Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised), Child = child,
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(1),
        };
    }

    public static string KindName(ProjectAssetKind kind) => kind switch
    {
        ProjectAssetKind.Sprite => "Sprite", ProjectAssetKind.TileMap => "Mapa", ProjectAssetKind.Image => "Imagem",
        ProjectAssetKind.Audio => "Som", ProjectAssetKind.Font => "Fonte", _ => "Dados",
    };

    private static string KindIcon(ProjectAssetKind kind) => kind switch
    {
        ProjectAssetKind.Sprite or ProjectAssetKind.Image => LunetIcons.Art,
        ProjectAssetKind.TileMap => LunetIcons.Map,
        ProjectAssetKind.Audio => LunetIcons.Sound,
        ProjectAssetKind.Font => LunetIcons.Notes,
        _ => LunetIcons.File,
    };

    private static TextBlock Section(string text) => LunetUi.Section(text);

    private static Button Row(string title, string detail, Action? onClick)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(LunetUi.Text(title, LunetTokens.Type.Callout, onClick is null ? LunetTokens.Color.TextTertiary : LunetTokens.Color.TextPrimary, FontWeight.SemiBold));
        if (detail.Length > 0) stack.Children.Add(LunetUi.Text(detail, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var b = new Button
        {
            Content = stack, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = 56, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S), IsEnabled = onClick is not null,
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.L),
        };
        AutomationProperties.SetName(b, title);
        if (onClick is not null) b.Click += (_, _) => onClick();
        return b;
    }

    private static Control Notice(string text, string color) => LunetUi.Notice(text, color);

    private static Control Problem(string text) => Notice(text, LunetTokens.Color.Danger);
    private static Control Done(string text) => Notice(text, LunetTokens.Color.Success);
}
