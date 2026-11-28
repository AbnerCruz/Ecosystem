using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Ajustes in the shell (P7), on the same <c>settings.json</c> as the previous screens. Each change is saved at once
/// (verified write) and applied to what is open — no Salvar button to forget. The project folder and the AI keep their
/// own screens (the system folder picker; the key vault), opened from here.
/// </summary>
public sealed class SettingsView : UserControl
{
    private readonly LunetShell _shell;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.Danger);

    public SettingsView(LunetShell shell)
    {
        _shell = shell;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        _status.IsVisible = false;
        Content = new ScrollViewer
        {
            Content = new Border { Child = _page, MaxWidth = 720, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.Xxs, LunetTokens.Space.M, LunetTokens.Space.Xl) },
        };
        Build();
    }

    private Control? _main;

    /// <summary>The open Pasta de projetos page, or null.</summary>
    public ProjectFolderView? Folder { get; private set; }

    /// <summary>Opens Pasta de projetos inside Ajustes (Back returns here).</summary>
    public ProjectFolderView ShowFolder(ProjectFolderHost host)
    {
        _main ??= (Control)Content!;
        Folder = new ProjectFolderView(_shell, host);
        var root = new DockPanel();
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar aos ajustes", () => Back());
        back.Margin = new Thickness(LunetTokens.Space.Xs);
        back.HorizontalAlignment = HorizontalAlignment.Left;
        DockPanel.SetDock(back, Dock.Top);
        root.Children.Add(back);
        root.Children.Add(Folder);
        Content = root;
        return Folder;
    }

    /// <summary>Back: from Pasta de projetos to Ajustes; false at Ajustes itself (the Home closes it).</summary>
    public bool Back()
    {
        if (Folder is null || _main is null) return false;
        Folder = null;
        Content = _main;
        Build();
        return true;
    }

    private void Build()
    {
        _page.Children.Clear();
        // A visible way back, like Aprender and Studios (Back/Esc also close it).
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => _shell.Home.CloseSheet());
        back.Name = "SettingsBack";
        var title = LunetUi.Title("Ajustes"); // the same sub-page header as Aprender and Studios
        title.VerticalAlignment = VerticalAlignment.Center;
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs, Children = { back, title },
            Margin = new Thickness(LunetTokens.Space.Xxs - LunetTokens.Space.M, 0, 0, 0) }; // the back arrow sits at the edge, as in Aprender
        _page.Children.Add(head);
        _page.Children.Add(_status);
        var settings = _shell.Settings;

        Section("Editor");
        var sample = new TextBlock
        {
            Text = "batch.Draw(heroi, posicao, Color.White);", FontFamily = LunetTokens.MonoFont, FontSize = settings.FontSize,
            Foreground = LunetTokens.Brush(LunetTokens.Color.Code), TextWrapping = TextWrapping.NoWrap,
        };
        var sizeLabel = LunetUi.Text($"Tamanho do código: {settings.FontSize}", LunetTokens.Type.Callout);
        var slider = new Slider
        {
            Minimum = EditorSettings.MinFontSize, Maximum = EditorSettings.MaxFontSize, Value = settings.FontSize,
            IsSnapToTickEnabled = true, TickFrequency = 1, MinHeight = LunetTokens.MinTouch, Name = "FontSize",
        };
        AutomationProperties.SetName(slider, "Tamanho do código");
        slider.ValueChanged += (_, e) =>
        {
            var size = (int)Math.Round(e.NewValue);
            sizeLabel.Text = $"Tamanho do código: {size}";
            sample.FontSize = size;
            Change(s => s.FontSize = size);
        };
        _page.Children.Add(sizeLabel);
        _page.Children.Add(slider);
        _page.Children.Add(new Border
        {
            Child = new ScrollViewer { Content = sample, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden },
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), CornerRadius = new CornerRadius(LunetTokens.Radius.M), Padding = new Thickness(LunetTokens.Space.S),
        });
        Toggle("Números de linha", "Ao lado do código", settings.ShowLineNumbers, v => Change(s => s.ShowLineNumbers = v), "LineNumbers");
        Toggle("Formatar ao rodar", "Arruma a indentação do arquivo aberto antes de rodar o jogo", settings.FormatOnRun, v => Change(s => s.FormatOnRun = v), "FormatOnRun");

        if (_shell.Game.GpuFactory is not null)
        {
            Section("Jogo");
            Toggle("Taxa de atualização alta", "Pede 90/120 Hz ao rodar o jogo, onde a tela tiver", settings.HighRefreshRate, v => Change(s => s.HighRefreshRate = v), "HighRefreshRate");
        }

        if (_shell.KeepScreenOn is not null)
        {
            Section("Aparelho");
            Toggle("Manter a tela ligada", "Enquanto o Lunet estiver aberto", settings.KeepScreenOn, v => Change(s => s.KeepScreenOn = v), "KeepScreenOn");
        }

        if (_shell.ProjectFolder is not null || _shell.AiHost is not null)
        {
            Section("Projetos e IA");
            if (_shell.ProjectFolder is { } folder)
                Link("Pasta de projetos", "Onde ficam seus jogos: interna ou uma pasta sua, com revisões", () => ShowFolder(folder), "ProjectFolder");
            // In the shell now (LUNET-521): the same store and vault as the AI of each game.
            if (_shell.AiHost is not null)
                Link("Configurar IA", "Provedor, modelo e chave, guardada no cofre do aparelho", () => _shell.Home.ShowGlobalAi(settings: true), "AiSettings");
        }

        Section("Sobre");
        var version = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(SettingsView).Assembly)?.InformationalVersion
            ?? typeof(SettingsView).Assembly.GetName().Version?.ToString(3);
        _page.Children.Add(LunetUi.Text("Lunet " + version?.Split('+')[0], LunetTokens.Type.Body));
        _page.Children.Add(LunetUi.Text("Tudo fica no aparelho. Nada é enviado sem você pedir.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
    }

    /// <summary>Saves one change now; a failure is shown and the setting goes back.</summary>
    private void Change(Action<EditorSettings> change)
    {
        var next = _shell.Settings.Clone();
        change(next);
        try
        {
            _shell.SaveSettings(next);
            _status.IsVisible = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Text = "Não foi possível salvar: " + e.Message;
            _status.IsVisible = true;
            Build();
        }
    }

    private void Section(string title)
    {
        var t = LunetUi.Text(title.ToUpperInvariant(), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
        t.Margin = new Thickness(0, LunetTokens.Space.L, 0, 0);
        _page.Children.Add(t);
    }

    private void Toggle(string title, string detail, bool value, Action<bool> set, string name)
    {
        var toggle = new ToggleSwitch { MinHeight = LunetTokens.MinTouch, IsChecked = value, OnContent = null, OffContent = null, Name = name, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(toggle, title);
        toggle.IsCheckedChanged += (_, _) => set(toggle.IsChecked == true);
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(LunetUi.Text(title, LunetTokens.Type.Callout));
        text.Children.Add(LunetUi.Text(detail, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var row = new DockPanel { MinHeight = 56 };
        DockPanel.SetDock(toggle, Dock.Right);
        row.Children.Add(toggle);
        row.Children.Add(text);
        _page.Children.Add(row);
    }

    private void Link(string title, string detail, Action open, string name)
    {
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(LunetUi.Text(title, LunetTokens.Type.Callout, weight: FontWeight.SemiBold));
        text.Children.Add(LunetUi.Text(detail, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        var b = new Button
        {
            Content = text, MinHeight = 56, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S), Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.L), Name = name,
        };
        AutomationProperties.SetName(b, title);
        b.Click += (_, _) => open();
        _page.Children.Add(b);
    }
}
