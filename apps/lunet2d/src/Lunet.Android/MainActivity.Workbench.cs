using Android.App;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Compiler;
using Lunet.Core;
using AndroidColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Mobile-first workbench navigation (LUNET-470, redesigned in LUNET-483): one top bar, a code-keys bar above the
/// keyboard, a compact bottom navigation hidden while typing, and a palette that finds commands and files.
/// </summary>
public sealed partial class MainActivity
{
    private readonly List<(string Key, Button Button)> _workbenchNavButtons = [];
    private View? _drawerScrim;
    private View? _extraKeys;
    private View? _workbenchNav;
    private bool _keyboardVisible;

    // Kept as aliases so older surfaces pick up the same tokens.
    private static readonly AndroidColor WorkbenchBackground = LunetUi.Background;
    private static readonly AndroidColor WorkbenchSurface = LunetUi.Surface;
    private static readonly AndroidColor WorkbenchBorder = LunetUi.Border;
    private static readonly AndroidColor WorkbenchAccent = LunetUi.Accent;
    private static readonly AndroidColor WorkbenchText = LunetUi.Text;
    private static readonly AndroidColor WorkbenchMuted = LunetUi.TextMuted;

    private Button WorkbenchButton(string title, Action action, bool emphasized = false)
    {
        var button = LunetUi.Button(this, title, action, emphasized ? ButtonStyle.Primary : ButtonStyle.Secondary);
        button.SetMinHeight(Dp(48));
        button.SetMinimumHeight(Dp(48));
        return button;
    }

    private void StyleWorkbenchButton(Button button, bool emphasized) =>
        LunetUi.Style(this, button, emphasized ? ButtonStyle.Selected : ButtonStyle.Quiet);

    private TextView WorkbenchLabel(string content, float size = 14, bool strong = false) =>
        LunetUi.Label(this, content, size, strong ? LunetUi.Text : LunetUi.TextMuted, bold: strong);

    private LinearLayout BuildWorkbenchNavigation()
    {
        _workbenchNavButtons.Clear();
        var nav = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        nav.SetGravity(GravityFlags.CenterVertical);
        nav.SetPadding(Dp(6), Dp(4), Dp(6), Dp(6));
        nav.SetBackgroundColor(LunetUi.Background);

        void Add(string key, string label, string description, Action action)
        {
            var button = LunetUi.Button(this, label, action, ButtonStyle.Quiet, description: description);
            button.TextSize = 11;
            button.SetSingleLine(true);
            button.SetPadding(Dp(2), Dp(4), Dp(2), Dp(2));
            var p = new LinearLayout.LayoutParams(0, Dp(52), 1f);
            p.SetMargins(Dp(2), 0, Dp(2), 0);
            nav.AddView(button, p);
            _workbenchNavButtons.Add((key, button));
        }

        Add("code", "⌨\nCódigo", "Código", FocusWorkbenchCode);
        Add("files", "▤\nArquivos", "Arquivos do projeto", ToggleExplorer);
        Add("studio", "◧\nStudio", "Studios de sprite, tile e áudio", ShowStudioHub);
        Add("problems", "⚠\nErros", "Erros de compilação", () => OpenWorkbenchPanel(false));
        Add("console", "❯\nConsole", "Console do jogo", () => OpenWorkbenchPanel(true));
        _workbenchNav = nav;
        return nav;
    }

    private void RefreshWorkbenchNavigation()
    {
        if (_workbenchNavButtons.Count == 0) return;
        var active = _drawer?.Visibility == ViewStates.Visible ? "files"
            : _layoutStore.Current.ShowPanel && _layoutStore.Current.PanelTab == "console" ? "console"
            : _layoutStore.Current.ShowPanel && _layoutStore.Current.PanelTab == "problems" ? "problems"
            : "code";
        var errors = _problems.Count(p => p.Severity == DiagnosticSeverity.Error);
        foreach (var (key, button) in _workbenchNavButtons)
        {
            StyleWorkbenchButton(button, key == active);
            if (key == "problems") LunetUi.SetLabel(button, errors > 0 ? $"⚠\nErros {errors}" : "⚠\nErros");
        }
    }

    // ---------- Code keys above the keyboard ----------

    /// <summary>
    /// Symbols that are slow to reach on phone keyboards, plus undo/redo/find. Buttons never take focus,
    /// so the keyboard stays open (Acode #2374, Godot proposals #7639).
    /// </summary>
    private View BuildExtraKeys()
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        row.SetPadding(Dp(4), Dp(3), Dp(4), Dp(3));
        void Key(string label, string description, Action action)
        {
            var key = LunetUi.Button(this, label, action, ButtonStyle.Secondary, keepsKeyboard: true, description: description);
            key.TextSize = 15;
            key.SetTypeface(LunetUi.Mono, global::Android.Graphics.TypefaceStyle.Normal);
            key.SetPadding(Dp(10), 0, Dp(10), 0);
            var p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(40));
            p.SetMargins(Dp(2), 0, Dp(2), 0);
            row.AddView(key, p);
        }
        Key("⇥", "Tab", () => InsertCode("    "));
        Key("↶", "Desfazer", () => _editor?.Undo());
        Key("↷", "Refazer", () => _editor?.Redo());
        foreach (var symbol in new[] { "{", "}", "(", ")", ";", "=", "\"", ".", ",", "<", ">", "[", "]", "+", "-", "*", "/", "!", "&", "|", "_" })
            Key(symbol, symbol, () => InsertCode(symbol));
        Key("←", "Cursor para a esquerda", () => MoveCaret(-1));
        Key("→", "Cursor para a direita", () => MoveCaret(1));
        Key("⌕", "Localizar no arquivo", ShowFind);
        var scroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false, Visibility = ViewStates.Gone };
        scroll.SetBackgroundColor(LunetUi.Surface);
        scroll.AddView(row);
        _extraKeys = scroll;
        return scroll;
    }

    private void InsertCode(string text)
    {
        if (_editor is null) return;
        int start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
        int end = Math.Max(_editor.SelectionStart, _editor.SelectionEnd);
        if (start < 0) return;
        _editor.Replace(start, end - start, text);
    }

    private void MoveCaret(int delta)
    {
        if (_editor is null) return;
        var length = _editor.Text?.Length ?? 0;
        var target = Math.Clamp(_editor.SelectionEnd + delta, 0, length);
        _editor.SetSelection(target);
    }

    /// <summary>While typing: show the code keys and hide the bottom navigation, giving the code the space.</summary>
    private void WatchKeyboard(View root)
    {
        root.ViewTreeObserver!.GlobalLayout += (_, _) =>
        {
            var visible = new global::Android.Graphics.Rect();
            root.GetWindowVisibleDisplayFrame(visible);
            var screen = root.RootView!.Height;
            bool keyboard = screen - visible.Bottom > screen * 0.18;
            if (keyboard == _keyboardVisible) return;
            _keyboardVisible = keyboard;
            bool editing = keyboard && _editor?.HasFocus == true;
            if (_extraKeys is not null) _extraKeys.Visibility = editing ? ViewStates.Visible : ViewStates.Gone;
            if (_workbenchNav is not null) _workbenchNav.Visibility = keyboard ? ViewStates.Gone : ViewStates.Visible;
        };
    }

    private void FocusWorkbenchCode()
    {
        if (_drawer is not null) _drawer.Visibility = ViewStates.Gone;
        if (_drawerScrim is not null) _drawerScrim.Visibility = ViewStates.Gone;
        _layoutStore.Current.FocusCode();
        ApplyWorkspaceLayout();
        SaveLayouts();
        RefreshWorkbenchNavigation();
        _editor?.RequestFocus();
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

    /// <summary>Studios are part of the project (TIC-80 model): one sheet lists them with what they edit.</summary>
    private void ShowStudioHub()
    {
        if (_project is null) return;
        var sheet = LunetUi.Column(this, 10);
        sheet.SetPadding(Dp(16), Dp(8), Dp(16), Dp(8));
        AlertDialog? dialog = null;
        void Card(string glyph, string title, string subtitle, Action open)
        {
            var card = LunetUi.Row(this, 12);
            card.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
            card.Background = LunetUi.Pressable(this, LunetUi.SurfaceRaised, LunetUi.Radius, LunetUi.Border);
            card.Clickable = true;
            card.Click += (_, _) => { dialog?.Dismiss(); open(); };
            card.AddView(LunetUi.IconImage(this, glyph, 26, LunetUi.AccentText), new LinearLayout.LayoutParams(Dp(34), Dp(34)));
            var text = LunetUi.Column(this, 2);
            text.AddView(LunetUi.Label(this, title, LunetUi.TextBody, LunetUi.Text, bold: true));
            text.AddView(LunetUi.Label(this, subtitle, LunetUi.TextCaption, LunetUi.TextMuted));
            card.AddView(text, LunetUi.Weight());
            sheet.AddView(card, LunetUi.FillWidth());
        }
        var count = _project.ListFiles();
        Card("sprite", "Sprites e animação", $"{count.Count(f => f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))} sprite(s) · pixel art, frames, onion skin", ShowSpriteAssets);
        Card("tilemap", "Tilemaps", $"{count.Count(f => f.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase))} mapa(s) · camadas, colisão, pincel", ShowArtTools);
        Card("waveform", "Áudio", "WAV: forma de onda, corte, loop, efeitos", ShowAudioStudio);
        Card("palette", "Paleta de cores", File.Exists(System.IO.Path.Combine(_project.Directory, Lunet.Studio.ProjectPalette.DefaultPath))
            ? "Cores oficiais do jogo · sprites, tiles, UI e código" : "Defina as cores do jogo para manter tudo coerente", ShowPaletteStudio);
        Card("sparkle", "Partículas", $"{count.Count(f => f.EndsWith(".particles.json", StringComparison.OrdinalIgnoreCase))} efeito(s) · fogo, fumaça, chuva, magia…", ShowParticleStudio);
        Card("data", "Dados", $"{count.Count(IsDataTable)} tabela(s) · inimigos, itens, fases, diálogos", ShowDataStudio);
        Card("folder", "Assets do projeto", "Imagens, sons e dados com prévia e uso no código", ShowAssets);
        Card("note", "Notas e esboços", "Ideias, roteiros e esquemas dentro do projeto", ShowNotes);
        var scroll = new ScrollView(this);
        scroll.AddView(sheet);
        dialog = new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Studio")!
            .SetView(scroll)!
            .SetNegativeButton("Fechar", (_, _) => { })!
            .Create();
        dialog.Show();
    }

    /// <summary>One search box for commands and project files.</summary>
    private void ShowCommandPalette()
    {
        var commands = new (string Label, Action Execute)[]
        {
            ("✦ IA · conversar sobre o código", () => OpenAiPanel(null)),
            ("✦ IA · configurar provedor, modelo e chave", () => ShowAiSettings()),
            ("▶ Rodar o jogo", Run),
            ("Buscar texto no projeto", ShowProjectSearch),
            ("Localizar e substituir", ShowFind),
            ("Ir para definição", GoToDefinition),
            ("Estrutura do arquivo", ShowOutline),
            ("Salvar arquivo", SaveCurrent),
            ("Formatar documento", () => FormatDocument(silent: false)),
            ("Desfazer", () => _editor?.Undo()),
            ("Refazer", () => _editor?.Redo()),
            ("Erros de compilação", () => OpenWorkbenchPanel(false)),
            ("Console do jogo", () => OpenWorkbenchPanel(true)),
            ("Studio · sprites, tilemaps, áudio", ShowStudioHub),
            ("Notas e esboços do projeto", ShowNotes),
            ("Assets do projeto", ShowAssets),
            ("Git", ShowGit),
            ("Gerar APK do jogo", AskBuildGameApk),
            ("Documentação do Lunet", ShowReference),
            ("Layout do workspace", ShowLayoutDialog),
            ("Configurações do editor", ShowSettings),
            ("Atalhos de teclado", ShowShortcutHelp),
            ("Menu completo (avançado)", ShowMenu),
        };
        var files = (_project?.ListFiles() ?? []).Where(f => !f.StartsWith(".lunet", StringComparison.Ordinal)).ToArray();
        var entries = commands.Select(c => (Label: c.Label, Run: c.Execute))
            .Concat(files.Select(f => (Label: "▤ " + f, Run: (Action)(() => OpenProjectEntry(f)))))
            .ToArray();
        var names = entries.Select(e => e.Label).ToArray();

        var content = LunetUi.Column(this, 8);
        content.SetPadding(Dp(14), Dp(6), Dp(14), Dp(6));
        var search = LunetUi.Input(this, "Buscar ação ou arquivo…");
        search.ContentDescription = "Buscar ações e arquivos do Lunet";
        content.AddView(search);
        var list = new ListView(this) { Divider = null };
        var adapter = new ThemedListAdapter(this, names.Take(60).ToList());
        list.Adapter = adapter;
        content.AddView(list, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, (int)(Resources!.DisplayMetrics!.HeightPixels * 0.5f)));
        var dialog = new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetView(content)!
            .Create();
        search.TextChanged += (_, _) =>
        {
            var filtered = WorkbenchCommands.Filter(names, search.Text);
            adapter.Clear();
            adapter.AddAll(filtered.Take(60).ToList());
            adapter.NotifyDataSetChanged();
        };
        search.EditorAction += (_, e) =>
        {
            if (adapter.Count == 0) return;
            var first = adapter.GetItem(0);
            dialog.Dismiss();
            entries.FirstOrDefault(x => x.Label == first).Run?.Invoke();
            e.Handled = true;
        };
        list.ItemClick += (_, args) =>
        {
            var selected = adapter.GetItem(args.Position);
            dialog.Dismiss();
            entries.FirstOrDefault(x => x.Label == selected).Run?.Invoke();
        };
        dialog.Show();
        search.RequestFocus();
        dialog.Window?.SetSoftInputMode(SoftInput.StateVisible);
    }

    private void OpenProjectEntry(string path)
    {
        if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && path.StartsWith(NotesFolder + "/", StringComparison.Ordinal))
                OpenNote(path);
            else OpenFile(path);
        }
        else OnExplorerTap(new ExplorerEntry(path, System.IO.Path.GetFileName(path), 0, false));
    }

    /// <summary>List rows that follow LunetUi tokens (the stock row uses the light theme's dark text).</summary>
    private sealed class ThemedListAdapter(MainActivity owner, List<string> items) : ArrayAdapter<string>(owner, 0, items)
    {
        public override View GetView(int position, View? convertView, ViewGroup parent)
        {
            var label = convertView as TextView ?? new TextView(owner) { TextSize = LunetUi.TextBody };
            var text = GetItem(position) ?? "";
            label.Text = text;
            label.SetTextColor(text.StartsWith("▤", StringComparison.Ordinal) ? LunetUi.TextMuted : LunetUi.Text);
            label.SetPadding(owner.Dp(12), owner.Dp(13), owner.Dp(12), owner.Dp(13));
            label.SetSingleLine(true);
            label.Ellipsize = TextUtils.TruncateAt.Middle;
            label.Background = LunetUi.Pressable(owner, AndroidColor.Transparent, LunetUi.RadiusSmall);
            return label;
        }
    }
}
