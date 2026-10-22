using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The Tutorial as a panel of its own (owner, DEC-0045, UC-40, UC-64): documentation, only to read — never the note
/// editor, never notes created in the vault by itself. Index by section, search in titles and text, the article rendered as
/// the editor's Visual mode draws a note, Anterior/Próximo and [[links]] that move inside the tutorial. The text is the
/// native app's (<see cref="AppDocs"/>); nothing is written to the vault.
/// </summary>
public sealed class TutorialPanel : Border
{
    private readonly IReadOnlyList<CityNote> _articles;
    private readonly TextBlock _section = new() { FontSize = 12, FontWeight = FontWeight.Bold, LetterSpacing = .6, Margin = new Thickness(20, 0, 20, 2) };
    private readonly TextBlock _title = new() { FontSize = 20, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBox _search = new()
    {
        PlaceholderText = "Buscar no tutorial",
        FontSize = 15,
        MinHeight = 40,
        CornerRadius = new CornerRadius(12),
        Margin = new Thickness(16, 0, 16, 8),
        VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Button _back, _prev, _next;
    private readonly Border _pager = new();

    public TutorialPanel(IReadOnlyList<CityNote> articles)
    {
        // Comece aqui first, then the articles in the given order, kept together by section
        var md = articles.Where(a => a.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToList();
        var sections = md.Select(Section).Distinct().ToList();
        _articles = md.OrderBy(a => a.Path == LegacyTutorial.Start ? 0 : 1)
            .ThenBy(a => sections.IndexOf(Section(a))).ToList();
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _section.Foreground = UrbeTheme.Brush(UrbeTheme.Accent);
        _back = HeadButton(UrbeTheme.Icons.Back, "Voltar ao índice");
        _back.Click += (_, _) => ShowIndex();
        var close = HeadButton(UrbeTheme.Icons.CloseUi, "Fechar o tutorial");
        close.Click += (_, _) => Close();
        var head = new DockPanel { Margin = new Thickness(8, 6, 8, 8), Height = 56 };
        DockPanel.SetDock(_back, Dock.Left);
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(_back);
        head.Children.Add(close);
        _title.Margin = new Thickness(8, 0);
        head.Children.Add(_title);
        _search.TextChanged += (_, _) => { if (Article is null) RenderIndex(); };
        _prev = PagerButton("‹ Anterior");
        _next = PagerButton("Próximo ›");
        _prev.Click += (_, _) => Step(-1);
        _next.Click += (_, _) => Step(1);
        var pagerRow = new DockPanel { LastChildFill = false, Margin = new Thickness(12, 8) };
        DockPanel.SetDock(_prev, Dock.Left);
        DockPanel.SetDock(_next, Dock.Right);
        pagerRow.Children.Add(_prev);
        pagerRow.Children.Add(_next);
        _pager.Child = pagerRow;
        _pager.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        _pager.BorderThickness = new Thickness(0, 1, 0, 0);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), MaxWidth = 760 };
        grid.Children.Add(head);
        Grid.SetRow(_search, 1);
        grid.Children.Add(_search);
        Grid.SetRow(_section, 2);
        grid.Children.Add(_section);
        Grid.SetRow(_scroll, 3);
        grid.Children.Add(_scroll);
        Grid.SetRow(_pager, 4);
        grid.Children.Add(_pager);
        Child = grid;
    }

    /// <summary>The article being read, or null on the index.</summary>
    public CityNote? Article { get; private set; }

    /// <summary>The articles listed on the index now (after the search).</summary>
    public IReadOnlyList<string> ListedPaths { get; private set; } = [];

    public event Action? Closed;

    /// <summary>"Copiar o tutorial para o vault": the articles as notes of the vault, only when asked.</summary>
    public event Action? CopyRequested;

    public void Open()
    {
        IsVisible = true;
        ShowIndex();
    }

    public void Close()
    {
        IsVisible = false;
        Closed?.Invoke();
    }

    public string SearchText
    {
        get => _search.Text ?? "";
        set
        {
            _search.Text = value;
            if (Article is null) RenderIndex();   // TextChanged comes later, on the dispatcher
        }
    }

    public void ShowIndex()
    {
        Article = null;
        _title.Text = "Tutorial";
        _back.IsVisible = false;
        _search.IsVisible = true;
        _section.IsVisible = false;
        _pager.IsVisible = false;
        RenderIndex();
    }

    /// <summary>Opens the article at <paramref name="path"/> (inside the tutorial); false when there is none.</summary>
    public bool ShowArticle(string path)
    {
        if (_articles.FirstOrDefault(a => a.Path == path) is not { } article) return false;
        Article = article;
        _title.Text = TitleOf(article);
        _back.IsVisible = true;
        _search.IsVisible = false;
        _section.Text = SectionName(Section(article)).ToUpperInvariant();
        _section.IsVisible = true;
        _pager.IsVisible = true;
        int i = IndexOf(article);
        _prev.IsEnabled = i > 0;
        _next.IsEnabled = i < _articles.Count - 1;
        // read only: the reader has no task toggles, formula or comment editing, only links
        var reader = new MarkdownReader { WikiLinkTapped = name => FollowLink(name) };
        _scroll.Content = new Border { Padding = new Thickness(20, 4, 20, 32), Child = reader.BuildMarkdown(article.Content) };
        _scroll.Offset = default;
        return true;
    }

    /// <summary>A [[link]] of an article: another article of the tutorial with that name.</summary>
    public bool FollowLink(string name)
    {
        // [[Nome#seção]] and [[Nome|texto]] go to Nome
        name = name.Split('|')[0].Split('#')[0].Trim();
        var target = _articles.FirstOrDefault(a => string.Equals(TitleOf(a), name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Path, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Path, LegacyTutorial.Folder + name + ".md", StringComparison.OrdinalIgnoreCase));
        return target is not null && ShowArticle(target.Path);
    }

    private void Step(int by)
    {
        if (Article is null) return;
        int i = IndexOf(Article) + by;
        if (i >= 0 && i < _articles.Count) ShowArticle(_articles[i].Path);
    }

    private int IndexOf(CityNote article) => _articles.ToList().FindIndex(a => a.Path == article.Path);

    private void RenderIndex()
    {
        var q = LegacyEditorTools.Fold(SearchText.Trim());
        var list = new StackPanel { Margin = new Thickness(8, 0, 8, 32) };
        var listed = new List<string>();
        string? section = null;
        foreach (var a in _articles)
        {
            if (q.Length > 0 && !LegacyEditorTools.Fold(TitleOf(a) + " " + a.Content).Contains(q, StringComparison.Ordinal)) continue;
            var s = Section(a);
            if (s != section)
            {
                section = s;
                list.Children.Add(new TextBlock
                {
                    Text = SectionName(s), FontSize = 12, FontWeight = FontWeight.Bold, LetterSpacing = .6,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(12, 16, 12, 6)
                });
            }
            listed.Add(a.Path);
            var path = a.Path;
            var label = new StackPanel { Spacing = 2 };
            label.Children.Add(new TextBlock { Text = TitleOf(a), FontSize = 15, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis });
            // a search shows where the words are
            if (q.Length > 0 && Excerpt(a.Content, q) is { } excerpt)
                label.Children.Add(new TextBlock { Text = excerpt, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap, MaxLines = 2 });
            var row = new Button
            {
                Content = label,
                MinHeight = 44, Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            row.Click += (_, _) => ShowArticle(path);
            list.Children.Add(row);
        }
        if (listed.Count == 0)
            list.Children.Add(new TextBlock { Text = "Nada no tutorial com “" + SearchText.Trim() + "”.", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(12, 24), TextWrapping = TextWrapping.Wrap });
        if (q.Length == 0)
        {
            var copy = new Button
            {
                Content = new TextBlock { Text = "Copiar o tutorial para o vault, como notas", FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextWrapping = TextWrapping.Wrap },
                Margin = new Thickness(12, 24, 12, 0), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1)
            };
            copy.Click += (_, _) => CopyRequested?.Invoke();
            list.Children.Add(copy);
        }
        ListedPaths = listed;
        _scroll.Content = list;
        _scroll.Offset = default;
    }

    /// <summary>The line of <paramref name="content"/> holding the folded query, without Markdown marks; null in the title only.</summary>
    internal static string? Excerpt(string content, string q)
    {
        foreach (var line in content.Split('\n'))
        {
            if (!LegacyEditorTools.Fold(line).Contains(q, StringComparison.Ordinal)) continue;
            var plain = line.TrimStart('#', '-', '>', '|', ' ').Replace("**", "").Replace("`", "").Replace("[[", "").Replace("]]", "").Trim();
            return plain.Length > 140 ? plain[..140] + "…" : plain;
        }
        return null;
    }

    private static string SectionName(string section) => section.Length == 0 ? "Começo" : section;

    private static string TitleOf(CityNote a) => System.IO.Path.GetFileNameWithoutExtension(a.Path);

    private static string Section(CityNote a)
    {
        var rel = a.Path.StartsWith(LegacyTutorial.Folder, StringComparison.Ordinal) ? a.Path[LegacyTutorial.Folder.Length..] : a.Path;
        return rel.Contains('/') ? rel[..rel.IndexOf('/')] : "";
    }

    private static Button HeadButton(string icon, string tip)
    {
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 22, UrbeTheme.Text2, 1.8),
            Width = 44, Height = 44, Padding = new Thickness(0), CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static Button PagerButton(string text)
    {
        var b = new Button
        {
            Content = text, MinHeight = 40, Padding = new Thickness(14, 0), CornerRadius = new CornerRadius(12),
            Background = UrbeTheme.Brush(UrbeTheme.Surface2), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            FontWeight = FontWeight.SemiBold, VerticalContentAlignment = VerticalAlignment.Center
        };
        b.Resources["ButtonBackgroundDisabled"] = Brushes.Transparent;
        b.PropertyChanged += (_, e) => { if (e.Property == IsEnabledProperty) b.Opacity = b.IsEnabled ? 1 : .35; };
        return b;
    }
}
