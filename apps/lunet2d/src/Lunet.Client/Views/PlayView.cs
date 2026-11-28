using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// Jogar in the shell (P4): your games as a grid of covers (the game's <c>Content/icon.png</c>, or its initial), one
/// tap to play. On Android the game runs full screen in its own process, as before (the head's
/// <see cref="LunetShell.PlayGame"/>); elsewhere it opens running in the workspace.
/// </summary>
public sealed class PlayView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly WrapPanel _grid = new() { Orientation = Orientation.Horizontal };

    public PlayView(LunetShell shell, Action close)
    {
        _shell = shell;
        _close = close;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var page = new StackPanel { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.L, LunetTokens.Space.M, LunetTokens.Space.L) };
        page.Children.Add(LunetUi.Headline("Jogar"));
        page.Children.Add(LunetUi.Secondary("Seus jogos em tela cheia, sem abrir o código."));
        page.Children.Add(_grid);
        Content = new ScrollViewer { Content = page };
        SizeChanged += (_, e) => { if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 1) Refresh(); };
        Refresh();
    }

    /// <summary>The games offered, in order.</summary>
    public IReadOnlyList<string> Games { get; private set; } = [];

    public void Refresh()
    {
        _grid.Children.Clear();
        Games = _shell.Workspace.Projects();
        if (Games.Count == 0)
        {
            _grid.Children.Add(LunetUi.EmptyState(LunetIcons.Play, "Nenhum jogo ainda", "Crie um jogo em Jogos e ele aparece aqui para jogar."));
            return;
        }
        // Two covers per row on a phone, 180 dp covers on wider screens.
        // Each cover keeps a right margin of S: two need 2·(w + S) of the row (the old sum forgot one S and wrapped).
        var width = Bounds.Width is > 0 and < 600 ? Math.Floor((Bounds.Width - 2 * LunetTokens.Space.M) / 2 - LunetTokens.Space.S) : 180;
        foreach (var name in Games) _grid.Children.Add(Cover(name, Math.Max(120, width)));
    }

    /// <summary>Plays a game (the same path as a tap).</summary>
    public void Play(string name)
    {
        if (_shell.PlayGame is null) _close(); // the game opens in the workspace: Jogar steps aside
        _shell.Play(name);
    }

    private Control Cover(string name, double width)
    {
        Control picture;
        Bitmap? bitmap = null;
        var files = _shell.Workspace.Store.Root;
        var icon = name + "/Content/icon.png";
        try
        {
            if (files.FileExists(icon))
            {
                using var stream = new MemoryStream(files.Read(icon), writable: false);
                bitmap = Bitmap.DecodeToWidth(stream, (int)(width * 2));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { }
        if (bitmap is not null) picture = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
        else picture = new TextBlock
        {
            Text = name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?", FontSize = width * 0.32, FontWeight = FontWeight.Bold,
            Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var play = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(LunetTokens.Radius.Pill), Background = LunetTokens.Brush(LunetTokens.Color.Primary),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(LunetTokens.Space.S),
            Child = LunetIcons.Create(LunetIcons.Play, LunetTokens.Color.OnPrimary, 20, filled: true),
        };
        var art = new Border
        {
            Height = width * 0.75, CornerRadius = new CornerRadius(LunetTokens.Radius.L), ClipToBounds = true,
            Background = LunetTokens.Brush(HomeView.CardColor(name)), Child = new Panel { Children = { picture, play } },
        };
        var title = LunetUi.Text(name, LunetTokens.Type.Callout, weight: FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Margin = new Thickness(LunetTokens.Space.Xxs, LunetTokens.Space.Xs, 0, 0);
        var b = new Button
        {
            Content = new StackPanel { Children = { art, title } }, Width = width, Padding = new Thickness(0),
            Margin = new Thickness(0, 0, LunetTokens.Space.S, LunetTokens.Space.M), Background = LunetTokens.Brush("#00000000"),
            BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Name = "Play-" + name,
        };
        AutomationProperties.SetName(b, "Jogar " + name);
        b.Click += (_, _) => Play(name);
        return b;
    }
}
