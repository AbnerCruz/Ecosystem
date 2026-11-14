using System.Text.RegularExpressions;
using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Lunet.Studio.Data;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>Data Studio entry points (LUNET-505). Tables live in <c>Content/Data/*.json</c> with an optional <c>.schema.json</c>.</summary>
public sealed partial class MainActivity
{
    private const string DataFolder = "Content/Data";

    private static bool IsDataTable(string path) =>
        path.StartsWith(DataFolder + "/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".particles.json", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".song.json", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".sfx.json", StringComparison.OrdinalIgnoreCase);

    private static string SchemaPath(string dataPath) => dataPath[..^".json".Length] + ".schema.json";

    private void ShowDataStudio()
    {
        if (_project is null) return;
        SaveCurrent();
        var tables = _project.ListFiles().Where(IsDataTable).ToArray();
        var items = new[] { "+ Nova tabela" }.Concat(tables).ToArray();
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Data Studio")!
            .SetItems(items, (_, e) =>
            {
                if (e.Which == 0) { NewDataTable(); return; }
                OpenDataTable(tables[e.Which - 1]);
            })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }

    private void NewDataTable()
    {
        var input = LunetUi.Input(this, "Nome da tabela (ex.: inimigos)", "");
        var frame = new FrameLayout(this);
        frame.SetPadding(Dp(20), Dp(8), Dp(20), Dp(4));
        frame.AddView(input);
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Nova tabela de dados")!.SetView(frame)!
            .SetPositiveButton("Criar", (_, _) =>
            {
                var name = ProjectPaletteIdentifier(input.Text ?? "").ToLowerInvariant();
                if (name.Length == 0) name = "dados";
                var className = Lunet.Studio.ProjectPalette.Identifier(name.TrimEnd('s'));
                var table = DataTable.Create(className,
                    new DataColumn { Name = "id", Type = DataColumnType.Text, Key = true, Required = true },
                    new DataColumn { Name = "nome", Type = DataColumnType.Text, Required = true });
                table.AddRow();
                var path = DataFolder + "/" + name + ".json";
                if (File.Exists(Path.Combine(_project!.Directory, path))) { Toast.MakeText(this, "Já existe " + path, ToastLength.Long)?.Show(); return; }
                SaveDataTable(path, table);
                OpenDataTable(path);
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void SaveDataTable(string path, DataTable table)
    {
        _project!.WriteText(path, table.ToJson());
        _project.WriteText(SchemaPath(path), table.SchemaToJson());
        RefreshExplorer();
    }

    /// <summary>Key values of every other table, by file name (for reference columns).</summary>
    private Dictionary<string, HashSet<string>> DataTableKeys(string except)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var path in _project!.ListFiles().Where(IsDataTable).Where(p => p != except))
        {
            try
            {
                var schemaPath = SchemaPath(path);
                var schema = File.Exists(Path.Combine(_project.Directory, schemaPath)) ? _project.ReadText(schemaPath) : null;
                result[Path.GetFileNameWithoutExtension(path)] = DataTable.Parse(_project.ReadText(path), schema).KeyValues();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
        }
        return result;
    }

    private void OpenDataTable(string path)
    {
        var project = _project!;
        DataTable table;
        try
        {
            var schemaPath = SchemaPath(path);
            var schema = File.Exists(Path.Combine(project.Directory, schemaPath)) ? project.ReadText(schemaPath) : null;
            table = DataTable.Parse(project.ReadText(path), schema, Lunet.Studio.ProjectPalette.Identifier(Path.GetFileNameWithoutExtension(path).TrimEnd('s')));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ProjectException)
        {
            Toast.MakeText(this, "Não foi possível abrir como tabela: " + ex.Message, ToastLength.Long)?.Show();
            return;
        }
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var view = new DataStudioView(this, table, path, t => SaveDataTable(path, t), t =>
        {
            var game = project.ReadText(project.Manifest.EntryPoint);
            var ns = Regex.Match(game, @"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : null;
            var className = Lunet.Studio.ProjectPalette.Identifier(t.Schema.ClassName);
            var target = "Data/" + className + ".cs";
            project.WriteText(target, t.ToCSharp(ns));
            RefreshExplorer();
            new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle("Classe gerada")!
                .SetMessage($"{target}\n\nNo LoadContent:\nvar lista = Content.LoadJson<List<{className}>>(\"{path["Content/".Length..]}\");\n\n" +
                            "Gere de novo quando mudar as colunas.")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
        }, () => DataTableKeys(path), () => dialog.Dismiss());
        dialog.SetContentView(view);
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }
}
