using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Opengl;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Android.Gles;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Input;
using AndroidColor = Android.Graphics.Color;
using AndroidUri = Android.Net.Uri;

namespace Lunet.Android;

[Activity(Label = "Lunet", MainLauncher = true, Exported = true,
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.ScreenSize |
                           global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenLayout,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public sealed class MainActivity : Activity
{
    private const int ExportRequestCode = 4101;
    private const int ImportRequestCode = 4102;
    private const int MaxConsoleLines = 300;
    private static readonly Lazy<GameCompiler> SharedCompiler = new(() =>
        new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly)));

    private ProjectStore _store = null!;
    private LunetProject? _project;
    private string? _openFile;
    private EditText? _editor;
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

    private LinearLayout Vertical() => new(this) { Orientation = Orientation.Vertical };

    private static LinearLayout.LayoutParams Fill(float weight = 0) =>
        new(ViewGroup.LayoutParams.MatchParent, weight > 0 ? 0 : ViewGroup.LayoutParams.WrapContent, weight);

    // ---------- Projects ----------

    private void ShowProjects()
    {
        SaveCurrent();
        DisposePreview();
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
            list.AddView(MakeButton(captured, () => OpenProject(captured)));
        }
        if (list.ChildCount == 0)
            list.AddView(new TextView(this) { Text = "Nenhum projeto ainda. Crie o primeiro." });
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        root.AddView(scroll, Fill(1));

        root.AddView(new TextView(this)
        {
            Text = $"Versão {BuildInfo.Version} · Os projetos ficam no armazenamento do app; exporte em ZIP para guardar.",
            TextSize = 12,
        });
        SetContentView(root);
    }

    private void AskForProjectName()
    {
        var input = new EditText(this) { Hint = "Nome do projeto" };
        input.SetSingleLine(true);
        new AlertDialog.Builder(this)!
            .SetTitle("Novo projeto")!
            .SetView(input)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try
                {
                    var project = _store.Create(input.Text ?? "");
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
        ShowWorkspace();
        OpenFile(_project.Manifest.EntryPoint);
    }

    private void ShowWorkspace()
    {
        DisposePreview();
        var project = _project!;
        var root = Vertical();

        var bar = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        bar.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
        bar.AddView(MakeButton("←", () => ShowProjects()));
        bar.AddView(MakeButton("▶ Run", Run));
        var files = MakeButton("Arquivos", ChooseFile);
        bar.AddView(files);
        bar.AddView(MakeButton("⋯", ShowMenu));
        root.AddView(bar);

        _status = new TextView(this) { TextSize = 12 };
        _status.SetPadding(Dp(8), 0, Dp(8), 0);
        root.AddView(_status);

        _editor = new EditText(this) { TextSize = 14, Gravity = GravityFlags.Top | GravityFlags.Left };
        _editor.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        _editor.SetHorizontallyScrolling(true);
        _editor.InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagNoSuggestions;
        _editor.SetBackgroundColor(AndroidColor.Rgb(24, 26, 31));
        _editor.SetTextColor(AndroidColor.Rgb(230, 230, 230));
        root.AddView(_editor, Fill(3));

        var tabs = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _problemsTab = MakeButton("Problemas", () => SetPanel(false));
        tabs.AddView(_problemsTab);
        tabs.AddView(MakeButton("Console", () => SetPanel(true)));
        root.AddView(tabs);

        _panelList = Vertical();
        var panelScroll = new ScrollView(this);
        panelScroll.AddView(_panelList);
        root.AddView(panelScroll, Fill(1));

        SetContentView(root);
        SetPanel(_showConsole);
        _status.Text = $"{project.Name}";
    }

    private void ChooseFile()
    {
        SaveCurrent();
        var files = _project!.ListFiles().Where(IsTextFile).ToArray();
        new AlertDialog.Builder(this)!
            .SetTitle("Arquivos")!
            .SetItems(files, (_, args) => OpenFile(files[args.Which]))!
            .SetNeutralButton("Novo arquivo", (_, _) => AskForNewFile())!
            .Show();
    }

    private static bool IsTextFile(string path) =>
        new[] { ".cs", ".json", ".md", ".txt", ".xml" }.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private void AskForNewFile()
    {
        var input = new EditText(this) { Hint = "Code/Player.cs" };
        input.SetSingleLine(true);
        new AlertDialog.Builder(this)!
            .SetTitle("Novo arquivo")!
            .SetView(input)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try
                {
                    var path = (input.Text ?? "").Trim();
                    _project!.CreateFile(path, path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "using Lunet;\n\n" : "");
                    OpenFile(path);
                }
                catch (Exception ex) when (ex is ProjectException or IOException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    private void OpenFile(string path)
    {
        if (_editor is null) return;
        SaveCurrent();
        try
        {
            _editor.Text = _project!.ReadText(path);
            _openFile = path;
            _status!.Text = $"{_project.Name} / {path}";
        }
        catch (Exception ex) when (ex is ProjectException or IOException)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void SaveCurrent()
    {
        if (_project is null || _openFile is null || _editor is null) return;
        try
        {
            _project.WriteText(_openFile, _editor.Text ?? "");
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void ShowMenu()
    {
        var items = new[] { "Salvar", "Referência rápida", "Exportar projeto (ZIP)", "Importar imagem PNG" };
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
        if (diagnostic.FilePath is null || _editor is null) return;
        if (diagnostic.FilePath != _openFile) OpenFile(diagnostic.FilePath);
        var text = _editor.Text ?? "";
        var offset = 0;
        for (var line = 1; line < diagnostic.Line && offset < text.Length; line++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0) break;
            offset = next + 1;
        }
        offset = Math.Min(text.Length, offset + Math.Max(0, diagnostic.Column - 1));
        _editor.RequestFocus();
        _editor.SetSelection(offset);
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
            new DirectoryContentSource(System.IO.Path.Combine(_project!.Directory, "Content")), (level, message) => RunOnUiThread(() => AppendConsole(level, message)));
        _glView = new GLSurfaceView(this);
        _glView.SetEGLContextClientVersion(3);
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
        var file = _openFile;
        ShowWorkspace();
        if (file is not null) OpenFile(file);
        _showConsole = true;
        SetPanel(true);
    }

    private void DisposePreview()
    {
        if (_glView is null) return;
        _glView.Touch -= OnPreviewTouch;
        var renderer = _renderer;
        _glView.QueueEvent(() => renderer?.Shutdown());
        _glView.OnPause();
        _glView = null;
        _renderer = null;
        _previewConsole = null;
    }

    // ---------- Export ----------

    private void ExportProject()
    {
        if (_project is null) return;
        SaveCurrent();
        _pendingExport = _project.Name;
        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("application/zip");
        intent.PutExtra(Intent.ExtraTitle, _project.Name + ".zip");
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
        _glView?.OnPause();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _glView?.OnResume();
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
