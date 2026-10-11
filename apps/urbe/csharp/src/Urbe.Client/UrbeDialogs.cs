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
public sealed class UrbeDialogs : Grid
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
            Dispatcher.UIThread.Post(() => (focus ?? (Control)this).Focus(), DispatcherPriority.Loaded);
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

    private static Button Action(string text, bool primary, bool danger = false)
    {
        var b = new Button
        {
            Content = text, MinHeight = 48, Padding = new Thickness(18, 0), CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = UrbeTheme.UiFont, FontSize = 15, FontWeight = FontWeight.SemiBold,
            BorderThickness = new Thickness(primary || danger ? 0 : 1), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            Background = UrbeTheme.Brush(danger ? UrbeTheme.Danger : primary ? UrbeTheme.Accent : UrbeTheme.Surface3),
            Foreground = UrbeTheme.Brush(danger ? "#2a0a0e" : primary ? UrbeTheme.AccentInk : UrbeTheme.Text)
        };
        return b;
    }

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
        void ShowError(string t) { error.Text = t; error.IsVisible = true; input.BorderBrush = UrbeTheme.Brush(UrbeTheme.Danger); input.Focus(); }
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

    /// <summary>UrbeDialogs.alert.</summary>
    public Task AlertAsync(string title, string message, string confirm = "Ok") => Open<bool>(finish =>
    {
        var ok = Action(confirm, true);
        ok.Click += (_, _) => finish(true);
        ok.Margin = new Thickness(0, 20, 0, 0);
        return (new StackPanel { Children = { Head(title, message, () => finish(false)), ok } }, false, () => finish(true), ok);
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
                Content = row, MinHeight = 54, Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12), Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center
            };
            b.Click += (_, _) => finish(index);
            list.Children.Add(b);
        }
        return (new StackPanel { Children = { Head(title, message, () => finish(null)), list } }, null, null, list.Children.Count > 0 ? list.Children[0] : null);
    });

    /// <summary>UrbeDialogs.menu: closes, then runs the chosen item.</summary>
    public async Task MenuAsync(string title, IReadOnlyList<DialogItem> items, string? message = null)
    {
        var i = await ChooseAsync(title, items, message);
        if (i is { } k && items[k].Run is { } run) await run();
    }
}
