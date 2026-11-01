using Android.App;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using AndroidColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>Camada de navegação mobile-first. Não modifica o editor, compilador ou Studio.</summary>
public sealed partial class MainActivity
{
    private readonly List<(string Key, Button Button)> _workbenchNavButtons = [];
    private View? _drawerScrim;

    private static readonly AndroidColor WorkbenchBackground = AndroidColor.Rgb(14, 18, 27);
    private static readonly AndroidColor WorkbenchSurface = AndroidColor.Rgb(27, 34, 47);
    private static readonly AndroidColor WorkbenchBorder = AndroidColor.Rgb(55, 65, 81);
    private static readonly AndroidColor WorkbenchAccent = AndroidColor.Rgb(73, 111, 236);
    private static readonly AndroidColor WorkbenchText = AndroidColor.Rgb(235, 240, 250);
    private static readonly AndroidColor WorkbenchMuted = AndroidColor.Rgb(155, 169, 188);

    private Button WorkbenchButton(string title, Action action, bool emphasized = false)
    {
        var button = MakeButton(title, action);
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.SetMinHeight(Dp(48));
        button.SetMinimumHeight(Dp(48));
        button.SetPadding(Dp(8), 0, Dp(8), 0);
        button.TextSize = 13;
        button.ContentDescription = title;
        StyleWorkbenchButton(button, emphasized);
        return button;
    }

    private void StyleWorkbenchButton(Button button, bool emphasized)
    {
        var background = new GradientDrawable();
        background.SetColor(emphasized ? WorkbenchAccent : WorkbenchSurface);
        background.SetCornerRadius(Dp(12));
        background.SetStroke(Dp(1), emphasized ? WorkbenchAccent : WorkbenchBorder);
        button.Background = background;
        button.SetTextColor(WorkbenchText);
        button.Elevation = 0;
    }

    private TextView WorkbenchLabel(string content, float size = 14, bool strong = false)
    {
        var label = new TextView(this)
        {
            Text = content,
            TextSize = size,
            Gravity = GravityFlags.CenterVertical,
        };
        label.SetTextColor(strong ? WorkbenchText : WorkbenchMuted);
        return label;
    }

    private LinearLayout BuildWorkbenchNavigation()
    {
        _workbenchNavButtons.Clear();
        var nav = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        nav.SetGravity(GravityFlags.CenterVertical);
        nav.SetPadding(Dp(5), Dp(4), Dp(5), Dp(4));
        nav.SetBackgroundColor(WorkbenchBackground);

        void Add(string key, string label, Action action)
        {
            var button = WorkbenchButton(label, action);
            var p = new LinearLayout.LayoutParams(0, Dp(50), 1f);
            p.SetMargins(Dp(2), 0, Dp(2), 0);
            nav.AddView(button, p);
            _workbenchNavButtons.Add((key, button));
        }

        Add("code", "Código", FocusWorkbenchCode);
        Add("files", "Arquivos", ToggleExplorer);
        Add("problems", "Erros", () => OpenWorkbenchPanel(false));
        Add("console", "Console", () => OpenWorkbenchPanel(true));
        Add("tools", "Ações", ShowCommandPalette);
        return nav;
    }

    private void RefreshWorkbenchNavigation()
    {
        if (_workbenchNavButtons.Count == 0) return;
        var active = _drawer?.Visibility == ViewStates.Visible ? "files"
            : _layoutStore.Current.ShowPanel && _layoutStore.Current.PanelTab == "console" ? "console"
            : _layoutStore.Current.ShowPanel && _layoutStore.Current.PanelTab == "problems" ? "problems"
            : "code";
        foreach (var (key, button) in _workbenchNavButtons)
            StyleWorkbenchButton(button, key == active);
    }

    private void FocusWorkbenchCode()
    {
        if (_drawer is not null) _drawer.Visibility = ViewStates.Gone;
        if (_drawerScrim is not null) _drawerScrim.Visibility = ViewStates.Gone;
        _layoutStore.Current.FocusCode();
        ApplyWorkspaceLayout();
        SaveLayouts();
        RefreshWorkbenchNavigation();
    }

    private void OpenWorkbenchPanel(bool console)
    {
        if (_drawer is not null) _drawer.Visibility = ViewStates.Gone;
        if (_drawerScrim is not null) _drawerScrim.Visibility = ViewStates.Gone;
        _layoutStore.Current.OpenPanel(console ? "console" : "problems");
        SetPanel(console);
        ApplyWorkspaceLayout();
        SaveLayouts();
        RefreshWorkbenchNavigation();
    }

    /// <summary>Comandos pesquisáveis com rótulos completos, sem esconder ações atrás de ícones ambíguos.</summary>
    private void ShowCommandPalette()
    {
        var commands = new (string Label, Action Execute)[]
        {
            ("Abrir arquivos", ToggleExplorer),
            ("Buscar no projeto", ShowProjectSearch),
            ("Localizar e substituir", ShowFind),
            ("Ir para definição", GoToDefinition),
            ("Estrutura do arquivo", ShowOutline),
            ("Salvar arquivo", SaveCurrent),
            ("Formatar documento", () => FormatDocument(silent: false)),
            ("Desfazer", () => _editor?.Undo()),
            ("Refazer", () => _editor?.Redo()),
            ("Executar jogo", Run),
            ("Problemas de compilação", () => OpenWorkbenchPanel(false)),
            ("Console do jogo", () => OpenWorkbenchPanel(true)),
            ("Assets do projeto", ShowAssets),
            ("Sprite e Tile Studio", ShowArtTools),
            ("Audio Studio", ShowAudioStudio),
            ("Assistente e ferramentas", ShowToolsMenu),
            ("Git", ShowGit),
            ("Gerar APK do jogo", AskBuildGameApk),
            ("Documentação", ShowReference),
            ("Layout do workspace", ShowLayoutDialog),
            ("Configurações", ShowSettings),
            ("Menu completo", ShowMenu),
        };
        var names = commands.Select(x => x.Label).ToArray();
        var search = new EditText(this) { Hint = "Pesquisar ação…" };
        search.SetSingleLine(true);
        search.ContentDescription = "Pesquisar comandos do Lunet";

        var list = new ListView(this);
        var adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleListItem1, names);
        list.Adapter = adapter;
        var content = Vertical();
        content.SetPadding(Dp(12), Dp(4), Dp(12), Dp(8));
        content.AddView(search);
        content.AddView(list, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(340)));
        var dialog = new AlertDialog.Builder(this)!
            .SetTitle("Ações rápidas")!
            .SetView(content)!
            .SetNegativeButton("Fechar", (_, _) => { })!
            .Create();

        search.TextChanged += (_, _) =>
        {
            var filtered = WorkbenchCommands.Filter(names, search.Text);
            adapter.Clear();
            foreach (var name in filtered) adapter.Add(name);
            adapter.NotifyDataSetChanged();
        };
        list.ItemClick += (_, args) =>
        {
            var selected = adapter.GetItem(args.Position);
            var command = commands.FirstOrDefault(x => x.Label == selected);
            dialog.Dismiss();
            command.Execute?.Invoke();
        };
        dialog.Show();
    }
}
