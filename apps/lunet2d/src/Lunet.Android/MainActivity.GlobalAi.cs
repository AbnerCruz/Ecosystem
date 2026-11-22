using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Ai;

namespace Lunet.Android;

/// <summary>LUNET-521. IA da área de trabalho não abre, lê nem escreve projetos.
/// Conexão/configuração são globais, sessões de projeto ficam separadas.</summary>
public sealed partial class MainActivity
{
    private WorkspaceAiChat? _workspaceChat;
    private string? _workspaceChatError;
    private bool _workspaceChatOpen;
    private bool _workspaceChatBusy;
    private string _workspaceChatDraft = "";
    private LinearLayout? _workspaceChatMessages;
    private ScrollView? _workspaceChatScroll;
    private EditText? _workspaceChatInput;
    private Button? _workspaceChatSend;
    private readonly HttpClient _workspaceChatHttp = new() { Timeout = TimeSpan.FromSeconds(100) };

    private WorkspaceAiChat? GetWorkspaceChat()
    {
        if (_workspaceChat is not null) return _workspaceChat;
        try
        {
            _workspaceChat = new WorkspaceAiChat(System.IO.Path.Combine(FilesDir!.AbsolutePath,
                "ai", "workspace-history.json"), _workspaceChatHttp);
            _workspaceChatError = null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _workspaceChatError = "O histórico não pôde ser lido. Nenhum dado foi apagado: " + e.Message;
        }
        return _workspaceChat;
    }

    private void ShowWorkspaceAi()
    {
        if (_workspaceChatInput is not null && _workspaceChatOpen)
            _workspaceChatDraft = _workspaceChatInput.Text ?? "";
        EnterLauncherSurface("home"); // chat global é uma superfície da Home, não um sexto destino.
        _workspaceChatOpen = true;
        var root = Vertical();
        root.SetBackgroundColor(LunetUi.Background);
        root.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        var header = LunetUi.Row(this, 8);
        header.AddView(LunetUi.Button(this, "← Início", ShowHome, ButtonStyle.Quiet));
        header.AddView(LunetUi.Label(this, "Lunet IA", LunetUi.TextTitle, LunetUi.Text, bold: true), LunetUi.Weight());
        header.AddView(LunetUi.Icon(this, "⚙", "Configurações globais da IA",
            () => ShowAiSettings(ShowWorkspaceAi)), new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        root.AddView(header);
        root.AddView(LunetUi.Label(this,
            "Chat geral · separado dos seus jogos · sem acesso a arquivos de projetos",
            LunetUi.TextCaption, LunetUi.TextMuted));
        var selected = Ai;
        root.AddView(LunetUi.Label(this, selected.IsConfigured
            ? LunetAiProviders.Find(selected.Settings.ProviderId).Name + " · " + selected.Settings.Model
            : "IA ainda não configurada. Use ⚙ para escolher provedor, modelo e chave.",
            LunetUi.TextCaption, LunetUi.AccentText));
        _workspaceChatScroll = new ScrollView(this) { FillViewport = true };
        _workspaceChatMessages = LunetUi.Column(this, 14);
        _workspaceChatMessages.SetPadding(Dp(3), Dp(14), Dp(3), Dp(14));
        _workspaceChatScroll.AddView(_workspaceChatMessages);
        root.AddView(_workspaceChatScroll, Fill(1));
        var compose = LunetUi.Row(this, 6);
        _workspaceChatInput = LunetUi.Input(this, "Pergunte sobre jogos, programação, ideias…", _workspaceChatDraft);
        _workspaceChatInput.SetMinLines(2);
        _workspaceChatInput.SetMaxLines(5);
        compose.AddView(_workspaceChatInput, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1f));
        _workspaceChatSend = LunetUi.Button(this, "Enviar", SendWorkspaceAi, ButtonStyle.Primary);
        _workspaceChatSend.Enabled = !_workspaceChatBusy;
        compose.AddView(_workspaceChatSend,
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(52)));
        root.AddView(compose);
        root.AddView(LunetUi.Button(this, "Nova conversa", ConfirmResetWorkspaceAi, ButtonStyle.Quiet));
        root.AddView(BuildLauncherNavigation("home"));
        SetContentView(root);
        RenderWorkspaceAi();
    }

    private void RenderWorkspaceAi()
    {
        if (!_workspaceChatOpen || _workspaceChatMessages is null) return;
        var container = _workspaceChatMessages;
        container.RemoveAllViews();
        var chat = GetWorkspaceChat();
        if (_workspaceChatError is not null)
        {
            container.AddView(LunetUi.Label(this, _workspaceChatError, LunetUi.TextBody, LunetUi.Warning));
            return;
        }
        if (chat is null) return;
        var history = chat.History;
        if (history.Count == 0)
        {
            container.AddView(LunetUi.Label(this,
                "Este é o espaço para explorar ideias e tirar dúvidas antes de abrir um jogo. " +
                "Os agentes dos projetos continuam com históricos próprios.",
                LunetUi.TextBody, LunetUi.TextMuted));
        }
        foreach (var turn in history.TakeLast(40))
        {
            var card = LunetUi.Column(this, 5);
            card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            card.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
            card.AddView(LunetUi.Label(this, turn.Role == "user" ? "Você" : "Lunet IA",
                LunetUi.TextCaption, LunetUi.AccentText, bold: true));
            card.AddView(LunetUi.Label(this, turn.Text, LunetUi.TextBody, LunetUi.Text));
            container.AddView(card, LunetUi.FillWidth());
        }
        if (_workspaceChatBusy)
            container.AddView(LunetUi.Label(this, "Aguardando resposta do modelo…",
                LunetUi.TextCaption, LunetUi.TextMuted));
        _workspaceChatScroll?.Post(() => _workspaceChatScroll?.FullScroll(FocusSearchDirection.Down));
    }

    private async void SendWorkspaceAi()
    {
        if (_workspaceChatBusy) return;
        var chat = GetWorkspaceChat();
        if (chat is null) return;
        var prompt = (_workspaceChatInput?.Text ?? "").Trim();
        if (prompt.Length == 0) return;
        var ai = Ai;
        if (!ai.IsConfigured)
        {
            _workspaceChatDraft = prompt;
            ShowAiSettings(ShowWorkspaceAi);
            return;
        }
        _workspaceChatBusy = true;
        if (_workspaceChatSend is not null) _workspaceChatSend.Enabled = false;
        RenderWorkspaceAi();
        try
        {
            await chat.AskAsync(prompt, ai);
            _workspaceChatDraft = "";
            if (IsDestroyed || !_workspaceChatOpen) return;
            if (_workspaceChatInput is not null) _workspaceChatInput.Text = "";
        }
        catch (Exception e) when (e is HttpRequestException or InvalidDataException or
            InvalidOperationException or ArgumentException or System.Text.Json.JsonException or TaskCanceledException or IOException)
        {
            _workspaceChatDraft = prompt;
            if (!IsDestroyed && _workspaceChatOpen)
                Toast.MakeText(this, "A mensagem não foi enviada: " + e.Message, ToastLength.Long)?.Show();
        }
        finally
        {
            _workspaceChatBusy = false;
            if (!IsDestroyed && _workspaceChatOpen)
            {
                if (_workspaceChatSend is not null) _workspaceChatSend.Enabled = true;
                RenderWorkspaceAi();
            }
        }
    }

    private void ConfirmResetWorkspaceAi()
    {
        if (_workspaceChatBusy)
        {
            Toast.MakeText(this, "Conclua a resposta antes de iniciar outra conversa.", ToastLength.Short)?.Show();
            return;
        }
        new AlertDialog.Builder(this)!.SetTitle("Nova conversa global")!
            .SetMessage("Apagar somente o histórico do chat geral? Nenhuma conversa de projeto será alterada.")!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Nova conversa", (_, _) =>
            {
                try { GetWorkspaceChat()?.Clear(); RenderWorkspaceAi(); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, e.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }
}
