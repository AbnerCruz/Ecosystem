using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Docs;

namespace Lunet.Client.Views;

/// <summary>
/// The offline documentation beside the code (P3): manual, guides and the API reference, with search and history.
/// The same pages as the previous Documentation screen (<see cref="DocumentationBrowser"/>), drawn with the shell's
/// components. Code blocks can be copied; "Documentação do símbolo" in the palette opens the page of what is under the caret.
/// </summary>
public sealed class DocsView : UserControl
{
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly ScrollViewer _scroll = new();
    private readonly TextBox _search = new()
    {
        PlaceholderText = "Buscar na documentação", MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.UiFont,
        FontSize = LunetTokens.Type.Callout, VerticalContentAlignment = VerticalAlignment.Center,
    };
    private readonly Button _back;
    private DocumentationBrowser? _browser;

    public DocsView()
    {
        AutomationProperties.SetName(_search, "Buscar na documentação");
        _search.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(_search.Text)) return;
            Navigate("search:" + _search.Text.Trim());
            e.Handled = true;
        };
        _back = LunetUi.IconButton(LunetIcons.Back, "Voltar na documentação", GoBack);
        var home = LunetUi.IconButton(LunetIcons.Home, "Início da documentação", () => Navigate("home"));
        var bar = new DockPanel { Margin = new Thickness(LunetTokens.Space.S, 0, LunetTokens.Space.M, LunetTokens.Space.Xs) };
        DockPanel.SetDock(_back, Dock.Left);
        DockPanel.SetDock(home, Dock.Left);
        bar.Children.Add(_back);
        bar.Children.Add(home);
        bar.Children.Add(_search);
        _scroll.Content = new Border { Child = _page, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.L) };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(_scroll);
        Content = root;
        AttachedToVisualTree += (_, _) => { if (Page is null) Navigate("home"); };
    }

    /// <summary>The page shown, or null before the first open.</summary>
    public DocPage? Page { get; private set; }

    /// <summary>Opens a target: <c>home</c>, <c>guide:…</c>, <c>id:T:…</c>, <c>ns:…</c>, <c>search:…</c>.</summary>
    public void Navigate(string target)
    {
        _browser ??= DocsLibrary.NewBrowser();
        Show(_browser.Navigate(target));
    }

    /// <summary>The page of a symbol of the API (documentation id from Roslyn), or a search by its name.</summary>
    public void OpenSymbol(string? documentationId, string name)
    {
        _browser ??= DocsLibrary.NewBrowser();
        Navigate(_browser.TargetForSymbol(documentationId, name));
    }

    public void GoBack()
    {
        if (_browser?.Back() is { } page) Show(page);
    }

    private void Show(DocPage page)
    {
        Page = page;
        _back.IsVisible = _browser?.CanGoBack == true;
        _page.Children.Clear();
        if (page.Target != "home") // the panel is already called Documentação
        {
            var title = LunetUi.Headline(page.Title);
            title.Margin = new Thickness(0, 0, 0, LunetTokens.Space.Xs);
            _page.Children.Add(title);
        }
        else _page.Children.Add(LunetUi.Secondary("Tudo aqui funciona sem internet."));
        foreach (var block in page.Blocks)
            _page.Children.Add(Block(block.Detail is null && block.Target is { } t && CategoryHints.TryGetValue(t, out var hint) ? block with { Detail = hint } : block));
        if (page.Blocks.Count == 0) _page.Children.Add(LunetUi.Secondary("Nada aqui ainda."));
        _scroll.Offset = default;
    }

    /// <summary>What each manual category holds, so the first page says where to go.</summary>
    private static readonly Dictionary<string, string> CategoryHints = new()
    {
        ["category:tutorials"] = "Do zero a um jogo, passo a passo",
        ["category:tasks"] = "Como fazer uma coisa específica",
        ["category:api"] = "Cada tipo e membro do Lunet, com exemplos",
        ["category:concepts"] = "Como o Lunet funciona por dentro",
    };

    private Control Block(DocBlock block) => block.Kind switch
    {
        DocBlockKind.Title => LunetUi.Title(block.Text),
        DocBlockKind.Heading => Heading(block.Text),
        DocBlockKind.Code => CodeBlock(block.Text),
        DocBlockKind.Signature => Signature(block.Text),
        DocBlockKind.Bullet => Rich("•  " + block.Text),
        DocBlockKind.Link => Link(block),
        DocBlockKind.Note => new Border
        {
            Child = Rich(block.Text), Padding = new Thickness(LunetTokens.Space.S),
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Warning), BorderThickness = new Thickness(3, 0, 0, 0),
        },
        _ => Rich(block.Text),
    };

    private static Control Heading(string text)
    {
        var t = LunetUi.Text(text, LunetTokens.Type.Title, weight: FontWeight.SemiBold);
        t.Margin = new Thickness(0, LunetTokens.Space.S, 0, 0);
        return t;
    }

    /// <summary>A paragraph with the guide's **bold**, *italic* and `code`.</summary>
    private static SelectableTextBlock Rich(string text)
    {
        var block = new SelectableTextBlock
        {
            FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Body, TextWrapping = TextWrapping.Wrap,
            Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), LineHeight = LunetTokens.Type.Body * 1.45,
        };
        foreach (var run in MarkdownLite.ParseInline(text))
        {
            var r = new Run(run.Text);
            switch (run.Style)
            {
                case InlineStyle.Bold: r.FontWeight = FontWeight.SemiBold; break;
                case InlineStyle.Italic: r.FontStyle = FontStyle.Italic; break;
                case InlineStyle.Code:
                    r.FontFamily = LunetTokens.MonoFont;
                    r.Foreground = LunetTokens.Brush(LunetTokens.Color.Primary);
                    break;
            }
            block.Inlines!.Add(r);
        }
        return block;
    }

    private static Control Signature(string text) => new SelectableTextBlock
    {
        Text = text, FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label, TextWrapping = TextWrapping.Wrap,
        Foreground = LunetTokens.Brush(LunetTokens.Color.Primary),
    };

    private Control CodeBlock(string code)
    {
        var text = new SelectableTextBlock
        {
            Text = code.TrimEnd('\n'), FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label,
            Foreground = LunetTokens.Brush(LunetTokens.Color.Code), TextWrapping = TextWrapping.NoWrap,
        };
        var copy = LunetUi.Button("Copiar", ButtonKind.Ghost, LunetIcons.Copy, () => _ = Copy(code.TrimEnd('\n')));
        copy.HorizontalAlignment = HorizontalAlignment.Right;
        return new Border
        {
            Background = LunetTokens.Brush(LunetTokens.Color.Stage), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xxs, LunetTokens.Space.Xxs, LunetTokens.Space.S),
            Child = new StackPanel
            {
                Children =
                {
                    copy,
                    new ScrollViewer
                    {
                        Content = text, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    },
                },
            },
        };
    }

    private async Task Copy(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
            Haptics.Confirm?.Invoke();
        }
    }

    private Control Link(DocBlock block)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(LunetUi.Text(block.Text, LunetTokens.Type.Callout, LunetTokens.Color.Primary, FontWeight.SemiBold));
        if (!string.IsNullOrWhiteSpace(block.Detail))
            stack.Children.Add(LunetUi.Text(block.Detail, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var b = new Button
        {
            Content = stack, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs),
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(LunetTokens.Radius.M),
        };
        AutomationProperties.SetName(b, block.Text);
        if (block.Target is { } target) b.Click += (_, _) => Navigate(target);
        return b;
    }
}
