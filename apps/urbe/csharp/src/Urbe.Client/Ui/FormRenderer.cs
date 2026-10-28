using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client.Ui;

/// <summary>One mounted native editor per session. Changes update values, never rebuild the form.</summary>
public sealed class FormRenderer : StackPanel
{
    public FormSession Session { get; }
    private readonly Dictionary<string, (Control Row, TextBlock Error)> _rows = [];
    private readonly Dictionary<string, TextBlock> _computed = [];
    private readonly IReadOnlyCollection<string> _notes, _folders;
    public Button SubmitButton { get; }
    public Button CancelButton { get; }
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public FormRenderer(FormSession session, Action changed, Action submit, Action cancel,
        IReadOnlyCollection<string>? notes = null, IReadOnlyCollection<string>? folders = null)
    {
        Session = session;
        _notes = notes ?? [];
        _folders = folders ?? [];
        Spacing = 12;
        Children.Add(Text(session.Definition.Title, 18));
        if (session.Definition.Description is { } description) Children.Add(Text(description, 13));
        if (session.Definition.SourcePath is { } source) Children.Add(Text("Origem: " + source, 12));
        string? group = null;
        foreach (var f in session.Definition.Fields)
        {
            if (f.Group is not null && f.Group != group) { group = f.Group; Children.Add(Text(group, 15)); }
            var row = new StackPanel { Spacing = 6 };
            var label = Text(f.Label + (f.Required ? " *" : ""), 14);
            row.Children.Add(label);
            if (f.Description is { } help) row.Children.Add(Text(help, 12));
            var editor = Editor(f, () => { Refresh(); changed(); });
            Avalonia.Automation.AutomationProperties.SetName(editor, label.Text);
            row.Children.Add(editor);
            var error = Text("", 12); error.Foreground = UrbeTheme.Brush(UrbeTheme.Danger); error.IsVisible = false;
            row.Children.Add(error);
            _rows[f.Id] = (row, error);
            Children.Add(row);
        }
        Status.Foreground = UrbeTheme.Brush(UrbeTheme.Danger);
        Children.Add(Status);
        SubmitButton = new Button { Content = "Enviar respostas", MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Accent), Foreground = Brushes.White, FontWeight = FontWeight.SemiBold };
        CancelButton = new Button { Content = "Cancelar formulário", MinHeight = UrbeTokens.MinTouch, Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
        SubmitButton.Click += (_, _) => { if (Validate()) submit(); };
        CancelButton.Click += (_, _) => cancel();
        Children.Add(new WrapPanel { Children = { SubmitButton, CancelButton } });
        Refresh();
    }
    private static TextBlock Text(string value, double size) => new() { Text = value, FontSize = size,
        TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text) };
    private Control Editor(FormField f, Action changed)
    {
        var initial = Session.Values[f.Id];
        void Set(JsonNode? value) { Session.Values[f.Id] = value; changed(); }
        if (f.Type == "calculated") { var label = Text("", 15); _computed[f.Id] = label; return label; }
        if (f.Type == "date")
        {
            var picker = new CalendarDatePicker { MinHeight = UrbeTokens.MinTouch, PlaceholderText = "Escolher data" };
            if (DateTime.TryParseExact(initial?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) picker.SelectedDate = date;
            picker.SelectedDateChanged += (_, _) => Set(JsonValue.Create(picker.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            return picker;
        }
        if (f.Type == "time")
        {
            var picker = new TimePicker { MinHeight = UrbeTokens.MinTouch, ClockIdentifier = "24HourClock" };
            if (TimeSpan.TryParse(initial?.ToString(), CultureInfo.InvariantCulture, out var time)) picker.SelectedTime = time;
            picker.SelectedTimeChanged += (_, _) => Set(JsonValue.Create(picker.SelectedTime?.ToString(@"hh\:mm", CultureInfo.InvariantCulture)));
            return picker;
        }
        if (f.Type == "scale")
        {
            var slider = new Slider { Minimum = f.Min ?? 0, Maximum = f.Max ?? 10, MinHeight = UrbeTokens.MinTouch, TickFrequency = 1, IsSnapToTickEnabled = true };
            slider.Value = double.TryParse(initial?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : slider.Minimum;
            Session.Values[f.Id] = slider.Value;
            var value = Text(slider.Value.ToString(CultureInfo.InvariantCulture), 13);
            slider.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { value.Text = slider.Value.ToString(CultureInfo.InvariantCulture); Set(JsonValue.Create(slider.Value)); } };
            return new StackPanel { Children = { slider, value } };
        }
        if (f.Type == "repeat")
        {
            var rows = new StackPanel { Spacing = 12 };
            var items = initial as JsonArray ?? new JsonArray();
            if (initial is not JsonArray) Session.Values[f.Id] = items;
            var add = new Button { Content = "Adicionar item", MinHeight = UrbeTokens.MinTouch };
            void Add(JsonObject row)
            {
                var sub = new FormSession { Definition = new() { Title = f.Label, Fields = f.Fields }, Values = row };
                var editor = new FormRenderer(sub, changed, () => { }, () => { }, _notes, _folders);
                editor.SubmitButton.IsVisible = false;
                editor.CancelButton.Content = "Remover item";
                editor.CancelButton.Click += (_, _) => { items.Remove(row); rows.Children.Remove(editor); changed(); };
                rows.Children.Insert(rows.Children.Count - 1, editor);
            }
            rows.Children.Add(add);
            foreach (var row in items.OfType<JsonObject>()) Add(row);
            add.Click += (_, _) =>
            {
                if (f.Max is not null && items.Count >= f.Max) return;
                var child = FormSession.Create(new() { Fields = f.Fields }).Values;
                items.Add(child); Add(child); changed();
            };
            return rows;
        }
        if (f.Type is "checkbox" or "toggle")
        {
            var box = new CheckBox { IsChecked = initial is JsonValue j && j.TryGetValue<bool>(out var b) && b, MinHeight = UrbeTokens.MinTouch };
            box.IsCheckedChanged += (_, _) => Set(JsonValue.Create(box.IsChecked == true));
            if (initial is null) Session.Values[f.Id] = false;
            return box;
        }
        if (f.Type is "select" or "note" or "folder")
        {
            var options = f.Type == "note" ? _notes : f.Type == "folder" ? _folders : f.Options;
            var box = new ComboBox { ItemsSource = options, SelectedItem = initial?.ToString(), MinHeight = UrbeTokens.MinTouch,
                HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "Selecionar" };
            box.SelectionChanged += (_, _) => Set(JsonValue.Create(box.SelectedItem as string));
            return box;
        }
        if (f.Type == "multi_select")
        {
            // CST-047: one box per option; a tap ticks or unticks it and keeps the others (a list's selection did not on touch)
            var selected = (initial as JsonArray ?? []).Select(x => x?.ToString() ?? "").ToHashSet(StringComparer.Ordinal);
            var boxes = new StackPanel { Spacing = 2 };
            foreach (var option in f.Options)
            {
                var box = new CheckBox { Content = option, IsChecked = selected.Contains(option), MinHeight = UrbeTokens.MinTouch };
                box.IsCheckedChanged += (_, _) => { FormInput.Toggle(Session, f, option, box.IsChecked == true); changed(); };
                boxes.Children.Add(box);
            }
            return boxes;
        }
        var text = new TextBox { Text = f.Type == "list" && initial is JsonArray a ? string.Join("\n", a.Select(x => x?.ToString())) : initial?.ToString() ?? "",
            AcceptsReturn = f.Type is "long_text" or "list", MinHeight = f.Type is "long_text" or "list" ? 96 : UrbeTokens.MinTouch,
            TextWrapping = TextWrapping.Wrap, PlaceholderText = f.Type switch { "date" => "AAAA-MM-DD", "time" => "HH:mm", "list" => "Um item por linha", "color" => "#RRGGBB", _ => "" } };
        TextActions.Attach(text);
        bool numeric = f.Type is "integer" or "number";
        // CST-047: the number keyboard, and only what a number may have gets in (whatever the keyboard sends)
        if (numeric) Avalonia.Input.TextInput.TextInputOptions.SetContentType(text, f.Type == "integer"
            ? Avalonia.Input.TextInput.TextInputContentType.Digits : Avalonia.Input.TextInput.TextInputContentType.Number);
        text.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            var value = text.Text ?? "";
            if (numeric)
            {
                var kept = FormInput.NumberText(value, f.Type == "integer");
                if (kept != value)
                {
                    int caret = Math.Max(0, text.CaretIndex - (value.Length - kept.Length));
                    text.Text = kept;
                    text.CaretIndex = Math.Min(caret, kept.Length);
                    return;
                }
                Set(FormInput.ParseNumber(kept) is { } n ? JsonValue.Create(n) : null);
                ShowError(f);
            }
            else if (f.Type == "list") Set(new JsonArray(value.Split('\n').Where(x => x.Length > 0).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()));
            else Set(JsonValue.Create(value));
        };
        return text;
    }
    /// <summary>A value that is wrong shows why while the person types (an empty one waits for Enviar).</summary>
    private void ShowError(FormField f)
    {
        if (!_rows.TryGetValue(f.Id, out var row)) return;
        var error = Session.Values[f.Id] is null ? null : Session.ErrorFor(f, _notes, _folders);
        row.Error.Text = error;
        row.Error.IsVisible = error is not null;
    }

    private void Refresh()
    {
        foreach (var f in Session.Definition.Fields)
        {
            _rows[f.Id].Row.IsVisible = Session.Visible(f);
            _rows[f.Id].Error.IsVisible = false;
        }
        var answers = Session.Answers();
        foreach (var item in _computed) item.Value.Text = answers[item.Key]?.ToString() ?? "";
    }
    public bool Validate()
    {
        var errors = Session.Validate(_notes, _folders);
        foreach (var pair in _rows) { pair.Value.Error.IsVisible = errors.ContainsKey(pair.Key); pair.Value.Error.Text = errors.GetValueOrDefault(pair.Key); }
        return errors.Count == 0;
    }
}
