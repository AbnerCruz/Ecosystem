using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// Home adaptativa (LUNET-510), independente do projeto e das execuções da IA.
/// </summary>
public sealed partial class MainActivity
{
    private LauncherPreferences? _launcherPreferences;
    private string? _launcherPreferenceIssue;
    private string _launcherSection = "home";
    private string _projectFilter = "";
    private IReadOnlyList<LauncherProjectEntry> _launcherCatalog = [];
    private bool _returnHomeAfterGame;
    private bool _globalChatVisible;
    private LinearLayout? _launcherShell;
    private LinearLayout? _launcherBody;
    private LinearLayout? _launcherNav;
    private ScrollView? _launcherScroll;
    private bool? _launcherWide;
    private string _launcherScrollKey = "home";
    private Action? _restoreLauncherChild;
    private Action? _backFromLauncherChild;
    private readonly Dictionary<string, int> _launcherPositions = new(StringComparer.Ordinal);

    private void RememberLauncherPosition()
    {
        if (_launcherScroll is { IsAttachedToWindow: true } scroll)
            _launcherPositions[_launcherScrollKey] = scroll.ScrollY;
        _launcherScroll = null;
    }

    private void SetLauncherContent(LinearLayout body, string section, ScrollView? scroll = null, string? scrollKey = null)
    {
        _launcherBody = body;
        _launcherNav = BuildLauncherNavigation(section);
        _launcherShell = new LinearLayout(this);
        _launcherWide = null;
        _launcherShell.SetBackgroundColor(LunetUi.Background);
        _launcherScroll = scroll;
        _launcherScrollKey = scrollKey ?? section;
        ArrangeLauncher();
        SetContentView(_launcherShell);
        if (scroll is not null && _launcherPositions.TryGetValue(_launcherScrollKey, out var position))
            scroll.Post(() => scroll.ScrollTo(0, position));
    }

    private void ArrangeLauncher()
    {
        if (_launcherShell is null || _launcherBody is null || _launcherNav is null) return;
        bool wide = Resources!.Configuration!.ScreenWidthDp >= 600;
        if (_launcherWide == wide) return;
        _launcherWide = wide;
        int? position = _launcherScroll?.ScrollY;
        var focused = _launcherBody.FindFocus();
        _launcherShell.RemoveAllViews();
        _launcherShell.Orientation = wide ? Orientation.Horizontal : Orientation.Vertical;
        _launcherNav.Orientation = wide ? Orientation.Vertical : Orientation.Horizontal;
        foreach (var button in Enumerable.Range(0, _launcherNav.ChildCount).Select(_launcherNav.GetChildAt))
        {
            if (button is null) continue;
            button.LayoutParameters = wide
                ? new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
                : new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1);
        }
        if (wide)
        {
            _launcherShell.AddView(_launcherNav, new LinearLayout.LayoutParams(Dp(132), ViewGroup.LayoutParams.MatchParent));
            _launcherShell.AddView(_launcherBody, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1));
        }
        else
        {
            _launcherShell.AddView(_launcherBody, Fill(1));
            _launcherShell.AddView(_launcherNav, LunetUi.FillWidth());
        }
        if (position is { } y && _launcherScroll is { } scroll)
            scroll.Post(() => scroll.ScrollTo(0, y));
        if (focused is not null) focused.RequestFocus();
    }

    private void RefreshLauncherCatalog()
    {
        _launcherCatalog = LauncherProjectCatalog.Scan(_store);
        PersistLauncher(p => p.UpgradeProjectReferences(_launcherCatalog));
    }

    private string? LauncherId(string name) =>
        LauncherProjectCatalog.UniqueIdForName(_launcherCatalog, name);

    private bool IsLauncherPinned(string name)
    {
        var id = LauncherId(name);
        return id is not null ? _launcherPreferences?.IsPinnedProject(id) == true
            : _launcherPreferences?.IsPinned(name) == true;
    }

    private void ToggleLauncherPin(string name)
    {
        var id = LauncherId(name);
        PersistLauncher(p =>
        {
            if (id is not null) p.SetPinnedProject(name, id, !p.IsPinnedProject(id));
            else p.SetPinned(name, !p.IsPinned(name));
        });
        if (_launcherSection == "home") ShowHome();
        else ShowProjects();
    }

    private void TrackLauncherOpen(string folderName)
    {
        var id = LauncherId(folderName);
        PersistLauncher(p =>
        {
            if (id is null) p.MarkOpened(folderName);
            else p.MarkOpenedProject(folderName, id);
        });
    }

    private void ReturnToLauncherOrigin()
    {
        if (_project is not null)
        {
            if (!SaveCurrent()) return;
            CaptureDocument();
            PersistDocuments();
        }
        if (_restoreLauncherChild is { } restore)
        {
            restore();
            return;
        }
        switch (_launcherSection)
        {
            case "projects": ShowProjects(); break;
            case "play": ShowGameLauncher(); break;
            case "learn": ShowLearning(); break;
            case "studios": ShowLauncherStudios(); break;
            case "explore": OpenInspirationExplorer(); break;
            default: ShowHome(); break;
        }
    }

    private void PlayFromLauncher(string folderName) => _ = PrepareGameplay(folderName);

    private void LoadLauncherPreferences()
    {
        try
        {
            _launcherPreferences = new LauncherPreferences(
                System.IO.Path.Combine(FilesDir!.AbsolutePath, "launcher-preferences.json"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Não sobrescreve preferências pessoais corrompidas.
            _launcherPreferenceIssue = ex.Message;
            _launcherPreferences = null;
        }
    }

    private void RestoreLauncherSection()
    {
        switch (_launcherPreferences?.Section)
        {
            case "projects": ShowProjects(); break;
            case "play": ShowGameLauncher(); break;
            case "studios": ShowLauncherStudios(); break;
            case "explore": OpenInspirationExplorer(); break;
            case "learn": ShowLearning(); break;
            default: ShowHome(); break;
        }
    }

    private void PersistLauncher(Action<LauncherPreferences> change)
    {
        if (_launcherPreferences is null) return;
        try { change(_launcherPreferences); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Toast.MakeText(this, "Preferências não salvas: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    /// <summary>Desmonta somente a superfície do editor; não cancela o coordenador de IA em segundo plano.</summary>
    private void EnterLauncherSurface(string section)
    {
        RememberLauncherPosition();
        _restoreLauncherChild = _backFromLauncherChild = null;
        _globalChatVisible = false;
        SaveCurrent();
        CaptureDocument();
        PersistDocuments();
        _documentsFile = null;
        InvalidateEditorAnalysis();
        _keyboardRoot = null;
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
        _aiModelChip = null;
        _aiPills = null;
        _workspaceChatOpen = false;
        _workbenchNavButtons.Clear();
        _inspirationOpen = false;
        _launcherSection = section;
        _returnHomeAfterGame = false;
        PersistLauncher(p => p.SelectSection(section));
    }

    private LinearLayout BuildLauncherNavigation(string selected)
    {
        var nav = LunetUi.Row(this, 2);
        nav.SetPadding(0, Dp(6), 0, 0);
        nav.ContentDescription = "Navegação principal";
        void Item(string id, string label, Action action)
        {
            var button = LunetUi.Button(this, label, action,
                id == selected ? ButtonStyle.Selected : ButtonStyle.Quiet,
                description: "Abrir " + label);
            button.TextSize = 12;
            button.SetPadding(0, 0, 0, 0);
            button.SetMinimumHeight(Dp(52));
            button.Selected = id == selected;
            nav.AddView(button);
        }
        Item("home", "Início", ShowHome);
        Item("projects", "Projetos", ShowProjects);
        Item("play", "Jogar", ShowGameLauncher);
        Item("studios", "Studios", ShowLauncherStudios);
        Item("explore", "Explorar", OpenInspirationExplorer);
        Item("learn", "Aprender", ShowLearning);
        return nav;
    }

    private View LauncherProjectCard(string name)
    {
        var card = LunetUi.Row(this, 10);
        card.SetPadding(Dp(12), Dp(12), Dp(8), Dp(12));
        card.Background = LunetUi.Pressable(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
        card.Clickable = true;
        card.ContentDescription = "Editar projeto " + name;
        card.Click += (_, _) => OpenProject(name);
        card.LongClick += (_, e) => { ShowProjectMenu(name); e.Handled = true; };
        var initial = LunetUi.Label(this, name.Length > 0 ? name[..1].ToUpperInvariant() : "?", 18,
            LunetUi.AccentText, bold: true);
        initial.Gravity = GravityFlags.Center;
        initial.Background = LunetUi.Shape(this, LunetUi.AccentSoft, LunetUi.Radius);
        card.AddView(initial, new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        var details = LunetUi.Column(this, 2);
        var title = LunetUi.Label(this, name, LunetUi.TextBody, LunetUi.Text, bold: true);
        title.SetSingleLine(true);
        title.Ellipsize = global::Android.Text.TextUtils.TruncateAt.End;
        details.AddView(title);
        details.AddView(LunetUi.Label(this, DescribeProject(name), LunetUi.TextCaption, LunetUi.TextMuted));
        card.AddView(details, LunetUi.Weight());
        if (_launcherPreferences is not null)
        {
            var pinned = IsLauncherPinned(name);
            var pin = LunetUi.Icon(this, pinned ? "★" : "☆",
                pinned ? "Desafixar " + name : "Fixar " + name, () => ToggleLauncherPin(name));
            card.AddView(pin, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        }
        card.AddView(LunetUi.Icon(this, "▶", "Jogar " + name, () => PlayFromLauncher(name)),
            new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        card.AddView(LunetUi.Icon(this, "⋮", "Opções de " + name, () => ShowProjectMenu(name)),
            new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        return card;
    }

    private void FillLauncherProjects(LinearLayout list, string? query)
    {
        list.RemoveAllViews();
        var all = _store.List();
        var matches = all.Where(name => string.IsNullOrWhiteSpace(query) ||
                name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(IsLauncherPinned)
            .ThenByDescending(ProjectTouched)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

        if (matches.Length == 0)
        {
            var message = all.Count == 0
                ? "Comece seu primeiro jogo. O projeto novo terá Game.cs estruturado e nenhum objeto de demonstração."
                : "Nenhum projeto corresponde à busca. Limpe o texto para visualizar os outros jogos.";
            var empty = LunetUi.Label(this, message, LunetUi.TextBody, LunetUi.TextMuted);
            empty.SetPadding(Dp(8), Dp(18), Dp(8), Dp(18));
            list.AddView(empty);
        }
        foreach (var name in matches)
            list.AddView(LauncherProjectCard(name), LunetUi.FillWidth());
    }

    private void AddHomeAction(LinearLayout list, string title, string description, Action action)
    {
        var card = LunetUi.Row(this, 10);
        card.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
        card.Background = LunetUi.Pressable(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
        card.Clickable = true;
        card.ContentDescription = title + ". " + description;
        card.Click += (_, _) => action();
        var labels = LunetUi.Column(this, 3);
        labels.AddView(LunetUi.Label(this, title, 16, LunetUi.Text, bold: true));
        labels.AddView(LunetUi.Label(this, description, LunetUi.TextLabel, LunetUi.TextMuted));
        card.AddView(labels, LunetUi.Weight());
        card.AddView(LunetUi.Label(this, "›", 24, LunetUi.TextMuted));
        list.AddView(card, LunetUi.FillWidth());
    }

    private void ShowHome()
    {
        EnterLauncherSurface("home");
        RefreshLauncherCatalog();
        var root = Vertical();
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(8));
        root.SetBackgroundColor(LunetUi.Background);
        var brand = LunetUi.Row(this, 8);
        brand.AddView(LunetUi.Label(this, "Lunet", LunetUi.TextTitle, LunetUi.AccentText, bold: true),
            LunetUi.Weight());
        brand.AddView(LunetUi.Icon(this, "⚙", "Configurações", ShowSettings),
            new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        root.AddView(brand);
        root.AddView(LunetUi.Label(this, "Área de trabalho", LunetUi.TextHero, LunetUi.Text, bold: true));
        var scroll = new ScrollView(this) { FillViewport = true };
        var content = LunetUi.Column(this, 12);
        content.SetPadding(0, Dp(16), 0, Dp(12));
        if (_launcherPreferenceIssue is not null)
            content.AddView(LunetUi.Label(this,
                "Preferências de início indisponíveis. O arquivo original foi preservado: " +
                _launcherPreferenceIssue, LunetUi.TextLabel, LunetUi.Warning));

        var names = _store.List();
        var lastId = _launcherPreferences?.LastProjectId;
        var last = lastId is not null
            ? LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, lastId)
            : _launcherPreferences?.LastProject;
        if (last is not null && !names.Contains(last, StringComparer.Ordinal)) last = null;
        if (last is not null)
        {
            content.AddView(LunetUi.Label(this, "CONTINUAR", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
            content.AddView(LunetUi.Label(this, last, LunetUi.TextBody, LunetUi.Text, bold: true));
            var resume = LunetUi.Row(this, 8);
            resume.AddView(LunetUi.Button(this, "Editar", () => OpenProject(last), ButtonStyle.Primary),
                new LinearLayout.LayoutParams(0, Dp(54), 1f));
            resume.AddView(LunetUi.Button(this, "▶ Jogar", () => PlayFromLauncher(last), ButtonStyle.Secondary),
                new LinearLayout.LayoutParams(0, Dp(54), 1f));
            content.AddView(resume);
        }
        else if (lastId is not null || _launcherPreferences?.LastProject is not null)
        {
            content.AddView(LunetUi.Label(this,
                "O último projeto não está disponível ou tem identidade duplicada. Seus arquivos não foram alterados; escolha outro jogo em Projetos.",
                LunetUi.TextBody, LunetUi.Warning));
            content.AddView(LunetUi.Button(this, "Abrir projetos", ShowProjects, ButtonStyle.Secondary),
                LunetUi.FillWidth(Dp(48)));
        }
        content.AddView(LunetUi.Button(this, "＋ Criar projeto", AskForProjectName,
            last is null ? ButtonStyle.Primary : ButtonStyle.Quiet), LunetUi.FillWidth(Dp(50)));

        var pinned = (_launcherPreferences?.PinnedProjectIds ?? [])
            .Select(id => LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, id))
            .Where(name => name is not null).Select(name => name!).Distinct(StringComparer.Ordinal).ToArray();
        var legacyPins = (_launcherPreferences?.PinnedProjects ?? [])
            .Where(name => names.Contains(name, StringComparer.Ordinal) && LauncherId(name) is null);
        pinned = pinned.Concat(legacyPins).Distinct(StringComparer.Ordinal).ToArray();

        if (pinned.Length > 0)
        {
            content.AddView(LunetUi.Label(this, "PROJETOS FIXADOS", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
            foreach (var name in pinned.Take(4))
                content.AddView(LauncherProjectCard(name), LunetUi.FillWidth());
        }

        var recent = (_launcherPreferences?.RecentProjectIds ?? [])
            .Select(id => LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, id))
            .Where(name => name is not null).Select(name => name!)
            .Where(name => !pinned.Contains(name, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).Take(3).ToArray();
        if (recent.Length == 0)
            recent = names.Where(name => !pinned.Contains(name, StringComparer.Ordinal))
                .OrderByDescending(ProjectTouched).Take(3).ToArray();
        content.AddView(LunetUi.Label(this, "PROJETOS RECENTES", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        if (recent.Length == 0 && pinned.Length == 0)
            content.AddView(LunetUi.Label(this, "Ainda não há projetos. Crie um jogo para começar.",
                LunetUi.TextBody, LunetUi.TextMuted));
        foreach (var name in recent)
            content.AddView(LauncherProjectCard(name), LunetUi.FillWidth());

        if (_settings.UseSharedProjectFolder)
        {
            var storageTip = _settings.ProjectWorkingCopy is { } copy && Directory.Exists(WorkingCopy(copy))
                ? "Grave uma revisão para levar as alterações à pasta escolhida"
                : "Cópia de trabalho ausente. Recupere uma revisão ou retome os projetos internos.";
            AddHomeAction(content, "Pasta de projetos", storageTip, ShowProjectFolder);
        }

        content.AddView(LunetUi.Label(this, "FERRAMENTAS", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        AddHomeAction(content, "Biblioteca pessoal", "Assets versionados e reutilizáveis entre jogos", ShowReusableAssetLibrary);
        AddHomeAction(content, "Lunet IA", "Chat global e configurações de IA; separado dos projetos", ShowWorkspaceAi);
        AddHomeAction(content, "Documentação", "API, exemplos e guias disponíveis offline", ShowReference);
        content.AddView(LunetUi.Button(this, "Importar projeto ZIP", ImportProject, ButtonStyle.Quiet));
        scroll.AddView(content);
        root.AddView(scroll, Fill(1));
        SetLauncherContent(root, "home", scroll);
    }

    /// <summary>Studios do Lunet não exigem escolher nem abrir um jogo.</summary>
    private void ShowLauncherStudios()
    {
        EnterLauncherSurface("studios");
        var root = Vertical();
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(8));
        root.SetBackgroundColor(LunetUi.Background);
        root.AddView(LunetUi.Label(this, "Studios", LunetUi.TextHero, LunetUi.Text, bold: true));
        root.AddView(LunetUi.Label(this,
            "Crie e edite sem projeto aberto. Guarde no Studio, publique na Biblioteca ou envie uma cópia para qualquer jogo.",
            LunetUi.TextBody, LunetUi.TextMuted));
        var scroll = new ScrollView(this);
        var list = LunetUi.Column(this, 10);
        list.SetPadding(0, Dp(16), 0, Dp(8));
        AddHomeAction(list, "Sprite Studio", "Pixel art editável · salvar PNG e fonte na biblioteca", NewStandaloneSprite);
        AddHomeAction(list, "Music Studio", "Compor e exportar música sem projeto aberto", NewStandaloneMusic);
        AddHomeAction(list, "Sound Designer", "Efeitos sintetizados e editáveis", () =>
            OpenSoundDesigner(null, Lunet.Studio.Music.SoundDesigner.Generate("coin", Environment.TickCount)));
        AddHomeAction(list, "Tile Studio", "Mapas em camadas e tilesets editáveis", NewStandaloneTileMap);
        AddHomeAction(list, "Paleta de cores", "Cores nomeadas e exportação GPL", NewStandalonePalette);
        AddHomeAction(list, "Particle Studio", "Presets editáveis e prévia de partículas", NewStandaloneParticles);
        AddHomeAction(list, "Data Studio", "Edite tabelas e esquemas do jogo", NewStandaloneData);
        AddHomeAction(list, "Editor WAV", "Crie um tom e edite o áudio em um rascunho", () =>
            OpenWaveAudio(Lunet.Studio.WaveAudioDocument.CreateEffect(440, 0.5, Lunet.Studio.WaveShape.Sine), "audio", StandaloneWorkspace));
        AddHomeAction(list, "Minhas criações", "Retomar rascunhos e publicar para reutilizar", ShowStandaloneDrafts);
        AddHomeAction(list, "Biblioteca pessoal", "Versões de assets · Usar em qualquer projeto · exportar ZIP", ShowReusableAssetLibrary);
        scroll.AddView(list);
        root.AddView(scroll, Fill(1));
        SetLauncherContent(root, "studios", scroll);
     }
}
