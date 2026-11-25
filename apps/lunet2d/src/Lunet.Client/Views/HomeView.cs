using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// Level 1 of the shell: your games, and the action of making a new one. One place, no repeated destinations
/// (the old Home showed Projetos/Studios/Explorar/Aprender both as cards and in the bar).
/// </summary>
public sealed class HomeView : UserControl
{
    private readonly LunetShell _shell;
    private readonly WrapPanel _grid = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _count = LunetUi.Secondary("");

    public HomeView(LunetShell shell)
    {
        _shell = shell;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        Build();
    }

    /// <summary>Lays the Home out (again when the head sets <see cref="LunetShell.OpenRoute"/>).</summary>
    public void Build()
    {
        var shell = _shell;
        if (_grid.Parent is Panel old) old.Children.Remove(_grid);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, LunetTokens.Space.L) };
        var create = LunetUi.Button("Novo jogo", ButtonKind.Primary, LunetIcons.Plus, () => _shell.CreateProject());
        create.Name = "NewProject";
        DockPanel.SetDock(create, Dock.Right);
        header.Children.Add(create);
        var titles = new StackPanel { Spacing = LunetTokens.Space.Xxs, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(LunetUi.Headline("Seus jogos"));
        if (_count.Parent is Panel oldParent) oldParent.Children.Remove(_count);
        titles.Children.Add(_count);
        header.Children.Add(titles);

        var content = new StackPanel { Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.L) };
        content.Children.Add(header);
        if (shell.OpenRoute is not null) content.Children.Add(Destinations());
        content.Children.Add(_grid);
        Content = new ScrollViewer { Content = content };
        Refresh();
    }

    /// <summary>Where else to go from the Home (Jogar, Studios, Aprender…), one row that scrolls sideways.</summary>
    private Control Destinations()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        foreach (var (route, label, icon) in LunetShell.HomeRoutes)
        {
            // Icon tile with its label below (D5): six destinations read at a glance, one thumb-tap each.
            var tile = new StackPanel { Spacing = LunetTokens.Space.Xxs, Width = 72 };
            tile.Children.Add(new Border
            {
                Width = 56, Height = 56, CornerRadius = new CornerRadius(LunetTokens.Radius.L), HorizontalAlignment = HorizontalAlignment.Center,
                Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
                Child = LunetIcons.Create(icon, LunetTokens.Color.Primary, 24),
            });
            var text = LunetUi.Text(label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
            text.TextAlignment = TextAlignment.Center; text.TextWrapping = TextWrapping.NoWrap;
            tile.Children.Add(text);
            var b = new Button
            {
                Content = tile, Padding = new Thickness(0), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            };
            b.Classes.Add("lunet-card");
            AutomationProperties.SetName(b, label);
            b.Click += (_, _) => _shell.OpenRoute?.Invoke(route, null);
            b.Name = "Route-" + route;
            row.Children.Add(b);
        }
        return new ScrollViewer
        {
            Content = row, Margin = new Thickness(0, 0, 0, LunetTokens.Space.L),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    /// <summary>The project names currently shown, in order (for tests and screen readers).</summary>
    public IReadOnlyList<string> Shown { get; private set; } = [];

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 1) Refresh(); // card columns follow the width
    }

    public void Refresh()
    {
        _grid.Children.Clear();
        Shown = _shell.Workspace.Projects();
        _count.Text = Shown.Count switch { 0 => "Comece criando um jogo", 1 => "1 jogo", _ => $"{Shown.Count} jogos" };
        if (Shown.Count == 0)
        {
            _grid.Children.Add(LunetUi.EmptyState(LunetIcons.Game, "Nenhum jogo ainda",
                "Crie um jogo: o código, a arte, o som e o jogo rodando ficam juntos, a um toque de distância.",
                LunetUi.Button("Criar o primeiro jogo", ButtonKind.Primary, LunetIcons.Plus, () => _shell.CreateProject())));
            return;
        }
        foreach (var name in Shown) _grid.Children.Add(ProjectCard(name));
    }

    /// <summary>A stable colour per game (its name hashed onto the accent family), so cards are told apart at a glance.</summary>
    public static string CardColor(string name)
    {
        string[] palette = ["#3b3270", "#27466b", "#2f5a4a", "#5a3b52", "#5a4a2b", "#2b4f5a"];
        uint h = 2166136261;
        foreach (var c in name) h = (h ^ c) * 16777619;
        return palette[h % (uint)palette.Length];
    }

    /// <summary>"editado agora", "há 5 min", "há 3 h", "ontem", "há 4 dias" or the date (D5).</summary>
    public static string Edited(DateTime utc, DateTime nowUtc)
    {
        var d = nowUtc - utc;
        if (d < TimeSpan.FromMinutes(1)) return "editado agora";
        if (d < TimeSpan.FromHours(1)) return $"editado há {(int)d.TotalMinutes} min";
        if (d < TimeSpan.FromDays(1)) return $"editado há {(int)d.TotalHours} h";
        if (d < TimeSpan.FromDays(2)) return "editado ontem";
        if (d < TimeSpan.FromDays(30)) return $"editado há {(int)d.TotalDays} dias";
        return "editado em " + utc.ToLocalTime().ToString("dd/MM/yyyy");
    }

    private Control ProjectCard(string name)
    {
        var folder = Path.Combine(_shell.Workspace.Root, name);
        var edited = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".lunet" + Path.DirectorySeparatorChar))
                .Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(Directory.GetLastWriteTimeUtc(folder)).Max()
            : DateTime.UtcNow;
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new Border
        {
            Height = 92, CornerRadius = new CornerRadius(LunetTokens.Radius.M), Margin = new Thickness(0, 0, 0, LunetTokens.Space.Xs),
            Background = LunetTokens.Brush(CardColor(name)),
            Child = new Viewbox { Width = 36, Height = 36, Child = LunetIcons.Create(LunetIcons.Game, LunetTokens.Color.TextPrimary, 36) },
        });
        var title = LunetUi.Text(name, LunetTokens.Type.Body, weight: FontWeight.SemiBold);
        title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        stack.Children.Add(title);
        stack.Children.Add(LunetUi.Text(Edited(edited, DateTime.UtcNow), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var button = new Button
        {
            Content = LunetUi.Card(stack, LunetTokens.Space.S),
            Margin = new Thickness(0, 0, LunetTokens.Space.S, LunetTokens.Space.S),
            Padding = new Thickness(0), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        // Two columns on a phone, as many 200 dp columns as fit on a wide screen.
        button.Width = Bounds.Width is > 0 and < 600 ? (Bounds.Width - 2 * LunetTokens.Space.M - LunetTokens.Space.S) / 2 - 1 : 200;
        button.Classes.Add("lunet-card");
        AutomationProperties.SetName(button, "Abrir " + name);
        button.Click += (_, _) => _shell.OpenProject(name);
        return button;
    }
}
