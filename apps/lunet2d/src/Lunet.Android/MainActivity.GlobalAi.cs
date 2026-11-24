using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Ai;

namespace Lunet.Android;

/// <summary>LUNET-521. IA da área de trabalho não abre, lê nem escreve projetos.
/// Conexão/configuração são globais, sessões de projeto ficam separadas.</summary>
public sealed partial class MainActivity
{
    private static WorkspaceAiChat? s_workspaceChat;
    private WorkspaceAiChat? _workspaceChat;
    private readonly Dictionary<string, TextView> _workspaceTurnViews = new(StringComparer.Ordinal);
    private bool _workspaceEmpty = true;
    private string? _workspaceChatError;
    private bool _workspaceChatOpen;
    private bool _workspaceChatBusy;
    private string _workspaceChatDraft = "";
    private LinearLayout? _workspaceChatMessages;
    private ScrollView? _workspaceChatScroll;
    private EditText? _workspaceChatInput;
    private Button? _workspaceChatSend;
    private static readonly HttpClient WorkspaceChatHttp = new(new HttpClientHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(100) };

    private WorkspaceAiChat? GetWorkspaceChat()
    {
        if (_workspaceChat is not null) return _workspaceChat;
        try
        {
            _workspaceChat = s_workspaceChat ??= new WorkspaceAiChat(System.IO.Path.Combine(FilesDir!.AbsolutePath,
                "ai", "workspace-history.json"), WorkspaceChatHttp);
            _workspaceChat.Changed += OnWorkspaceChatChanged;
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
        EnterLauncherSurface("home");
        _globalChatVisible = true;
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
        _workspaceTurnViews.Clear();
        _workspaceEmpty = false;
        _workspaceChatScroll = new ScrollView(this) { FillViewport = true };
        _workspaceChatMessages = LunetUi.Column(this, 14);
        _workspaceChatMessages.SetPadding(Dp(3), Dp(14), Dp(3), Dp(14));
        _workspaceChatScroll.AddView(_workspaceChatMessages);
        root.AddView(_workspaceChatScroll, Fill(1));
        var compose = LunetUi.Row(this, 6);
        _workspaceChatDraft = AiDrafts.GetString("global", "") ?? "";
        _workspaceChatInput = LunetUi.Input(this, "Pergunte sobre jogos, programação, ideias…", _workspaceChatDraft);
        var draftInput = _workspaceChatInput;
        _workspaceChatInput.TextChanged += (_, _) =>
            AiDrafts.Edit()!.PutString("global", draftInput.Text ?? "")!.Apply();
        _workspaceChatInput.SetMinLines(2);
        _workspaceChatInput.SetMaxLines(5);
        compose.AddView(_workspaceChatInput, new LinearLayout.LayoutParams(0,
            ViewGroup.LayoutParams.WrapContent, 1f));
        _workspaceChatSend = LunetUi.Button(this, "Enviar", SendWorkspaceAi, ButtonStyle.Primary);
        _workspaceChatSend.Enabled = true;
        compose.AddView(_workspaceChatSend,
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(52)));
        root.AddView(compose);
        root.AddView(LunetUi.Button(this, "Nova conversa", ConfirmResetWorkspaceAi, ButtonStyle.Quiet));
        SetLauncherContent(root, "home");
        RenderWorkspaceAi();
    }

    private void OnWorkspaceChatChanged()
    {
        if (!IsDestroyed) RunOnUiThread(() =>
        {
            if (!IsDestroyed && _workspaceChatOpen) RenderWorkspaceAi();
        });
    }

    private void RenderWorkspaceAi()
    {
        if (!_workspaceChatOpen || _workspaceChatMessages is not { } container) return;
        var chat = GetWorkspaceChat();
        if (_workspaceChatError is not null)
        {
            container.RemoveAllViews();
            container.AddView(LunetUi.Label(this, _workspaceChatError, LunetUi.TextBody, LunetUi.Warning));
            return;
        }
        if (chat is null) return;
        var history = chat.History;
        bool follow = _workspaceChatScroll is null ||
            container.Height - _workspaceChatScroll.ScrollY - _workspaceChatScroll.Height < Dp(64);
        if (history.Count == 0)
        {
            if (!_workspaceEmpty)
            {
                container.RemoveAllViews();
                _workspaceTurnViews.Clear();
                container.AddView(LunetUi.Label(this, "Pergunte sobre jogos, programação ou uso do Lunet.",
                    LunetUi.TextBody, LunetUi.TextMuted));
                _workspaceEmpty = true;
            }
        }
        else if (_workspaceEmpty)
        {
            container.RemoveAllViews();
            _workspaceEmpty = false;
        }
        foreach (var turn in history)
        {
            var id = turn.Id ?? "";
            if (!_workspaceTurnViews.TryGetValue(id, out var label))
            {
                var card = LunetUi.Column(this, 5);
                card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
                card.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
                card.AddView(LunetUi.Label(this, turn.Role == "user" ? "Você" : "Lunet IA",
                    LunetUi.TextCaption, LunetUi.AccentText, bold: true));
                label = LunetUi.Label(this, "", LunetUi.TextBody, LunetUi.Text);
                label.SetTextIsSelectable(true);
                card.AddView(label);
                container.AddView(card, LunetUi.FillWidth());
                _workspaceTurnViews[id] = label;
            }
            var text = turn.Pending && turn.Text.Length == 0 ? "Aguardando o modelo…" : turn.Text;
            if (turn.Interrupted) text += (text.Length > 0 ? "\n\n" : "") + "Resposta interrompida.";
            if (label.Text != text) label.Text = text;
        }
        if (_workspaceChatSend is { } send)
            LunetUi.SetLabel(send, chat.IsRunning ? "Cancelar" : "Enviar");
        if (follow && _workspaceChatScroll is { } scroll)
            scroll.Post(() => scroll.ScrollTo(0, container.Height));
    }

    private async void SendWorkspaceAi()
    {
        var chat = GetWorkspaceChat();
        if (chat is null) return;
        if (chat.IsRunning) { chat.Cancel(); return; }
        if (_workspaceChatBusy) return;
        var prompt = (_workspaceChatInput?.Text ?? "").Trim();
        if (prompt.Length == 0) return;
        var ai = Ai;
        if (!ai.IsConfigured)
        {
            ShowAiSettings(ShowWorkspaceAi);
            return;
        }
        _workspaceChatBusy = true;
        var input = _workspaceChatInput;
        if (input is not null) input.Text = "";
        try { await chat.AskAsync(prompt, ai); }
        catch (Exception e) when (e is HttpRequestException or InvalidDataException or
            InvalidOperationException or ArgumentException or System.Text.Json.JsonException or OperationCanceledException or IOException)
        {
            if (!IsDestroyed && _workspaceChatOpen && e is not OperationCanceledException)
                Toast.MakeText(this, "A resposta não foi concluída: " + e.Message, ToastLength.Long)?.Show();
        }
        finally
        {
            _workspaceChatBusy = false;
            if (!IsDestroyed && _workspaceChatOpen) RenderWorkspaceAi();
        }
    }

    private void ConfirmResetWorkspaceAi()
    {
        if (GetWorkspaceChat()?.IsRunning == true)
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
