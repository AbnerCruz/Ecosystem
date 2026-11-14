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
public sealed partial class MainActivity : Activity
{
    private const int ExportRequestCode = 4101;
    private const int ImportRequestCode = 4102;
    private const int ImportProjectRequestCode = 4103;
    private const int GameApkExportRequestCode = 4104;
    private bool _importingProject;
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
    private bool _showProfiler;
    private string? _lastProfilerReport;
    private IReadOnlyList<LunetDiagnostic> _problems = [];
    private readonly List<string> _console = [];
    private int _compileCounter;
    private IReadOnlyList<SourceFile>? _previousRun;
    private ChangeReport? _pendingChange;
    private string? _pendingExport;
    private string? _pendingGameApkPath;
    private string? _pendingGameApkName;

    private PreviewHost? _preview;
    private FrameLayout? _workspaceFrame;
    private bool _isolatedRunning;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _store = new ProjectStore(System.IO.Path.Combine(FilesDir!.AbsolutePath, "Projects"));
        _pendingExport = savedInstanceState?.GetString("pendingExport");
        _pendingGameApkPath = savedInstanceState?.GetString("pendingGameApkPath");
        _pendingGameApkName = savedInstanceState?.GetString("pendingGameApkName");
        LoadSettings();
        ShowProjects();
#if LUNET_UI_DEMO
        RunDemo(Intent);
#endif
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
        _body = null;
        _editorColumn = null;
        _panelColumn = null;
        _splitter = null;
        _minimap = null;
        _drawer = null;
        _drawerScrim = null;
        if (_overlay is not null) CloseOverlay();
        _workspaceFrame = null;
        _aiPanel = null;
        _workbenchNavButtons.Clear();

        var root = Vertical();
        root.SetPadding(Dp(20), Dp(22), Dp(20), Dp(10));
        root.SetBackgroundColor(LunetUi.Background);

        var brand = LunetUi.Row(this, 8);
        brand.AddView(LunetUi.Label(this, "☾", 22, LunetUi.AccentText));
        brand.AddView(LunetUi.Label(this, "Lunet", 15, LunetUi.TextMuted, bold: true), LunetUi.Weight());
        brand.AddView(LunetUi.Icon(this, "⚙", "Configurações", ShowSettings), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        root.AddView(brand);
        var title = LunetUi.Label(this, "Seus jogos", LunetUi.TextHero, LunetUi.Text, bold: true);
        title.SetPadding(0, Dp(14), 0, Dp(2));
        root.AddView(title);
        var subtitle = LunetUi.Label(this, "Programe em C#, desenhe, teste e gere o APK, tudo no celular.", LunetUi.TextBody, LunetUi.TextMuted);
        subtitle.SetPadding(0, 0, 0, Dp(18));
        root.AddView(subtitle);

        var primary = LunetUi.Button(this, "＋  Novo jogo", AskForProjectName, ButtonStyle.Primary);
        primary.TextSize = 16;
        root.AddView(primary, LunetUi.FillWidth(Dp(54)));
        var actions = LunetUi.Row(this, 8);
        actions.SetPadding(0, Dp(10), 0, Dp(18));
        actions.AddView(LunetUi.Button(this, "Importar ZIP", ImportProject), new LinearLayout.LayoutParams(0, Dp(46), 1f));
        actions.AddView(LunetUi.Button(this, "Clonar do Git", AskClone), new LinearLayout.LayoutParams(0, Dp(46), 1f));
        root.AddView(actions);

        var names = _store.List();
        var section = LunetUi.Label(this, names.Count == 0 ? "" : $"RECENTES · {names.Count}", LunetUi.TextCaption, LunetUi.TextFaint, bold: true);
        section.LetterSpacing = 0.08f;
        section.SetPadding(Dp(2), 0, 0, Dp(8));
        root.AddView(section);

        var list = LunetUi.Column(this, 8);
        foreach (var name in names.OrderByDescending(n => ProjectTouched(n)))
        {
            var captured = name;
            var card = LunetUi.Row(this, 14);
            card.SetPadding(Dp(14), Dp(12), Dp(8), Dp(12));
            card.Background = LunetUi.Pressable(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
            card.Clickable = true;
            card.ContentDescription = "Abrir " + captured + ". Toque longo para opções.";
            card.Click += (_, _) => OpenProject(captured);
            card.LongClick += (_, e) => { ShowProjectMenu(captured); e.Handled = true; };
            var badge = LunetUi.Label(this, captured.Length > 0 ? captured[..1].ToUpperInvariant() : "?", 18, LunetUi.AccentText, bold: true);
            badge.Gravity = GravityFlags.Center;
            badge.Background = LunetUi.Shape(this, LunetUi.AccentSoft, LunetUi.Radius);
            card.AddView(badge, new LinearLayout.LayoutParams(Dp(44), Dp(44)));
            var text = LunetUi.Column(this, 2);
            var nameLabel = LunetUi.Label(this, captured, 16, LunetUi.Text, bold: true);
            nameLabel.SetSingleLine(true);
            nameLabel.Ellipsize = TextUtils.TruncateAt.End;
            text.AddView(nameLabel);
            text.AddView(LunetUi.Label(this, DescribeProject(captured), LunetUi.TextCaption, LunetUi.TextMuted));
            card.AddView(text, LunetUi.Weight());
            card.AddView(LunetUi.Icon(this, "⋮", "Opções de " + captured, () => ShowProjectMenu(captured)), new LinearLayout.LayoutParams(Dp(40), Dp(44)));
            list.AddView(card, LunetUi.FillWidth());
        }
        if (names.Count == 0)
        {
            var empty = LunetUi.Column(this, 6);
            empty.SetPadding(Dp(18), Dp(22), Dp(18), Dp(22));
            empty.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
            empty.AddView(LunetUi.Label(this, "Comece seu primeiro jogo", LunetUi.TextBody, LunetUi.Text, bold: true));
            empty.AddView(LunetUi.Label(this, "Toque em “Novo jogo” para criar a partir de um modelo pronto para rodar. A IA pode ajudar a construir as mecânicas.", LunetUi.TextLabel, LunetUi.TextMuted));
            list.AddView(empty, LunetUi.FillWidth());
        }
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        root.AddView(scroll, Fill(1));

        var footer = LunetUi.Row(this, 8);
        footer.SetPadding(Dp(2), Dp(8), 0, 0);
        footer.AddView(LunetUi.Label(this, $"v{BuildInfo.Version} · projetos neste aparelho · exporte ZIP para backup", LunetUi.TextCaption, LunetUi.TextFaint), LunetUi.Weight());
        footer.AddView(LunetUi.Button(this, "Conexões", ShowEcosystemConnection, ButtonStyle.Quiet), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(36)));
        root.AddView(footer);
        SetContentView(root);
    }

    private DateTime ProjectTouched(string name)
    {
        try { return Directory.GetLastWriteTimeUtc(System.IO.Path.Combine(_store.RootDirectory, name)); }
        catch (IOException) { return DateTime.MinValue; }
    }

    private string DescribeProject(string name)
    {
        try
        {
            var directory = System.IO.Path.Combine(_store.RootDirectory, name);
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(System.IO.Path.DirectorySeparatorChar + ".")).ToArray();
            int code = files.Count(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
            int art = files.Count(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            var when = ProjectTouched(name).ToLocalTime();
            var age = DateTime.Now - when;
            var ago = age.TotalMinutes < 2 ? "agora" : age.TotalHours < 1 ? $"há {(int)age.TotalMinutes} min"
                : age.TotalDays < 1 ? $"há {(int)age.TotalHours} h" : when.ToString("dd/MM");
            return $"{code} arquivo(s) C# · {art} imagem(ns) · {ago}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return "projeto local"; }
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
            ("Demo: Animação e tween (quadros, movimento, pausa)", ProjectTemplate.Animation),
            ("Demo: Partículas (explosões, fluxo, capacidade fixa)", ProjectTemplate.Particles),
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
        form.AddView(new TextView(this) { Text = "Orientação do jogo", TextSize = 15 });
        string orientation = "portrait";
        var orientationGroup = new RadioGroup(this);
        var vertical = new RadioButton(this) { Id = View.GenerateViewId(), Text = "Vertical (9:16)" };
        var horizontal = new RadioButton(this) { Id = View.GenerateViewId(), Text = "Horizontal (16:9)" };
        orientationGroup.AddView(vertical);
        orientationGroup.AddView(horizontal);
        orientationGroup.Check(vertical.Id);
        orientationGroup.CheckedChange += (_, _) =>
            orientation = orientationGroup.CheckedRadioButtonId == horizontal.Id ? "landscape" : "portrait";
        form.AddView(orientationGroup);
        var scroll = new ScrollView(this);
        scroll.AddView(form);
        new AlertDialog.Builder(this)!
            .SetTitle("Novo projeto")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try
                {
                    var project = _store.Create(input.Text ?? "", template[0], orientation);
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
        _lastProfilerReport = null;
        _openFile = null;
        _assistant = new EditorAssistant();
        _ = _assistant.LoadProjectAsync(_project.LoadSources().Select(f => (f.Path, f.Text)).ToList());
        _journal = new AutosaveJournal(_project);
        ShowWorkspace();
        OpenFile(_project.Manifest.EntryPoint);
        OfferRecovery();
    }

    /// <summary>Após um encerramento inesperado, permite recuperar a última edição ou uma versão anterior.</summary>
    private void OfferRecovery()
    {
        var recoveries = _journal?.FindRecoveries() ?? [];
        if (recoveries.Count == 0)
        {
            Toast.MakeText(this, "Nenhuma alteração não salva para recuperar.", ToastLength.Short)?.Show();
            return;
        }
        OfferRecoveryAt(recoveries, 0);
    }

    private void OfferRecoveryAt(IReadOnlyList<Recovery> recoveries, int index)
    {
        if (index >= recoveries.Count) return;
        var recovery = recoveries[index];
        var versions = _journal!.FindHistory(recovery);
        var labels = versions.Select((version, i) =>
            i == 0 ? "Última edição não salva" : $"Versão anterior · {version.BufferTimeUtc:dd/MM HH:mm:ss} UTC").ToArray();
        var selected = 0;
        new AlertDialog.Builder(this)!
            .SetTitle($"Recuperar {recovery.Path} ({index + 1}/{recoveries.Count})")!
            .SetSingleChoiceItems(labels, 0, (_, args) => selected = args.Which)!
            .SetCancelable(false)!
            .SetPositiveButton("Recuperar", (_, _) =>
            {
                try
                {
                    _journal!.Restore(versions[selected]);
                    if (_openFile == recovery.Path)
                    {
                        _session.Unload();
                        _openFile = null;
                        OpenFile(recovery.Path);
                    }
                    Toast.MakeText(this, "Arquivo recuperado", ToastLength.Short)?.Show();
                    OfferRecoveryAt(recoveries, index + 1);
                }
                catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, "Não foi possível recuperar: " + ex.Message, ToastLength.Long)?.Show();
                    OfferRecoveryAt(recoveries, index);
                }
            })!
            .SetNegativeButton("Descartar", (_, _) =>
            {
                _journal!.Discard(recovery.Path);
                OfferRecoveryAt(recoveries, index + 1);
            })!.Show();
    }

    private void ShowWorkspace()
    {
        DisposePreview();
        _session.Unload(); // novo editor: nenhum buffer antigo deve ser gravado antes da carga
        var project = _project!;
        var root = Vertical();
        root.SetBackgroundColor(WorkbenchBackground);

        // One top bar (LUNET-483): back · project/file (tap = files) · IA · Run · actions.
        var bar = LunetUi.Row(this, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.SetBackgroundColor(LunetUi.Background);
        bar.AddView(LunetUi.Icon(this, "‹", "Voltar para os projetos", ShowProjects), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        var titleBlock = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true };
        titleBlock.Background = LunetUi.Pressable(this, LunetUi.Background, LunetUi.RadiusSmall);
        titleBlock.SetPadding(Dp(6), Dp(2), Dp(6), Dp(2));
        titleBlock.ContentDescription = "Arquivos do projeto";
        titleBlock.Click += (_, _) => ToggleExplorer();
        var projectTitle = LunetUi.Label(this, project.Name, 15, LunetUi.Text, bold: true);
        projectTitle.SetSingleLine(true);
        projectTitle.Ellipsize = TextUtils.TruncateAt.End;
        titleBlock.AddView(projectTitle);
        _status = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        _status.SetSingleLine(true);
        _status.Ellipsize = TextUtils.TruncateAt.Middle;
        titleBlock.AddView(_status);
        bar.AddView(titleBlock, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        var aiButton = LunetUi.Button(this, "✦ IA", ToggleAiPanel, ButtonStyle.Selected, description: "Abrir a IA");
        bar.AddView(aiButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        var runButton = LunetUi.Button(this, "▶ Rodar", Run, ButtonStyle.Primary, description: "Compilar e rodar o jogo");
        bar.AddView(runButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        bar.AddView(LunetUi.Icon(this, "⋯", "Todas as ações", ShowCommandPalette), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        root.AddView(bar);
        root.AddView(LunetUi.Divider(this));

        _chips = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _chipScroll = new HorizontalScrollView(this) { Visibility = ViewStates.Gone, HorizontalScrollBarEnabled = false };
        _chipScroll.AddView(_chips);

        var tabs = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        tabs.SetBackgroundColor(WorkbenchBackground);
        void AddPanelTab(Button tab)
        {
            var parameters = new LinearLayout.LayoutParams(0, Dp(48), 1f);
            parameters.SetMargins(Dp(3), 0, Dp(3), 0);
            tabs.AddView(tab, parameters);
        }
        _problemsTab = WorkbenchButton("Problemas", () => OpenWorkbenchPanel(false));
        AddPanelTab(_problemsTab);
        AddPanelTab(WorkbenchButton("Console", () => OpenWorkbenchPanel(true)));
        AddPanelTab(WorkbenchButton("Profiler", () =>
        {
            _layoutStore.Current.OpenPanel("profiler");
            SetProfilerPanel();
            ApplyWorkspaceLayout();
            SaveLayouts();
            RefreshWorkbenchNavigation();
        }));

        _panelList = Vertical();
        _panelList.SetPadding(Dp(10), Dp(6), Dp(10), Dp(8));
        var panelScroll = new ScrollView(this);
        panelScroll.SetBackgroundColor(WorkbenchSurface);
        panelScroll.AddView(_panelList);

        _showConsole = _layoutStore.Current.PanelTab == "console";
        root.AddView(BuildBody(BuildEditorHost(), _chipScroll, tabs, panelScroll), Fill(1));
        root.AddView(BuildExtraKeys());
        root.AddView(BuildWorkbenchNavigation());

        var frame = new FrameLayout(this);
        frame.AddView(root, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        BuildDrawer(frame);
        _workspaceFrame = frame;
        _aiPanel = null;
        SetContentView(frame);
        if (_layoutStore.Current.PanelTab == "profiler") SetProfilerPanel();
        else SetPanel(_showConsole);
        RefreshWorkbenchNavigation();
        WatchKeyboard(root);
        _status.Text = _openFile ?? "";
    }

    // ---------- Explorer ----------

    private void BuildDrawer(FrameLayout frame)
    {
        _drawerScrim = new View(this) { Visibility = ViewStates.Gone, Clickable = true };
        _drawerScrim.SetBackgroundColor(AndroidColor.Argb(174, 0, 0, 0));
        _drawerScrim.Click += (_, _) => ToggleExplorer();
        frame.AddView(_drawerScrim, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        _drawer = Vertical();
        _drawer.SetBackgroundColor(LunetUi.Surface);
        _drawer.SetPadding(Dp(6), 0, Dp(6), Dp(6));
        _drawer.Elevation = Dp(16);
        _drawer.Visibility = ViewStates.Gone;
        _drawer.Clickable = true; // não deixa toques passarem para o editor

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        header.SetPadding(Dp(8), Dp(8), Dp(8), Dp(4));
        header.AddView(WorkbenchLabel("ARQUIVOS", 14, true), new LinearLayout.LayoutParams(0, Dp(48), 1));
        var newItem = WorkbenchButton("+", () => ShowActionMenu("Criar no projeto",
        [
            ("Novo arquivo", () => AskForNewEntry("", isDirectory: false)),
            ("Nova pasta", () => AskForNewEntry("", isDirectory: true)),
        ]));
        newItem.ContentDescription = "Criar arquivo ou pasta";
        header.AddView(newItem, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        var closeDrawer = WorkbenchButton("×", ToggleExplorer);
        closeDrawer.ContentDescription = "Fechar explorador de arquivos";
        header.AddView(closeDrawer, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        _drawer.AddView(header);

        _drawerList = Vertical();
        var scroll = new ScrollView(this);
        scroll.AddView(_drawerList);
        _drawer.AddView(scroll, Fill(1));
        frame.AddView(_drawer, new FrameLayout.LayoutParams(Dp(_layoutStore.Current.ExplorerWidthDp), ViewGroup.LayoutParams.MatchParent, GravityFlags.Left));
    }

    private void ToggleExplorer()
    {
        if (_drawer is null) return;
        var show = _drawer.Visibility != ViewStates.Visible;
        _drawer.Visibility = show ? ViewStates.Visible : ViewStates.Gone;
        if (_drawerScrim is not null) _drawerScrim.Visibility = show ? ViewStates.Visible : ViewStates.Gone;
        if (show) RefreshExplorer();
        RefreshWorkbenchNavigation();
    }

    private void RefreshExplorer()
    {
        if (_drawerList is null || _project is null) return;
        _drawerList.RemoveAllViews();
        foreach (var entry in _project.ListTree())
        {
            if (HasCollapsedAncestor(entry.Path)) continue;
            var captured = entry;
            var glyph = entry.IsDirectory ? (_collapsed.Contains(entry.Path) ? "▸  " : "▾  ")
                : entry.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "{ }  "
                : entry.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "▦  "
                : entry.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "¶  "
                : entry.Path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? "∿  " : "·  ";
            var row = new TextView(this) { Text = glyph + entry.Name, TextSize = 14 };
            row.SetPadding(Dp(12 + entry.Depth * 14), Dp(12), Dp(8), Dp(12));
            row.SetMinHeight(Dp(46));
            row.SetSingleLine(true);
            row.Ellipsize = TextUtils.TruncateAt.Middle;
            row.ContentDescription = entry.IsDirectory ? "Pasta " + entry.Path : "Arquivo " + entry.Path;
            row.Background = LunetUi.Pressable(this, entry.Path == _openFile ? LunetUi.AccentSoft : LunetUi.Surface, LunetUi.RadiusSmall);
            row.SetTextColor(entry.IsDirectory ? LunetUi.AccentText : IsTextFile(entry.Path) ? LunetUi.Text : LunetUi.TextMuted);
            if (entry.IsDirectory) row.SetTypeface(LunetUi.Mono, TypefaceStyle.Bold);
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
            _status!.Text = path;
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

    private void ShowReference()
    {
        if (DocumentationPanel.Open(this)) return;
        var text = new TextView(this) { Text = QuickReference.Text, TextSize = 12 };
        text.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        text.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        var scroll = new ScrollView(this);
        scroll.AddView(text);
        new AlertDialog.Builder(this)!.SetTitle("Documentação")!.SetView(scroll)!.SetPositiveButton("Fechar", (_, _) => { })!.Show();
    }

    private void SetPanel(bool console)
    {
        _showProfiler = false;
        _showConsole = console;
        if (_layoutStore.Current.PanelTab != (console ? "console" : "problems"))
        {
            _layoutStore.Current.PanelTab = console ? "console" : "problems";
            SaveLayouts();
        }
        if (_panelList is null) return;
        _panelList.RemoveAllViews();
        if (console)
        {
            foreach (var line in _console)
            {
                var view = LunetUi.Label(this, line, 12, line.StartsWith("[Error]", StringComparison.Ordinal) ? LunetUi.Danger : LunetUi.Text);
                view.SetTypeface(LunetUi.Mono, TypefaceStyle.Normal);
                view.SetTextIsSelectable(true);
                _panelList.AddView(view);
            }
            if (_console.Count == 0) _panelList.AddView(LunetUi.Label(this, "Sem mensagens do jogo. Rode o jogo para ver o console.", 13, LunetUi.TextMuted));
            else _panelList.AddView(LunetUi.Chip(this, "✦ Perguntar à IA sobre o console", () => OpenAiPanel("Analise o console do jogo e explique o que está acontecendo: ")));
        }
        else
        {
            int errors = _problems.Count(p => p.Severity == DiagnosticSeverity.Error);
            if (errors > 0)
            {
                var fix = LunetUi.Button(this, $"✦ Corrigir {errors} erro(s) com a IA", () =>
                {
                    OpenAiPanel("Corrija os erros de compilação do projeto e confirme que compila.", autoSend: true);
                }, ButtonStyle.Selected);
                _panelList.AddView(fix, LunetUi.FillWidth(Dp(42)));
            }
            foreach (var problem in _problems)
            {
                var captured = problem;
                bool error = problem.Severity == DiagnosticSeverity.Error;
                var card = LunetUi.Column(this, 2);
                card.SetPadding(Dp(10), Dp(8), Dp(10), Dp(8));
                card.Background = LunetUi.Pressable(this, error ? LunetUi.DangerSoft : LunetUi.SurfaceRaised, LunetUi.RadiusSmall);
                card.Clickable = true;
                card.Click += (_, _) => GoTo(captured);
                card.AddView(LunetUi.Label(this, (error ? "✕ " : "! ") + problem.Message, 13, error ? LunetUi.Danger : LunetUi.Warning));
                card.AddView(LunetUi.Label(this, $"{problem.FilePath ?? "projeto"}:{problem.Line} · {problem.Id} · toque para ir", 11, LunetUi.TextMuted));
                _panelList.AddView(card, LunetUi.FillWidth());
                var spacer = new View(this);
                _panelList.AddView(spacer, LunetUi.FillWidth(Dp(6)));
            }
            if (_problems.Count == 0) _panelList.AddView(LunetUi.Label(this, "✓ Nenhum problema. Toque em ▶ Rodar para compilar.", 13, LunetUi.Success));
        }
        RefreshWorkbenchNavigation();
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
        _minimap?.Refresh(text);
        if (path is not null && _session.IsDirty(text))
        {
            try { _journal?.WriteBuffer(path, text); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException)
            {
                // O diário é uma rede de segurança extra; o salvamento normal e o botão Salvar seguem funcionando.
            }
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
            if (!_showConsole && !_showProfiler) SetPanel(false);
            ShowChips(task.Result.Completions);
            _assistant?.RunAsync(a => a.GetFoldRegions(path)).ContinueWith(folds => RunOnUiThread(() =>
            {
                if (!folds.IsFaulted && _editor is not null && _openFile == path) _editor.FoldRegions = folds.Result;
            }));
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
            var dialog = new AlertDialog.Builder(this)!
                .SetTitle(hover is null ? "Sem informação" : hover.Kind)!
                .SetMessage(hover?.Signature ?? "Coloque o cursor sobre um nome.")!
                .SetPositiveButton("Ok", (_, _) => { })!;
            if (hover?.DocumentationId is { } id)
            {
                var source = _editor?.Text ?? "";
                var name = hover.Start >= 0 && hover.Start + hover.Length <= source.Length ? source.Substring(hover.Start, hover.Length) : hover.Signature;
                dialog.SetNeutralButton("Explicar na Documentação", (_, _) =>
                {
                    var target = DocumentationPanel.TargetFor(this, id, name);
                    if (target is not null) DocumentationPanel.Open(this, target);
                });
            }
            dialog.Show();
        }));
    }

    /// <summary>Abre a documentação do nome sob o cursor de texto (toque numa palavra do código antes).</summary>
    private void ExplainSymbol()
    {
        if (_editor is null || _openFile is null || _assistant is null) return;
        var path = _openFile;
        var caret = _editor.SelectionStart;
        var source = _editor.Text ?? "";
        _assistant.RunAsync(a => a.GetHover(path, caret)).ContinueWith(task => RunOnUiThread(() =>
        {
            var hover = task.IsFaulted ? null : task.Result;
            string? name = null;
            if (hover is not null && hover.Start >= 0 && hover.Start + hover.Length <= source.Length)
                name = source.Substring(hover.Start, hover.Length);
            else
            {
                int start = Math.Min(caret, source.Length), end = start;
                while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] == '_')) start--;
                while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_')) end++;
                if (end > start) name = source.Substring(start, end - start);
            }
            if (string.IsNullOrEmpty(name))
            {
                Toast.MakeText(this, "Toque numa palavra do código (ex.: SpriteBatch) e abra este item de novo.", ToastLength.Long)?.Show();
                return;
            }
            var target = DocumentationPanel.TargetFor(this, hover?.DocumentationId, name);
            if (target is not null) DocumentationPanel.Open(this, target);
        }));
    }

    private string _projectQuery = "";

    private void ShowProjectSearch()
    {
        if (_project is null) return;
        var form = Vertical();
        form.SetPadding(Dp(16), Dp(8), Dp(16), 0);
        var query = new EditText(this) { Hint = "Buscar em todos os arquivos", Text = _projectQuery };
        query.SetSingleLine(true);
        var matchCase = new CheckBox(this) { Text = "Diferenciar maiúsculas" };
        var whole = new CheckBox(this) { Text = "Palavra inteira" };
        var regex = new CheckBox(this) { Text = "Expressão regular" };
        foreach (var view in new View[] { query, matchCase, whole, regex }) form.AddView(view);

        new AlertDialog.Builder(this)!
            .SetTitle("Buscar no projeto")!
            .SetView(form)!
            .SetNegativeButton("Fechar", (_, _) => { })!
            .SetPositiveButton("Buscar", (_, _) =>
            {
                _projectQuery = query.Text ?? "";
                SaveCurrent();
                var project = _project;
                var options = new ProjectSearchOptions(matchCase.Checked, whole.Checked, regex.Checked);
                var text = _projectQuery;
                Task.Run(() => ProjectSearch.Search(project, text, options)).ContinueWith(task => RunOnUiThread(() =>
                {
                    var found = task.IsFaulted ? [] : task.Result;
                    if (found.Count == 0)
                    {
                        Toast.MakeText(this, "Nada encontrado.", ToastLength.Short)?.Show();
                        return;
                    }
                    var labels = found.Select(m => $"{m.Path}:{m.Line}  {m.Preview}").ToArray();
                    new AlertDialog.Builder(this)!
                        .SetTitle($"{found.Count} resultado(s)")!
                        .SetItems(labels, (_, args) => Jump(found[args.Which].Path, found[args.Which].Line, found[args.Which].Column))!.Show();
                }));
            })!.Show();
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
        if (_settings.FormatOnRun) FormatDocument(silent: true);
        SaveCurrent();
        _status!.Text = "Compilando… (a primeira compilação demora mais)";
        var sources = _project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
        _pendingChange = _previousRun is null ? null : ChangeClassifier.Classify(_previousRun, sources);
        _previousRun = sources;
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
            OpenWorkbenchPanel(false);
            return;
        }
        var result = task.Result;
        _problems = result.Diagnostics;
        if (!result.Success)
        {
            _status!.Text = $"Compilação falhou: {result.ErrorCount} erro(s)";
            OpenWorkbenchPanel(false);
            return;
        }
        _status!.Text = "Compilado" + DescribeChange(_pendingChange, result.FromCache);
        SetPanel(false);
        ShowPreview(result);
    }

    private void ShowPreview(CompileResult result)
    {
        DisposePreview();
        _lastProfilerReport = null;
        _console.Clear();
        if (_settings.IsolatedPreview)
        {
            LaunchIsolatedPreview(result);
            return;
        }

        var renderer = new PreviewRenderer(result.Assembly!, result.Symbols,
            new ProjectContentSource(_project!.Directory),
            () => new AndroidAudioBackend(System.IO.Path.Combine(CacheDir!.AbsolutePath, "audio"),
                message => RunOnUiThread(() => AppendConsole(LogLevel.Error, message))),
            new DirectorySaveStore(System.IO.Path.Combine(_project!.Directory, ".lunet", "saves")),
            new AndroidHaptics(this), (level, message) => RunOnUiThread(() => AppendConsole(level, message)));
        _preview = new PreviewHost(this, renderer, () => _project?.ListFiles() ?? [], StopPreview, _settings.HighRefreshRate);
        SetContentView(_preview);
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e) => _preview?.OnKey(keyCode, true, e) == true || base.OnKeyDown(keyCode, e);

    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e) => _preview?.OnKey(keyCode, false, e) == true || base.OnKeyUp(keyCode, e);

    public override bool OnGenericMotionEvent(MotionEvent? e) => _preview?.OnGenericMotion(e) == true || base.OnGenericMotionEvent(e);

    private void AppendConsole(LogLevel level, string message)
    {
        var prefix = level switch { LogLevel.Error => "[erro] ", LogLevel.Warning => "[aviso] ", _ => "" };
        _console.Add(prefix + message);
        if (_console.Count > MaxConsoleLines) _console.RemoveRange(0, _console.Count - MaxConsoleLines);
        _preview?.SetConsole(string.Join('\n', _console.Skip(Math.Max(0, _console.Count - 6))));
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
        if (_lastProfilerReport is not null)
        {
            _layoutStore.Current.OpenPanel("profiler");
            SetProfilerPanel();
            ApplyWorkspaceLayout();
            SaveLayouts();
            RefreshWorkbenchNavigation();
        }
        else OpenWorkbenchPanel(true);
    }

    private void DisposePreview()
    {
        if (_preview is null) return;
        _lastProfilerReport = _preview.CaptureProfilerReport();
        _preview.Shutdown();
        _preview = null;
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

    private void ImportProject()
    {
        if (_importingProject)
        {
            Toast.MakeText(this, "Aguarde a importação em andamento.", ToastLength.Short)?.Show();
            return;
        }
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        // Alguns provedores identificam ZIP como octet-stream; o Core valida o conteúdo.
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "application/zip", "application/x-zip-compressed", "application/octet-stream" });
        StartActivityForResult(intent, ImportProjectRequestCode);
    }

    private async void CompleteProjectImport(AndroidUri uri)
    {
        if (_importingProject) return;
        _importingProject = true;
        Toast.MakeText(this, "Importando projeto…", ToastLength.Short)?.Show();
        try
        {
            var project = await Task.Run(() =>
            {
                using var stream = ContentResolver?.OpenInputStream(uri)
                    ?? throw new IOException("Não foi possível abrir o ZIP.");
                return _store.ImportZip(stream);
            });
            if (IsFinishing || IsDestroyed) return;
            var folder = System.IO.Path.GetFileName(project.Directory);
            // Atualiza a lista sem trocar um projeto que o usuário abriu enquanto aguardava.
            if (_project is null) ShowProjects();
            Toast.MakeText(this, $"Projeto importado: {folder}", ToastLength.Long)?.Show();
        }
        catch (Exception ex) when (ex is IOException or ProjectException or UnauthorizedAccessException or global::Java.Lang.SecurityException)
        {
            if (!IsFinishing && !IsDestroyed)
                new AlertDialog.Builder(this)!.SetTitle("Falha ao importar ZIP")!
                    .SetMessage(ex.Message)!.SetPositiveButton("Ok", (_, _) => { })!.Show();
        }
        finally { _importingProject = false; }
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
        if (requestCode == ImportProjectRequestCode)
        {
            if (resultCode == Result.Ok && data?.Data is AndroidUri projectUri) CompleteProjectImport(projectUri);
            return;
        }
        if (requestCode == ImportRequestCode)
        {
            if (resultCode == Result.Ok && data?.Data is AndroidUri picked) CompleteImport(picked);
            return;
        }
        if (requestCode == ImportAudioRequestCode)
        {
            if (resultCode == Result.Ok && data?.Data is AndroidUri audioUri) CompleteAudioImport(audioUri);
            return;
        }
        if (requestCode == GameApkExportRequestCode)
        {
            CompleteApkExport(resultCode, data?.Data);
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
        outState.PutString("pendingGameApkPath", _pendingGameApkPath);
        outState.PutString("pendingGameApkName", _pendingGameApkName);
        base.OnSaveInstanceState(outState);
    }

    protected override void OnPause()
    {
        _spriteStudio?.Suspend();
        _tileStudio?.Suspend();
        _audioStudio?.Suspend();
        SaveCurrent();
        _preview?.Pause();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _preview?.Resume();
        CollectIsolatedPreviewResult();
    }

    protected override void OnDestroy()
    {
        _audioStudio?.Dispose();
        DisposeEcosystemConnection();
        DisposePreview();
        base.OnDestroy();
    }

    public override void OnBackPressed()
    {
        if (_preview is not null) StopPreview();
        else if (AiPanelOpen) CloseAiPanel();
        else if (OverlayOpen) CloseOverlay();
        else if (_drawer?.Visibility == ViewStates.Visible) ToggleExplorer();
        else if (_project is not null && _layoutStore.Current.ShowPanel) FocusWorkbenchCode();
        else if (_project is not null) ShowProjects();
        else base.OnBackPressed();
    }
}
