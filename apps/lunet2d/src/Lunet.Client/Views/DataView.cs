using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;
using DataColumn = Lunet.Studio.Data.DataColumn;
using DataColumnType = Lunet.Studio.Data.DataColumnType;
using DataTable = Lunet.Studio.Data.DataTable;

namespace Lunet.Client.Views;

/// <summary>
/// The game's data tables in the shell (A5, Data Studio): on a phone a table is a list of cards (one per record, as
/// Airtable and Notion do on mobile), and a record opens as a form with one editor per column type. Records can be
/// added, duplicated and removed; columns added and removed; the table's problems show at the top. Every change
/// writes the <c>Content/Data/*.json</c> and its <c>.schema.json</c> together.
/// </summary>
public sealed class DataView : UserControl
{
    public const string Folder = "Content/Data";
    private readonly LunetShell _shell;
    private readonly string _path;
    private readonly Action _close;
    private readonly StudioAssetWorkspace _storage;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly TextBlock _title = LunetUi.Title("");
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly Button _add;
    private int? _record;

    public DataView(LunetShell shell, string path, Action close, StudioAssetWorkspace? storage = null)
    {
        _shell = shell;
        _path = path;
        _close = close;
        // A game's folder, or the standalone Studio folder (Home → Studios) when given.
        _storage = storage ?? new StudioAssetWorkspace(shell.Workspace.Project!.Directory);
        var schema = SchemaPath(path);
        Table = DataTable.Parse(_storage.ReadText(path), File.Exists(Path.Combine(_storage.RootDirectory, schema)) ? _storage.ReadText(schema) : null);
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Back());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        var raw = LunetUi.IconButton(LunetIcons.Code, "Editar como texto", () => EditAsText());
        raw.Name = "DataAsText";
        raw.IsVisible = storage is null; // the Código editor belongs to a game
        DockPanel.SetDock(back, Dock.Left);
        DockPanel.SetDock(raw, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(raw);
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _title, _status } });
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = new Border { Child = _page, MaxWidth = 760, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, 96) } });
        // New record under the thumb (a FAB, as Run in the workspace), only on the list.
        _add = LunetUi.Button("Novo registro", ButtonKind.Primary, LunetIcons.Plus, () => AddRecord());
        _add.Name = "AddRecord";
        _add.MinHeight = 56;
        _add.CornerRadius = new CornerRadius(LunetTokens.Radius.L);
        _add.HorizontalAlignment = HorizontalAlignment.Right;
        _add.VerticalAlignment = VerticalAlignment.Bottom;
        _add.Margin = new Thickness(LunetTokens.Space.M);
        Content = new Grid { Children = { root, _add } };
        Refresh();
    }

    public DataTable Table { get; }
    public int? Record => _record;

    public static string SchemaPath(string dataPath) => dataPath[..^".json".Length] + ".schema.json";

    public static bool IsTable(string path) =>
        path.StartsWith(Folder + "/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        && !new[] { ".schema.json", ".tilemap.json", ".particles.json", ".song.json", ".sfx.json" }.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>A new table with an id and a name column and one record (as the previous Data Studio).</summary>
    public static string Create(LunetShell shell, string name) =>
        Create(new StudioAssetWorkspace(shell.Workspace.Project!.Directory), shell.Workspace.Files(), name);

    public static string Create(StudioAssetWorkspace storage, IReadOnlyCollection<string> files, string name)
    {
        var clean = new string(name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (clean.Length == 0) clean = "dados";
        var path = $"{Folder}/{clean}.json";
        for (var n = 2; files.Contains(path); n++) path = $"{Folder}/{clean}-{n}.json";
        var className = Lunet.Studio.ProjectPalette.Identifier(clean.TrimEnd('s'));
        var table = DataTable.Create(className,
            new DataColumn { Name = "id", Type = DataColumnType.Text, Key = true, Required = true },
            new DataColumn { Name = "nome", Type = DataColumnType.Text, Required = true });
        table.AddRow();
        storage.WriteTextPair(path, table.ToJson(), SchemaPath(path), table.SchemaToJson());
        return path;
    }

    /// <summary>Back: from a record to the list; from the list, leave.</summary>
    public bool Back()
    {
        if (_record is not null) { _record = null; Refresh(); return true; }
        _close();
        return true;
    }

    /// <summary>Leaves the Studio and opens the table's JSON in the Código editor.</summary>
    public void EditAsText()
    {
        _close();
        _shell.State.SelectTab(Lunet.Client.Shell.WorkspaceTab.Code);
        _shell.WorkspaceView.Code.Load(_path);
    }

    public void OpenRecord(int row) { _record = row; Refresh(); }
    public int AddRecord() { var row = Table.AddRow(); Save(); _record = row; Refresh(); return row; }
    public int DuplicateRecord(int row) { var copy = Table.DuplicateRow(row); Save(); _record = copy; Refresh(); return copy; }
    public void RemoveRecord(int row) { Table.RemoveRow(row); _record = null; Save(); Refresh(); }

    /// <summary>Sets a field from its text; null when it was accepted, else the reason (nothing changes).</summary>
    public string? SetField(int row, string column, string text)
    {
        var error = Table.Set(row, column, text);
        if (error is null) Save();
        return error;
    }

    public void AddColumn(string name, DataColumnType type)
    {
        Table.AddColumn(new DataColumn { Name = name.Trim(), Type = type });
        Save();
        Refresh();
    }

    public void RemoveColumn(string name) { Table.RemoveColumn(name); Save(); Refresh(); }

    private void Save()
    {
        try
        {
            _storage.WriteTextPair(_path, Table.ToJson(), SchemaPath(_path), Table.SchemaToJson());
            _status.Text = $"{Table.Rows.Count} registros · salvo";
            _status.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            _status.Text = "Não foi possível salvar: " + e.Message;
            _status.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
        }
    }

    private void Refresh()
    {
        _title.Text = _record is { } r && r < Table.Rows.Count ? Label(r) : Path.GetFileNameWithoutExtension(_path);
        if (string.IsNullOrEmpty(_status.Text)) _status.Text = $"{Table.Rows.Count} registros · {Table.Columns.Count} colunas";
        _page.Children.Clear();
        _add.IsVisible = _record is null;
        if (_record is { } row && row < Table.Rows.Count) RecordForm(row);
        else { _record = null; List(); }
    }

    private string Label(int row)
    {
        var key = Table.Columns.FirstOrDefault(c => c.Key);
        var name = Table.Columns.FirstOrDefault(c => !c.Key && c.Type == DataColumnType.Text);
        var k = key is null ? $"#{row + 1}" : Table.Display(row, key.Name);
        var n = name is null ? "" : Table.Display(row, name.Name);
        return string.IsNullOrWhiteSpace(n) ? k : $"{n}";
    }

    private void List()
    {
        var issues = Table.Validate();
        if (issues.Count > 0)
            _page.Children.Add(Notice(string.Join("\n", issues.Take(5).Select(i => $"Registro {i.Row + 1} · {i.Column}: {i.Message}")), LunetTokens.Color.Warning));
        var key = Table.Columns.FirstOrDefault(c => c.Key);
        var shown = Table.Columns.Where(c => !c.Key).Take(3).ToArray();
        for (var i = 0; i < Table.Rows.Count; i++)
        {
            var index = i;
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(LunetUi.Text(Label(i), LunetTokens.Type.Callout, weight: FontWeight.SemiBold));
            var details = string.Join(" · ", new[] { key is null ? null : Table.Display(i, key.Name) }.Concat(shown.Skip(1).Where(c => Table.Display(i, c.Name).Length > 0).Select(c => $"{c.Name}: {Table.Display(i, c.Name)}")).Where(s => !string.IsNullOrWhiteSpace(s)));
            if (details.Length > 0) text.Children.Add(LunetUi.Text(details, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
            var card = new Button
            {
                Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                MinHeight = 56, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S), Background = LunetTokens.Brush(LunetTokens.Color.Surface),
                BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.L), Name = "Record-" + i,
            };
            AutomationProperties.SetName(card, "Abrir " + Label(i));
            card.Click += (_, _) => OpenRecord(index);
            _page.Children.Add(card);
        }

        _page.Children.Add(Section("Colunas"));
        foreach (var column in Table.Columns)
        {
            var c = column;
            var row = new DockPanel { MinHeight = LunetTokens.MinTouch };
            if (!column.Key)
            {
                var remove = LunetUi.IconButton(LunetIcons.Trash, "Remover a coluna " + column.Name, () => RemoveColumn(c.Name), LunetTokens.Color.TextSecondary);
                DockPanel.SetDock(remove, Dock.Right);
                row.Children.Add(remove);
            }
            var label = LunetUi.Text($"{column.Name} · {TypeName(column.Type)}{(column.Key ? " · chave" : "")}{(column.Required ? " · obrigatória" : "")}", LunetTokens.Type.Body);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);
            _page.Children.Add(row);
        }
        var name = new TextBox { PlaceholderText = "Nome da coluna", MinHeight = LunetTokens.MinTouch, VerticalContentAlignment = VerticalAlignment.Center, Name = "ColumnName" };
        AutomationProperties.SetName(name, "Nome da nova coluna");
        var type = new ComboBox
        {
            ItemsSource = Enum.GetValues<DataColumnType>().Where(t => t is not (DataColumnType.Reference or DataColumnType.Json or DataColumnType.Choice)).Select(TypeName).ToArray(),
            SelectedIndex = 0, MinHeight = LunetTokens.MinTouch, Name = "ColumnType",
        };
        AutomationProperties.SetName(type, "Tipo da nova coluna");
        var addColumn = LunetUi.Button("Adicionar coluna", ButtonKind.Secondary, LunetIcons.Plus, () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) return;
            var chosen = Enum.GetValues<DataColumnType>().First(t => TypeName(t) == (string)type.SelectedItem!);
            try { AddColumn(name.Text, chosen); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { _status.Text = e.Message; _status.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger); }
        });
        _page.Children.Add(new StackPanel { Spacing = LunetTokens.Space.Xs, Children = { name, type, addColumn } });
    }

    private void RecordForm(int row)
    {
        foreach (var column in Table.Columns)
        {
            var c = column;
            _page.Children.Add(LunetUi.Text(c.Name + (c.Description.Length > 0 ? " · " + c.Description : ""), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary, FontWeight.SemiBold));
            var error = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.Danger);
            error.IsVisible = false;
            void Apply(string text)
            {
                var problem = SetField(row, c.Name, text);
                error.Text = problem ?? "";
                error.IsVisible = problem is not null;
            }
            Control editor;
            if (c.Type == DataColumnType.Boolean)
            {
                var toggle = new ToggleSwitch { IsChecked = Table.Display(row, c.Name) == "sim", OnContent = null, OffContent = null, Name = "Field-" + c.Name };
                toggle.IsCheckedChanged += (_, _) => Apply(toggle.IsChecked == true ? "true" : "false");
                editor = toggle;
            }
            else if (c.Type == DataColumnType.Choice && c.Choices.Count > 0)
            {
                var combo = new ComboBox { ItemsSource = c.Choices, SelectedItem = Table.Display(row, c.Name), MinHeight = LunetTokens.MinTouch, Name = "Field-" + c.Name };
                combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string s) Apply(s); };
                editor = combo;
            }
            else
            {
                var box = new TextBox
                {
                    Text = Table.Display(row, c.Name), MinHeight = LunetTokens.MinTouch, VerticalContentAlignment = VerticalAlignment.Center,
                    AcceptsReturn = c.Type == DataColumnType.Json, Name = "Field-" + c.Name,
                    FontFamily = c.Type is DataColumnType.Json ? LunetTokens.MonoFont : LunetTokens.UiFont,
                };
                if (c.Type is DataColumnType.Integer or DataColumnType.Number)
                    Avalonia.Input.TextInput.TextInputOptions.SetContentType(box, c.Type == DataColumnType.Integer ? Avalonia.Input.TextInput.TextInputContentType.Digits : Avalonia.Input.TextInput.TextInputContentType.Number);
                box.LostFocus += (_, _) => { if ((box.Text ?? "") != Table.Display(row, c.Name)) Apply(box.Text ?? ""); };
                box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter && !box.AcceptsReturn) { Apply(box.Text ?? ""); e.Handled = true; } };
                AutomationProperties.SetName(box, c.Name);
                editor = box;
            }
            _page.Children.Add(editor);
            _page.Children.Add(error);
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs, Margin = new Thickness(0, LunetTokens.Space.M, 0, 0) };
        var dup = LunetUi.Button("Duplicar", ButtonKind.Secondary, LunetIcons.Copy, () => DuplicateRecord(row));
        dup.Name = "DuplicateRecord";
        var remove = LunetUi.Button("Apagar registro", ButtonKind.Ghost, LunetIcons.Trash, () => RemoveRecord(row));
        remove.Name = "RemoveRecord";
        actions.Children.Add(dup);
        actions.Children.Add(remove);
        _page.Children.Add(actions);
    }

    private static string TypeName(DataColumnType type) => type switch
    {
        DataColumnType.Text => "Texto", DataColumnType.Integer => "Número inteiro", DataColumnType.Number => "Número",
        DataColumnType.Boolean => "Sim/não", DataColumnType.Choice => "Escolha", DataColumnType.Reference => "Referência", _ => "JSON",
    };

    private static TextBlock Section(string text) => LunetUi.Section(text);

    private static Control Notice(string text, string color) => LunetUi.Notice(text, color);
}
