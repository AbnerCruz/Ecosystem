using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>What the assistant needs from the app: the vault's documents, writing them (the vault file first), the editor, the dialogs.</summary>
public interface IAssistantApp
{
    DocumentStore? Documents { get; }
    string VaultName { get; }
    /// <summary>The note open in the editor and the text selected in it.</summary>
    (string? Path, string? Selection) EditorNote();
    Task<UrbeDocument> CreateNoteAsync(string path, string content);
    Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content);
    Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath);
    Task TrashNoteAsync(UrbeDocument doc);
    Task CreateFolderAsync(string path);
    Task<bool> RestoreTrashedAsync(string id);
    Task WriteBackAsync(UrbeDocument current, string path, string content);
    /// <summary>Opens a note by path or title; false when there is none.</summary>
    bool OpenReference(string reference);
    UrbeDialogs Dialogs { get; }
    void Notify(string text);
}

/// <summary>
/// The Assistente (src/ai/ui.js), native: a conversation with agents that use tools. Each tool becomes a card showing
/// what the agent is doing; changes ask for approval with a preview (diff) of the note; the run can be stopped, undone
/// as a block and continued; the agent, the provider, the model and the permissions change at any time. Conversations,
/// keys and memory stay on this device only (AssistantStore), never in the vault.
/// </summary>
public sealed partial class AssistantPanel : Border
{
    private readonly IAssistantApp _app;
    private readonly AssistantStore _store;
    private AiConfig _cfg = new();
    private AiConversation _conv = new();
    private CancellationTokenSource? _running;
    private (AiApprovalRequest Request, TaskCompletionSource<AiApproval> Answer)? _pending;
    private readonly Dictionary<string, string> _progress = [];
    private bool _useOpen = true;
    private bool _renderPosted;
    private readonly HashSet<AiChatMessage> _dirty = [];
    private readonly Dictionary<AiChatMessage, Control> _views = [];

    private readonly Button _agentButton = new();
    private readonly Button _modelChip = new();
    private readonly Button _policyChip = new();
    private readonly TextBlock _usage = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly StackPanel _log = new() { Spacing = 12, Margin = new Thickness(14, 10, 14, 16) };
    private readonly ScrollViewer _scroll;
    private readonly TextBox _input;
    private readonly Button _send = new();
    private readonly Border _contextRow = new() { IsVisible = false, Margin = new Thickness(0, 0, 0, 6) };
    private readonly Border _view = new() { IsVisible = false };

    public AssistantPanel(IAssistantApp app, IAssistantStorage storage)
    {
        _app = app;
        _store = new AssistantStore(storage);
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        BorderThickness = new Thickness(1, 0, 0, 0);

        var head = new DockPanel { Margin = new Thickness(8, 6, 6, 2), Height = 52 };
        foreach (var (icon, tip, act) in new (string, string, Action)[]
                 {
                     (UrbeTheme.Icons.Close, "Fechar", Close), (UrbeTheme.Icons.Settings, "Configurações do Assistente", () => ShowSettings()),
                     (UrbeTheme.Icons.Plus, "Nova conversa", NewConversationClicked), (UrbeTheme.Icons.Clock, "Conversas", ShowHistory)
                 })
        {
            var b = IconButton(icon, tip);
            b.Click += (_, _) => act();
            DockPanel.SetDock(b, Dock.Right);
            head.Children.Add(b);
        }
        Chip(_agentButton, 15);
        _agentButton.FontWeight = FontWeight.SemiBold;
        _agentButton.HorizontalAlignment = HorizontalAlignment.Left;
        _agentButton.Click += (_, _) => _ = PickAgentAsync();
        Avalonia.Automation.AutomationProperties.SetName(_agentButton, "Trocar de agente");
        head.Children.Add(_agentButton);

        Chip(_modelChip, 12);
        _modelChip.Click += (_, _) => _ = PickModelAsync();
        Chip(_policyChip, 12);
        _policyChip.Margin = new Thickness(6, 0, 0, 0);
        _policyChip.Click += (_, _) => _ = PickPolicyAsync();
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 12, 6), Children = { _modelChip, _policyChip, _usage } };

        _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _log };

        _input = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 180, MinHeight = 44, FontSize = 15, PlaceholderText = "Peça algo ao Assistente…",
            VerticalContentAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 10)
        };
        Avalonia.Automation.AutomationProperties.SetName(_input, "Mensagem para o Assistente");
        _input.KeyDown += (_, e) =>
        {
            // Enter sends on a keyboard (desktop); on touch Enter is a new line, as in the original (coarse pointer)
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !OperatingSystem.IsAndroid()) { e.Handled = true; Submit(); }
        };
        _send.Width = 44;
        _send.Height = 44;
        _send.CornerRadius = new CornerRadius(22);
        _send.Margin = new Thickness(8, 0, 0, 0);
        _send.VerticalAlignment = VerticalAlignment.Bottom;
        _send.HorizontalContentAlignment = HorizontalAlignment.Center;
        _send.VerticalContentAlignment = VerticalAlignment.Center;
        _send.Padding = new Thickness(0);
        _send.Click += (_, _) => { if (_running is not null) Stop(); else Submit(); };
        var row = new DockPanel();
        DockPanel.SetDock(_send, Dock.Right);
        row.Children.Add(_send);
        row.Children.Add(_input);
        var compose = new StackPanel { Margin = new Thickness(12, 6, 12, 12), Children = { _contextRow, row } };

        var layout = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(compose, Dock.Bottom);
        layout.Children.Add(head);
        layout.Children.Add(bar);
        layout.Children.Add(compose);
        layout.Children.Add(_scroll);
        _view.Background = UrbeTheme.Brush(UrbeTheme.Bg);
        Child = new Grid { Children = { layout, _view } };
    }

    /// <summary>The HTTP layer (tests replace the handler).</summary>
    public AssistantProviders Providers { get; set; } = new();

    /// <summary>The conversation shown (tests).</summary>
    public AiConversation Conversation => _conv;
    /// <summary>The configuration (tests).</summary>
    public AiConfig Config => _cfg;
    /// <summary>A run is going on.</summary>
    public bool Running => _running is not null;
    /// <summary>The approval waiting for the user, if any.</summary>
    public AiApprovalRequest? PendingApproval => _pending?.Request;
    /// <summary>The run going on now (tests await it).</summary>
    public Task? CurrentRun { get; private set; }
    /// <summary>The input box (tests type in it).</summary>
    public TextBox Input => _input;

    private bool _loaded;

    /// <summary>opened(): the panel shown, the configuration and the last conversation of this vault (if recent) loaded once.</summary>
    public void Open()
    {
        if (!_loaded)
        {
            _loaded = true;
            _cfg = _store.GetConfig();
            var last = _store.ListConversations().FirstOrDefault(c => c.Vault == _app.VaultName);
            _conv = last is not null && Now() - last.Updated < 6 * 3600_000 ? last : NewConversation();
        }
        IsVisible = true;
        RenderAll();
        if (!OperatingSystem.IsAndroid()) Dispatcher.UIThread.Post(() => _input.Focus(), DispatcherPriority.Background);
    }

    public void Close() { CloseView(); IsVisible = false; }

    /// <summary>prefill(): the composer with the start of a request (e.g. from Personalização or Páginas).</summary>
    public void Prefill(string text)
    {
        if (string.IsNullOrWhiteSpace(_input.Text)) _input.Text = text;
        _input.CaretIndex = _input.Text?.Length ?? 0;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // ---------------------------------------------------------------- configuration

    private AiProviderConfig? Provider() =>
        _cfg.Providers.FirstOrDefault(p => p.Id == (_conv.ProviderId ?? _cfg.ProviderId)) ?? _cfg.Providers.FirstOrDefault();

    private string ModelId() => _conv.Model.Length > 0 ? _conv.Model : _cfg.Model;
    private void SaveConfig() => _store.SetConfig(_cfg);

    private List<string> MemoryList()
    {
        var v = VaultKey;
        return [.. _cfg.Memory.GetValueOrDefault("*") ?? [], .. _cfg.Memory.GetValueOrDefault(v) ?? []];
    }

    private string VaultKey => _app.VaultName.Length > 0 ? _app.VaultName : "Urbe";

    private void Remember(string fact)
    {
        if (!_cfg.Memory.TryGetValue(VaultKey, out var list)) _cfg.Memory[VaultKey] = list = [];
        list.Add(fact);
        SaveConfig();
    }

    private AiModelInfo? ModelMeta()
    {
        var p = Provider();
        return p is not null && _cfg.ModelCache.TryGetValue(p.Id, out var cache) ? cache.List.FirstOrDefault(m => m.Id == ModelId()) : null;
    }

    private AiConversation NewConversation() => new()
    {
        Id = AssistantAgent.NewId("c"), Created = Now(), Updated = Now(), AgentId = _cfg.AgentId, ProviderId = _cfg.ProviderId, Model = _cfg.Model, Policy = _cfg.Policy, Vault = _app.VaultName
    };

    private void Persist() { if (_conv.Messages.Count > 0) _store.SaveConversation(_conv); }

    private void NewConversationClicked()
    {
        if (_running is not null) return;
        _conv = NewConversation();
        _views.Clear();
        RenderAll();
    }

    private (string? Path, string? Selection) EditorInfo() => _app.EditorNote();

    // ---------------------------------------------------------------- sending and running

    private void Submit()
    {
        var text = (_input.Text ?? "").Trim();
        if (text.Length == 0 || _running is not null) return;
        _input.Text = "";
        Send(text);
    }

    /// <summary>send(): the request (with the open note as hidden context when the chip is on) and the run.</summary>
    public void Send(string text)
    {
        if (_cfg.Providers.Count == 0) { ShowSettings("openrouter"); return; }
        if (ModelId().Length == 0) { _ = PickModelAsync(); return; }
        var e = EditorInfo();
        var msg = new AiChatMessage { Id = AssistantAgent.NewId("m"), Role = "user", Content = [AiChatBlock.TextBlock(text)], Meta = new AiChatMeta() };
        if (e.Path is { Length: > 0 } path && _useOpen)
        {
            var sel = e.Selection ?? "";
            msg.Meta.Context = path + (sel.Length > 0 ? " · seleção" : "");
            msg.Content.Add(new AiChatBlock
            {
                Type = "text", Hidden = true,
                Text = "[Contexto automático: nota aberta no editor = " + path + (sel.Length > 0 ? "; texto selecionado pelo usuário:\n\"\"\"\n" + (sel.Length > 4000 ? sel[..4000] : sel) + "\n\"\"\"" : "") + "]"
            });
        }
        if (_conv.Title.Length == 0) _conv.Title = AssistantAgent.TitleFrom(text);
        _conv.Messages.Add(msg);
        RenderAll();
        CurrentRun = StartAsync();
    }

    private void Stop()
    {
        if (_running is null) return;
        _running.Cancel();
        if (_pending is { } p) { _pending = null; p.Answer.TrySetResult(AiApproval.Reject()); }
    }

    private async Task StartAsync()
    {
        if (_running is not null) return;
        var p = Provider();
        if (p is null) { ShowSettings(); return; }
        if (_app.Documents is not { } documents) { await _app.Dialogs.AlertAsync("Assistente", "Abra um vault para conversar com o Assistente sobre as suas notas."); return; }
        var cts = new CancellationTokenSource();
        var conv = _conv;
        var meta = ModelMeta();
        _running = cts;
        _progress.Clear();
        var run = new AiRunRecord { Id = AssistantAgent.NewId("r"), Started = Now() };
        RenderAll();
        using var knowledge = new KnowledgeIndex(documents);
        try
        {
            var r = await AssistantAgent.RunAsync(new AiAgentRun
            {
                Host = new Host(this, documents, knowledge), Provider = p, Model = ModelId(), Messages = conv.Messages,
                Agent = AssistantAgent.AgentById(conv.AgentId, _cfg.CustomAgents), Policy = conv.Policy, Cancel = cts.Token,
                MaxSteps = _cfg.MaxSteps, ContextTokens = meta?.Context ?? 128000, Instructions = _cfg.Instructions, Memory = MemoryList(), VaultName = _app.VaultName,
                Stream = Providers.StreamAsync, Approve = ApproveAsync, OnUpdate = OnUpdate
            });
            run.Changes = r.Changes;
            run.Stopped = r.Stopped;
            run.Error = r.Error;
            conv.Usage = new AiUsage(conv.Usage.Input + r.Usage.Input, conv.Usage.Output + r.Usage.Output, r.Usage.Cost is null ? conv.Usage.Cost : (conv.Usage.Cost ?? 0) + r.Usage.Cost);
        }
        catch (Exception e)
        {
            run.Error = e.Message;
            run.Stopped = "error";
        }
        finally
        {
            _running = null;
            _pending = null;
            conv.Runs.Add(run);
            if (conv.Runs.Count > 20) conv.Runs.RemoveRange(0, conv.Runs.Count - 20);
            if (ReferenceEquals(conv, _conv)) { _views.Clear(); RenderAll(); }
            if (conv.Messages.Count > 0) _store.SaveConversation(conv);
        }
    }

    private void OnUpdate(AiAgentEvent e)
    {
        if (e.Type == "tool_progress" && e.Call is not null && e.Text is not null) _progress[e.Call.Id] = e.Text;
        if (e.Call is not null) { if (MessageOf(e.Call.Id) is { } m) Schedule(m); return; }
        if (e.Message is not null) Schedule(e.Message);
    }

    private Task<AiApproval> ApproveAsync(AiApprovalRequest req, CancellationToken cancel)
    {
        var answer = new TaskCompletionSource<AiApproval>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = (req, answer);
        cancel.Register(() => Dispatcher.UIThread.Post(() =>
        {
            if (_pending is { } p && ReferenceEquals(p.Request, req)) { _pending = null; answer.TrySetResult(AiApproval.Reject()); }
        }));
        if (MessageOf(req.Call.Id) is { } m) Schedule(m);
        if (!IsVisible) _app.Notify("O Assistente está esperando sua aprovação.");
        return answer.Task;
    }

    /// <summary>Answers the approval waiting (the buttons; tests).</summary>
    public void Answer(AiApproval answer)
    {
        if (_pending is not { } p) return;
        _pending = null;
        p.Answer.TrySetResult(answer);
        if (MessageOf(p.Request.Call.Id) is { } m) Schedule(m);
    }

    private AiChatMessage? MessageOf(string toolId) =>
        _conv.Messages.FirstOrDefault(m => m.Role == "assistant" && m.Content.Any(b => b.Type == "tool_use" && b.Id == toolId));

    /// <summary>undoLast(): the last run's changes undone after a confirmation; notes edited afterwards are kept.</summary>
    public async Task UndoLastAsync(bool confirmed = false)
    {
        var r = _conv.Runs.LastOrDefault();
        if (r is null || r.Changes.Count == 0 || r.Reverted) return;
        if (!confirmed && !await _app.Dialogs.ConfirmAsync("Desfazer as alterações?", "As notas voltam ao estado de antes desta resposta.", "Desfazer")) return;
        if (_app.Documents is not { } documents) return;
        var rep = await AssistantUndo.RevertAsync(new Undo(_app, documents), r.Changes);
        r.Reverted = true;
        Persist();
        RenderAll();
        if (rep.Skipped.Count > 0)
            await _app.Dialogs.AlertAsync("Algumas notas foram mantidas", "Você editou estas notas depois do Assistente, então elas não foram revertidas:\n" + string.Join("\n", rep.Skipped));
        else _app.Notify("Alterações desfeitas.");
    }

    /// <summary>The tools' host: reads from the vault's documents, writes through the app, memory and links through the panel.</summary>
    private sealed class Host(AssistantPanel panel, DocumentStore documents, KnowledgeIndex knowledge) : IAssistantHost
    {
        public DocumentStore Documents => documents;
        public KnowledgeIndex Knowledge => knowledge;
        public (string? Path, string? Selection) EditorContext() => panel.EditorInfo();
        public int ReadChars => 2000;
        public Task<UrbeDocument> CreateNoteAsync(string path, string content) => panel._app.CreateNoteAsync(path, content);
        public Task<UrbeDocument> SaveNoteAsync(UrbeDocument doc, string content) => panel._app.SaveNoteAsync(doc, content);
        public Task<UrbeDocument> RenameNoteAsync(UrbeDocument doc, string newPath) => panel._app.RenameNoteAsync(doc, newPath);
        public Task TrashNoteAsync(UrbeDocument doc) => panel._app.TrashNoteAsync(doc);
        public Task CreateFolderAsync(string path) => panel._app.CreateFolderAsync(path);
        public void OpenNote(string path) => panel._app.OpenReference(path);
        public void Remember(string fact) => panel.Remember(fact);
        public void SetPlan(IReadOnlyList<AiPlanStep> steps) { }
    }

    private sealed class Undo(IAssistantApp app, DocumentStore documents) : IAssistantUndoHost
    {
        public DocumentStore Documents => documents;
        public Task<bool> RestoreTrashedAsync(string id) => app.RestoreTrashedAsync(id);
        public Task TrashAsync(UrbeDocument doc) => app.TrashNoteAsync(doc);
        public Task WriteBackAsync(UrbeDocument current, string path, string content) => app.WriteBackAsync(current, path, content);
    }

    // ---------------------------------------------------------------- rendering

    private void Schedule(AiChatMessage m)
    {
        _dirty.Add(m);
        if (_renderPosted) return;
        _renderPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _renderPosted = false;
            bool near = NearEnd();
            foreach (var d in _dirty) _views.Remove(d);
            _dirty.Clear();
            RenderLog();
            RenderChrome();
            if (near) ScrollEnd();
        }, DispatcherPriority.Background);
    }

    private bool NearEnd() => _scroll.Extent.Height - _scroll.Offset.Y - _scroll.Viewport.Height < 140;
    private void ScrollEnd() => Dispatcher.UIThread.Post(() => _scroll.Offset = new Vector(0, Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height)), DispatcherPriority.Background);

    public void RenderAll()
    {
        RenderChrome();
        _views.Clear();
        RenderLog();
        ScrollEnd();
    }

    private void RenderChrome()
    {
        var a = AssistantAgent.AgentById(_conv.AgentId, _cfg.CustomAgents);
        var p = Provider();
        _agentButton.Content = Row(UrbeTheme.Icon(UrbeTheme.Icons.Assistant, 18, UrbeTheme.Accent, 1.8), Label(a.Name, 15, UrbeTheme.Text, FontWeight.SemiBold), UrbeTheme.Icon(UrbeTheme.Icons.Chevron, 14, UrbeTheme.Text3));
        _modelChip.Content = p is null ? Label("Conectar um modelo", 12, UrbeTheme.Text2) : Row(new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = UrbeTheme.Brush(UrbeTheme.Accent), VerticalAlignment = VerticalAlignment.Center },
            Label(ModelId().Length > 0 ? ModelId().Split('/')[^1] : "Escolher modelo", 12, UrbeTheme.Text2));
        ToolTip.SetTip(_modelChip, p is null ? null : (p.Name ?? p.Id) + " · " + ModelId());
        _policyChip.Content = Label(_conv.Policy switch { "auto" => "Edita sozinho", "readonly" => "Só leitura", _ => "Pede aprovação" }, 12, UrbeTheme.Text2);
        var u = _conv.Usage;
        double? cost = u.Cost ?? Estimate(u);
        _usage.Text = u.Input + u.Output > 0 ? FmtTokens(u.Input + u.Output) + (cost is > 0 ? " · US$ " + cost.Value.ToString(cost < .01 ? "0.0000" : "0.00", System.Globalization.CultureInfo.InvariantCulture) : "") : "";
        _usage.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        bool running = _running is not null;
        _send.Content = UrbeTheme.Icon(running ? UrbeTheme.Icons.Close : UrbeTheme.Icons.ArrowUp, 20, "#ffffff", 2.2);
        _send.Background = UrbeTheme.Brush(running ? "#c0504d" : UrbeTheme.Accent);
        Avalonia.Automation.AutomationProperties.SetName(_send, running ? "Parar" : "Enviar");
        var e = EditorInfo();
        if (e.Path is { Length: > 0 } path)
        {
            var chip = new Button
            {
                Content = Row(UrbeTheme.Icon(UrbeTheme.Icons.File, 14, _useOpen ? UrbeTheme.Accent : UrbeTheme.Text3), Label(path.Split('/')[^1] + (e.Selection is { Length: > 0 } ? " · seleção" : ""), 12, _useOpen ? UrbeTheme.Text : UrbeTheme.Text3)),
                Padding = new Thickness(10, 4), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1),
                BorderBrush = UrbeTheme.Brush(_useOpen ? UrbeTheme.Accent : UrbeTheme.Line2), Background = _useOpen ? UrbeTheme.Brush(UrbeTheme.Accent, .12) : Brushes.Transparent
            };
            ToolTip.SetTip(chip, "Incluir a nota aberta no pedido");
            chip.Click += (_, _) => { _useOpen = !_useOpen; RenderChrome(); };
            _contextRow.Child = chip;
            _contextRow.IsVisible = true;
        }
        else _contextRow.IsVisible = false;
    }

    private static string FmtTokens(long n) => n >= 1000 ? (n / 1000.0).ToString(n >= 100000 ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture) + "k tokens" : n + " tokens";

    private double? Estimate(AiUsage u) => ModelMeta()?.Price is { } price ? (u.Input * price.Input + u.Output * price.Output) / 1e6 : null;

    private void RenderLog()
    {
        _log.Children.Clear();
        if (_cfg.Providers.Count == 0) { _log.Children.Add(Onboarding()); return; }
        if (_conv.Messages.Count == 0) { _log.Children.Add(Empty()); return; }
        foreach (var m in _conv.Messages)
        {
            if (!_views.TryGetValue(m, out var view)) _views[m] = view = MessageView(m);
            if (view.Parent is Panel old) old.Children.Remove(view);
            _log.Children.Add(view);
        }
        if (Tail() is { } tail) _log.Children.Add(tail);
    }

    // ---------------------------------------------------------------- small builders

    private static TextBlock Label(string text, double size, string color, FontWeight? weight = null) =>
        new() { Text = text, FontSize = size, Foreground = UrbeTheme.Brush(color), FontWeight = weight ?? FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

    private static TextBlock Wrap(string text, double size, string color, FontWeight? weight = null) =>
        new() { Text = text, FontSize = size, Foreground = UrbeTheme.Brush(color), FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap };

    private static StackPanel Row(params Control[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var i in items) { i.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(i); }
        return row;
    }

    private static void Chip(Button b, double size)
    {
        b.Padding = new Thickness(10, 5);
        b.CornerRadius = new CornerRadius(14);
        b.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        b.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        b.BorderThickness = new Thickness(1);
        b.FontSize = size;
        b.VerticalAlignment = VerticalAlignment.Center;
    }

    private static Button IconButton(string icon, string tip)
    {
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 20, UrbeTheme.Text2, 1.8), Width = 42, Height = 42, Padding = new Thickness(0), CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        return b;
    }

    private static Button TextButton(string text, Action click, bool primary = false, bool danger = false)
    {
        var b = new Button
        {
            Content = text, Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(10), FontSize = 14, FontWeight = FontWeight.SemiBold,
            Background = primary ? UrbeTheme.Brush(UrbeTheme.Accent) : danger ? UrbeTheme.Brush("#c0504d") : UrbeTheme.Brush(UrbeTheme.Surface),
            Foreground = UrbeTheme.Brush(primary || danger ? "#ffffff" : UrbeTheme.Text), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(primary || danger ? 0 : 1)
        };
        b.Click += (_, _) => click();
        return b;
    }

    private static Button LinkButton(string text, Action click)
    {
        var b = new Button
        {
            Content = Label(text, 13, UrbeTheme.Accent, FontWeight.SemiBold), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        b.Click += (_, _) => click();
        return b;
    }

    private static Border Card(Control child, string? border = null, string? background = null) => new()
    {
        Child = child, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1),
        BorderBrush = UrbeTheme.Brush(border ?? UrbeTheme.Line2), Background = UrbeTheme.Brush(background ?? UrbeTheme.Surface)
    };

    private Control Markdown(string text) =>
        new MarkdownReader { WikiLinkTapped = name => OpenRef(name) }.Build(NoteMarks.Parse(text));

    private void OpenRef(string reference)
    {
        if (!_app.OpenReference(reference)) { _app.Notify("A nota “" + reference + "” não existe."); return; }
        if ((TopLevel.GetTopLevel(this)?.Bounds.Width ?? 0) < 900) Close();
    }
}
