using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>Disposição dos painéis do workspace: encaixe, tamanho, visibilidade e layouts salvos.</summary>
public sealed partial class MainActivity
{
    private LayoutStore _layoutStore = null!;
    private LinearLayout? _body;
    private LinearLayout? _editorColumn;
    private LinearLayout? _panelColumn;
    private SplitterView? _splitter;

    private void LoadLayouts()
    {
        _layoutStore = new LayoutStore(System.IO.Path.Combine(FilesDir!.AbsolutePath, "layouts.json"));
        _layoutStore.Load();
    }

    private bool IsLandscape => Resources?.Configuration?.Orientation == global::Android.Content.Res.Orientation.Landscape;

    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (_body is not null) ApplyWorkspaceLayout();
    }

    /// <summary>Monta o corpo do workspace (editor + painel inferior + divisória) na orientação e proporção do layout atual.</summary>
    private LinearLayout BuildBody(View editorHost, View chips, View tabs, View panel)
    {
        _editorColumn = Vertical();
        _editorColumn.AddView(editorHost, Fill(1));
        _editorColumn.AddView(chips, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        _panelColumn = Vertical();
        _panelColumn.AddView(tabs);
        _panelColumn.AddView(panel, Fill(1));

        _body = new LinearLayout(this);
        _splitter = new SplitterView(this, _body);
        _splitter.Resized += (fraction, done) =>
        {
            _layoutStore.Current.PanelFraction = fraction;
            _layoutStore.Current.Normalized();
            ApplyWorkspaceLayout();
            if (done) SaveLayouts();
        };
        _body.AddView(_editorColumn);
        _body.AddView(_splitter);
        _body.AddView(_panelColumn);
        ApplyWorkspaceLayout();
        return _body;
    }

    private void ApplyWorkspaceLayout()
    {
        if (_body is null || _editorColumn is null || _panelColumn is null || _splitter is null) return;
        var layout = _layoutStore.Current;
        var right = layout.EffectiveDock(IsLandscape) == PanelDock.Right;
        var fraction = (float)layout.PanelFraction;

        _body.Orientation = right ? Orientation.Horizontal : Orientation.Vertical;
        var visibility = layout.ShowPanel ? ViewStates.Visible : ViewStates.Gone;
        _panelColumn.Visibility = visibility;
        _splitter.Visibility = visibility;
        _splitter.Horizontal = right;

        _editorColumn.LayoutParameters = right
            ? new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, layout.ShowPanel ? 1 - fraction : 1f)
            : new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, layout.ShowPanel ? 1 - fraction : 1f);
        _panelColumn.LayoutParameters = right
            ? new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, fraction)
            : new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, fraction);
        _splitter.LayoutParameters = right
            ? new LinearLayout.LayoutParams(Dp(16), ViewGroup.LayoutParams.MatchParent)
            : new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(16));

        if (_drawer?.LayoutParameters is FrameLayout.LayoutParams drawerParams && drawerParams.Width != Dp(layout.ExplorerWidthDp))
        {
            drawerParams.Width = Dp(layout.ExplorerWidthDp);
            _drawer.LayoutParameters = drawerParams;
        }
        _body.RequestLayout();
    }

    private void SaveLayouts()
    {
        try { _layoutStore.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível salvar o layout: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void ShowLayoutDialog()
    {
        var draft = _layoutStore.Current.Clone();
        var form = Vertical();
        form.SetPadding(Dp(16), Dp(8), Dp(16), 0);

        var show = new CheckBox(this) { Text = "Mostrar painel de Problemas / Console", Checked = draft.ShowPanel };
        show.CheckedChange += (_, args) => draft.ShowPanel = args.IsChecked;
        form.AddView(show);

        form.AddView(new TextView(this) { Text = "Posição do painel", TextSize = 13 });
        var dock = new RadioGroup(this) { Orientation = Orientation.Horizontal };
        var options = new[] { (PanelDock.Auto, "Automática"), (PanelDock.Bottom, "Embaixo"), (PanelDock.Right, "À direita") };
        foreach (var (value, label) in options)
        {
            var radio = new RadioButton(this) { Text = label, Id = View.GenerateViewId() };
            radio.Tag = new Java.Lang.String(value.ToString());
            dock.AddView(radio);
            if (value == draft.Dock) radio.Checked = true;
        }
        dock.CheckedChange += (_, args) =>
        {
            var picked = dock.FindViewById<RadioButton>(args.CheckedId);
            if (picked?.Tag?.ToString() is { } name && Enum.TryParse<PanelDock>(name, out var value)) draft.Dock = value;
        };
        form.AddView(dock);

        var sizeLabel = new TextView(this) { TextSize = 13 };
        var size = new SeekBar(this) { Max = 100, Progress = (int)Math.Round(draft.PanelFraction * 100) };
        void UpdateSize() => sizeLabel.Text = $"Tamanho do painel: {Math.Max(1, size.Progress)}%";
        size.ProgressChanged += (_, args) =>
        {
            draft.PanelFraction = Math.Max(1, args.Progress) / 100.0;
            UpdateSize();
        };
        UpdateSize();
        form.AddView(sizeLabel);
        form.AddView(size);

        var widthLabel = new TextView(this) { TextSize = 13 };
        var width = new SeekBar(this) { Max = WorkspaceLayout.MaxExplorerWidth - WorkspaceLayout.MinExplorerWidth, Progress = draft.ExplorerWidthDp - WorkspaceLayout.MinExplorerWidth };
        void UpdateWidth() => widthLabel.Text = $"Largura do Explorer: {width.Progress + WorkspaceLayout.MinExplorerWidth} dp";
        width.ProgressChanged += (_, args) =>
        {
            draft.ExplorerWidthDp = args.Progress + WorkspaceLayout.MinExplorerWidth;
            UpdateWidth();
        };
        UpdateWidth();
        form.AddView(widthLabel);
        form.AddView(width);

        form.AddView(MakeButton("Carregar layout…", () => ShowLoadLayout()));
        form.AddView(MakeButton("Salvar como…", () => AskSaveLayout(draft)));
        form.AddView(MakeButton("Excluir layout salvo…", ShowDeleteLayout));

        var scroll = new ScrollView(this);
        scroll.AddView(form);
        new AlertDialog.Builder(this)!
            .SetTitle("Layout do workspace")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Aplicar", (_, _) =>
            {
                _layoutStore.Current = draft;
                ApplyWorkspaceLayout();
                SaveLayouts();
            })!.Show();
    }

    private void ShowLoadLayout()
    {
        var names = _layoutStore.Names.ToArray();
        new AlertDialog.Builder(this)!.SetTitle("Carregar layout")!.SetItems(names, (_, args) =>
        {
            if (!_layoutStore.Apply(names[args.Which])) return;
            ApplyWorkspaceLayout();
            SaveLayouts();
        })!.Show();
    }

    private void ShowDeleteLayout()
    {
        var names = _layoutStore.Names.Where(n => !_layoutStore.IsPreset(n)).ToArray();
        if (names.Length == 0)
        {
            Toast.MakeText(this, "Não há layouts salvos por você.", ToastLength.Short)?.Show();
            return;
        }
        new AlertDialog.Builder(this)!.SetTitle("Excluir layout")!.SetItems(names, (_, args) =>
        {
            _layoutStore.Delete(names[args.Which]);
            SaveLayouts();
        })!.Show();
    }

    private void AskSaveLayout(WorkspaceLayout draft)
    {
        var input = new EditText(this) { Hint = "Nome do layout" };
        input.SetSingleLine(true);
        var holder = Vertical();
        holder.SetPadding(Dp(16), Dp(8), Dp(16), 0);
        holder.AddView(input);
        new AlertDialog.Builder(this)!
            .SetTitle("Salvar layout")!
            .SetView(holder)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) =>
            {
                _layoutStore.Current = draft;
                if (!_layoutStore.SaveAs(input.Text ?? ""))
                {
                    Toast.MakeText(this, "Nome inválido, vazio ou reservado.", ToastLength.Long)?.Show();
                    return;
                }
                ApplyWorkspaceLayout();
                SaveLayouts();
                Toast.MakeText(this, "Layout salvo", ToastLength.Short)?.Show();
            })!.Show();
    }
}
