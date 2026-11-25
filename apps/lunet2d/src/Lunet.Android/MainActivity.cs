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
using Lunet.Studio;
using AndroidColor = Android.Graphics.Color;
using AndroidUri = Android.Net.Uri;

namespace Lunet.Android;

[Activity(Label = "Lunet", MainLauncher = true, Exported = true, Theme = "@android:style/Theme.DeviceDefault.NoActionBar",
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation | global::Android.Content.PM.ConfigChanges.ScreenSize |
                           global::Android.Content.PM.ConfigChanges.KeyboardHidden | global::Android.Content.PM.ConfigChanges.ScreenLayout | global::Android.Content.PM.ConfigChanges.UiMode,
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
    private AndroidGameImagePicker? _gameImagePicker;
    private FrameLayout? _workspaceFrame;
    private bool _isolatedRunning;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        LunetAiRuns.Changed += OnAiRunChanged;
        _pendingExport = savedInstanceState?.GetString("pendingExport");
        _pendingLibraryAssetId = savedInstanceState?.GetString("pendingLibraryAssetId");
        _pendingLibraryAssetVersion = savedInstanceState?.GetInt("pendingLibraryAssetVersion") ?? 0;
        _pendingGameApkPath = savedInstanceState?.GetString("pendingGameApkPath");
        _pendingGameApkName = savedInstanceState?.GetString("pendingGameApkName");
        _gameplaySession = savedInstanceState?.GetString("gameplaySession");
        _gameplayId = savedInstanceState?.GetString("gameplayId");
        _gameplayFolder = savedInstanceState?.GetString("gameplayFolder");
        LoadSettings();
        LoadProjectFolder();
        LoadLauncherPreferences();
        _projectFilter = savedInstanceState?.GetString("projectFilter") ?? "";
        foreach (var section in new[] { "home", "projects", "play", "studios", "explore", "learn" })
            _launcherPositions[section] = savedInstanceState?.GetInt("scroll-" + section) ?? 0;
        RestoreLauncherSection();
        _folderPickerFromSetup = savedInstanceState?.GetBoolean("folderPickerFromSetup") ?? false;
        if (_settings.FirstUseStep < 3 && !_folderPickerFromSetup && !IsUiDemo()) ShowFirstUse();
#if LUNET_UI_DEMO
        RunDemo(Intent);
#endif
    }

    // ---------- UI helpers ----------

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density);

    private Button MakeButton(string label, Action action)
    {
        return LunetUi.Button(this, label, action);
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
        EnterLauncherSurface("projects");
        RefreshLauncherCatalog();

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

        root.AddView(LunetUi.Button(this, "Explorar gêneros, mecânicas e jogos", OpenInspirationExplorer,
            ButtonStyle.Secondary), LunetUi.FillWidth(Dp(48)));

        var names = _store.List();
        var section = LunetUi.Label(this, $"PROJETOS · {names.Count}", LunetUi.TextCaption, LunetUi.TextFaint, bold: true);
        section.SetPadding(Dp(2), Dp(2), 0, Dp(8));
        root.AddView(section);
        var search = LunetUi.Input(this, "Pesquisar meus projetos", _projectFilter);
        root.AddView(search, LunetUi.FillWidth(Dp(48)));
        var list = LunetUi.Column(this, 8);
        FillLauncherProjects(list, _projectFilter);
        search.TextChanged += (_, _) =>
        {
            _projectFilter = search.Text ?? "";
            FillLauncherProjects(list, _projectFilter);
        };
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        root.AddView(scroll, Fill(1));

        var footer = LunetUi.Row(this, 8);
        footer.SetPadding(Dp(2), Dp(8), 0, 0);
        footer.AddView(LunetUi.Label(this, $"v{BuildInfo.Version} · projetos neste aparelho · exporte ZIP para backup", LunetUi.TextCaption, LunetUi.TextFaint), LunetUi.Weight());
        footer.AddView(LunetUi.Button(this, "Conexões", ShowEcosystemConnection, ButtonStyle.Quiet), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(36)));
        root.AddView(footer);
        SetLauncherContent(root, "projects", scroll);
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
            var when = ProjectTouched(name).ToLocalTime();
            var age = DateTime.Now - when;
            var ago = age.TotalMinutes < 2 ? "agora" : age.TotalHours < 1 ? $"há {(int)age.TotalMinutes} min"
                : age.TotalDays < 1 ? $"há {(int)age.TotalHours} h" : when.ToString("dd/MM");
            return when == DateTime.FromFileTimeUtc(0).ToLocalTime() ? "Projeto local" : "Atualizado " + ago;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return "projeto local"; }
    }

    private void ShowProjectMenu(string name)
    {
        var pinned = IsLauncherPinned(name);
        var items = new List<(string Label, Action Action)>
        {
            (pinned ? "Desafixar do início" : "Fixar no início", () => ToggleLauncherPin(name)),
            ("Exportar ZIP", () => ExportProject(name)),
            ("Excluir projeto", () => ConfirmDeleteProject(name)),
        };
        if (pinned)
            items.Insert(1, ("Mover fixado para o início", () =>
            {
                PersistLauncher(p => p.MovePinToStart(name, LauncherId(name)));
                ReturnToLauncherOrigin();
            }));
        ShowActionMenu(name, items);
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
                    var gameId = LauncherId(name);
                    _store.Delete(name);
                    PersistLauncher(p => p.ForgetProject(name, gameId));
                    ShowProjects();
                }
                catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    /// <summary>Somente projetos novos; demos e protótipos não fazem parte do launcher.</summary>
    private void AskForProjectName()
    {
        var form = Vertical();
        form.SetPadding(Dp(16), Dp(8), Dp(16), Dp(12));

        EditText AddField(string label, string value, string hint, bool numeric = false)
        {
            var caption = LunetUi.Label(this, label, LunetUi.TextLabel, LunetUi.TextMuted);
            caption.SetPadding(0, Dp(10), 0, 0);
            form.AddView(caption);
            var field = new EditText(this) { Text = value, Hint = hint };
            field.SetSingleLine(true);
            if (numeric) field.InputType = InputTypes.ClassNumber;
            form.AddView(field, LunetUi.FillWidth(Dp(48)));
            return field;
        }

        var nameInput = AddField("Nome do projeto", "", "Ex.: MeuJogo");
        form.AddView(LunetUi.Label(this,
            "Cria um Game.cs com ciclo de vida e SpriteBatch, sem personagens nem demonstrações.",
            LunetUi.TextCaption, LunetUi.TextMuted));

        form.AddView(LunetUi.Label(this, "Orientação", LunetUi.TextLabel, LunetUi.TextMuted, bold: true));
        var orientationGroup = new RadioGroup(this);
        var portrait = new RadioButton(this) { Id = View.GenerateViewId(), Text = "Vertical (retrato)" };
        var landscape = new RadioButton(this) { Id = View.GenerateViewId(), Text = "Horizontal (paisagem)" };
        orientationGroup.AddView(portrait);
        orientationGroup.AddView(landscape);
        orientationGroup.Check(portrait.Id);
        form.AddView(orientationGroup);

        var widthInput = AddField("Resolução virtual · largura (px)", "360", "Ex.: 360", numeric: true);
        var heightInput = AddField("Resolução virtual · altura (px)", "640", "Ex.: 640", numeric: true);
        orientationGroup.CheckedChange += (_, _) =>
        {
            // Só troca o preset; dimensões personalizadas jamais são sobrescritas.
            string width = widthInput.Text?.Trim() ?? "";
            string height = heightInput.Text?.Trim() ?? "";
            if (orientationGroup.CheckedRadioButtonId == landscape.Id && width == "360" && height == "640")
            {
                widthInput.Text = "640";
                heightInput.Text = "360";
            }
            else if (orientationGroup.CheckedRadioButtonId == portrait.Id && width == "640" && height == "360")
            {
                widthInput.Text = "360";
                heightInput.Text = "640";
            }
        };

        var updatesInput = AddField("Atualizações por segundo", "60", "Ex.: 60", numeric: true);
        form.AddView(LunetUi.Label(this, "Ajuste de tela", LunetUi.TextLabel, LunetUi.TextMuted, bold: true));
        var viewportGroup = new RadioGroup(this);
        var fit = new RadioButton(this) { Id = View.GenerateViewId(), Text = "Fit — exibe tudo, com barras se necessário" };
        var fill = new RadioButton(this) { Id = View.GenerateViewId(), Text = "Fill — preenche a tela, pode recortar" };
        viewportGroup.AddView(fit);
        viewportGroup.AddView(fill);
        viewportGroup.Check(fit.Id);
        form.AddView(viewportGroup);

        var versionInput = AddField("Versão inicial do jogo", "0.1.0", "Ex.: 1.0.0");
        var packageInput = AddField("Identificador Android (opcional)", "",
            "Automático e único — ou com.exemplo.jogo");
        form.AddView(LunetUi.Label(this,
            "O identificador e o GameId são estáveis para futuras atualizações do APK. O formato do projeto, o Framework e o arquivo Game.cs são gerenciados pelo Lunet.",
            LunetUi.TextCaption, LunetUi.TextMuted));

        AlertDialog? dialog = null;
        var actions = LunetUi.Row(this, 8);
        actions.SetPadding(0, Dp(16), 0, 0);
        actions.AddView(LunetUi.Button(this, "Cancelar", () => dialog?.Dismiss(), ButtonStyle.Quiet),
            new LinearLayout.LayoutParams(0, Dp(48), 1f));
        actions.AddView(LunetUi.Button(this, "Criar projeto", () =>
        {
            if (!int.TryParse(widthInput.Text?.Trim(), out int width) ||
                !int.TryParse(heightInput.Text?.Trim(), out int height) ||
                !int.TryParse(updatesInput.Text?.Trim(), out int updates))
            {
                Toast.MakeText(this, "Preencha largura, altura e atualizações com números inteiros.", ToastLength.Long)?.Show();
                return;
            }
            try
            {
                var settings = new ProjectCreationSettings(
                    nameInput.Text ?? "",
                    orientationGroup.CheckedRadioButtonId == landscape.Id ? "landscape" : "portrait",
                    width,
                    height,
                    updates,
                    viewportGroup.CheckedRadioButtonId == fill.Id ? "Fill" : "Fit",
                    versionInput.Text?.Trim() ?? "",
                    packageInput.Text?.Trim());
                var project = _store.Create(settings);
                dialog?.Dismiss();
                OpenProject(project.Name);
            }
            catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
            {
                Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            }
        }, ButtonStyle.Primary), new LinearLayout.LayoutParams(0, Dp(48), 1.3f));
        form.AddView(actions);

        var scroll = new ScrollView(this);
        scroll.AddView(form);
        dialog = new AlertDialog.Builder(this)!
            .SetTitle("Novo projeto · configuração")!
            .SetView(scroll)!
            .Create();
        dialog.Show();
    }

    // ---------- Workspace ----------

    private void OpenProject(string name)
    {
        _inspirationOpen = false;
        try
        {
            RememberLauncherPosition();
            _project = _store.Open(name);
            RefreshLauncherCatalog();
            TrackLauncherOpen(name);
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            return;
        }
        ResolveLearningWorkspaceContext();
        _collapsed.Add("Project");
        _problems = [];
        _console.Clear();
        _lastProfilerReport = null;
        _openFile = null;
        _assistant = new EditorAssistant();
        _ = _assistant.LoadProjectAsync(_project.LoadSources().Select(f => (f.Path, f.Text)).ToList());
        _journal = new AutosaveJournal(_project);
        LoadEditorDocuments();
        ShowWorkspace();
        if (!OpenFile(_documents.Current ?? _project.Manifest.EntryPoint)) ShowEditorWithoutFile();
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
        InvalidateEditorAnalysis();
        _session.Unload(); // novo editor: nenhum buffer antigo deve ser gravado antes da carga
        var project = _project!;
        var root = Vertical();
        root.SetBackgroundColor(WorkbenchBackground);

        // One top bar (LUNET-483): back · project/file (tap = files) · IA · Run · actions.
        var bar = LunetUi.Row(this, 4);
        bar.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        bar.SetBackgroundColor(LunetUi.Background);
        bar.AddView(LunetUi.Icon(this, "‹", "Voltar à origem", ReturnToLauncherOrigin), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
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
        var aiButton = LunetUi.Button(this, "✦ IA", ShowQuickAssistant, ButtonStyle.Selected, description: "Abrir assistente compacto sem sair do editor");
        bar.AddView(aiButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        var runButton = LunetUi.Button(this, "▶ Rodar", Run, ButtonStyle.Primary, description: "Compilar e rodar o jogo");
        bar.AddView(runButton, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40)));
        bar.AddView(LunetUi.Icon(this, "⋯", "Todas as ações", ShowCommandPalette), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        root.AddView(bar);
        root.AddView(LunetUi.Divider(this));
        root.AddView(BuildDocumentTabs());

        _chips = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _chipScroll = new HorizontalScrollView(this) { Visibility = ViewStates.Gone, HorizontalScrollBarEnabled = false };
        _chipScroll.SetBackgroundColor(LunetUi.Surface);
        _chipScroll.AddView(_chips);

        var tabs = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        tabs.SetBackgroundColor(WorkbenchBackground);
        tabs.SetPadding(Dp(6), Dp(2), Dp(6), Dp(2));
        void AddPanelTab(Button tab)
        {
            LunetUi.Style(this, tab, ButtonStyle.Quiet);
            tab.TextSize = 12;
            tab.SetMinHeight(Dp(34)); tab.SetMinimumHeight(Dp(34));
            var parameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(36));
            parameters.SetMargins(Dp(2), 0, Dp(2), 0);
            tabs.AddView(tab, parameters);
        }
        _problemsTab = WorkbenchButton("Problemas", () => OpenWorkbenchPanel(false));
        AddPanelTab(_problemsTab);
        AddPanelTab(WorkbenchButton("Console", () => OpenWorkbenchPanel(true)));
        if (_selectedLearningProjectId is not null)
            AddPanelTab(WorkbenchButton("Aprender", OpenLearningWorkbenchPanel));
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

        var tabsScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false, FillViewport = true };
        tabsScroll.AddView(tabs);
        _showConsole = _layoutStore.Current.PanelTab == "console";
        root.AddView(BuildBody(BuildEditorHost(), BuildEditorHelp(), tabsScroll, panelScroll), Fill(1));
        root.AddView(BuildExtraKeys());
        root.AddView(BuildWorkbenchNavigation());

        var frame = new FrameLayout(this);
        frame.AddView(root, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        BuildDrawer(frame);
        _workspaceFrame = frame;
        _aiPanel = null;
        SetContentView(frame);
        BuildAiCompact(frame);
        if (_layoutStore.Current.PanelTab == "learn" && _selectedLearningProjectId is not null) SetLearningWorkbenchPanel();
        else if (_layoutStore.Current.PanelTab == "profiler") SetProfilerPanel();
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
        var newItem = WorkbenchButton("＋", () => ShowActionMenu("Criar no projeto",
        [
            ("Novo arquivo", () => AskForNewEntry("", isDirectory: false)),
            ("Nova pasta", () => AskForNewEntry("", isDirectory: true)),
            ("Importações · presets", ChooseImportsPreset),
        ]));
        newItem.ContentDescription = "Criar arquivo ou pasta";
        header.AddView(newItem, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        var closeDrawer = WorkbenchButton("✕", ToggleExplorer);
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
            var label = entry.Path == "Project" && entry.IsDirectory ? "Projeto"
                : entry.Path == ProjectImports.Path ? "Importações · Imports.cs" : entry.Name;
            var row = new TextView(this) { Text = label, TextSize = 14 };
            row.SetPadding(Dp(12 + entry.Depth * 14), Dp(12), Dp(8), Dp(12));
            row.SetMinHeight(Dp(46));
            row.SetSingleLine(true);
            row.Ellipsize = TextUtils.TruncateAt.Middle;
            row.ContentDescription = entry.IsDirectory ? "Pasta " + entry.Path : "Arquivo " + entry.Path;
            row.Background = LunetUi.Pressable(this, entry.Path == _openFile ? LunetUi.AccentSoft : LunetUi.Surface, LunetUi.RadiusSmall);
            row.SetTextColor(entry.IsDirectory ? LunetUi.AccentText : IsTextFile(entry.Path) ? LunetUi.Text : LunetUi.TextMuted);
            if (entry.IsDirectory) row.SetTypeface(LunetUi.Mono, TypefaceStyle.Bold);
            LunetUi.SetLeadingIcon(row, LunetIcons.Get(ExplorerIcon(entry)), 14);
            row.Click += (_, _) => OnExplorerTap(captured);
            row.LongClick += (_, _) => ShowEntryMenu(captured);
            _drawerList.AddView(row);
        }
    }

    /// <summary>Ícone de um item do Explorer: o tipo do arquivo, não um caractere decorativo.</summary>
    private string ExplorerIcon(ExplorerEntry entry)
    {
        if (entry.IsDirectory) return _collapsed.Contains(entry.Path) ? "chevron-right" : "chevron-down";
        string path = entry.Path.ToLowerInvariant();
        if (path.EndsWith(".sprite.json")) return "sprite";
        if (path.EndsWith(".tilemap.json")) return "tilemap";
        if (path.EndsWith(".cs")) return "code";
        if (path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".jpeg")) return "image";
        if (path.EndsWith(".md") || path.EndsWith(".txt")) return "note";
        if (path.EndsWith(".wav") || path.EndsWith(".ogg") || path.EndsWith(".mp3")) return "waveform";
        if (path.EndsWith(".ttf") || path.EndsWith(".otf")) return "font";
        if (path.EndsWith(".json")) return "data";
        return "file";
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
        if (!IsTextFile(entry.Path) || entry.Path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
        {
            if (!ShowAssetPreview(entry.Path))
                Toast.MakeText(this, "Sem prévia para este arquivo. Use-o pelo código (ex.: Content.LoadTexture).", ToastLength.Short)?.Show();
            return;
        }
        if (OpenFile(entry.Path)) ToggleExplorer();
    }

    private void ShowEntryMenu(ExplorerEntry entry)
    {
        var items = entry.IsDirectory
            ? new[] { "Novo arquivo aqui", "Nova pasta aqui", "Renomear", "Excluir" }
            : IsTextFile(entry.Path) ? new[] { "Abrir em nova aba", "Renomear", "Excluir" } : new[] { "Renomear", "Excluir" };
        items = [.. items, "Copiar caminho"];
        if (entry.Path == ProjectImports.Path || entry.Path == "Project") items = [.. items, "Presets de importações"];
        new AlertDialog.Builder(this)!.SetTitle(entry.Path)!.SetItems(items, (_, args) =>
        {
            switch (items[args.Which])
            {
                case "Abrir em nova aba": if (OpenFile(entry.Path, additional: true)) ToggleExplorer(); break;
                case "Novo arquivo aqui": AskForNewEntry(entry.Path + "/", isDirectory: false); break;
                case "Nova pasta aqui": AskForNewEntry(entry.Path + "/", isDirectory: true); break;
                case "Renomear": AskToRename(entry); break;
                case "Excluir": AskToDelete(entry); break;
                case "Copiar caminho": CopyToClipboard(entry.Path); break;
                case "Presets de importações": ChooseImportsPreset(); break;
            }
        })!.Show();
    }

    /// <summary>Abre a prévia de um arquivo de Content/ ou Assets/ tocado no Explorer (sprite, imagem, som, fonte…).</summary>
    private bool ShowAssetPreview(string path)
    {
        if (_project is null) return false;
        var assets = ProjectAssetCatalog.Build(_project.ListFiles());
        var asset = assets.FirstOrDefault(a => string.Equals(a.Path, path, StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(a => string.Equals(a.PreviewPath, path, StringComparison.OrdinalIgnoreCase));
        if (asset is null) return false;
        _explorerOpenForPreview = true;
        ShowAssetDetails(asset, null);
        return true;
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

    private void ChooseImportsPreset()
    {
        if (_project is null || !SaveCurrent()) return;
        ShowActionMenu("Importações · Project/Imports.cs",
        [
            ("Essencial", () => PreviewImportsPreset(false)),
            ("Amplo · experimental", () => PreviewImportsPreset(true))
        ]);
    }

    private void PreviewImportsPreset(bool broad)
    {
        var project = _project;
        if (project is null || !SaveCurrent()) return;
        try
        {
            var observed = project.ListFiles().Contains(ProjectImports.Path, StringComparer.Ordinal)
                ? project.ReadText(ProjectImports.Path) : null;
            var proposed = ProjectImports.Source(broad);
            string message = "A alteração é explícita. Nomes e extensões podem tornar-se ambíguos. "
                + "O modo SDK explícito usa sdkImportsVersion=1 no lunet.json.\n\n"
                + "ANTES:\n" + (observed ?? "(arquivo inexistente)") + "\nDEPOIS:\n" + proposed;
            new AlertDialog.Builder(this)!.SetTitle(broad ? "Preset Amplo" : "Preset Essencial")!
                .SetMessage(message)!.SetNegativeButton("Cancelar", (_, _) => { })!
                .SetPositiveButton("Aplicar", (_, _) =>
                {
                    if (!ReferenceEquals(project, _project) || !SaveCurrent()) return;
                    try
                    {
                        // Preserve the normal editor history for existing documents.
                        if (observed is not null)
                        {
                            if (project.ReadText(ProjectImports.Path) != observed || !OpenFile(ProjectImports.Path))
                                throw new ProjectException("As importações mudaram. Abra novamente a prévia.");
                            if (_editor is not { } editor || editor.Text != observed)
                                throw new ProjectException("As importações mudaram. Abra novamente a prévia.");
                            editor.Replace(0, observed.Length, proposed);
                            if (!SaveCurrent()) return;
                        }
                        else
                        {
                            if (!project.WriteTextIfUnchanged(ProjectImports.Path, null, proposed, out _))
                                throw new ProjectException("As importações mudaram. Abra novamente a prévia.");
                        }
                        if (project.Manifest.SdkImportsVersion != ProjectImports.ExplicitSdkVersion)
                        {
                            string oldManifest = project.ReadText(ProjectStore.ManifestFileName);
                            var manifest = ProjectManifest.Parse(oldManifest);
                            manifest.SdkImportsVersion = ProjectImports.ExplicitSdkVersion;
                            if (!project.WriteTextIfUnchanged(ProjectStore.ManifestFileName, oldManifest, manifest.ToJson(), out _))
                                throw new ProjectException("lunet.json mudou; confira a política SDK antes de exportar.");
                            project.Manifest.SdkImportsVersion = manifest.SdkImportsVersion;
                        }
                        _assistant?.SetFile(ProjectImports.Path, proposed);
                        _collapsed.Remove("Project");
                        RefreshExplorer();
                        if (OpenFile(ProjectImports.Path) && _drawer?.Visibility == ViewStates.Visible) ToggleExplorer();
                    }
                    catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                    { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
                })!.Show();
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        { Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show(); }
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
        _documents.Reconcile(_project.ListFiles().Where(IsTextFile));
        if (to is not null) OpenFile(to + open[from.Length..]);
        else if (!OpenFile(_documents.Current ?? _project.Manifest.EntryPoint)) ShowEditorWithoutFile();
    }

    private bool OpenFile(string path, bool additional = false)
    {
        if (_editor is not { } editor || _project is not { } project) return false;
        if (_openFile == path && _session.Path == path) return true;
        if (!SaveCurrent()) return false;
        try
        {
            var text = project.ReadText(path);
            CaptureDocument();
            InvalidateEditorAnalysis();
            _documents.Open(path, additional);
            _openFile = path;
            _session.Load(path, text);
            var history = _documentHistories.TryGetValue(path, out var previous) && previous.Text == text
                ? previous.History : null;
            editor.LoadText(text, history);
            editor.Visibility = ViewStates.Visible;
            if (_documentEmpty is not null) _documentEmpty.Visibility = ViewStates.Gone;
            ApplyEditorSettings();
            _status!.Text = path;
            RestoreDocumentViewport(_documents.Viewport);
            RefreshDocumentTabs();
            RefreshUndoButtons();
            RefreshKeyboard();
            return true;
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
            return false;
        }
    }

    private bool SaveCurrent()
    {
        if (_project is null || _editor is null) return true;
        try
        {
            _session.TrySave(_project, _editor.Text ?? "", _journal);
            return true;
        }
        catch (EditorConflictException conflict)
        {
            ShowEditorConflict(conflict);
            return false;
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
            return false;
        }
    }

    private bool _conflictShown;

    /// <summary>
    /// The open file changed on disk (usually the AI) while the person was also editing it. Nothing is lost: the
    /// person's text is already in the autosave journal, and they choose which version stays in the file.
    /// </summary>
    private void ShowEditorConflict(EditorConflictException conflict)
    {
        if (_conflictShown || IsFinishing || IsDestroyed) return;
        _conflictShown = true;
        new AlertDialog.Builder(this)!
            .SetTitle("O arquivo mudou enquanto você editava")!
            .SetMessage($"{conflict.FilePath} foi alterado fora do editor (provavelmente pela IA) e você também tem edições não salvas.\n\n" +
                        "Escolha qual versão fica no arquivo. Se escolher a do disco, a sua é copiada para a área de transferência; " +
                        "se escolher a sua, as mudanças da IA podem ser refeitas pela conversa.")!
            .SetCancelable(false)!
            .SetPositiveButton("Manter a minha", (_, _) =>
            {
                _conflictShown = false;
                try { if (_project is not null && _editor is not null) _session.ForceSave(_project, _editor.Text ?? "", _journal); }
                catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                { Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show(); }
            })!
            .SetNegativeButton("Usar a do disco", (_, _) =>
            {
                _conflictShown = false;
                if (_openFile is not { } path || _editor is null || _project is null) return;
                CopyToClipboard(_editor.Text ?? "");
                _editor.LoadText(conflict.DiskText);
                _session.Load(path, conflict.DiskText);
                Toast.MakeText(this, "Sua versão foi copiada para a área de transferência.", ToastLength.Long)?.Show();
            })!.Show();
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
            if (_selectedLearningProjectId is not null)
                _panelList.AddView(LunetUi.Button(this, "Voltar à missão", OpenLearningWorkbenchPanel, ButtonStyle.Quiet),
                    LunetUi.FillWidth(Dp(48)));
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
                if (error)
                {
                    string target = captured.Id.StartsWith("CS", StringComparison.Ordinal)
                        ? "guide:ler-erros#erro-de-compilacao" : "guide:ler-erros#erro-durante-o-jogo";
                    var help = LunetUi.Button(this, "Ajuda para " + captured.Id, () => DocumentationPanel.Open(this, target),
                        ButtonStyle.Quiet);
                    _panelList.AddView(help, LunetUi.FillWidth());
                }
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
        if (_editor is not { } editor) return;
        var previousPath = _openFile;
        var previous = CurrentEditorViewport(editor);
        if (!OpenFile(path)) return;
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
        if (previousPath is not null && (path != previousPath || offset != previous.End))
        {
            _navigationHistory.Push((previousPath, previous));
            RefreshDocumentTabs();
        }
    }

    // ---------- Assistência do editor ----------

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
        if (_project is not { } project) return;
        if (_settings.FormatOnRun) FormatDocument(silent: true);
        if (!SaveCurrent()) return;
        _status!.Text = "Compilando… (a primeira compilação demora mais)";
        var sources = project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
        _pendingChange = _previousRun is null ? null : ChangeClassifier.Classify(_previousRun, sources);
        _previousRun = sources;
        var run = ++_compileCounter;
        var mission = _selectedLearningProjectId;
        var assemblyName = $"LunetGame{run}";
        Task.Run(() => SharedCompiler.Value.Compile(assemblyName, sources)).ContinueWith(task =>
            RunOnUiThread(() =>
            {
                if (!IsDestroyed && ReferenceEquals(_project, project) && _compileCounter == run)
                {
                    RememberLearningCompilation(mission, project.Manifest.GameId, task.IsCompletedSuccessfully && task.Result.Success);
                    OnCompiled(task);
                }
            }));
    }

    private void OnCompiled(Task<CompileResult> task)
    {
        if (_project is null) return;
        if (task.IsFaulted)
        {
            _returnHomeAfterGame = false;
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
            _returnHomeAfterGame = false;
            _status!.Text = $"Compilação falhou: {result.ErrorCount} erro(s)";
            OpenWorkbenchPanel(false);
            return;
        }
        _status!.Text = "Compilado" + DescribeChange(_pendingChange, result.FromCache);
        if (_layoutStore.Current.PanelTab != "learn") SetPanel(false);
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

        _gameImagePicker = new AndroidGameImagePicker(this);
        var renderer = new PreviewRenderer(result.Assembly!, result.Symbols,
            new ProjectContentSource(_project!.Directory),
            () => new AndroidAudioBackend(System.IO.Path.Combine(CacheDir!.AbsolutePath, "audio"),
                message => RunOnUiThread(() => AppendConsole(LogLevel.Error, message))),
            new DirectorySaveStore(System.IO.Path.Combine(_project!.Directory, ".lunet", "saves")),
            new AndroidHaptics(this), (level, message) => RunOnUiThread(() => AppendConsole(level, message)),
            _gameImagePicker);
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
        if (_returnHomeAfterGame)
        {
            _returnHomeAfterGame = false;
            ReturnToLauncherOrigin();
            return;
        }
        SaveCurrent();
        CaptureDocument();
        var file = _openFile;
        _openFile = null;
        ShowWorkspace();
        if (file is null || !OpenFile(file)) ShowEditorWithoutFile();
        if (_selectedLearningProjectId is not null && _layoutStore.Current.PanelTab == "learn")
            OpenLearningWorkbenchPanel();
        else if (_lastProfilerReport is not null)
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
        _gameImagePicker = null;
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
        if (requestCode == GameplayActivity.RequestCode) { CompleteGameplay(resultCode, data); return; }
        if (HandleProjectFolderResult(requestCode, resultCode, data)) return;
        if (HandleAttachmentResult(requestCode, resultCode, data)) return;
        if (requestCode == AndroidGameImagePicker.RequestCode)
        {
            _gameImagePicker?.Complete(resultCode == Result.Ok ? data?.Data : null);
            return;
        }
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
        if (requestCode == AssetLibraryExportRequestCode)
        {
            CompleteLibraryAssetExport(resultCode, data?.Data);
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
        outState.PutString("gameplaySession", _gameplaySession);
        outState.PutString("gameplayId", _gameplayId);
        outState.PutString("gameplayFolder", _gameplayFolder);
        outState.PutString("projectFilter", _projectFilter);
        outState.PutBoolean("folderPickerFromSetup", _folderPickerFromSetup);
        foreach (var (section, position) in _launcherPositions)
            outState.PutInt("scroll-" + section, section == _launcherScrollKey && _launcherScroll is not null ? _launcherScroll.ScrollY : position);
        outState.PutString("pendingExport", _pendingExport);
        outState.PutString("pendingLibraryAssetId", _pendingLibraryAssetId);
        outState.PutInt("pendingLibraryAssetVersion", _pendingLibraryAssetVersion);
        outState.PutString("pendingGameApkPath", _pendingGameApkPath);
        outState.PutString("pendingGameApkName", _pendingGameApkName);
        base.OnSaveInstanceState(outState);
    }

    protected override void OnPause()
    {
        if (_inspirationOpen) SaveInspirationView();
        _spriteStudio?.Suspend();
        _tileStudio?.Suspend();
        _audioStudio?.Suspend();
        SaveCurrent();
        _preview?.Pause();
        CaptureDocument();
        PersistDocuments();
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
        LunetAiRuns.Changed -= OnAiRunChanged;
        if (_workspaceChat is not null) _workspaceChat.Changed -= OnWorkspaceChatChanged;
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
        else if (_project is not null && DismissEditorHelp()) { }
        else if (_project is not null && _layoutStore.Current.ShowPanel) FocusWorkbenchCode();
        else if (_project is not null) ReturnToLauncherOrigin();
        else if (_backFromLauncherChild is { } back) back();
        else if (_globalChatVisible || _inspirationOpen || _launcherSection != "home") ShowHome();
        else base.OnBackPressed();
    }
}
