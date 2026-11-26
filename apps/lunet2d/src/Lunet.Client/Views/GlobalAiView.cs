using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Lunet.Ai;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// The AI settings of the whole app (provider, model, address, key in the device vault), in the shell. Opened from
/// Ajustes → Configurar IA and from the global AI's gear. The same store as the AI of each game (LUNET-521).
/// </summary>
public sealed class AiSettingsView : UserControl
{
    private readonly AiHost _host;
    private readonly Action? _saved;
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary);

    public AiSettingsView(AiHost host, Action? saved = null)
    {
        _host = host;
        _saved = saved;
        Build();
    }

    public string Status => _status.Text ?? "";

    private void Build()
    {
        var loaded = _host.Store.Load();
        var all = LunetAiProviders.All.ToList();
        var provider = LunetAiProviders.Find(loaded.Settings.ProviderId);
        var providers = new ComboBox { ItemsSource = all.Select(p => p.Name).ToArray(), SelectedIndex = Math.Max(0, all.IndexOf(provider)), HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = LunetTokens.MinTouch, Name = "AiProvider" };
        var key = new TextBox { PasswordChar = '•', MinHeight = LunetTokens.MinTouch, Name = "AiKey" };
        var model = new TextBox { Text = loaded.Settings.Model.Length > 0 ? loaded.Settings.Model : provider.SuggestedModels.FirstOrDefault() ?? "", MinHeight = LunetTokens.MinTouch, Name = "AiModel" };
        var endpoint = new TextBox { Text = loaded.Settings.Endpoint, PlaceholderText = "https://…/chat/completions", MinHeight = LunetTokens.MinTouch, Name = "AiEndpoint" };
        var endpointField = LunetUi.Field("Endereço", endpoint);
        void ShowProvider(LunetAiProvider p)
        {
            key.PlaceholderText = _host.Store.HasKey(p.Id) ? "chave guardada no cofre — deixe vazio para manter" : p.KeyHint;
            endpointField.IsVisible = p.Id == "custom";
        }
        ShowProvider(provider);
        providers.SelectionChanged += (_, _) =>
        {
            var p = all[Math.Max(0, providers.SelectedIndex)];
            model.Text = p.SuggestedModels.FirstOrDefault() ?? "";
            endpoint.Text = p.Endpoint;
            ShowProvider(p);
        };
        AutomationProperties.SetName(key, "Chave da API"); AutomationProperties.SetName(model, "Modelo"); AutomationProperties.SetName(providers, "Provedor");

        var save = LunetUi.Button("Salvar", ButtonKind.Primary, null, () =>
        {
            var p = all[Math.Max(0, providers.SelectedIndex)];
            var url = (p.Id == "custom" ? endpoint.Text : p.Endpoint)?.Trim() ?? "";
            var hasKey = !string.IsNullOrWhiteSpace(key.Text) || _host.Store.HasKey(p.Id);
            if (!hasKey || string.IsNullOrWhiteSpace(model.Text) || url.Length == 0)
            {
                _status.Text = "Preencha a chave, o modelo e o endereço.";
                return;
            }
            try { LunetAiEndpoint.Validate(url); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UriFormatException) { _status.Text = ex.Message; return; }
            _host.Store.Save(_host.Store.Load().Settings with { ProviderId = p.Id, Endpoint = url, Model = model.Text!.Trim() }, key.Text);
            key.Text = "";
            ShowProvider(p);
            _status.Text = "Salvo. A chave fica no cofre do aparelho e vale para todo o Lunet.";
            _saved?.Invoke();
        });
        save.Name = "AiSave";
        var forget = LunetUi.Button("Apagar a chave deste provedor", ButtonKind.Ghost, LunetIcons.Close, () =>
        {
            var p = all[Math.Max(0, providers.SelectedIndex)];
            _host.Store.ClearKey(p.Id);
            ShowProvider(p);
            _status.Text = "Chave apagada do cofre.";
        });
        forget.Name = "AiForget";

        var form = new StackPanel { Spacing = LunetTokens.Space.M, Margin = new Thickness(LunetTokens.Space.M) };
        form.Children.Add(LunetUi.Secondary("Configure uma vez: vale para a IA de cada jogo e para a conversa sem jogo. A chave fica no cofre do aparelho, nunca no jogo."));
        form.Children.Add(LunetUi.Field("Provedor", providers));
        form.Children.Add(LunetUi.Field("Chave da API", key));
        form.Children.Add(LunetUi.Field("Modelo", model));
        form.Children.Add(endpointField);
        form.Children.Add(save);
        form.Children.Add(forget);
        form.Children.Add(_status);
        Content = new ScrollViewer { Content = form };
    }
}

/// <summary>
/// The AI without a game (LUNET-521), in the shell: a conversation that never opens, reads or writes a project, with
/// its own history (the same file as the previous screen, so the conversation carries over). Gear → AI settings.
/// </summary>
public sealed class GlobalAiView : UserControl
{
    private readonly LunetShell _shell;
    private readonly AiHost _host;
    private readonly Action _close;
    private readonly StackPanel _messages = new() { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M) };
    private readonly ScrollViewer _scroll = new();
    private readonly TextBox _input = new()
    {
        AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxHeight = 140, MinHeight = LunetTokens.MinTouch,
        PlaceholderText = "Pergunte sobre C#, jogos, ideias…", Name = "GlobalAiInput",
    };
    private readonly Border _sendHost = new();
    private readonly ContentControl _body = new();
    private readonly TextBlock _error = LunetUi.Text("", LunetTokens.Type.Label, LunetTokens.Color.Danger);
    private WorkspaceAiChat? _chat;
    private bool _settings;

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(100) };

    public GlobalAiView(LunetShell shell, AiHost host, Action close, HttpClient? http = null)
    {
        _shell = shell;
        _host = host;
        _close = close;
        AutomationProperties.SetName(_input, "Mensagem para a IA");
        _scroll.Content = _messages;
        try
        {
            _chat = new WorkspaceAiChat(Path.Combine(host.HistoryDirectory, "workspace-history.json"), http ?? Http);
            _chat.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(Render);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _error.Text = "O histórico não pôde ser lido. Nenhum dado foi apagado: " + e.Message;
        }
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var root = new DockPanel();
        var header = new DockPanel { Margin = new Thickness(LunetTokens.Space.Xs) };
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Back());
        DockPanel.SetDock(back, Dock.Left);
        header.Children.Add(back);
        var gear = LunetUi.IconButton(LunetIcons.Speed, "Configurar IA", () => { _settings = !_settings; Show(); });
        gear.Name = "GlobalAiSettings";
        DockPanel.SetDock(gear, Dock.Right);
        header.Children.Add(gear);
        var clear = LunetUi.IconButton(LunetIcons.Close, "Limpar conversa", ClearConversation);
        clear.Name = "GlobalAiClear";
        DockPanel.SetDock(clear, Dock.Right);
        header.Children.Add(clear);
        var title = LunetUi.Title("Lunet IA");
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(_body);
        Content = root;
        Show();
    }

    public TextBox Input => _input;
    public WorkspaceAiChat? Chat => _chat;
    public bool ShowingSettings => _settings;

    /// <summary>Shows the AI settings page (Ajustes → Configurar IA).</summary>
    public void OpenSettings() { _settings = true; Show(); }

    /// <summary>Back: the settings page first, then the AI itself.</summary>
    public bool Back()
    {
        if (_settings) { _settings = false; Show(); return true; }
        _close();
        return true;
    }

    private bool Configured => _host.Store.Load().IsConfigured;

    private void Show()
    {
        if (_settings || !Configured)
        {
            _body.Content = new AiSettingsView(_host, () => { if (_settings) { _settings = false; Show(); } else Show(); });
            return;
        }
        var composer = new DockPanel { Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.S) };
        DockPanel.SetDock(_sendHost, Dock.Right);
        if (_sendHost.Parent is Panel sp) sp.Children.Remove(_sendHost);
        if (_input.Parent is Panel ip) ip.Children.Remove(_input);
        composer.Children.Add(_sendHost);
        composer.Children.Add(_input);
        var body = new DockPanel();
        DockPanel.SetDock(composer, Dock.Bottom);
        body.Children.Add(composer);
        if (_scroll.Parent is Panel scp) scp.Children.Remove(_scroll);
        body.Children.Add(_scroll);
        _body.Content = body;
        Render();
    }

    private void Render()
    {
        var running = _chat?.IsRunning == true;
        var send = running
            ? LunetUi.IconButton(LunetIcons.Stop, "Parar a IA", () => _chat?.Cancel(), LunetTokens.Color.Danger)
            : LunetUi.IconButton(LunetIcons.Play, "Enviar", () => _ = SendAsync(), LunetTokens.Color.Primary);
        send.Focusable = false; // the message keeps the focus and the keyboard
        send.Name = running ? "GlobalAiStop" : "GlobalAiSend";
        _sendHost.Child = send;
        _messages.Children.Clear();
        if (_error.Text is { Length: > 0 }) _messages.Children.Add(_error);
        var history = _chat?.History ?? [];
        if (history.Count == 0)
            _messages.Children.Add(LunetUi.Secondary("Uma conversa sem jogo aberto: ela não lê nem altera seus projetos. Para mexer num jogo, use a IA dentro dele."));
        foreach (var turn in history)
        {
            var user = turn.Role == "user";
            var text = new SelectableTextBlock
            {
                Text = turn.Interrupted && !user ? turn.Text + "\n\n(interrompida)" : turn.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                FontSize = LunetTokens.Type.Body, Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary),
            };
            _messages.Children.Add(new Border
            {
                Child = text, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S), CornerRadius = new CornerRadius(LunetTokens.Radius.L),
                Background = LunetTokens.Brush(user ? LunetTokens.Color.PrimarySoft : LunetTokens.Color.Surface),
                HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                Margin = user ? new Thickness(48, 0, 0, 0) : new Thickness(0),
            });
        }
        _scroll.ScrollToEnd();
    }

    /// <summary>Sends the message (tests await it). Errors are shown; nothing in the history is lost.</summary>
    public async Task SendAsync()
    {
        var prompt = (_input.Text ?? "").Trim();
        if (prompt.Length == 0 || _chat is null || _chat.IsRunning) return;
        var loaded = _host.Store.Load();
        if (!loaded.IsConfigured) { Show(); return; }
        _input.Text = "";
        _error.Text = "";
        try { await _chat.AskAsync(prompt, loaded); }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or ArgumentException or IOException or InvalidDataException)
        {
            _error.Text = "A IA não respondeu: " + ex.Message;
            if (string.IsNullOrEmpty(_input.Text)) _input.Text = prompt; // the message comes back to be sent again
        }
        Render();
    }

    private void ClearConversation()
    {
        if (_chat is null || _chat.IsRunning) return;
        _chat.Clear();
        Render();
    }
}
