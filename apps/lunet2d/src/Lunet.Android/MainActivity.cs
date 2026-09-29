using Android.App;
using Android.OS;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using System.Text.Json;

namespace Lunet.Android;

[Activity(Label = "Lunet", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private readonly ProjectStore _store = new();
    private EditText? _editor;
    private string? _currentProject;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _store.Initialize(FilesDir!.AbsolutePath);
        ShowProjects();
    }

    private LinearLayout Page(string title)
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(24, 24, 24, 24);
        root.AddView(new TextView(this) { Text = title, TextSize = 26 },
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        SetContentView(root);
        return root;
    }

    private Button Action(LinearLayout parent, string label, Action action)
    {
        var button = new Button(this) { Text = label };
        button.Click += (_, _) => action();
        parent.AddView(button);
        return button;
    }

    private void ShowProjects()
    {
        SaveCurrent();
        _currentProject = null;
        _editor = null;
        var root = Page("Lunet · Projetos");
        Action(root, "Novo projeto", AskForProjectName);

        var scroll = new ScrollView(this);
        var list = new LinearLayout(this) { Orientation = Orientation.Vertical };
        foreach (var project in _store.List())
        {
            var name = project;
            Action(list, name, () => OpenProject(name));
        }
        scroll.AddView(list);
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        root.AddView(new TextView(this)
        {
            Text = "Versão 0.0.1 · Editor inicial · Preview e compilação ainda indisponíveis",
            TextSize = 12
        });
    }

    private void AskForProjectName()
    {
        var input = new EditText(this) { Hint = "Nome do projeto" };
        input.SetSingleLine(true);
        new AlertDialog.Builder(this)
            .SetTitle("Novo projeto")
            .SetView(input)
            .SetNegativeButton("Cancelar", (_, _) => { })
            .SetPositiveButton("Criar", (_, _) =>
            {
                try
                {
                    var name = _store.Create(input.Text ?? "");
                    OpenProject(name);
                }
                catch (ArgumentException ex)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
                catch (IOException ex)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            }).Show();
    }

    private void OpenProject(string name)
    {
        SaveCurrent();
        _currentProject = name;
        var root = Page(name + " / Game.cs");
        Action(root, "← Projetos", ShowProjects);
        var editor = new EditText(this)
        {
            Text = _store.Read(name),
            TextSize = 14,
            Gravity = GravityFlags.Top | GravityFlags.Left
        };
        editor.SetTypeface(Android.Graphics.Typeface.Monospace, Android.Graphics.TypefaceStyle.Normal);
        editor.SetHorizontallyScrolling(true);
        editor.InputType = Android.Text.InputTypes.ClassText |
                           Android.Text.InputTypes.TextFlagMultiLine |
                           Android.Text.InputTypes.TextFlagNoSuggestions;
        root.AddView(editor, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
        _editor = editor;
        Action(root, "Salvar", () =>
        {
            SaveCurrent();
            Toast.MakeText(this, "Arquivo salvo", ToastLength.Short)?.Show();
        });
        root.AddView(new TextView(this)
        {
            Text = "O Run aparecerá quando a compilação e o Preview funcionarem no Android.",
            TextSize = 12
        });
    }

    private void SaveCurrent()
    {
        if (_currentProject is not null && _editor is not null)
        {
            try { _store.Save(_currentProject, _editor.Text ?? ""); }
            catch (IOException ex) { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
        }
    }

    protected override void OnPause()
    {
        SaveCurrent();
        base.OnPause();
    }

    public override void OnBackPressed()
    {
        if (_currentProject is not null) ShowProjects();
        else base.OnBackPressed();
    }
}

internal sealed class ProjectStore
{
    private string _root = "";

    public void Initialize(string privateFilesDirectory)
    {
        _root = Path.Combine(privateFilesDirectory, "Projects");
        Directory.CreateDirectory(_root);
    }

    public IEnumerable<string> List() => Directory.EnumerateDirectories(_root)
        .Select(Path.GetFileName).Where(name => name is not null).Cast<string>()
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

    public string Create(string rawName)
    {
        var name = rawName.Trim();
        if (name.Length is < 1 or > 60 || name is "." or ".." ||
            name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ArgumentException("Use 1–60 letras, números, _ ou -.");

        var directory = Path.Combine(_root, name);
        if (Directory.Exists(directory)) throw new ArgumentException("Esse projeto já existe.");
        Directory.CreateDirectory(directory);
        try
        {
            var manifest = new { name, gameId = Guid.NewGuid().ToString("D"), formatVersion = 1 };
            File.WriteAllText(Path.Combine(directory, "lunet.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(directory, "Game.cs"),
                "// Primeiro jogo Lunet. A execução de C# será habilitada após a validação do runtime.\n" +
                "public class Game\n{\n    // Seu código aqui.\n}\n");
        }
        catch
        {
            Directory.Delete(directory, recursive: true);
            throw;
        }
        return name;
    }

    public string Read(string name) => File.ReadAllText(Path.Combine(_root, name, "Game.cs"));

    public void Save(string name, string code)
    {
        var path = Path.Combine(_root, name, "Game.cs");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, code);
        File.Move(temporary, path, overwrite: true);
    }
}
