using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>
/// #1005: the study library as a panel of its own — the sets with what is due today, a set with its cards, and the
/// review (front, then the answer, then how well it was remembered). It draws only; the data and every change go
/// through <see cref="MainView"/>'s one study operation, the same the Assistente uses.
/// </summary>
public sealed class FlashcardsPanel : Border
{
    public sealed record Review(Flashcard Card, IReadOnlyList<(Recall Grade, string When)> Grades);

    /// <summary>What the panel needs from the app.</summary>
    public interface IHost
    {
        IReadOnlyList<(StudySet Set, StudySetProgress Progress)> Sets();
        /// <summary>The cards due in a set (all of them when <paramref name="all"/>).</summary>
        IReadOnlyList<Flashcard> Due(StudySet set, bool all);
        IReadOnlyList<(string Label, string Detail)> When(Flashcard card);
        void Grade(Flashcard card, Recall grade);
        string StateOf(Flashcard card);
        Task NewSetAsync();
        Task SetMenuAsync(StudySet set);
        Task CardMenuAsync(StudySet set, Flashcard card);
        void OpenNote(string path);
        void Close();
    }

    private readonly IHost _host;
    private readonly TextBlock _title = UrbeTokens.Type.Title.Apply(new TextBlock { Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextPrimary), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
    private readonly Button _back;
    private readonly ContentControl _body = new();
    private string? _setId;
    private (StudySet Set, List<Flashcard> Queue, int At, int Done, int Right, bool Shown)? _review;

    /// <summary>"list", "set", "review" or "summary" (tests and automation).</summary>
    public string Screen { get; private set; } = "list";

    public FlashcardsPanel(IHost host)
    {
        _host = host;
        Background = UrbeTheme.Brush(UrbeTokens.Color.Background);
        Focusable = true;
        _back = IconButton(UrbeTheme.Icons.Back, "Voltar", () => GoBack());
        var close = IconButton(UrbeTheme.Icons.Close, "Fechar Flashcards", () => _host.Close());
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(8, 8, 8, 4) };
        head.Children.Add(_back);
        Grid.SetColumn(_title, 1);
        _title.Margin = new Thickness(8, 0);
        head.Children.Add(_title);
        Grid.SetColumn(close, 2);
        head.Children.Add(close);
        var layout = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        layout.Children.Add(head);
        layout.Children.Add(new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Child = layout;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = GoBack(); if (!e.Handled) _host.Close(); e.Handled = true; }
            else if (Screen == "review" && _review is { Shown: false } && e.Key is Key.Space or Key.Enter) { Reveal(); e.Handled = true; }
            else if (Screen == "review" && _review is { Shown: true } && e.Key is >= Key.D1 and <= Key.D4) { GradeCurrent((Recall)(e.Key - Key.D1)); e.Handled = true; }
        };
        ShowList();
    }

    /// <summary>Shows the list, or a set when <paramref name="setId"/> names one.</summary>
    public void Show(string? setId)
    {
        if (setId is not null && _host.Sets().FirstOrDefault(s => s.Set.Id == setId || s.Set.DeckId == setId).Set is { } set) ShowSet(set.Id);
        else ShowList();
    }

    /// <summary>Redraws what is on the screen after a change (a set renamed, a card edited).</summary>
    public void Refresh()
    {
        if (Screen == "set" && _setId is { } id) ShowSet(id);
        else if (Screen == "list") ShowList();
    }

    /// <summary>Back one level; false at the list (the panel itself then closes).</summary>
    public bool GoBack()
    {
        if (Screen is "review" or "summary" && _review is { } r) { _review = null; ShowSet(r.Set.Id); return true; }
        if (Screen == "set") { ShowList(); return true; }
        return false;
    }

    public void ShowList()
    {
        Screen = "list";
        _setId = null;
        _review = null;
        _title.Text = "Flashcards";
        _back.IsVisible = false;
        var sets = _host.Sets();
        var list = new StackPanel { Spacing = 2, Margin = new Thickness(12, 4, 12, 24), MaxWidth = 760 };
        int due = sets.Sum(s => s.Progress.Due);
        list.Children.Add(Caption(sets.Count == 0 ? "Nenhum conjunto ainda." : (due == 0 ? "Nada para revisar hoje." : due + (due == 1 ? " cartão para hoje" : " cartões para hoje")) + " · " + sets.Count + (sets.Count == 1 ? " conjunto" : " conjuntos")));
        list.Children.Add(Primary("Novo conjunto", () => _ = NewAsync()));
        if (sets.Count == 0)
            list.Children.Add(Caption("Crie um aqui, peça ao Assistente (\"faça flashcards sobre…\") ou escreva pergunta::resposta numa linha de uma nota."));
        foreach (var (set, p) in sets)
            list.Children.Add(Row(set.Title,
                p.Cards + (p.Cards == 1 ? " cartão" : " cartões") + " · " + (p.Due == 0 ? (p.Next is { } n ? "próximo em " + n.ToString("dd/MM") : "nada para hoje") : p.Due + " para hoje") + (set.Kind == "note" ? " · da nota" : ""),
                set.Kind == "note" ? UrbeTheme.Icons.File : UrbeTheme.Icons.Book, () => ShowSet(set.Id), "Conjunto " + set.Title));
        _body.Content = list;
    }

    private async Task NewAsync()
    {
        await _host.NewSetAsync();
        ShowList();
    }

    public void ShowSet(string setId)
    {
        var (set, p) = _host.Sets().FirstOrDefault(s => s.Set.Id == setId);
        if (set is null) { ShowList(); return; }
        Screen = "set";
        _setId = set.Id;
        _title.Text = set.Title;
        _back.IsVisible = true;
        var list = new StackPanel { Spacing = 2, Margin = new Thickness(12, 4, 12, 24), MaxWidth = 760 };
        list.Children.Add(Caption(p.Cards + (p.Cards == 1 ? " cartão" : " cartões") + " · " + p.Reviewed + " já revisados" + (set.NotePath is { } np ? " · da nota " + np : "")));
        var actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        actions.Children.Add(Primary(p.Due == 0 ? "Revisar todos" : "Revisar " + p.Due + (p.Due == 1 ? " cartão" : " cartões"), () => StartReview(set, all: p.Due == 0)));
        actions.Children.Add(Secondary(set.Kind == "note" ? "Abrir a nota" : "Mais ações", () =>
        {
            if (set.NotePath is { } path) _host.OpenNote(path);
            else _ = MenuAsync(set);
        }));
        list.Children.Add(actions);
        foreach (var card in set.Cards)
            list.Children.Add(Row(card.Front, card.Back + " · " + _host.StateOf(card), null,
                () => { if (set.Kind == "deck") _ = CardAsync(set, card); }, "Cartão " + card.Front));
        _body.Content = list;
    }

    private async Task MenuAsync(StudySet set) { await _host.SetMenuAsync(set); if (_host.Sets().Any(s => s.Set.Id == set.Id)) ShowSet(set.Id); else ShowList(); }
    private async Task CardAsync(StudySet set, Flashcard card) { await _host.CardMenuAsync(set, card); ShowSet(set.Id); }

    public void StartReview(StudySet set, bool all)
    {
        var queue = _host.Due(set, all).ToList();
        if (queue.Count == 0) return;
        _review = (set, queue, 0, 0, 0, false);
        ShowCard();
    }

    private void ShowCard()
    {
        if (_review is not { } r) return;
        if (r.At >= r.Queue.Count) { ShowSummary(); return; }
        Screen = "review";
        _back.IsVisible = true;
        var card = r.Queue[r.At];
        _title.Text = r.Set.Title + " · " + (r.At + 1) + " de " + r.Queue.Count;
        var page = new StackPanel { Spacing = 16, Margin = new Thickness(16, 24, 16, 24), MaxWidth = 640 };
        page.Children.Add(new Border
        {
            Background = UrbeTheme.Brush(UrbeTokens.Color.Surface), CornerRadius = new CornerRadius(UrbeTokens.Radius.M), Padding = new Thickness(UrbeTokens.Space.L, UrbeTokens.Space.Xl), MinHeight = 160,
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    UrbeTokens.Type.Headline.Apply(new SelectableTextBlock { Text = card.Front, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextPrimary), TextAlignment = TextAlignment.Center }),
                    UrbeTokens.Type.Body.Apply(new SelectableTextBlock { Text = card.Back, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextSecondary), TextAlignment = TextAlignment.Center, IsVisible = r.Shown, Name = "verso" }),
                }
            }
        });
        if (!r.Shown) page.Children.Add(Primary("Mostrar resposta", Reveal, stretch: true));
        else
        {
            var grades = new Avalonia.Controls.Primitives.UniformGrid { Columns = 4 };
            var when = _host.When(card);
            string[] names = ["Errei", "Difícil", "Bom", "Fácil"];
            for (int g = 0; g < 4; g++)
            {
                var grade = (Recall)g;
                var b = new Button
                {
                    Content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center, Children =
                    {
                        UrbeTokens.Type.Label.Apply(new TextBlock { Text = names[g], HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(g == 0 ? UrbeTokens.Color.Danger : UrbeTokens.Color.TextPrimary) }),
                        UrbeTokens.Type.Caption.Apply(new TextBlock { Text = when[g].Detail, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextTertiary) })
                    } },
                    MinHeight = 56, Margin = new Thickness(UrbeTokens.Space.Xxs), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center
                };
                Avalonia.Automation.AutomationProperties.SetName(b, names[g]);
                b.Click += (_, _) => GradeCurrent(grade);
                grades.Children.Add(b);
            }
            page.Children.Add(grades);
        }
        _body.Content = page;
        Focus();
    }

    public void Reveal()
    {
        if (_review is not { Shown: false } r) return;
        _review = r with { Shown = true };
        ShowCard();
    }

    public void GradeCurrent(Recall grade)
    {
        if (_review is not { Shown: true } r) return;
        _host.Grade(r.Queue[r.At], grade);
        _review = r with { At = r.At + 1, Done = r.Done + 1, Right = r.Right + (grade > Recall.Errei ? 1 : 0), Shown = false };
        ShowCard();
    }

    private void ShowSummary()
    {
        if (_review is not { } r) return;
        Screen = "summary";
        _title.Text = r.Set.Title;
        var page = new StackPanel { Spacing = 12, Margin = new Thickness(16, 32), MaxWidth = 640 };
        page.Children.Add(UrbeTokens.Type.Headline.Apply(new TextBlock { Text = r.Done + (r.Done == 1 ? " cartão revisado" : " cartões revisados"), Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center }));
        page.Children.Add(UrbeTokens.Type.Callout.Apply(new TextBlock { Text = r.Right + " lembrados, " + (r.Done - r.Right) + " para rever", Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextSecondary), HorizontalAlignment = HorizontalAlignment.Center }));
        page.Children.Add(Primary("Voltar ao conjunto", () => GoBack(), stretch: true));
        _body.Content = page;
    }

    private static TextBlock Caption(string text) => UrbeTokens.Type.Footnote.Apply(new TextBlock { Text = text, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextSecondary), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(UrbeTokens.Space.Xxs, 0, UrbeTokens.Space.Xxs, UrbeTokens.Space.Xs) });

    private static Button Primary(string label, Action click, bool stretch = false)
    {
        var b = new Button
        {
            Content = UrbeTokens.Type.Button.Apply(new TextBlock { Text = label, Foreground = UrbeTheme.Brush(UrbeTokens.Color.OnPrimary) }),
            Background = UrbeTheme.Brush(UrbeTokens.Color.Primary), MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(UrbeTokens.Space.L, UrbeTokens.Space.S), Margin = new Thickness(0, 0, UrbeTokens.Space.Xs, UrbeTokens.Space.Xs), CornerRadius = new CornerRadius(UrbeTokens.Radius.M),
            HorizontalAlignment = stretch ? HorizontalAlignment.Stretch : HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Center
        };
        Avalonia.Automation.AutomationProperties.SetName(b, label);
        b.Click += (_, _) => click();
        return b;
    }

    private static Button Secondary(string label, Action click)
    {
        var b = new Button
        {
            Content = UrbeTokens.Type.Button.Apply(new TextBlock { Text = label, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextPrimary) }), Background = UrbeTheme.Brush(UrbeTokens.Color.SurfaceRaised),
            MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(UrbeTokens.Space.M, UrbeTokens.Space.S), Margin = new Thickness(0, 0, UrbeTokens.Space.Xs, UrbeTokens.Space.Xs), CornerRadius = new CornerRadius(UrbeTokens.Radius.M)
        };
        Avalonia.Automation.AutomationProperties.SetName(b, label);
        b.Click += (_, _) => click();
        return b;
    }

    private static Button Row(string title, string detail, string? icon, Action click, string name)
    {
        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(UrbeTokens.Type.BodyStrong.Apply(new TextBlock { Text = title, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextPrimary), TextTrimming = TextTrimming.CharacterEllipsis }));
        text.Children.Add(UrbeTokens.Type.Footnote.Apply(new TextBlock { Text = detail, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextSecondary), TextTrimming = TextTrimming.CharacterEllipsis }));
        var content = new DockPanel();
        if (icon is not null)
        {
            var glyph = UrbeTheme.Icon(icon, 20, UrbeTokens.Color.TextSecondary);
            glyph.Margin = new Thickness(0, 0, UrbeTokens.Space.S, 0);
            DockPanel.SetDock(glyph, Dock.Left);
            content.Children.Add(glyph);
        }
        content.Children.Add(text);
        var b = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = Brushes.Transparent, Padding = new Thickness(UrbeTokens.Space.S, UrbeTokens.Space.Xs), MinHeight = 56 };
        Avalonia.Automation.AutomationProperties.SetName(b, name);
        b.Click += (_, _) => click();
        return b;
    }

    private static Button IconButton(string icon, string name, Action click)
    {
        var b = new Button { Content = UrbeTheme.Icon(icon, 22), Width = UrbeTokens.MinTouch, Height = UrbeTokens.MinTouch, Background = Brushes.Transparent, Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(b, name);
        ToolTip.SetTip(b, name);
        b.Click += (_, _) => click();
        return b;
    }
}
