using System.Reflection;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Lunet.Runtime.Inspection;
using AndroidColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Inspector do jogo em execução: lista campos e propriedades do <c>Game</c> (ou o Inspector customizado do projeto), atualiza os
/// valores ao vivo e grava as edições na thread do jogo.
/// </summary>
internal sealed class InspectorPanel : LinearLayout
{
    private const int RefreshMilliseconds = 300;

    private sealed class Row(InspectorItem item)
    {
        public InspectorItem Item = item;
        public Action<InspectorItem>? Update;
    }

    private readonly Func<Game?> _game;
    private readonly Func<IReadOnlyList<string>> _projectFiles;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly LinearLayout _list;
    private readonly List<Row> _rows = [];
    private readonly int _pad;
    private string _signature = "";
    private Game? _lastGame;
    private IReadOnlyList<IInspector> _customs = [];
    private bool _running;

    public InspectorPanel(Context context, Func<Game?> game, Func<IReadOnlyList<string>> projectFiles) : base(context)
    {
        _game = game;
        _projectFiles = projectFiles;
        _pad = (int)(8 * context.Resources!.DisplayMetrics!.Density);
        Orientation = Orientation.Vertical;
        SetBackgroundColor(AndroidColor.Argb(225, 20, 22, 27));
        Clickable = true; // não deixa toques passarem para o jogo

        var header = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        header.SetPadding(_pad, _pad, _pad, _pad / 2);
        header.AddView(new TextView(context) { Text = "Inspector", TextSize = 16 }, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        AddView(header);

        _list = new LinearLayout(context) { Orientation = Orientation.Vertical };
        _list.SetPadding(_pad, 0, _pad, _pad * 2);
        var scroll = new ScrollView(context);
        scroll.AddView(_list);
        AddView(scroll, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
    }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        _running = true;
        _handler.Post(Tick);
    }

    protected override void OnDetachedFromWindow()
    {
        _running = false;
        _handler.RemoveCallbacksAndMessages(null);
        base.OnDetachedFromWindow();
    }

    private void Tick()
    {
        if (!_running) return;
        try { Refresh(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or TargetInvocationException or NullReferenceException)
        {
            // O jogo mexe nos próprios dados em outra thread; na próxima leitura costuma dar certo.
        }
        _handler.PostDelayed(Tick, RefreshMilliseconds);
    }

    private void Refresh()
    {
        var game = _game();
        if (game is null)
        {
            if (_signature != "none") ShowMessage("O jogo não está em execução. Aperte Run.");
            _signature = "none";
            _lastGame = null;
            return;
        }
        if (!ReferenceEquals(game, _lastGame))
        {
            _lastGame = game;
            _customs = ObjectInspector.FindCustomInspectors(game.GetType().Assembly);
            _signature = "";
        }
        var items = ObjectInspector.Build(game, _customs);
        var signature = string.Join('|', items.Select(i => $"{(int)i.Kind}:{i.Label}:{i.Depth}"));
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild(items);
            return;
        }
        for (var i = 0; i < _rows.Count && i < items.Count; i++)
        {
            _rows[i].Item = items[i];
            _rows[i].Update?.Invoke(items[i]);
        }
    }

    private void ShowMessage(string text)
    {
        _list.RemoveAllViews();
        _rows.Clear();
        _list.AddView(new TextView(Context) { Text = text, TextSize = 13 });
    }

    private void Rebuild(IReadOnlyList<InspectorItem> items)
    {
        _list.RemoveAllViews();
        _rows.Clear();
        foreach (var item in items)
        {
            var row = new Row(item);
            _rows.Add(row);
            _list.AddView(Build(row));
        }
    }

    private View Build(Row row)
    {
        var item = row.Item;
        var indent = (int)(item.Depth * 12 * Resources!.DisplayMetrics!.Density);
        switch (item.Kind)
        {
            case InspectorItemKind.Header:
            {
                var header = new TextView(Context) { Text = item.Label, TextSize = 14 };
                header.SetTypeface(null, TypefaceStyle.Bold);
                header.SetTextColor(AndroidColor.Argb(255, 130, 180, 255));
                header.SetPadding(indent, _pad, 0, _pad / 3);
                return header;
            }
            case InspectorItemKind.Label:
            {
                var label = new TextView(Context) { Text = item.Label, TextSize = 12 };
                label.SetPadding(indent, _pad / 4, 0, _pad / 4);
                return label;
            }
            case InspectorItemKind.Separator:
            {
                var line = new View(Context);
                line.SetBackgroundColor(AndroidColor.Argb(80, 255, 255, 255));
                line.LayoutParameters = new LayoutParams(ViewGroup.LayoutParams.MatchParent, 2);
                return line;
            }
            case InspectorItemKind.Button:
            {
                var button = new Button(Context) { Text = item.Label };
                button.SetAllCaps(false);
                button.Click += (_, _) => PostToGame(row, () => row.Item.Action?.Invoke());
                return button;
            }
            default:
                return BuildField(row, indent);
        }
    }

    private View BuildField(Row row, int indent)
    {
        var item = row.Item;
        var container = new LinearLayout(Context) { Orientation = Orientation.Vertical };
        container.SetPadding(indent, _pad / 4, 0, _pad / 4);

        var line = new LinearLayout(Context) { Orientation = Orientation.Horizontal };
        line.SetGravity(GravityFlags.CenterVertical);
        var label = new TextView(Context) { Text = item.Label, TextSize = 12 };
        line.AddView(label, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        container.AddView(line);
        if (!string.IsNullOrEmpty(item.Tooltip))
        {
            var hint = new TextView(Context) { Text = item.Tooltip, TextSize = 10 };
            hint.SetTextColor(AndroidColor.Argb(255, 150, 155, 165));
            container.AddView(hint);
        }

        var weight = new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.4f);
        switch (item.FieldKind)
        {
            case InspectorFieldKind.Bool:
            {
                var box = new CheckBox(Context);
                var updating = false;
                box.CheckedChange += (_, args) =>
                {
                    if (updating) return;
                    var value = args.IsChecked;
                    PostToGame(row, () => row.Item.Setter?.Invoke(value));
                };
                box.Enabled = item.IsEditable;
                row.Update = it => { updating = true; box.Checked = it.Getter?.Invoke() is true; updating = false; };
                row.Update(item);
                line.AddView(box);
                break;
            }
            case InspectorFieldKind.Enum:
            {
                var button = new Button(Context);
                button.SetAllCaps(false);
                button.Enabled = item.IsEditable;
                button.Click += (_, _) =>
                {
                    var names = row.Item.EnumNames?.ToArray() ?? [];
                    new AlertDialog.Builder(Context)!.SetTitle(row.Item.Label)!.SetItems(names, (_, args) =>
                    {
                        if (row.Item.ValueType is { } type && Enum.TryParse(type, names[args.Which], out var value))
                            PostToGame(row, () => row.Item.Setter?.Invoke(value));
                    })!.Show();
                };
                row.Update = it => button.Text = it.Getter?.Invoke()?.ToString() ?? "";
                row.Update(item);
                line.AddView(button, weight);
                break;
            }
            default:
                AddTextEditor(row, line, container, weight);
                break;
        }
        return container;
    }

    private void AddTextEditor(Row row, LinearLayout line, LinearLayout container, LayoutParams weight)
    {
        var item = row.Item;
        var edit = new EditText(Context) { TextSize = 12 };
        edit.Enabled = item.IsEditable;
        var multiline = item.MultilineLines > 1;
        edit.SetSingleLine(!multiline);
        if (multiline) edit.SetLines(item.MultilineLines);
        edit.SetMinWidth((int)(80 * Resources!.DisplayMetrics!.Density));
        if (item.FieldKind is InspectorFieldKind.Int or InspectorFieldKind.Float)
            edit.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagSigned | (item.FieldKind == InspectorFieldKind.Float ? InputTypes.NumberFlagDecimal : 0);

        void Commit()
        {
            if (row.Item.ValueType is not { } type) return;
            if (InspectorValue.TryParse(type, edit.Text ?? "", out var value))
                PostToGame(row, () => row.Item.Setter?.Invoke(value));
            else
            {
                edit.Text = InspectorValue.Format(type, row.Item.Getter?.Invoke());
                Toast.MakeText(Context, "Valor inválido para " + row.Item.Label, ToastLength.Short)?.Show();
            }
        }
        edit.EditorAction += (_, args) =>
        {
            Commit();
            args.Handled = false;
            edit.ClearFocus();
            (Context?.GetSystemService(Context.InputMethodService) as InputMethodManager)?.HideSoftInputFromWindow(edit.WindowToken, 0);
        };
        edit.FocusChange += (_, args) =>
        {
            if (!args.HasFocus && edit.Enabled) Commit();
        };

        View? swatch = null;
        if (item.FieldKind == InspectorFieldKind.Color)
        {
            swatch = new View(Context);
            var size = (int)(24 * Resources!.DisplayMetrics!.Density);
            line.AddView(swatch, new LayoutParams(size, size));
        }

        row.Update = it =>
        {
            if (edit.HasFocus) return;
            var text = it.ValueType is null ? "" : InspectorValue.Format(it.ValueType, it.Getter?.Invoke());
            if (edit.Text != text) edit.Text = text;
            if (swatch is not null && InspectorValue.TryParseColor(text, out var parsed) && parsed is Lunet.Graphics.Color c)
                swatch.SetBackgroundColor(AndroidColor.Argb(c.A, c.R, c.G, c.B));
        };
        row.Update(item);
        line.AddView(edit, weight);

        if (item.Min is { } min && item.Max is { } max && item.FieldKind is InspectorFieldKind.Int or InspectorFieldKind.Float)
        {
            var slider = new SeekBar(Context) { Max = 1000 };
            var updating = false;
            slider.ProgressChanged += (_, args) =>
            {
                if (updating || !args.FromUser || row.Item.ValueType is not { } type) return;
                var value = min + (max - min) * args.Progress / 1000f;
                object? typed = row.Item.FieldKind == InspectorFieldKind.Int ? (object)(int)MathF.Round(value) : value;
                PostToGame(row, () => row.Item.Setter?.Invoke(InspectorValue.Coerce(type, typed)));
            };
            slider.Enabled = item.IsEditable;
            var previous = row.Update;
            row.Update = it =>
            {
                previous?.Invoke(it);
                if (slider.Pressed || it.Getter?.Invoke() is not { } current) return;
                updating = true;
                slider.Progress = (int)Math.Clamp((Convert.ToSingle(current, System.Globalization.CultureInfo.InvariantCulture) - min) / (max - min) * 1000f, 0f, 1000f);
                updating = false;
            };
            row.Update(item);
            container.AddView(slider);
        }

        if (item.FileFilter is not null || item.Asset is not null)
        {
            var pick = new Button(Context) { Text = "…" };
            pick.SetMinWidth(0);
            pick.SetMinimumWidth(0);
            pick.Enabled = item.IsEditable;
            pick.Click += (_, _) => PickFile(row, edit);
            line.AddView(pick);
        }
    }

    private void PickFile(Row row, EditText edit)
    {
        var filter = row.Item.FileFilter?.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var extensions = row.Item.Asset switch
        {
            global::Lunet.AssetKind.Texture => new[] { ".png" },
            global::Lunet.AssetKind.Sound => new[] { ".wav", ".ogg", ".mp3" },
            global::Lunet.AssetKind.Music => new[] { ".ogg", ".mp3", ".wav" },
            global::Lunet.AssetKind.Data => new[] { ".json", ".txt", ".csv" },
            _ => filter,
        };
        var files = _projectFiles().Where(f => extensions.Length == 0 || extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (files.Length == 0)
        {
            Toast.MakeText(Context, "Nenhum arquivo compatível no projeto.", ToastLength.Short)?.Show();
            return;
        }
        new AlertDialog.Builder(Context)!.SetTitle(row.Item.Label)!.SetItems(files, (_, args) =>
        {
            var chosen = files[args.Which];
            // Recursos ficam em Content/: o jogo os carrega pelo caminho relativo a essa pasta.
            var value = chosen.StartsWith("Content/", StringComparison.Ordinal) ? chosen["Content/".Length..] : chosen;
            edit.Text = value;
            PostToGame(row, () => row.Item.Setter?.Invoke(value));
        })!.Show();
    }

    /// <summary>Roda a ação na thread do jogo (o jogo não é thread-safe).</summary>
    private void PostToGame(Row row, Action action)
    {
        var game = _game();
        if (game is null) return;
        game.Dispatcher.Post(() =>
        {
            try { action(); }
            catch (Exception ex) when (ex is InvalidCastException or ArgumentException or TargetInvocationException or FormatException or OverflowException)
            {
                game.Log.Error($"Inspector: não foi possível gravar {row.Item.Label}: {ex.Message}");
            }
        });
    }
}
