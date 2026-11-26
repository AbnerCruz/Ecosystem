using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Studios on the Home (without a game): the same Studios as inside a game, over the standalone Studio folder
/// (<see cref="LunetShell.StudioWorkspacePath"/>, the previous screens' StudioWorkspace, so drafts carry over).
/// Create tiles at the top, "Minhas criações" below (grouped as Recursos); a tap opens the Studio full screen, and
/// Publicar sends a creation to the Biblioteca, from where any game can use it.
/// </summary>
public sealed class StudiosView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly Action<string, string> _publish;
    private readonly StackPanel _list = new() { Spacing = LunetTokens.Space.Xxs };
    private readonly Control _home;
    private Control? _studio;

    public StudiosView(LunetShell shell, Action close, Action<string, string> publish)
    {
        _shell = shell;
        _close = close;
        _publish = publish;
        Directory.CreateDirectory(shell.StudioWorkspacePath);
        Storage = new StudioAssetWorkspace(shell.StudioWorkspacePath);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Back());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        var title = LunetUi.Title("Studios");
        title.VerticalAlignment = VerticalAlignment.Center;
        bar.Children.Add(title);
        var page = new StackPanel { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xl) };
        page.Children.Add(LunetUi.Secondary("Crie sem abrir um jogo. Publique na Biblioteca para usar em qualquer jogo."));
        page.Children.Add(Tiles());
        page.Children.Add(_list);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = new Border { Child = page, MaxWidth = 900 } });
        _home = root;
        Content = root;
        Fill();
    }

    public StudioAssetWorkspace Storage { get; }
    public Control? Studio => _studio;
    public IReadOnlyList<string> Creations => Files();

    private IReadOnlyList<string> Files()
    {
        var root = Storage.RootDirectory;
        return new[] { "Content", "Assets" }.Select(f => Path.Combine(root, f)).Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .Where(p => !p.Contains(".tmp", StringComparison.Ordinal)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Back: a Studio closes (saving), then the page.</summary>
    public bool Back()
    {
        if (_studio is not null)
        {
            switch (_studio)
            {
                case SpriteStudioView s: return s.Close();
                case TileStudioView t: return t.Close();
                case SoundStudioView so: return so.Close();
                case MusicStudioView m: return m.Close();
                case ParticleStudioView pa: return pa.Close();
                case DataView d: return d.Back();
                case PaletteView pv: return pv.Close();
                case WaveEditorView wv: return wv.Close();
            }
        }
        _close();
        return true;
    }

    private void Show(Control studio) { _studio = studio; Content = studio; }
    private void Home() { _studio = null; Content = _home; Fill(); }

    private string Unique(string folder, string stem, string ext)
    {
        var files = Files();
        var n = 1;
        while (files.Contains($"{folder}/{stem}-{n}{ext}")) n++;
        return $"{folder}/{stem}-{n}{ext}";
    }

    // ---------- opening (the same Studios as in a game) ----------

    public SpriteStudioView OpenSprite(string path, Action? reopen = null)
    {
        var view = new SpriteStudioView(_shell, path, () => { if (reopen is not null) reopen(); else Home(); }, Storage);
        Show(view);
        return view;
    }

    public TileStudioView OpenMap(string path)
    {
        var view = new TileStudioView(_shell, path, Home, Storage);
        view.EditTileset = tileset => OpenSprite(tileset, reopen: () => OpenMap(path));
        Show(view);
        return view;
    }

    public SoundStudioView OpenSound(string path) { var v = new SoundStudioView(_shell, path, Home, Storage); Show(v); return v; }
    public MusicStudioView OpenMusic(string path) { var v = new MusicStudioView(_shell, path, Home, Storage); Show(v); return v; }
    public ParticleStudioView OpenParticles(string path) { var v = new ParticleStudioView(_shell, path, Home, Storage); Show(v); return v; }
    public DataView OpenData(string path) { var v = new DataView(_shell, path, Home, Storage); Show(v); return v; }

    public PaletteView OpenPalette(string path) { var v = new PaletteView(_shell, Home, Storage, path, p => _publish(Storage.RootDirectory, p)); Show(v); return v; }
    /// <summary>A tone to shape in the WAV editor (as the previous screens' "Editor WAV").</summary>
    public WaveEditorView NewTone(double hz = 440, double seconds = 0.5, Lunet.Studio.WaveShape shape = Lunet.Studio.WaveShape.Sine)
    {
        var v = new WaveEditorView(_shell, Storage, Lunet.Studio.WaveAudioDocument.CreateEffect(hz, seconds, shape), "tom", Home);
        Show(v);
        return v;
    }

    public PaletteView NewPalette() => OpenPalette(Unique("Assets", "paleta", ".palette.json"));

    public SpriteStudioView NewSprite() => OpenSprite(Unique("Content/Textures", "sprite", ".sprite.json"));
    public TileStudioView NewMap() => OpenMap(TileStudioView.Create(Storage, Files()));
    public SoundStudioView NewSound() => OpenSound(Unique("Content/Audio", "efeito", ".sfx.json"));
    public MusicStudioView NewMusic() => OpenMusic(Unique("Content/Audio", "musica", ".song.json"));
    public ParticleStudioView NewParticles() => OpenParticles(Unique("Content/Effects", "efeito", ".particles.json"));
    public DataView NewData(string name = "dados") => OpenData(DataView.Create(Storage, Files(), name));

    /// <summary>A tap on a creation: the Studio that edits it.</summary>
    public bool Activate(string f)
    {
        if (f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)) OpenSprite(f);
        else if (f.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase)) OpenMap(f);
        else if (f.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase)) OpenSound(f);
        else if (f.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase)) OpenMusic(f);
        else if (f.EndsWith(".particles.json", StringComparison.OrdinalIgnoreCase)) OpenParticles(f);
        else if (DataView.IsTable(f)) OpenData(f);
        else if (f.EndsWith(".palette.json", StringComparison.OrdinalIgnoreCase)) OpenPalette(f);
        else return false;
        return true;
    }

    // ---------- page ----------

    private Control Tiles()
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
            AutomationProperties.SetName(b, "Novo " + label);
            b.Click += (_, _) => act();
            tiles.Children.Add(b);
        }
        Tile(LunetIcons.Edit, "Sprite", () => NewSprite(), "StudioNewSprite", primary: true);
        Tile(LunetIcons.Map, "Mapa", () => NewMap(), "StudioNewMap");
        Tile(LunetIcons.Sound, "Efeito", () => NewSound(), "StudioNewSound");
        Tile(LunetIcons.Sound, "Música", () => NewMusic(), "StudioNewMusic");
        Tile(LunetIcons.Pulse, "Partículas", () => NewParticles(), "StudioNewParticles");
        Tile(LunetIcons.File, "Tabela", () => NewData(), "StudioNewData");
        Tile(LunetIcons.Art, "Paleta", () => NewPalette(), "StudioNewPalette");
        Tile(LunetIcons.Pulse, "Tom (WAV)", () => NewTone(), "StudioNewTone");
        return tiles;
    }

    private void Fill()
    {
        _list.Children.Clear();
        var heading = LunetUi.Text("MINHAS CRIAÇÕES", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
        heading.Margin = new Thickness(0, LunetTokens.Space.M, 0, 0);
        _list.Children.Add(heading);
        var any = false;
        var all = Files();
        var palettes = all.Where(f => f.EndsWith(".palette.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var (label, icon, files) in ResourcesPage.Groups(all).Append(("Paletas", LunetIcons.Art, (IReadOnlyList<string>)palettes)))
        {
            var editable = files.Where(f => f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".particles.json", StringComparison.OrdinalIgnoreCase) || DataView.IsTable(f) || f.EndsWith(".palette.json", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (editable.Length == 0) continue;
            any = true;
            var group = LunetUi.Text($"{label} · {editable.Length}", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary, FontWeight.SemiBold);
            group.Margin = new Thickness(0, LunetTokens.Space.S, 0, 0);
            _list.Children.Add(group);
            foreach (var f in editable) _list.Children.Add(Row(f, icon));
        }
        if (!any) _list.Children.Add(LunetUi.Text("Nada ainda. Comece por um dos Studios acima.", LunetTokens.Type.Label, LunetTokens.Color.TextTertiary));
    }

    private Control Row(string f, string icon)
    {
        var path = f;
        var publish = LunetUi.Button("Publicar", ButtonKind.Ghost, LunetIcons.Share, () => _publish(Storage.RootDirectory, path));
        publish.Name = "Publish-" + f;
        AutomationProperties.SetName(publish, "Publicar " + Path.GetFileName(f) + " na Biblioteca");
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        text.Children.Add(LunetUi.Text(Path.GetFileName(f), LunetTokens.Type.Callout, weight: FontWeight.SemiBold));
        text.Children.Add(LunetUi.Text((Path.GetDirectoryName(f) ?? "").Replace('\\', '/'), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var lead = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(LunetTokens.Radius.S), Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised), Child = LunetIcons.Create(icon, LunetTokens.Color.Primary, 20), Margin = new Thickness(0, 0, LunetTokens.Space.S, 0) };
        var open = new Button
        {
            Content = new DockPanel { Children = { Docked(lead), text } }, MinHeight = 56, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            Padding = new Thickness(0, LunetTokens.Space.Xxs), Name = "Creation-" + f,
        };
        AutomationProperties.SetName(open, "Editar " + Path.GetFileName(f));
        open.Click += (_, _) => Activate(path);
        var row = new DockPanel();
        DockPanel.SetDock(publish, Dock.Right);
        row.Children.Add(publish);
        row.Children.Add(open);
        return row;
    }

    private static Control Docked(Control c) { DockPanel.SetDock(c, Dock.Left); return c; }
}
