using Android.App;
using Android.Content;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Studio.Data;

namespace Lunet.Android;

/// <summary>
/// Data Studio (LUNET-505): a game data table on the phone — tap a cell to edit (keyboard and pickers by column type),
/// tap a column header to change its type and rules, hold a row for row actions; problems are highlighted and listed.
/// </summary>
internal sealed class DataStudioView : LinearLayout
{
    private readonly DataTable _table;
    private readonly Action<DataTable> _save;
    private readonly Action<DataTable> _generateCode;
    private readonly Action _close;
    private readonly Func<IReadOnlyDictionary<string, HashSet<string>>> _otherTables;
    private readonly TableLayout _grid;
    private readonly TextView _status;
    private IReadOnlyList<DataIssue> _issues = [];

    public DataStudioView(Context context, DataTable table, string name, Action<DataTable> save, Action<DataTable> generateCode,
        Func<IReadOnlyDictionary<string, HashSet<string>>> otherTables, Action close) : base(context)
    {
        _table = table; _save = save; _generateCode = generateCode; _otherTables = otherTables; _close = close;
        Orientation = Orientation.Vertical;
        SetBackgroundColor(LunetUi.Background);

        var bar = LunetUi.Row(context, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.AddView(LunetUi.Icon(context, "‹", "Voltar", () => { _save(_table); _close(); }), new LayoutParams(Dp(44), Dp(44)));
        var titles = LunetUi.Column(context, 0);
        titles.AddView(LunetUi.Label(context, name, LunetUi.TextLabel, LunetUi.Text, bold: true));
        _status = LunetUi.Label(context, "", LunetUi.TextCaption, LunetUi.TextMuted);
        titles.AddView(_status);
        bar.AddView(titles, LunetUi.Weight());
        bar.AddView(LunetUi.Button(context, "Salvar", () => { _save(_table); Refresh(); }, ButtonStyle.Primary), new LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        AddView(bar);

        var tools = LunetUi.Row(context, 6);
        tools.SetPadding(Dp(8), Dp(2), Dp(8), Dp(6));
        void Tool(string label, Action run) => tools.AddView(LunetUi.Button(context, label, run, ButtonStyle.Secondary));
        Tool("＋ Linha", () => { _table.AddRow(); Changed(); });
        Tool("＋ Coluna", () => EditColumn(null));
        Tool("Ordenar", SortMenu);
        Tool("Problemas", ShowIssues);
        Tool("Classe C#", () => { _save(_table); _generateCode(_table); });
        var toolScroll = new HorizontalScrollView(context) { HorizontalScrollBarEnabled = false };
        toolScroll.AddView(tools);
        AddView(toolScroll);

        _grid = new TableLayout(context);
        _grid.SetPadding(Dp(8), 0, Dp(8), Dp(16));
        var horizontal = new HorizontalScrollView(context);
        horizontal.AddView(_grid);
        var vertical = new ScrollView(context) { FillViewport = true };
        vertical.AddView(horizontal);
        AddView(vertical, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        Refresh();
    }

    private int Dp(int v) => LunetUi.Dp(Context!, v);

    private void Changed() { Refresh(); }

    private void Refresh()
    {
        _issues = _table.Validate(_otherTables());
        _status.Text = $"{_table.Rows.Count} linhas · {_table.Columns.Count} colunas · " +
                       (_issues.Count == 0 ? "sem problemas" : $"{_issues.Count} problema(s)");
        _status.SetTextColor(_issues.Count == 0 ? LunetUi.TextMuted : LunetUi.Danger);
        _grid.RemoveAllViews();
        var header = new TableRow(Context);
        header.AddView(Cell("#", header: true));
        foreach (var column in _table.Columns)
        {
            var c = column;
            var label = Cell(c.Name + (c.Key ? " 🔑" : "") + "\n" + TypeName(c), header: true);
            label.Click += (_, _) => EditColumn(c);
            header.AddView(label);
        }
        _grid.AddView(header);
        for (int r = 0; r < _table.Rows.Count; r++)
        {
            int row = r;
            var line = new TableRow(Context);
            var number = Cell((r + 1).ToString(), header: true);
            number.LongClick += (_, e) => { RowMenu(row); e.Handled = true; };
            number.Click += (_, _) => RowMenu(row);
            line.AddView(number);
            foreach (var column in _table.Columns)
            {
                var c = column;
                var cell = Cell(_table.Display(row, c.Name), header: false, problem: _issues.Any(i => i.Row == row && i.Column == c.Name));
                cell.Click += (_, _) => EditCell(row, c);
                line.AddView(cell);
            }
            _grid.AddView(line);
        }
    }

    private static string TypeName(DataColumn c) => c.Type switch
    {
        DataColumnType.Integer => "inteiro", DataColumnType.Number => "número", DataColumnType.Boolean => "sim/não",
        DataColumnType.Choice => "opções", DataColumnType.Reference => "→ " + c.ReferenceTable, DataColumnType.Json => "JSON", _ => "texto",
    };

    private TextView Cell(string text, bool header, bool problem = false)
    {
        var view = LunetUi.Label(Context!, text.Length > 40 ? text[..40] + "…" : text, header ? LunetUi.TextCaption : LunetUi.TextBody,
            header ? LunetUi.TextMuted : LunetUi.Text, bold: header);
        view.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8));
        view.SetMinWidth(Dp(header && text.Length < 4 ? 36 : 96));
        view.Clickable = true;
        view.Background = LunetUi.Pressable(Context!, problem ? LunetUi.AccentSoft : header ? LunetUi.SurfaceRaised : LunetUi.Surface, 0,
            problem ? LunetUi.Danger : LunetUi.Border);
        return view;
    }

    private void EditCell(int row, DataColumn column)
    {
        if (column.Type == DataColumnType.Boolean)
        {
            _table.Set(row, column.Name, _table.Display(row, column.Name) == "sim" ? "não" : "sim");
            Changed();
            return;
        }
        if (column.Type == DataColumnType.Choice && column.Choices.Count > 0 || column.Type == DataColumnType.Reference)
        {
            var options = column.Type == DataColumnType.Choice ? column.Choices.ToArray()
                : (_otherTables().GetValueOrDefault(column.ReferenceTable ?? "") ?? []).Order(StringComparer.Ordinal).ToArray();
            if (options.Length > 0)
            {
                new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                    .SetTitle(column.Name)!
                    .SetItems(options, (_, e) => { _table.Set(row, column.Name, options[e.Which]); Changed(); })!
                    .SetNeutralButton("Limpar", (_, _) => { _table.Set(row, column.Name, ""); Changed(); })!
                    .Show();
                return;
            }
        }
        var input = LunetUi.Input(Context!, TypeName(column), _table.Display(row, column.Name));
        input.InputType = column.Type switch
        {
            DataColumnType.Integer => InputTypes.ClassNumber | InputTypes.NumberFlagSigned,
            DataColumnType.Number => InputTypes.ClassNumber | InputTypes.NumberFlagSigned | InputTypes.NumberFlagDecimal,
            DataColumnType.Json => InputTypes.ClassText | InputTypes.TextFlagMultiLine,
            _ => InputTypes.ClassText | InputTypes.TextFlagCapSentences,
        };
        var frame = new FrameLayout(Context);
        frame.SetPadding(Dp(20), Dp(8), Dp(20), Dp(4));
        frame.AddView(input);
        var dialog = new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle($"{column.Name} · linha {row + 1}")!
            .SetView(frame)!
            .SetPositiveButton("OK", (_, _) => { })!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .Create();
        dialog.Show();
        dialog.GetButton((int)DialogButtonType.Positive)!.Click += (_, _) =>
        {
            if (_table.Set(row, column.Name, input.Text ?? "") is { } problem && column.Type is DataColumnType.Integer or DataColumnType.Number or DataColumnType.Boolean or DataColumnType.Choice)
            {
                input.Error = problem; // keep the dialog open: the value was not accepted
                return;
            }
            dialog.Dismiss();
            Changed();
        };
    }

    private void RowMenu(int row)
    {
        var items = new (string Label, Action Run)[]
        {
            ("Duplicar", () => { _table.DuplicateRow(row); Changed(); }),
            ("Mover para cima", () => { if (row > 0) { _table.MoveRow(row, row - 1); Changed(); } }),
            ("Mover para baixo", () => { if (row < _table.Rows.Count - 1) { _table.MoveRow(row, row + 1); Changed(); } }),
            ("Excluir", () => { _table.RemoveRow(row); Changed(); }),
        };
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle($"Linha {row + 1}")!.SetItems(items.Select(i => i.Label).ToArray(), (_, e) => items[e.Which].Run())!.Show();
    }

    private void SortMenu()
    {
        var names = _table.Columns.Select(c => c.Name).ToArray();
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Ordenar por")!
            .SetItems(names.SelectMany(n => new[] { n + " ↑", n + " ↓" }).ToArray(), (_, e) => { _table.SortBy(names[e.Which / 2], e.Which % 2 == 1); Changed(); })!
            .Show();
    }

    private void ShowIssues()
    {
        if (_issues.Count == 0) { Toast.MakeText(Context, "Nenhum problema nos dados.", ToastLength.Short)?.Show(); return; }
        new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Problemas nos dados")!
            .SetItems(_issues.Select(i => (i.Row >= 0 ? $"Linha {i.Row + 1}: " : "") + i.Message).ToArray(), (_, e) =>
            {
                var issue = _issues[e.Which];
                if (issue.Row >= 0) EditCell(issue.Row, _table.Column(issue.Column));
            })!.Show();
    }

    /// <summary>New column (null) or edit: name, type, key, required, range, choices, reference, description.</summary>
    private void EditColumn(DataColumn? existing)
    {
        var form = LunetUi.Column(Context!, 6);
        form.SetPadding(Dp(20), Dp(8), Dp(20), Dp(4));
        var name = LunetUi.Input(Context!, "Nome (como no JSON)", existing?.Name ?? "");
        form.AddView(name);
        var types = Enum.GetValues<DataColumnType>();
        var selected = existing?.Type ?? DataColumnType.Text;
        var typeRow = LunetUi.Row(Context!, 6);
        var typeChips = new List<(DataColumnType Type, TextView Chip)>();
        void PaintTypes() { foreach (var (t, chip) in typeChips) { chip.SetTextColor(t == selected ? LunetUi.AccentText : LunetUi.Text); chip.Background = LunetUi.Pressable(Context!, t == selected ? LunetUi.AccentSoft : LunetUi.SurfaceRaised, 99, t == selected ? LunetUi.Accent : LunetUi.Border); } }
        foreach (var t in types)
        {
            var chip = LunetUi.Chip(Context!, TypeName(new DataColumn { Name = "", Type = t, ReferenceTable = "tabela" }), () => { selected = t; PaintTypes(); });
            typeChips.Add((t, chip));
            typeRow.AddView(chip);
        }
        PaintTypes();
        var typeScroll = new HorizontalScrollView(Context) { HorizontalScrollBarEnabled = false };
        typeScroll.AddView(typeRow);
        form.AddView(typeScroll);
        var key = new CheckBox(Context) { Text = "Identificador único (chave)", Checked = existing?.Key ?? false };
        var required = new CheckBox(Context) { Text = "Obrigatório", Checked = existing?.Required ?? false };
        form.AddView(key); form.AddView(required);
        var min = LunetUi.Input(Context!, "Mínimo (números)", existing?.Min?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
        var max = LunetUi.Input(Context!, "Máximo (números)", existing?.Max?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "");
        var choices = LunetUi.Input(Context!, "Opções separadas por vírgula", string.Join(", ", existing?.Choices ?? []));
        var reference = LunetUi.Input(Context!, "Tabela referenciada (ex.: itens)", existing?.ReferenceTable ?? "");
        var description = LunetUi.Input(Context!, "Descrição (vai para o código)", existing?.Description ?? "");
        foreach (var v in new View[] { min, max, choices, reference, description }) form.AddView(v);
        var scroll = new ScrollView(Context);
        scroll.AddView(form);
        var builder = new AlertDialog.Builder(Context, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(existing is null ? "Nova coluna" : "Coluna " + existing.Name)!
            .SetView(scroll)!
            .SetPositiveButton("OK", (_, _) => { })!
            .SetNegativeButton("Cancelar", (_, _) => { })!;
        if (existing is not null) builder.SetNeutralButton("Excluir coluna", (_, _) => { _table.RemoveColumn(existing.Name); Changed(); });
        var dialog = builder.Create();
        dialog.Show();
        dialog.GetButton((int)DialogButtonType.Positive)!.Click += (_, _) =>
        {
            static double? Number(string? text) => double.TryParse((text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
            var column = new DataColumn
            {
                Name = (name.Text ?? "").Trim(), Type = selected, Key = key.Checked, Required = required.Checked || key.Checked,
                Min = Number(min.Text), Max = Number(max.Text),
                Choices = (choices.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                ReferenceTable = string.IsNullOrWhiteSpace(reference.Text) ? null : reference.Text.Trim(),
                Description = (description.Text ?? "").Trim(),
            };
            try
            {
                if (existing is null) _table.AddColumn(column); else _table.ReplaceColumn(existing.Name, column);
            }
            catch (ArgumentException ex) { name.Error = ex.Message; return; }
            dialog.Dismiss();
            Changed();
        };
    }
}
