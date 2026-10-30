using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Urbe.Client;

/// <summary>A choice of a 1.8.4 menu (UrbeDialogs.menu / abrirMenuExplorer).</summary>
public sealed record DialogItem(string Label, string? Icon = null, string? Detail = null, bool Danger = false, Func<Task>? Run = null);

/// <summary>Options of UrbeDialogs.prompt.</summary>
public sealed record PromptOptions(string Title, string? Label = null, string Value = "", string Placeholder = "", string? Hint = null,
    string Confirm = "Confirmar", string Cancel = "Cancelar", Func<string, string?>? Validate = null, bool Required = true);

/// <summary>
/// src/ui/dialogs.js natively: one dialog at a time (the next ones wait), a bottom sheet on a
/// phone and a centred card from 720 px, Escape/backdrop cancel and Enter confirms. Visual from
/// theme.css .udlg / #explorerActionSheet (they share it).
/// </summary>
public sealed partial class UrbeDialogs : Grid
{
    private const double Wide = 720;
    private Task _queue = Task.CompletedTask;
    private readonly Border _card = new();
    private Action? _cancel, _enter;

    public UrbeDialogs()
    {
        IsVisible = false;
        Background = new SolidColorBrush(Color.FromArgb(153, 5, 6, 8)); // rgba(5,6,8,.6)
        _card.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        _card.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _card.BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000");
        Children.Add(_card);
        PointerPressed += (_, e) => { if (e.Source == this) _cancel?.Invoke(); };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; _cancel?.Invoke(); }
            else if (e.Key == Key.Enter && _enter is not null) { e.Handled = true; _enter(); }
        };
        Focusable = true;
    }

    /// <summary>True while a dialog is open.</summary>
    public bool IsOpen => IsVisible;

    /// <summary>Dismiss the active dialog using its own cancellation path (Android system Back).</summary>
    public void Cancel() { if (IsOpen) _cancel?.Invoke(); }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Layout(e.NewSize.Width);
    }

    private void Layout(double width)
    {
        bool wide = width >= Wide;
        _card.HorizontalAlignment = wide ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _card.VerticalAlignment = wide ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _card.Width = wide ? Math.Min(460, Math.Max(0, width - 48)) : double.NaN;
        _card.CornerRadius = wide ? new CornerRadius(22) : new CornerRadius(22, 22, 0, 0);
        _card.BorderThickness = wide ? new Thickness(1) : new Thickness(0, 1, 0, 0);
        _card.Padding = wide ? new Thickness(24, 20, 24, 24) : new Thickness(20, 8, 20, 20);
        _card.MaxHeight = Math.Max(200, Bounds.Height * .88);
    }

    private Task<T> Open<T>(Func<Action<T>, (Control Body, T CancelValue, Action? Enter, Control? Focus)> build)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Run()
        {
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool closed = false;
            void Finish(T value)
            {
                if (closed) return;
                closed = true;
                IsVisible = false;
                _cancel = _enter = null;
                _card.Child = null;
                done.TrySetResult(value);
                finished.TrySetResult();
            }
            var (body, cancelValue, enter, focus) = build(Finish);
            var stack = new StackPanel();
            if (Bounds.Width < Wide)   // the sheet's grabber
                stack.Children.Add(new Border { Width = 40, Height = 4, CornerRadius = new CornerRadius(4), Background = UrbeTheme.Brush(UrbeTheme.Line2), Margin = new Thickness(0, 4, 0, 12), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(body);
            _card.Child = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            _cancel = () => Finish(cancelValue);
            _enter = enter;
            Layout(Bounds.Width);
            IsVisible = true;
            // the scrim fades in and the card rises into place (UX.md P6); the dialog is usable at once
            Ui.UrbeMotion.Enter(this, 0, 0, UrbeTokens.Motion.Fast);
            Ui.UrbeMotion.Enter(_card, 0, Bounds.Width < Wide ? 32 : 8);
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsVisible) return;
                (KeyboardFocusPolicy.AutoFocusOnOpen(OperatingSystem.IsAndroid()) ? focus ?? (Control)this : this).Focus();
            }, DispatcherPriority.Loaded);
            await finished.Task;
        }
        async Task After(Task previous)
        {
            try { await previous; } catch { /* a failed dialog never blocks the next one */ }
            await Run();
        }
        _queue = After(_queue);
        return done.Task;
    }

    // ---------------------------------------------------------------- pieces

    private Control Head(string title, string? message, Action cancel)
    {
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 20, UrbeTheme.Text2),
            Width = 40, Height = 40, CornerRadius = new CornerRadius(20), Background = Brushes.Transparent,
            Margin = new Thickness(0, 0, -8, 0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, "Fechar");
        Avalonia.Automation.AutomationProperties.SetName(close, "Fechar");
        Ui.UrbeMotion.Pressable(close);
        close.Click += (_, _) => cancel();
        var t = new TextBlock { Text = title, FontFamily = UrbeTheme.UiFont, FontSize = 18, FontWeight = FontWeight.Bold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(t);
        var box = new StackPanel { Children = { head } };
        if (!string.IsNullOrEmpty(message))
            box.Children.Add(new TextBlock { Text = message, FontFamily = UrbeTheme.UiFont, FontSize = 15, LineHeight = 22.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 16) });
        return box;
    }

    // .ui-btn / .ui-btn-primary / .ui-btn-danger: the shared button of the visual system (UC-69)
    private static Button Action(string text, bool primary, bool danger = false) =>
        new Ui.UrbeButton(text, danger ? Ui.ButtonKind.Danger : primary ? Ui.ButtonKind.Primary : Ui.ButtonKind.Secondary)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

    // ---------------------------------------------------------------- dialogs

    /// <summary>UrbeDialogs.prompt: the typed text, or null when cancelled.</summary>
    public Task<string?> PromptAsync(PromptOptions o) => Open<string?>(finish =>
    {
        var input = new TextBox
        {
            Text = o.Value, PlaceholderText = o.Placeholder, MinHeight = 50, Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(12),
            FontFamily = UrbeTheme.UiFont, FontSize = 16, FontWeight = FontWeight.Medium,
            Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent)
        };
        input.Resources["TextControlBackgroundFocused"] = UrbeTheme.Brush(UrbeTheme.Bg);
        input.Resources["TextControlBackgroundPointerOver"] = UrbeTheme.Brush(UrbeTheme.Bg);
        input.Resources["TextControlBorderBrushFocused"] = UrbeTheme.Brush(UrbeTheme.Accent);
        var error = new TextBlock { IsVisible = false, Foreground = UrbeTheme.Brush(UrbeTheme.Danger), FontFamily = UrbeTheme.UiFont, FontSize = 13, FontWeight = FontWeight.Medium, Margin = new Thickness(2, 6, 2, 0), TextWrapping = TextWrapping.Wrap };
        void ShowError(string t) { error.Text = t; error.IsVisible = true; input.BorderBrush = UrbeTheme.Brush(UrbeTheme.Danger); if (KeyboardFocusPolicy.AutoFocusOnOpen(OperatingSystem.IsAndroid())) input.Focus(); }
        input.TextChanged += (_, _) => { error.IsVisible = false; input.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2); };
        void Submit()
        {
            var v = input.Text ?? "";
            if (o.Required && v.Trim().Length == 0) { ShowError("Preencha este campo."); return; }
            if (o.Validate?.Invoke(v) is { Length: > 0 } problem) { ShowError(problem); return; }
            finish(v);
        }
        var field = new StackPanel { Margin = new Thickness(0, 12, 0, 4) };
        if (o.Label is not null)
            field.Children.Add(new TextBlock { Text = o.Label, FontFamily = UrbeTheme.UiFont, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), Margin = new Thickness(0, 0, 0, 8) });
        field.Children.Add(input);
        var cancel = Action(o.Cancel, false);
        cancel.Click += (_, _) => finish(null);
        var ok = Action(o.Confirm, true);
        ok.Click += (_, _) => Submit();
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*"), Margin = new Thickness(0, 20, 0, 0) };
        Grid.SetColumn(ok, 2);
        actions.Children.Add(cancel);
        actions.Children.Add(ok);
        var body = new StackPanel { Children = { Head(o.Title, null, () => finish(null)), field, error } };
        if (o.Hint is not null)
            body.Children.Add(new TextBlock { Text = o.Hint, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 8, 2, 0) });
        body.Children.Add(actions);
        // selectName: the base name selected, without the extension
        Dispatcher.UIThread.Post(() =>
        {
            var v = input.Text ?? "";
            int dot = v.LastIndexOf('.');
            input.SelectionStart = 0;
            input.SelectionEnd = dot > 0 ? dot : v.Length;
        }, DispatcherPriority.Background);
        return (body, null, Submit, input);
    });

    /// <summary>
    /// The comments on a word (<c>{"…"}</c>, UC-49): all of them, to scroll and read, and a box to write one more.
    /// Returns the new comment, or null when closed without one.
    /// </summary>
    public Task<string?> CommentsAsync(string title, IReadOnlyList<string> comments) => Open<string?>(finish =>
    {
        var list = new StackPanel { Spacing = 8 };
        foreach (var c in comments)
            list.Children.Add(new Border
            {
                Background = UrbeTheme.Brush(UrbeTheme.Bg), CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10),
                BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
                Child = new SelectableTextBlock { Text = c, TextWrapping = TextWrapping.Wrap, FontFamily = UrbeTheme.UiFont, FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text) }
            });
        // no sideways scroll: with it the text gets unbounded width, never wraps and is cut at the edge
        var scroll = new ScrollViewer { Content = list, MaxHeight = 280, Margin = new Thickness(0, 12, 0, 0), IsVisible = comments.Count > 0,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var input = new TextBox
        {
            PlaceholderText = "Escreva um comentário", MinHeight = 50, Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(12),
            FontFamily = UrbeTheme.UiFont, FontSize = 16, AcceptsReturn = false, TextWrapping = TextWrapping.Wrap,
            Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text), Margin = new Thickness(0, 12, 0, 0),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent)
        };
        Avalonia.Automation.AutomationProperties.SetName(input, "Novo comentário");
        input.Resources["TextControlBackgroundFocused"] = UrbeTheme.Brush(UrbeTheme.Bg);
        input.Resources["TextControlBorderBrushFocused"] = UrbeTheme.Brush(UrbeTheme.Accent);
        void Submit() { var v = (input.Text ?? "").Trim(); finish(v.Length > 0 ? v : null); }
        var close = Action("Fechar", false);
        close.Click += (_, _) => finish(null);
        var add = Action("Adicionar", true);
        add.Click += (_, _) => Submit();
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*"), Margin = new Thickness(0, 16, 0, 0) };
        Grid.SetColumn(add, 2);
        actions.Children.Add(close);
        actions.Children.Add(add);
        var count = comments.Count switch { 0 => "Nenhum comentário ainda.", 1 => "1 comentário", var n => n + " comentários" };
        return (new StackPanel { Children = { Head(title, count, () => finish(null)), scroll, input, actions } }, null, Submit, input);
    });

    /// <summary>UrbeDialogs.alert.</summary>
    public Task AlertAsync(string title, string message, string confirm = "Ok") => Open<bool>(finish =>
    {
        var ok = Action(confirm, true);
        ok.Click += (_, _) => finish(true);
        ok.Margin = new Thickness(0, 20, 0, 0);
        return (new StackPanel { Children = { Head(title, message, () => finish(false)), ok } }, false, () => finish(true), ok);
    });

    /// <summary>UrbeDialogs.confirm: true when confirmed (Enter confirms; Escape, the backdrop and Cancelar do not).
    /// A danger confirmation uses .ui-btn-danger.</summary>
    public Task<bool> ConfirmAsync(string title, string? message, string confirm = "Confirmar", bool danger = false, string cancel = "Cancelar") => Open<bool>(finish =>
    {
        var no = Action(cancel, false);
        no.Click += (_, _) => finish(false);
        var yes = Action(confirm, !danger, danger);
        yes.Click += (_, _) => finish(true);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*"), Margin = new Thickness(0, 20, 0, 0) };
        Grid.SetColumn(yes, 2);
        actions.Children.Add(no);
        actions.Children.Add(yes);
        return (new StackPanel { Children = { Head(title, message, () => finish(false)), actions } }, false, () => finish(true), yes);
    });

    /// <summary>UrbeDialogs.choose: the index chosen, or null.</summary>
    public Task<int?> ChooseAsync(string title, IReadOnlyList<DialogItem> options, string? message = null) => Open<int?>(finish =>
    {
        var list = new StackPanel { Spacing = 2, Margin = new Thickness(-8, 8, -8, 0) };
        for (int i = 0; i < options.Count; i++)
        {
            var x = options[i];
            int index = i;
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = x.Label, FontFamily = UrbeTheme.UiFont, FontSize = 16, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(x.Danger ? UrbeTheme.Danger : UrbeTheme.Text) });
            if (x.Detail is not null)
                text.Children.Add(new TextBlock { Text = x.Detail, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            if (x.Icon is not null)
                row.Children.Add(new Border
                {
                    Width = 36, Height = 36, CornerRadius = new CornerRadius(10),
                    Background = x.Danger ? UrbeTheme.Brush(UrbeTheme.Danger, .12) : UrbeTheme.Brush(UrbeTheme.Surface3),
                    Child = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = UrbeTheme.Icon(x.Icon, 20, x.Danger ? UrbeTheme.Danger : UrbeTheme.Text2) }
                });
            row.Children.Add(text);
            var b = new Button
            {
                Content = row, MinHeight = UrbeAppearance.DialogOption.MinHeight, Padding = UrbeAppearance.DialogOption.Padding, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center
            };
            b.Click += (_, _) => finish(index);
            b.Tag = Fold(x.Label + " " + x.Detail);
            list.Children.Add(b);
        }
        // a long list (the folders of "Mover para") gets a search box and scrolls (UC-54)
        if (options.Count <= SearchFrom)
            return (new StackPanel { Children = { Head(title, message, () => finish(null)), list } }, null, null, list.Children.Count > 0 ? list.Children[0] : null);
        var search = new TextBox
        {
            PlaceholderText = "Buscar", MinHeight = 44, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 12, 0, 0),
            FontFamily = UrbeTheme.UiFont, FontSize = 15, Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent)
        };
        Avalonia.Automation.AutomationProperties.SetName(search, "Buscar");
        // CST-039: a search that finds nothing says so, instead of an empty list
        var none = new TextBlock
        {
            IsVisible = false, Margin = new Thickness(4, 12, 4, 4), TextWrapping = TextWrapping.Wrap, FontFamily = UrbeTheme.UiFont, FontSize = 14,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text3)
        };
        search.TextChanged += (_, _) =>
        {
            var q = Fold(search.Text ?? "");
            foreach (var c in list.Children) if (c is Button) c.IsVisible = q.Length == 0 || ((string?)c.Tag ?? "").Contains(q, StringComparison.Ordinal);
            none.IsVisible = !list.Children.Any(c => c is Button && c.IsVisible);
            none.Text = "Nada com “" + (search.Text ?? "").Trim() + "”.";
        };
        list.Children.Add(none);
        // Enter takes the first one still shown
        void First() { if (list.Children.FirstOrDefault(c => c.IsVisible) is Button f) f.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); }
        var scroll = new ScrollViewer { Content = list, MaxHeight = 420, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        return (new StackPanel { Children = { Head(title, message, () => finish(null)), search, scroll } }, null, First, search);
    });

    /// <summary>From this many options a choice list has a search box.</summary>
    public const int SearchFrom = 7;

    /// <summary>Lower case without accents, for searching ("Ação" finds "acao").</summary>
    private static string Fold(string text)
    {
        var d = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var ch in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>
    /// The region dialog of the 1.8.4 (#regionDialog: Nome, Descrição, Cor, "Salvar alterações"): the three values,
    /// or null when cancelled. The colour is one of <paramref name="colors"/> (the current one marked).
    /// </summary>
    public Task<(string Name, string Description, string Color)?> RegionAsync(string title, string name, string description, string color,
        IReadOnlyList<string> colors, string confirm) => Open<(string, string, string)?>(finish =>
    {
        TextBox Field(string value, bool multi)
        {
            var box = NewField(value, multi);
            box.Resources["TextControlBackgroundFocused"] = UrbeTheme.Brush(UrbeTheme.Bg);
            box.Resources["TextControlBackgroundPointerOver"] = UrbeTheme.Brush(UrbeTheme.Bg);
            box.Resources["TextControlBorderBrushFocused"] = UrbeTheme.Brush(UrbeTheme.Accent);
            return box;
        }
        TextBox NewField(string value, bool multi) => new()
        {
            Text = value, MinHeight = multi ? 76 : 50, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(12), FontFamily = UrbeTheme.UiFont, FontSize = 16,
            Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent)
        };
        TextBlock Label(string text) => new() { Text = text, FontFamily = UrbeTheme.UiFont, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), Margin = new Thickness(0, 12, 0, 8) };
        var nameBox = Field(name, false);
        var descBox = Field(description, true);
        var chosen = colors.Contains(color, StringComparer.OrdinalIgnoreCase) ? color : colors.Count > 0 ? colors[0] : color;
        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        void Paint()
        {
            foreach (var b in swatches.Children.OfType<Button>())
            {
                bool on = string.Equals((string)b.Tag!, chosen, StringComparison.OrdinalIgnoreCase);
                b.BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Line2);
                b.BorderThickness = new Thickness(on ? 3 : 1);
            }
        }
        foreach (var c in colors)
        {
            var swatch = new Button { Tag = c, Width = 38, Height = 38, CornerRadius = new CornerRadius(19), Background = UrbeTheme.Brush(c) };
            Avalonia.Automation.AutomationProperties.SetName(swatch, "Cor " + c);
            swatch.Click += (_, _) => { chosen = c; Paint(); };
            swatches.Children.Add(swatch);
        }
        Paint();
        void Submit() => finish(((nameBox.Text ?? "").Trim(), descBox.Text ?? "", chosen));
        var cancel = Action("Cancelar", false);
        cancel.Click += (_, _) => finish(null);
        var ok = Action(confirm, true);
        ok.Click += (_, _) => Submit();
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*"), Margin = new Thickness(0, 20, 0, 0) };
        Grid.SetColumn(ok, 2);
        actions.Children.Add(cancel);
        actions.Children.Add(ok);
        var body = new StackPanel { Children = { Head(title, null, () => finish(null)), Label("Nome"), nameBox, Label("Descrição"), descBox, Label("Cor"), swatches, actions } };
        return (body, null, Submit, nameBox);
    });

    /// <summary>A choice among pictures (the buildings' Visual, DEC-0045): a grid of pixel-art previews with their names;
    /// the current one is outlined. The index chosen, or null.</summary>
    public Task<int?> PickArtAsync(string title, IReadOnlyList<(string Label, IImage? Art)> options, int? current = null, string? message = null) => Open<int?>(finish =>
    {
        var grid = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-4, 8, -4, 0), HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < options.Count; i++)
        {
            var (label, art) = options[i];
            int index = i;
            var image = new Image { Source = art, Width = 60, Height = 70, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            var cell = new StackPanel { Spacing = 4, Children =
            {
                image,
                new TextBlock { Text = label, FontFamily = UrbeTheme.UiFont, FontSize = 12, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(UrbeTheme.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 84 }
            } };
            var b = new Button
            {
                Content = cell, Width = 92, Margin = new Thickness(4), Padding = new Thickness(4, 8), CornerRadius = new CornerRadius(12),
                Background = UrbeTheme.Brush(index == current ? UrbeTheme.Surface3 : UrbeTheme.Bg),
                BorderBrush = UrbeTheme.Brush(index == current ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(index == current ? 2 : 1),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Top
            };
            Avalonia.Automation.AutomationProperties.SetName(b, label);
            b.Click += (_, _) => finish(index);
            grid.Children.Add(b);
        }
        var scroll = new ScrollViewer { Content = grid, MaxHeight = 420, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        return (new StackPanel { Children = { Head(title, message, () => finish(null)), scroll } }, null, null, grid.Children.Count > 0 ? grid.Children[current ?? 0] : null);
    });

    /// <summary>UrbeDialogs.menu: closes, then runs the chosen item.</summary>
    public async Task MenuAsync(string title, IReadOnlyList<DialogItem> items, string? message = null)
    {
        var i = await ChooseAsync(title, items, message);
        if (i is { } k && items[k].Run is { } run) await run();
    }
}
