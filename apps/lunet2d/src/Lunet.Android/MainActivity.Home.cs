using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// Home mobile-first (LUNET-510). A shell sem projeto usa os serviços existentes e mantém o trabalho da IA
/// independente das telas. O Studio ainda precisa de projeto; Library global é responsabilidade do LUNET-511.
/// </summary>
public sealed partial class MainActivity
{
    private LauncherPreferences? _launcherPreferences;
    private string? _launcherPreferenceIssue;
    private string _launcherSection = "home";
    private string _projectFilter = "";

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
            case "studios": ShowLauncherStudios(); break;
            case "explore": OpenInspirationExplorer(); break;
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
        _inspirationOpen = false;
        _launcherSection = section;
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
            button.SetSingleLine(true);
            nav.AddView(button, new LinearLayout.LayoutParams(0, Dp(52), 1f));
        }
        Item("home", "Início", ShowHome);
        Item("projects", "Projetos", ShowProjects);
        Item("studios", "Studios", ShowLauncherStudios);
        Item("explore", "Explorar", OpenInspirationExplorer);
        return nav;
    }

    private View LauncherProjectCard(string name)
    {
        var card = LunetUi.Row(this, 10);
        card.SetPadding(Dp(12), Dp(12), Dp(8), Dp(12));
        card.Background = LunetUi.Pressable(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
        card.Clickable = true;
        card.ContentDescription = "Abrir projeto " + name;
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
        title.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        details.AddView(title);
        details.AddView(LunetUi.Label(this, DescribeProject(name), LunetUi.TextCaption, LunetUi.TextMuted));
        card.AddView(details, LunetUi.Weight());
        if (_launcherPreferences is not null)
        {
            var pinned = _launcherPreferences.IsPinned(name);
            var pin = LunetUi.Icon(this, pinned ? "★" : "☆", pinned ? "Desafixar " + name : "Fixar " + name, () =>
            {
                PersistLauncher(p => p.SetPinned(name, !p.IsPinned(name)));
                if (_launcherSection == "home") ShowHome();
                else ShowProjects();
            });
            card.AddView(pin, new LinearLayout.LayoutParams(Dp(44), Dp(48)));
        }
        card.AddView(LunetUi.Icon(this, "⋮", "Opções de " + name, () => ShowProjectMenu(name)),
            new LinearLayout.LayoutParams(Dp(44), Dp(48)));
        return card;
    }

    private void FillLauncherProjects(LinearLayout list, string? query)
    {
        list.RemoveAllViews();
        var all = _store.List();
        var matches = all.Where(name => string.IsNullOrWhiteSpace(query) ||
                name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(name => _launcherPreferences?.IsPinned(name) == true)
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
        card.AddView(LunetUi.IconImage(this, "arrow-right", 20));
        list.AddView(card, LunetUi.FillWidth());
    }

    private void ShowHome()
    {
        EnterLauncherSurface("home");
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
        root.AddView(LunetUi.Label(this,
            "Seus projetos, ferramentas e referências. Nada é aberto automaticamente.",
            LunetUi.TextLabel, LunetUi.TextMuted));
        var scroll = new ScrollView(this) { FillViewport = true };
        var content = LunetUi.Column(this, 12);
        content.SetPadding(0, Dp(16), 0, Dp(12));
        if (_launcherPreferenceIssue is not null)
            content.AddView(LunetUi.Label(this,
                "Preferências de início indisponíveis. O arquivo original foi preservado: " +
                _launcherPreferenceIssue, LunetUi.TextLabel, LunetUi.Warning));

        var names = _store.List();
        var last = _launcherPreferences?.LastProject;
        if (last is not null && names.Contains(last, StringComparer.Ordinal))
            content.AddView(LunetUi.Button(this, "Continuar: " + last, () => OpenProject(last),
                ButtonStyle.Primary), LunetUi.FillWidth(Dp(54)));
        else
            content.AddView(LunetUi.Button(this, "＋ Criar projeto", AskForProjectName,
                ButtonStyle.Primary), LunetUi.FillWidth(Dp(54)));

        var pinned = _launcherPreferences?.PinnedProjects
            .Where(n => names.Contains(n, StringComparer.Ordinal)).ToArray() ?? [];
        content.AddView(LunetUi.Label(this, pinned.Length > 0 ? "PROJETOS FIXADOS" : "PROJETOS RECENTES",
            LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        var cards = pinned.Length > 0 ? pinned : names.OrderByDescending(ProjectTouched).Take(3).ToArray();
        if (cards.Length == 0)
            content.AddView(LunetUi.Label(this, "Ainda não há projetos. Crie um jogo para começar.",
                LunetUi.TextBody, LunetUi.TextMuted));
        foreach (var name in cards)
            content.AddView(LauncherProjectCard(name), LunetUi.FillWidth());

        content.AddView(LunetUi.Label(this, "FERRAMENTAS", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        AddHomeAction(content, "Projetos", "Pesquisar, fixar, criar, importar e exportar", ShowProjects);
        AddHomeAction(content, "Studios", "Sprite, tilemap, áudio, paleta, partículas e dados", ShowLauncherStudios);
        AddHomeAction(content, "Explorar", "Ideias, gêneros e mecânicas para seus jogos", OpenInspirationExplorer);
        AddHomeAction(content, "Documentação", "API, exemplos e guias disponíveis offline", ShowReference);
        AddHomeAction(content, "Configurações", "Preferências do ambiente e do editor", ShowSettings);
        content.AddView(LunetUi.Button(this, "Importar projeto ZIP", ImportProject, ButtonStyle.Quiet));
        scroll.AddView(content);
        root.AddView(scroll, Fill(1));
        root.AddView(BuildLauncherNavigation("home"));
        SetContentView(root);
    }

    /// <summary>
    /// Enquanto o LUNET-511 não separar os workspaces, a navegação seleciona um projeto real.
    /// Não apresenta uma falsa Library global, nem cria um projeto temporário escondido.
    /// </summary>
    private void ShowLauncherStudios()
    {
        EnterLauncherSurface("studios");
        var root = Vertical();
        root.SetPadding(Dp(16), Dp(16), Dp(16), Dp(8));
        root.SetBackgroundColor(LunetUi.Background);
        root.AddView(LunetUi.Label(this, "Studios", LunetUi.TextHero, LunetUi.Text, bold: true));
        root.AddView(LunetUi.Label(this,
            "Escolha o projeto para abrir os Studios existentes. A biblioteca independente será integrada separadamente.",
            LunetUi.TextBody, LunetUi.TextMuted));
        var scroll = new ScrollView(this);
        var list = LunetUi.Column(this, 10);
        list.SetPadding(0, Dp(16), 0, Dp(8));
        var names = _store.List().OrderByDescending(name => _launcherPreferences?.IsPinned(name) == true)
            .ThenByDescending(ProjectTouched).ToArray();
        foreach (var name in names)
        {
            var selected = name;
            AddHomeAction(list, selected, "Abrir ferramentas de criação deste projeto", () =>
            {
                OpenProject(selected);
                if (_project is not null) ShowStudioHub();
            });
        }
        if (names.Length == 0)
        {
            list.AddView(LunetUi.Label(this, "Para usar os Studios atuais, crie um projeto.",
                LunetUi.TextBody, LunetUi.TextMuted));
            list.AddView(LunetUi.Button(this, "＋ Criar projeto", AskForProjectName, ButtonStyle.Primary),
                LunetUi.FillWidth(Dp(52)));
        }
        scroll.AddView(list);
        root.AddView(scroll, Fill(1));
        root.AddView(BuildLauncherNavigation("studios"));
        SetContentView(root);
    }
}
