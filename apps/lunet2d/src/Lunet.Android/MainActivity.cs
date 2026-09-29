using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware;
using Android.Opengl;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Android.Gles;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Android.Editor;
using Lunet.Core;
using Lunet.Editor;
using Lunet.Input;
using Lunet.Storage;
using AndroidColor = Android.Graphics.Color;
using AndroidUri = Android.Net.Uri;

namespace Lunet.Android;

[Activity(Label = "Lunet", MainLauncher = true, Exported = true,
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.ScreenSize |
                           global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenLayout,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public sealed class MainActivity : Activity, ISensorEventListener
{
    private const int ExportRequestCode = 4101;
    private const int ImportRequestCode = 4102;
    private const int MaxConsoleLines = 300;
    private static readonly Lazy<GameCompiler> SharedCompiler = new(() =>
        new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly)));

    private ProjectStore _store = null!;
    private LunetProject? _project;
    private string? _openFile;
    private CodeEditText? _editor;
    private EditorAssistant? _assistant;
    private AutosaveJournal? _journal;
    private readonly EditorSession _session = new();
    private LinearLayout? _drawer;
    private LinearLayout? _drawerList;
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private LinearLayout? _chips;
    private HorizontalScrollView? _chipScroll;
    private FindOptions _findOptions;
    private string _findQuery = "";
    private string _replaceText = "";
    private TextView? _status;
    private LinearLayout? _panelList;
    private Button? _problemsTab;
    private bool _showConsole;
    private IReadOnlyList<LunetDiagnostic> _problems = [];
    private readonly List<string> _console = [];
    private int _compileCounter;
    private string? _pendingExport;

    private GLSurfaceView? _glView;
    private PreviewRenderer? _renderer;
    private TextView? _previewConsole;
    private bool _previewPaused;
    private SensorManager? _sensors;
    private GamepadButtons _padButtons;
    private System.Numerics.Vector2 _padLeft, _padRight;
    private float _padLeftTrigger, _padRightTrigger;
    private bool _padSeen;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _store = new ProjectStore(System.IO.Path.Combine(FilesDir!.AbsolutePath, "Projects"));
        _pendingExport = savedInstanceState?.GetString("pendingExport");
        ShowProjects();
    }

    // ---------- UI helpers ----------

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);

    private Button MakeButton(string label, Action action)
    {
        var button = new Button(this) { Text = label };
        button.SetAllCaps(false);
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>Botão compacto para barras: divide a largura com os vizinhos em vez de sair da tela.</summary>
    private Button MakeBarButton(string label, Action action, float weight = 1f)
    {
        var button = MakeButton(label, action);
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.SetPadding(Dp(4), 0, Dp(4), 0);
        button.LayoutParameters = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, weight);
        return button;
    }

    private LinearLayout Vertical() => new(this) { Orientation = Orientation.Vertical };

    private static LinearLayout.LayoutParams Fill(float weight = 0) =>
        new(ViewGroup.LayoutParams.MatchParent, weight > 0 ? 0 : ViewGroup.LayoutParams.WrapContent, weight);

    // ---------- Projects ----------

    private void ShowProjects()
    {
        SaveCurrent();
        DisposePreview();
        _session.Unload();
        _project = null;
        _openFile = null;
        _editor = null;

        var root = Vertical();
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(16));
        root.AddView(new TextView(this) { Text = "Lunet", TextSize = 28 });
        root.AddView(new TextView(this) { Text = "Projetos" , TextSize = 16 });
        root.AddView(MakeButton("Novo projeto", AskForProjectName));

        var list = Vertical();
        foreach (var name in _store.List())
        {
            var captured = name;
            var row = MakeButton(captured, () => OpenProject(captured));
            row.LongClick += (_, e) =>
            {
                ShowProjectMenu(captured);
                e.Handled = true;
            };
            list.AddView(row);
        }
        if (list.ChildCount == 0)
            list.AddView(new TextView(this) { Text = "Nenhum projeto ainda. Crie o primeiro." });
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        root.AddView(scroll, Fill(1));

        root.AddView(new TextView(this)
        {
            Text = $"Versão {BuildInfo.Version} · Toque longo num projeto: exportar ZIP ou excluir. Os projetos ficam no armazenamento do app; exporte em ZIP para guardar.",
            TextSize = 12,
        });
        SetContentView(root);
    }

    private void ShowProjectMenu(string name)
    {
        var items = new[] { "Exportar ZIP", "Excluir projeto" };
        new AlertDialog.Builder(this)!.SetTitle(name)!.SetItems(items, (_, args) =>
        {
            if (args.Which == 0) ExportProject(name);
            else ConfirmDeleteProject(name);
        })!.Show();
    }

    private void ConfirmDeleteProject(string name)
    {
        new AlertDialog.Builder(this)!
            .SetTitle("Excluir projeto")!
            .SetMessage($"Excluir \"{name}\" com todos os arquivos? Isso não pode ser desfeito. Exporte um ZIP antes se tiver dúvida.")!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Excluir", (_, _) =>
            {
                try
                {
                    _store.Delete(name);
                    ShowProjects();
                }
                catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    private void AskForProjectName()
    {
        var input = new EditText(this) { Hint = "Nome do projeto" };
        input.SetSingleLine(true);
        var template = new[] { ProjectTemplate.Blank };
        var form = Vertical();
        form.SetPadding(Dp(16), Dp(8), Dp(16), 0);
        form.AddView(input);
        // RadioButtons criados por código precisam de id para o RadioGroup desmarcar o anterior.
        var group = new RadioGroup(this);
        var options = new (string Text, ProjectTemplate Template)[]
        {
            ("Em branco (bola que segue o toque)", ProjectTemplate.Blank),
            ("Demo: Coletor de moedas (texto, gestos, som, salvamento)", ProjectTemplate.CoinCatcher),
            ("Laboratório: testa música, sensores, controle, vibração, gestos e gráficos", ProjectTemplate.Lab),
        };
        var byId = new Dictionary<int, ProjectTemplate>();
        foreach (var (text, choice) in options)
        {
            var button = new RadioButton(this) { Id = View.GenerateViewId(), Text = text };
            byId[button.Id] = choice;
            group.AddView(button);
            if (choice == ProjectTemplate.Blank) group.Check(button.Id);
        }
        group.CheckedChange += (_, e) =>
        {
            if (byId.TryGetValue(e.CheckedId, out var chosen)) template[0] = chosen;
        };
        form.AddView(group);
        new AlertDialog.Builder(this)!
            .SetTitle("Novo projeto")!
            .SetView(form)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try
                {
                    var project = _store.Create(input.Text ?? "", template[0]);
                    OpenProject(project.Name);
                }
                catch (Exception ex) when (ex is ProjectException or IOException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    // ---------- Workspace ----------

    private void OpenProject(string name)
    {
        try
        {
            _project = _store.Open(name);
        }
        catch (ProjectException ex)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            return;
        }
        _problems = [];
        _console.Clear();
        _openFile = null;
        _assistant = new EditorAssistant();
        _ = _assistant.LoadProjectAsync(_project.LoadSources().Select(f => (f.Path, f.Text)).ToList());
        _journal = new AutosaveJournal(_project);
        ShowWorkspace();
        OpenFile(_project.Manifest.EntryPoint);
        OfferRecovery();
    }

    /// <summary>Após um encerramento inesperado, oferece as alterações que estavam só no buffer de trabalho.</summary>
    private void OfferRecovery()
    {
        var recoveries = _journal?.FindRecoveries() ?? [];
        if (recoveries.Count == 0) return;
        var names = string.Join("\n", recoveries.Select(r => "• " + r.Path));
        new AlertDialog.Builder(this)!
            .SetTitle("Alterações não salvas")!
            .SetMessage("O Lunet foi encerrado antes de salvar:\n\n" + names + "\n\nRecuperar essas alterações?")!
            .SetCancelable(false)!
            .SetPositiveButton("Recuperar", (_, _) =>
            {
                foreach (var recovery in recoveries) _journal!.Restore(recovery);
                if (_openFile is not null) { _session.Unload(); var file = _openFile; _openFile = null; OpenFile(file); }
                Toast.MakeText(this, "Alterações recuperadas", ToastLength.Short)?.Show();
            })!
            .SetNegativeButton("Descartar", (_, _) =>
            {
                foreach (var recovery in recoveries) _journal!.Discard(recovery.Path);
            })!.Show();
    }

    private void ShowWorkspace()
    {
        DisposePreview();
        _session.Unload(); // o editor abaixo é novo e vazio: nada pode ser gravado a partir dele até um arquivo ser carregado
        var project = _project!;
        var root = Vertical();

        var bar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        bar.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
        bar.AddView(MakeBarButton("←", () => ShowProjects()));
        bar.AddView(MakeBarButton("☰", ToggleExplorer));
        bar.AddView(MakeBarButton("▶ Run", Run, 2f));
        bar.AddView(MakeBarButton("↶", () => _editor?.Undo()));
        bar.AddView(MakeBarButton("↷", () => _editor?.Redo()));
        bar.AddView(MakeBarButton("⋯", ShowMenu));
        root.AddView(bar);

        _status = new TextView(this) { TextSize = 12 };
        _status.SetPadding(Dp(8), 0, Dp(8), 0);
        root.AddView(_status);

        _editor = new CodeEditText(this);
        _editor.Settled += OnEditorSettled;
        root.AddView(_editor, Fill(3));

        _chips = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _chipScroll = new HorizontalScrollView(this) { Visibility = ViewStates.Gone, HorizontalScrollBarEnabled = false };
        _chipScroll.AddView(_chips);
        root.AddView(_chipScroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        var tabs = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _problemsTab = MakeBarButton("Problemas", () => SetPanel(false));
        tabs.AddView(_problemsTab);
        tabs.AddView(MakeBarButton("Console", () => SetPanel(true)));
        root.AddView(tabs);

        _panelList = Vertical();
        var panelScroll = new ScrollView(this);
        panelScroll.AddView(_panelList);
        root.AddView(panelScroll, Fill(1));

        var frame = new FrameLayout(this);
        frame.AddView(root, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        BuildDrawer(frame);
        SetContentView(frame);
        SetPanel(_showConsole);
        _status.Text = $"{project.Name}";
    }

    // ---------- Explorer ----------

    private void BuildDrawer(FrameLayout frame)
    {
        _drawer = Vertical();
        _drawer.SetBackgroundColor(AndroidColor.Rgb(30, 33, 40));
        _drawer.Visibility = ViewStates.Gone;
        _drawer.Clickable = true; // não deixa toques passarem para o editor

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        header.SetPadding(Dp(8), Dp(8), Dp(8), Dp(4));
        header.AddView(new TextView(this) { Text = "Explorer", TextSize = 16 }, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(MakeBarButton("+ arq.", () => AskForNewEntry("", isDirectory: false), 1.4f));
        header.AddView(MakeBarButton("+ pasta", () => AskForNewEntry("", isDirectory: true), 1.4f));
        header.AddView(MakeBarButton("✕", ToggleExplorer, 0.8f));
        _drawer.AddView(header);

        _drawerList = Vertical();
        var scroll = new ScrollView(this);
        scroll.AddView(_drawerList);
        _drawer.AddView(scroll, Fill(1));
        frame.AddView(_drawer, new FrameLayout.LayoutParams(Dp(300), ViewGroup.LayoutParams.MatchParent, GravityFlags.Left));
    }

    private void ToggleExplorer()
    {
        if (_drawer is null) return;
        var show = _drawer.Visibility != ViewStates.Visible;
        _drawer.Visibility = show ? ViewStates.Visible : ViewStates.Gone;
        if (show) RefreshExplorer();
    }

    private void RefreshExplorer()
    {
        if (_drawerList is null || _project is null) return;
        _drawerList.RemoveAllViews();
        foreach (var entry in _project.ListTree())
        {
            if (HasCollapsedAncestor(entry.Path)) continue;
            var captured = entry;
            var label = (entry.IsDirectory ? (_collapsed.Contains(entry.Path) ? "▸ " : "▾ ") : "") + entry.Name;
            var row = new TextView(this) { Text = label, TextSize = 15 };
            row.SetPadding(Dp(12 + entry.Depth * 16), Dp(10), Dp(8), Dp(10));
            if (entry.Path == _openFile) row.SetBackgroundColor(AndroidColor.Rgb(50, 56, 70));
            row.SetTextColor(entry.IsDirectory ? AndroidColor.Rgb(170, 200, 255) : IsTextFile(entry.Path) ? AndroidColor.Rgb(230, 230, 230) : AndroidColor.Rgb(140, 140, 140));
            row.Click += (_, _) => OnExplorerTap(captured);
            row.LongClick += (_, _) => ShowEntryMenu(captured);
            _drawerList.AddView(row);
        }
    }

    private bool HasCollapsedAncestor(string path)
    {
        var parent = path;
        while (true)
        {
            var slash = parent.LastIndexOf('/');
            if (slash < 0) return false;
            parent = parent[..slash];
            if (_collapsed.Contains(parent)) return true;
        }
    }

    private void OnExplorerTap(ExplorerEntry entry)
    {
        if (entry.IsDirectory)
        {
            if (!_collapsed.Remove(entry.Path)) _collapsed.Add(entry.Path);
            RefreshExplorer();
            return;
        }
        if (!IsTextFile(entry.Path))
        {
            Toast.MakeText(this, "Arquivo binário: use-o pelo código (ex.: Content.LoadTexture).", ToastLength.Short)?.Show();
            return;
        }
        OpenFile(entry.Path);
        ToggleExplorer();
    }

    private void ShowEntryMenu(ExplorerEntry entry)
    {
        var items = entry.IsDirectory
            ? new[] { "Novo arquivo aqui", "Nova pasta aqui", "Renomear", "Excluir" }
            : new[] { "Renomear", "Excluir" };
        new AlertDialog.Builder(this)!.SetTitle(entry.Path)!.SetItems(items, (_, args) =>
        {
            switch (items[args.Which])
            {
                case "Novo arquivo aqui": AskForNewEntry(entry.Path + "/", isDirectory: false); break;
                case "Nova pasta aqui": AskForNewEntry(entry.Path + "/", isDirectory: true); break;
                case "Renomear": AskToRename(entry); break;
                case "Excluir": AskToDelete(entry); break;
            }
        })!.Show();
    }

    private static bool IsTextFile(string path) =>
        new[] { ".cs", ".json", ".md", ".txt", ".xml" }.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private void AskForNewEntry(string prefix, bool isDirectory)
    {
        var input = new EditText(this) { Hint = isDirectory ? "Nome da pasta" : "Player.cs" };
        input.SetSingleLine(true);
        new AlertDialog.Builder(this)!
            .SetTitle(isDirectory ? "Nova pasta" : "Novo arquivo")!
            .SetView(input)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try
                {
                    var path = prefix + (input.Text ?? "").Trim();
                    if (isDirectory) _project!.CreateDirectory(path);
                    else
                    {
                        _project!.CreateFile(path, path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "using Lunet;\n\n" : "");
                        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) _assistant?.SetFile(path, "using Lunet;\n\n");
                        OpenFile(path);
                    }
                    RefreshExplorer();
                }
                catch (Exception ex) when (ex is ProjectException or IOException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    private void AskToRename(ExplorerEntry entry)
    {
        var input = new EditText(this) { Text = entry.Name };
        input.SetSingleLine(true);
        new AlertDialog.Builder(this)!
            .SetTitle("Renomear")!
            .SetView(input)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Renomear", (_, _) =>
            {
                try
                {
                    SaveCurrent();
                    var slash = entry.Path.LastIndexOf('/');
                    var target = (slash < 0 ? "" : entry.Path[..(slash + 1)]) + (input.Text ?? "").Trim();
                    var before = _project!.ListFiles();
                    _project.Rename(entry.Path, target);
                    AfterPathChanged(entry.Path, target, before);
                    RefreshExplorer();
                }
                catch (Exception ex) when (ex is ProjectException or IOException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    private void AskToDelete(ExplorerEntry entry)
    {
        new AlertDialog.Builder(this)!
            .SetTitle("Excluir")!
            .SetMessage($"Excluir \"{entry.Path}\"{(entry.IsDirectory ? " e tudo dentro dela" : "")}? Isso não pode ser desfeito (exporte um ZIP antes se tiver dúvida).")!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Excluir", (_, _) =>
            {
                try
                {
                    SaveCurrent();
                    var before = _project!.ListFiles();
                    _project.Delete(entry.Path);
                    AfterPathChanged(entry.Path, null, before);
                    RefreshExplorer();
                }
                catch (Exception ex) when (ex is ProjectException or IOException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    /// <summary>Mantém editor e análise coerentes depois de renomear (<paramref name="to"/>) ou apagar (nulo) um caminho.</summary>
    private void AfterPathChanged(string from, string? to, IReadOnlyList<string> filesBefore)
    {
        foreach (var known in filesBefore.Where(f => f.StartsWith(from + "/") || f == from))
            _assistant?.RemoveFile(known);
        if (to is not null)
            foreach (var file in _project.ListFiles().Where(f => (f == to || f.StartsWith(to + "/")) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
                _assistant?.SetFile(file, _project.ReadText(file));

        var open = _openFile;
        if (open is null || !(open == from || open.StartsWith(from + "/"))) return;
        _journal?.Discard(open);
        _session.Unload(); // o arquivo antigo não existe mais: não pode ser regravado
        _openFile = null;
        if (to is not null) OpenFile(to + open[from.Length..]);
        else OpenFile(_project.Manifest.EntryPoint);
    }

    private void OpenFile(string path)
    {
        if (_editor is null) return;
        SaveCurrent();
        try
        {
            _openFile = path;
            HideChips();
            var text = _project!.ReadText(path);
            _editor.LoadText(text);
            _session.Load(path, text);
            _status!.Text = $"{_project.Name} / {path}";
        }
        catch (Exception ex) when (ex is ProjectException or IOException)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void SaveCurrent()
    {
        if (_project is null || _editor is null) return;
        try
        {
            _session.TrySave(_project, _editor.Text ?? "", _journal);
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void ShowMenu()
    {
        var items = new[] { "Salvar", "Referência rápida", "Exportar projeto (ZIP)", "Importar imagem PNG", "Localizar e substituir", "Ir para definição", "Dica do símbolo", "Referências do símbolo" };
        new AlertDialog.Builder(this)!.SetItems(items, (_, args) =>
        {
            switch (args.Which)
            {
                case 0:
                    SaveCurrent();
                    Toast.MakeText(this, "Salvo", ToastLength.Short)?.Show();
                    break;
                case 1:
                    ShowReference();
                    break;
                case 2:
                    ExportProject();
                    break;
                case 3:
                    ImportImage();
                    break;
                case 4:
                    ShowFind();
                    break;
                case 5:
                    GoToDefinition();
                    break;
                case 6:
                    ShowHover();
                    break;
                case 7:
                    ShowReferences();
                    break;
            }
        })!.Show();
    }

    private void ShowReference()
    {
        var text = new TextView(this) { Text = QuickReference.Text, TextSize = 12 };
        text.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        text.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        var scroll = new ScrollView(this);
        scroll.AddView(text);
        new AlertDialog.Builder(this)!.SetTitle("Documentação")!.SetView(scroll)!.SetPositiveButton("Fechar", (_, _) => { })!.Show();
    }

    private void SetPanel(bool console)
    {
        _showConsole = console;
        if (_panelList is null) return;
        _panelList.RemoveAllViews();
        if (console)
        {
            foreach (var line in _console)
                _panelList.AddView(new TextView(this) { Text = line, TextSize = 12 });
            if (_console.Count == 0) _panelList.AddView(new TextView(this) { Text = "Sem mensagens do jogo.", TextSize = 12 });
        }
        else
        {
            foreach (var problem in _problems)
            {
                var captured = problem;
                var view = new TextView(this) { Text = problem.ToString(), TextSize = 12 };
                view.SetTextColor(problem.Severity == DiagnosticSeverity.Error ? AndroidColor.Rgb(255, 120, 120) : AndroidColor.Rgb(255, 210, 120));
                view.SetPadding(Dp(8), Dp(4), Dp(8), Dp(4));
                view.Click += (_, _) => GoTo(captured);
                _panelList.AddView(view);
            }
            if (_problems.Count == 0) _panelList.AddView(new TextView(this) { Text = "Nenhum problema.", TextSize = 12 });
        }
        if (_problemsTab is not null)
            _problemsTab.Text = _problems.Count == 0 ? "Problemas" : $"Problemas ({_problems.Count})";
    }

    private void GoTo(LunetDiagnostic diagnostic)
    {
        if (diagnostic.FilePath is not null) Jump(diagnostic.FilePath, diagnostic.Line, diagnostic.Column);
    }

    private void Jump(string path, int line, int column)
    {
        if (_editor is null) return;
        if (path != _openFile) OpenFile(path);
        var text = _editor.Text ?? "";
        var offset = 0;
        for (var current = 1; current < line && offset < text.Length; current++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0) break;
            offset = next + 1;
        }
        offset = Math.Min(text.Length, offset + Math.Max(0, column - 1));
        _editor.RequestFocus();
        _editor.SetSelection(offset);
        _editor.BringPointIntoView(offset);
    }

    // ---------- Assistência do editor ----------

    private void OnEditorSettled(string text, int version, int caret)
    {
        var path = _openFile;
        if (path is not null && _session.IsDirty(text))
        {
            try { _journal?.WriteBuffer(path, text); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException) { }
        }
        if (path is null || _assistant is null || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            HideChips();
            return;
        }
        _assistant.AnalyzeAsync(path, text, caret, version).ContinueWith(task => RunOnUiThread(() =>
        {
            if (task.IsFaulted || _editor is null || _editor.Version != task.Result.Version || _openFile != path) return;
            _problems = task.Result.Diagnostics;
            if (!_showConsole) SetPanel(false);
            ShowChips(task.Result.Completions);
        }));
    }

    private void HideChips()
    {
        _chips?.RemoveAllViews();
        if (_chipScroll is not null) _chipScroll.Visibility = ViewStates.Gone;
    }

    private void ShowChips(IReadOnlyList<CompletionItem> items)
    {
        if (_chips is null || _chipScroll is null) return;
        _chips.RemoveAllViews();
        foreach (var item in items.Take(30))
        {
            var captured = item;
            var chip = new Button(this) { Text = $"{Symbol(item.Kind)} {item.Label}" };
            chip.SetAllCaps(false);
            chip.TextSize = 12;
            chip.SetMinimumHeight(0);
            chip.SetMinHeight(0);
            chip.SetPadding(Dp(10), Dp(4), Dp(10), Dp(4));
            chip.Click += (_, _) =>
            {
                _editor?.Replace(captured.ReplaceStart, captured.ReplaceLength, captured.InsertText);
                _editor?.SetSelection(captured.ReplaceStart + captured.InsertText.Length);
                HideChips();
            };
            _chips.AddView(chip);
        }
        _chipScroll.Visibility = items.Count == 0 ? ViewStates.Gone : ViewStates.Visible;
    }

    private static string Symbol(CompletionKind kind) => kind switch
    {
        CompletionKind.Keyword => "◇",
        CompletionKind.Method => "ƒ",
        CompletionKind.Property or CompletionKind.Field => "▪",
        CompletionKind.Class or CompletionKind.Struct or CompletionKind.Interface or CompletionKind.Enum => "◉",
        CompletionKind.Namespace => "▤",
        CompletionKind.Local or CompletionKind.Parameter => "•",
        _ => "·",
    };

    private void GoToDefinition()
    {
        if (_editor is null || _openFile is null || _assistant is null) return;
        var path = _openFile;
        var caret = _editor.SelectionStart;
        SaveCurrent();
        _assistant.RunAsync(a => a.GetDefinition(path, caret)).ContinueWith(task => RunOnUiThread(() =>
        {
            if (task.IsFaulted || task.Result is null)
                Toast.MakeText(this, "Definição não encontrada (símbolos do framework não têm código-fonte aqui).", ToastLength.Short)?.Show();
            else
                Jump(task.Result.FilePath, task.Result.Line, task.Result.Column);
        }));
    }

    private void ShowHover()
    {
        if (_editor is null || _openFile is null || _assistant is null) return;
        var path = _openFile;
        var caret = _editor.SelectionStart;
        _assistant.RunAsync(a => a.GetHover(path, caret)).ContinueWith(task => RunOnUiThread(() =>
        {
            var hover = task.IsFaulted ? null : task.Result;
            new AlertDialog.Builder(this)!
                .SetTitle(hover is null ? "Sem informação" : hover.Kind)!
                .SetMessage(hover?.Signature ?? "Coloque o cursor sobre um nome.")!
                .SetPositiveButton("Ok", (_, _) => { })!.Show();
        }));
    }

    private void ShowReferences()
    {
        if (_editor is null || _openFile is null || _assistant is null) return;
        var path = _openFile;
        var caret = _editor.SelectionStart;
        _assistant.RunAsync(a => a.FindReferences(path, caret)).ContinueWith(task => RunOnUiThread(() =>
        {
            var refs = task.IsFaulted ? [] : task.Result;
            if (refs.Count == 0)
            {
                Toast.MakeText(this, "Nenhuma referência encontrada.", ToastLength.Short)?.Show();
                return;
            }
            var labels = refs.Select(r => $"{r.FilePath}:{r.Line}:{r.Column}").ToArray();
            new AlertDialog.Builder(this)!
                .SetTitle($"{refs.Count} referência(s)")!
                .SetItems(labels, (_, args) => Jump(refs[args.Which].FilePath, refs[args.Which].Line, refs[args.Which].Column))!.Show();
        }));
    }

    private void ShowFind()
    {
        if (_editor is null) return;
        var form = Vertical();
        form.SetPadding(Dp(16), Dp(8), Dp(16), 0);
        var find = new EditText(this) { Hint = "Localizar", Text = _findQuery };
        find.SetSingleLine(true);
        var replace = new EditText(this) { Hint = "Substituir por", Text = _replaceText };
        replace.SetSingleLine(true);
        var matchCase = new CheckBox(this) { Text = "Diferenciar maiúsculas", Checked = _findOptions.MatchCase };
        var whole = new CheckBox(this) { Text = "Palavra inteira", Checked = _findOptions.WholeWord };
        var regex = new CheckBox(this) { Text = "Expressão regular", Checked = _findOptions.UseRegex };
        foreach (var view in new View[] { find, replace, matchCase, whole, regex }) form.AddView(view);

        var dialog = new AlertDialog.Builder(this)!
            .SetTitle("Localizar e substituir")!
            .SetView(form)!
            .SetNegativeButton("Fechar", (_, _) => { })!
            .SetNeutralButton("Próximo", (_, _) => { })!
            .SetPositiveButton("Substituir tudo", (_, _) => { })!
            .Create()!;
        dialog.Show();

        void Capture()
        {
            _findQuery = find.Text ?? "";
            _replaceText = replace.Text ?? "";
            _findOptions = new FindOptions(matchCase.Checked, whole.Checked, regex.Checked);
        }

        dialog.GetButton((int)DialogButtonType.Neutral)!.Click += (_, _) =>
        {
            Capture();
            try
            {
                var text = _editor.Text ?? "";
                var match = FindReplace.FindNext(text, _findQuery, _editor.SelectionEnd, _findOptions);
                if (match is not { } m) { Toast.MakeText(this, "Não encontrado.", ToastLength.Short)?.Show(); return; }
                _editor.SetSelection(m.Start, m.Start + m.Length);
                _editor.BringPointIntoView(m.Start);
                var total = FindReplace.FindAll(text, _findQuery, _findOptions).Count;
                Toast.MakeText(this, $"{total} ocorrência(s).", ToastLength.Short)?.Show();
            }
            catch (FormatException ex)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            }
        };
        dialog.GetButton((int)DialogButtonType.Positive)!.Click += (_, _) =>
        {
            Capture();
            try
            {
                var text = _editor.Text ?? "";
                var result = FindReplace.ReplaceAll(text, _findQuery, _replaceText, _findOptions, out var count);
                if (count > 0) _editor.Replace(0, text.Length, result);
                Toast.MakeText(this, $"{count} substituição(ões).", ToastLength.Short)?.Show();
            }
            catch (FormatException ex)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            }
        };
    }

    // ---------- Run / Preview ----------

    private void Run()
    {
        if (_project is null) return;
        SaveCurrent();
        _status!.Text = "Compilando… (a primeira compilação demora mais)";
        var sources = _project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
        var assemblyName = $"LunetGame{++_compileCounter}";
        Task.Run(() => SharedCompiler.Value.Compile(assemblyName, sources)).ContinueWith(task =>
            RunOnUiThread(() => OnCompiled(task)));
    }

    private void OnCompiled(Task<CompileResult> task)
    {
        if (_project is null) return;
        if (task.IsFaulted)
        {
            var message = task.Exception?.GetBaseException().Message ?? "erro desconhecido";
            _problems = [new LunetDiagnostic(DiagnosticSeverity.Error, "LUNET0002", "Falha interna do compilador: " + message, null, 0, 0, 0, 0)];
            _status!.Text = "Falha ao compilar";
            SetPanel(false);
            return;
        }
        var result = task.Result;
        _problems = result.Diagnostics;
        if (!result.Success)
        {
            _status!.Text = $"Compilação falhou: {result.ErrorCount} erro(s)";
            SetPanel(false);
            return;
        }
        _status!.Text = "Compilado";
        SetPanel(false);
        ShowPreview(result);
    }

    private void ShowPreview(CompileResult result)
    {
        DisposePreview();
        _console.Clear();
        _previewPaused = false;

        _renderer = new PreviewRenderer(result.Assembly!, result.Symbols,
            new DirectoryContentSource(System.IO.Path.Combine(_project!.Directory, "Content")),
            () => new AndroidAudioBackend(System.IO.Path.Combine(CacheDir!.AbsolutePath, "audio")),
            new DirectorySaveStore(System.IO.Path.Combine(_project!.Directory, ".lunet", "saves")),
            new AndroidHaptics(this), (level, message) => RunOnUiThread(() => AppendConsole(level, message)));
        _glView = new GLSurfaceView(this);
        _glView.SetEGLContextClientVersion(3);
        _glView.PreserveEGLContextOnPause = true; // ao voltar do segundo plano o jogo continua, se o driver mantiver o contexto
        _glView.SetRenderer(_renderer);
        _glView.RenderMode = Rendermode.Continuously;
        _glView.Touch += OnPreviewTouch;

        var root = new FrameLayout(this);
        root.AddView(_glView, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var controls = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        controls.SetBackgroundColor(AndroidColor.Argb(140, 0, 0, 0));
        controls.AddView(MakeButton("■ Stop", StopPreview));
        controls.AddView(MakeButton("↻", () => _renderer?.RequestRestart()));
        Button? pause = null;
        pause = MakeButton("⏸", () =>
        {
            _previewPaused = !_previewPaused;
            _renderer?.SetPaused(_previewPaused);
            pause!.Text = _previewPaused ? "▶" : "⏸";
        });
        controls.AddView(pause);
        controls.AddView(MakeButton("⏭", () => _renderer?.RequestStep()));
        root.AddView(controls, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.Left));

        _previewConsole = new TextView(this) { TextSize = 11, Clickable = false, Focusable = false };
        _previewConsole.SetTextColor(AndroidColor.White);
        _previewConsole.SetBackgroundColor(AndroidColor.Argb(110, 0, 0, 0));
        root.AddView(_previewConsole, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom));

        SetContentView(root);
        RequestHighRefreshRate(true);
        _glView.LayoutChange += (_, _) => UpdateDisplayInfo();
        _glView.Post(UpdateDisplayInfo);
        StartSensors();
    }

    /// <summary>Pede ao sistema o modo de tela de maior taxa de atualização (mesma resolução), para 90/120 Hz onde houver.</summary>
    private void RequestHighRefreshRate(bool enable)
    {
        try
        {
            var attributes = Window?.Attributes;
            var display = WindowManager?.DefaultDisplay;
            if (attributes is null || display is null) return;
            if (!enable)
            {
                attributes.PreferredDisplayModeId = 0;
            }
            else
            {
                var current = display.GetMode();
                var best = display.GetSupportedModes()?
                    .Where(m => m.PhysicalWidth == current.PhysicalWidth && m.PhysicalHeight == current.PhysicalHeight)
                    .OrderByDescending(m => m.RefreshRate)
                    .FirstOrDefault();
                if (best is null) return;
                attributes.PreferredDisplayModeId = best.ModeId;
            }
            Window!.Attributes = attributes;
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
        {
            // Não é essencial: sem alta taxa, o jogo roda em 60 Hz.
        }
    }

    /// <summary>Envia ao jogo a densidade da tela e os recuos seguros (recorte de câmera, cantos arredondados).</summary>
    private void UpdateDisplayInfo()
    {
        if (_glView is null || _renderer is null) return;
        int left = 0, top = 0, right = 0, bottom = 0;
        var cutout = _glView.RootWindowInsets?.DisplayCutout;
        if (cutout is not null)
        {
            var location = new int[2];
            _glView.GetLocationInWindow(location);
            left = Math.Max(0, cutout.SafeInsetLeft - location[0]);
            top = Math.Max(0, cutout.SafeInsetTop - location[1]);
            right = cutout.SafeInsetRight;
            bottom = cutout.SafeInsetBottom;
        }
        _renderer.SetDisplay(Resources!.DisplayMetrics!.Density, left, top, right, bottom);
    }

    private void StartSensors()
    {
        _sensors = GetSystemService(SensorService) as SensorManager;
        var accelerometer = _sensors?.GetDefaultSensor(SensorType.Accelerometer);
        if (accelerometer is not null) _sensors!.RegisterListener(this, accelerometer, SensorDelay.Game);
        var gyroscope = _sensors?.GetDefaultSensor(SensorType.Gyroscope);
        if (gyroscope is not null) _sensors!.RegisterListener(this, gyroscope, SensorDelay.Game);
    }

    private void StopSensors()
    {
        _sensors?.UnregisterListener(this);
        _sensors = null;
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values is not { Count: >= 3 } v) return;
        var value = new System.Numerics.Vector3(v[0], v[1], v[2]);
        if (e.Sensor?.Type == SensorType.Gyroscope) _renderer?.SetGyroscope(value);
        else _renderer?.SetAccelerometer(value);
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e) => RouteKey(keyCode, true, e) || base.OnKeyDown(keyCode, e);

    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e) => RouteKey(keyCode, false, e) || base.OnKeyUp(keyCode, e);

    public override bool OnGenericMotionEvent(MotionEvent? e)
    {
        if (e is null || _renderer is null || (e.Source & InputSourceType.Joystick) != InputSourceType.Joystick || e.Action != MotionEventActions.Move)
            return base.OnGenericMotionEvent(e);
        _padSeen = true;
        _padLeft = new System.Numerics.Vector2(e.GetAxisValue(global::Android.Views.Axis.X), e.GetAxisValue(global::Android.Views.Axis.Y));
        _padRight = new System.Numerics.Vector2(e.GetAxisValue(global::Android.Views.Axis.Z), e.GetAxisValue(global::Android.Views.Axis.Rz));
        _padLeftTrigger = System.Math.Max(e.GetAxisValue(global::Android.Views.Axis.Ltrigger), e.GetAxisValue(global::Android.Views.Axis.Brake));
        _padRightTrigger = System.Math.Max(e.GetAxisValue(global::Android.Views.Axis.Rtrigger), e.GetAxisValue(global::Android.Views.Axis.Gas));
        var hatX = e.GetAxisValue(global::Android.Views.Axis.HatX);
        var hatY = e.GetAxisValue(global::Android.Views.Axis.HatY);
        SetPadButton(GamepadButtons.DPadLeft, hatX < -0.5f);
        SetPadButton(GamepadButtons.DPadRight, hatX > 0.5f);
        SetPadButton(GamepadButtons.DPadUp, hatY < -0.5f);
        SetPadButton(GamepadButtons.DPadDown, hatY > 0.5f);
        PushGamepad();
        return true;
    }

    private void SetPadButton(GamepadButtons button, bool down) =>
        _padButtons = down ? _padButtons | button : _padButtons & ~button;

    private void PushGamepad() =>
        _renderer?.SetGamepad(new GamepadState(_padSeen, _padButtons, _padLeft, _padRight, _padLeftTrigger, _padRightTrigger));

    private bool RouteKey(Keycode code, bool down, KeyEvent? e)
    {
        if (_renderer is null) return false;
        var fromGamepad = e is not null && (e.Source & InputSourceType.Gamepad) == InputSourceType.Gamepad;
        var button = code switch
        {
            Keycode.ButtonA => GamepadButtons.A,
            Keycode.ButtonB => GamepadButtons.B,
            Keycode.ButtonX => GamepadButtons.X,
            Keycode.ButtonY => GamepadButtons.Y,
            Keycode.ButtonL1 => GamepadButtons.LeftShoulder,
            Keycode.ButtonR1 => GamepadButtons.RightShoulder,
            Keycode.ButtonStart => GamepadButtons.Start,
            Keycode.ButtonSelect => GamepadButtons.Back,
            Keycode.ButtonThumbl => GamepadButtons.LeftStick,
            Keycode.ButtonThumbr => GamepadButtons.RightStick,
            Keycode.DpadLeft when fromGamepad => GamepadButtons.DPadLeft,
            Keycode.DpadRight when fromGamepad => GamepadButtons.DPadRight,
            Keycode.DpadUp when fromGamepad => GamepadButtons.DPadUp,
            Keycode.DpadDown when fromGamepad => GamepadButtons.DPadDown,
            _ => GamepadButtons.None,
        };
        if (button != GamepadButtons.None)
        {
            _padSeen = true;
            SetPadButton(button, down);
            PushGamepad();
            if (code is not (Keycode.DpadLeft or Keycode.DpadRight or Keycode.DpadUp or Keycode.DpadDown)) return true;
        }
        var key = code switch
        {
            >= Keycode.A and <= Keycode.Z => (Keys)((int)Keys.A + (code - Keycode.A)),
            >= Keycode.Num0 and <= Keycode.Num9 => (Keys)((int)Keys.D0 + (code - Keycode.Num0)),
            Keycode.Space => Keys.Space,
            Keycode.Enter => Keys.Enter,
            Keycode.Escape => Keys.Escape,
            Keycode.Tab => Keys.Tab,
            Keycode.ShiftLeft or Keycode.ShiftRight => Keys.Shift,
            Keycode.DpadLeft => Keys.Left,
            Keycode.DpadRight => Keys.Right,
            Keycode.DpadUp => Keys.Up,
            Keycode.DpadDown => Keys.Down,
            _ => Keys.None,
        };
        if (key == Keys.None) return false;
        _renderer.SetKey(key, down);
        return true;
    }

    private void OnPreviewTouch(object? sender, View.TouchEventArgs args)
    {
        var e = args.Event;
        if (e is null || _renderer is null) return;
        var touches = new List<TouchPoint>(e.PointerCount);
        var action = e.ActionMasked;
        var index = e.ActionIndex;
        for (var i = 0; i < e.PointerCount; i++)
        {
            var phase = TouchPhase.Moved;
            if (action is MotionEventActions.Down or MotionEventActions.PointerDown && i == index) phase = TouchPhase.Pressed;
            else if (action is MotionEventActions.Up or MotionEventActions.PointerUp && i == index) phase = TouchPhase.Released;
            else if (action == MotionEventActions.Cancel) phase = TouchPhase.Cancelled;
            touches.Add(new TouchPoint(e.GetPointerId(i), phase, new System.Numerics.Vector2(e.GetX(i), e.GetY(i))));
        }
        _renderer.SetTouches(touches.ToArray());
        args.Handled = true;
    }

    private void AppendConsole(LogLevel level, string message)
    {
        var prefix = level switch { LogLevel.Error => "[erro] ", LogLevel.Warning => "[aviso] ", _ => "" };
        _console.Add(prefix + message);
        if (_console.Count > MaxConsoleLines) _console.RemoveRange(0, _console.Count - MaxConsoleLines);
        if (_previewConsole is not null)
            _previewConsole.Text = string.Join('\n', _console.Skip(Math.Max(0, _console.Count - 6)));
    }

    private void StopPreview()
    {
        DisposePreview();
        if (_project is null) return;
        SaveCurrent(); // o editor antigo ainda guarda o texto; salva antes de a tela ser recriada
        var file = _openFile;
        _openFile = null;
        ShowWorkspace();
        if (file is not null) OpenFile(file);
        _showConsole = true;
        SetPanel(true);
    }

    private void DisposePreview()
    {
        if (_glView is null) return;
        StopSensors();
        RequestHighRefreshRate(false);
        _glView.Touch -= OnPreviewTouch;
        var renderer = _renderer;
        _glView.QueueEvent(() => renderer?.Shutdown());
        _glView.OnPause();
        _glView = null;
        _renderer = null;
        _previewConsole = null;
    }

    // ---------- Export ----------

    /// <summary>Exporta o projeto aberto (ou o de nome dado, na lista de projetos) como ZIP pelo seletor de documentos do Android.</summary>
    private void ExportProject(string? name = null)
    {
        name ??= _project?.Name;
        if (name is null) return;
        if (_project is not null && _project.Name == name) SaveCurrent();
        _pendingExport = name;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/zip");
        intent.PutExtra(Intent.ExtraTitle, name + ".zip");
        StartActivityForResult(intent, ExportRequestCode);
    }

    private void ImportImage()
    {
        if (_project is null) return;
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("image/png");
        StartActivityForResult(intent, ImportRequestCode);
    }

    private void CompleteImport(AndroidUri uri)
    {
        if (_project is null) return;
        try
        {
            string? name = null;
            using (var cursor = ContentResolver?.Query(uri, null, null, null, null))
            {
                if (cursor is not null && cursor.MoveToFirst())
                {
                    var column = cursor.GetColumnIndex(global::Android.Provider.IOpenableColumns.DisplayName);
                    if (column >= 0) name = cursor.GetString(column);
                }
            }
            using var stream = ContentResolver?.OpenInputStream(uri) ?? throw new IOException("Não foi possível abrir o arquivo.");
            var path = _project.Import("Content/Textures", name ?? "image.png", stream);
            Toast.MakeText(this, $"Importado em {path}. Use Content.LoadTexture(\"Textures/{System.IO.Path.GetFileName(path)}\")", ToastLength.Long)?.Show();
        }
        catch (Exception ex) when (ex is IOException or ProjectException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Falha ao importar: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == ImportRequestCode)
        {
            if (resultCode == Result.Ok && data?.Data is AndroidUri picked) CompleteImport(picked);
            return;
        }
        if (requestCode != ExportRequestCode) return;
        var name = _pendingExport;
        _pendingExport = null;
        if (resultCode != Result.Ok || data?.Data is not AndroidUri uri || name is null) return;
        try
        {
            using var stream = ContentResolver?.OpenOutputStream(uri) ?? throw new IOException("Não foi possível abrir o destino.");
            _store.Open(name).ExportZip(stream);
            Toast.MakeText(this, "Projeto exportado", ToastLength.Short)?.Show();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException)
        {
            Toast.MakeText(this, "Falha ao exportar: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    // ---------- Lifecycle ----------

    protected override void OnSaveInstanceState(Bundle outState)
    {
        outState.PutString("pendingExport", _pendingExport);
        base.OnSaveInstanceState(outState);
    }

    protected override void OnPause()
    {
        SaveCurrent();
        _renderer?.SetAppPaused(true);
        _glView?.OnPause();
        StopSensors();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _glView?.OnResume();
        _renderer?.SetAppPaused(false);
        if (_glView is not null) StartSensors();
    }

    protected override void OnDestroy()
    {
        DisposePreview();
        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        if (_glView is not null) StopPreview();
        else if (_project is not null) ShowProjects();
        else base.OnBackPressed();
    }
}
