using System.Security.Cryptography;
using System.Text;
using IModelProvider = AgentRuntime.IModelProvider;
using ProviderException = AgentRuntime.ProviderException;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Ai;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// What a head gives the shell for the AI (LUNET-536 M7): where settings live, the secret vault for the key and
/// where conversations are kept. On Android these are the same files and Keystore vault the previous screens use,
/// so the key is configured once for the whole app. <paramref name="Provider"/> replaces the network model (tests).
/// </summary>
public sealed record AiHost(LunetAiSettingsStore Store, string HistoryDirectory, IModelProvider? Provider = null, LunetAiModelCatalog? Catalog = null)
{
    private static readonly Lazy<LunetAiModelCatalog> Shared = new(() => new LunetAiModelCatalog(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }));

    /// <summary>The provider's model list (memory cache of a few minutes, per provider, address and key).</summary>
    public LunetAiModelCatalog Models => Catalog ?? Shared.Value;
}

/// <summary>Compiles the open game for the AI's checks with the shell's own compiler (portable).</summary>
internal sealed class ShellAiBuild(Lunet.Host.WorkspaceSession workspace) : ILunetAiBuild
{
    public IReadOnlyList<string> LastErrors { get; private set; } = [];

    public Task<LunetAiBuildReport> CompileAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = workspace.Compile();
        var errors = result.Diagnostics.Where(d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Error).Select(d => d.ToString())
            .Concat(LunetAiAssetValidation.Validate(workspace.Project!)).ToArray();
        LastErrors = errors;
        return new LunetAiBuildReport(result.Success && errors.Length == 0, errors);
    }, cancellationToken);
}

/// <summary>
/// The AI of the game, in the support sheet (M7). Answers to the owner's complaints of 14/11: Estudo is the default and
/// Fazer is chosen per message ("pedi só para explicar e ela saiu fazendo"); everything is saved before sending and every
/// reply that changed files shows them with their diff and an Undo backed by the on-disk checkpoint ("cancelei e não foi
/// desfeito"); settings and key are the ones of the whole app ("configurar toda hora"); Stop cancels for real.
/// </summary>
public sealed class AiPanelView : UserControl
{
    private readonly LunetShell _shell;
    private readonly StackPanel _messages = new() { Spacing = LunetTokens.Space.S };
    private readonly ScrollViewer _scroll = new();
    private readonly TextBox _input = new()
    {
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 140, MinHeight = LunetTokens.MinTouch,
        PlaceholderText = "Pergunte ou peça algo sobre o jogo", FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Body,
        CornerRadius = new CornerRadius(LunetTokens.Radius.L), Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S),
    };
    private readonly StackPanel _composer = new() { Spacing = LunetTokens.Space.Xs };
    private readonly HashSet<string> _openDiffs = new(StringComparer.Ordinal);
    private LunetAiSession? _session;
    private string? _sessionGame;
    private string? _sessionName;
    private CancellationTokenSource? _run;
    private readonly List<LunetAiStep> _steps = [];
    private string _partial = "";

    public AiPanelView(LunetShell shell)
    {
        _shell = shell;
        AutomationProperties.SetName(_input, "Mensagem para a IA");
        _scroll.Content = _messages;
        _scroll.ScrollChanged += (_, _) => _followTail = _scroll.Offset.Y >= _scroll.Extent.Height - _scroll.Viewport.Height - 48;
        Content = new DockPanel { Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.S) };
    }

    /// <summary>
    /// Prepares a hint request in Estudo (from Objetivos): the text goes into the message field and the person sends it.
    /// Estudo explains and does not change the game; nothing is sent or written by itself.
    /// </summary>
    public void AskForHint(string request)
    {
        Mode = LunetAiMode.Teach;
        _input.Text = request;
        Refresh();
        _input.CaretIndex = request.Length;
    }

    /// <summary>The mode of the next message: Estudo unless the person picks Fazer for it.</summary>
    public LunetAiMode Mode { get; private set; } = LunetAiMode.Teach;
    public bool Running => _run is not null;
    public LunetAiSession? Session => _session;
    /// <summary>The GameId of the conversation shown (the running one keeps its game until it ends).</summary>
    public string? SessionGameId => _sessionGame;
    public TextBox Input => _input;

    /// <summary>The last reply (tests).</summary>
    public LunetAiReply? LastReply { get; private set; }

    public void Refresh()
    {
        var root = (DockPanel)Content!;
        if (_shell.AiHost is not { } host)
        {
            root.Children.Clear();
            root.Children.Add(LunetUi.EmptyState(LunetIcons.Code, "IA indisponível aqui", "Esta versão do Lunet não tem onde guardar a chave com segurança."));
            return;
        }
        var loaded = host.Store.Load();
        if (!loaded.IsConfigured && host.Provider is null) { root.Children.Clear(); root.Children.Add(Setup(host, loaded)); return; }
        EnsureSession(host);
        BuildComposer();
        // Already in place: nothing moves (the message being written keeps its focus and the keyboard).
        if (root.Children.Count != 2 || !ReferenceEquals(root.Children[0], _composer) || !ReferenceEquals(root.Children[1], _scroll))
        {
            root.Children.Clear();
            DockPanel.SetDock(_composer, Dock.Bottom);
            root.Children.Add(_composer);
            if (_scroll.Parent is Panel p) p.Children.Remove(_scroll);
            root.Children.Add(_scroll);
        }
        RenderMessages();
    }

    private void EnsureSession(AiHost host)
    {
        var project = _shell.Workspace.Project;
        // A run in progress keeps its own game's session until it ends (G07: no cross-game run). Opening another game
        // meanwhile shows that run, says which game it is changing, and only then switches.
        if (Running) return;
        if (project is null) { _session = null; return; }
        if (_session is not null && _sessionGame == project.Manifest.GameId) return;
        // The same history file as the previous screens: one conversation per game, wherever it was started.
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(project.Manifest.GameId))).ToLowerInvariant();
        _session = new LunetAiSession(project, Path.Combine(host.HistoryDirectory, id + ".json"));
        _session.MarkInterruptedIfPending();
        _sessionGame = project.Manifest.GameId;
        _sessionName = project.Name;
    }

    // ---------------- setup (once for the whole app) ----------------

    /// <summary>Not configured yet: the one AI settings form of the app (no copy here — anexo A §6.6).</summary>
    private Control Setup(AiHost host, LunetAiLoadedSettings loaded) => new AiSettingsView(host, Refresh);

    // ---------------- conversation ----------------

    private readonly StackPanel _modes = new() { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
    private readonly Border _sendHost = new();

    /// <summary>
    /// Refreshes the mode buttons and Enviar/Parar. The message field is built once and never moves (moving a focused
    /// field clears its focus and Android closes the keyboard), and the buttons do not take the focus from it: the
    /// keyboard stays open while you pick Fazer or send (estudo do editor, 26/11/2026).
    /// </summary>
    private void BuildComposer()
    {
        if (_composer.Children.Count == 0)
        {
            var row = new DockPanel();
            DockPanel.SetDock(_sendHost, Dock.Right);
            row.Children.Add(_sendHost);
            if (_input.Parent is Panel p) p.Children.Remove(_input);
            row.Children.Add(_input);
            _composer.Children.Add(_modes);
            _composer.Children.Add(row);
        }
        _modes.Children.Clear();
        foreach (var (mode, label, hint) in new[] { (LunetAiMode.Teach, "Estudar", "Explica e responde, não altera nada"), (LunetAiMode.Do, "Fazer", "Altera o jogo, mostra o que mudou e permite desfazer") })
        {
            var b = LunetUi.Button(label, Mode == mode ? ButtonKind.Selected : ButtonKind.Ghost, null, () => { Mode = mode; BuildComposer(); });
            b.MinHeight = 40;
            b.Focusable = false;
            AutomationProperties.SetHelpText(b, hint);
            _modes.Children.Add(b);
        }
        var send = Running
            ? LunetUi.IconButton(LunetIcons.Stop, "Parar a IA", Stop, LunetTokens.Color.Danger)
            : LunetUi.IconButton(LunetIcons.Play, "Enviar", () => _ = SendAsync(), LunetTokens.Color.Primary);
        send.Focusable = false;
        _sendHost.Child = send;
    }

    /// <summary>Sends the input in the chosen mode (then the mode goes back to Estudo). Saves everything first.</summary>
    public async Task SendAsync()
    {
        var prompt = (_input.Text ?? "").Trim();
        if (prompt.Length == 0 || Running || _shell.AiHost is not { } host || _shell.Workspace.Project is null) return;
        EnsureSession(host);
        var loaded = host.Store.Load();
        if (!loaded.IsConfigured && host.Provider is null) { Refresh(); return; }
        _shell.SaveAll(); // the AI reads from disk: what you see is what it gets
        var mode = Mode;
        var editor = _shell.WorkspaceView.Code;
        var code = editor.Surface as CodeSurface;
        var context = new LunetAiEditorContext(
            editor.OpenPath,
            code is null || code.SelectionEnd <= code.SelectionStart ? null : code.Text[code.SelectionStart..code.SelectionEnd],
            code?.Editor.TextArea.Caret.Line ?? 0,
            _shell.LiveProblems.Select(d => d.ToString()).ToArray(),
            _shell.WorkspaceView.Support.ConsoleLines.TakeLast(20).ToArray());
        var request = new LunetAiRequest(loaded.Settings with { Model = loaded.Settings.Model.Length > 0 ? loaded.Settings.Model : "teste" },
            loaded.ApiKey ?? "chave-local-de-teste", context, new ShellAiBuild(_shell.Workspace), host.Provider,
            Mode: mode,
            Steps: new Progress<LunetAiStep>(step => { var i = _steps.FindIndex(s => s.Id == step.Id); if (i >= 0) _steps[i] = step; else _steps.Add(step); ScheduleLive(); }),
            PartialText: new Progress<string>(text => { _partial = text; ScheduleLive(); }));
        _input.Text = "";
        Mode = LunetAiMode.Teach; // Fazer is a choice for one message, never sticky
        _steps.Clear(); _partial = "";
        _run = new CancellationTokenSource();
        BuildComposer();
        RenderMessages();
        try
        {
            LastReply = await _session!.AskAsync(prompt, request, _run.Token);
        }
        catch (OperationCanceledException) { _shell.Report("IA interrompida. Nada pela metade ficou sem desfazer: veja a última resposta."); }
        catch (Exception ex) when (ex is HttpRequestException or ProviderException or InvalidOperationException or IOException)
        {
            _shell.Report("A IA não respondeu: " + ex.Message);
        }
        finally
        {
            _run?.Dispose();
            _run = null;
            _steps.Clear(); _partial = "";
            // Show what the AI changed — only when its game is the one open: another game opened during the run may
            // hold edits not saved yet, and reloading it would drop them (G07).
            if (_shell.Workspace.Project?.Manifest.GameId == _sessionGame) _shell.WorkspaceView.ReloadFromDisk();
            else if (_shell.AiHost is { } h) EnsureSession(h); // the run ended: the conversation follows the open game
            BuildComposer();
            RenderMessages();
        }
    }

    public void Stop() => _run?.Cancel();

    // ---- the live part of a run (steps and streamed text) ----
    // Every token and step used to rebuild the whole conversation (Markdown, code blocks, diffs): a long history made
    // the streaming stutter (GOAL H4: "evitar rebuild por token"). Now the history is built at the start and end of a
    // run only; while it runs, one live block is updated in place, at most ten times a second.
    private readonly StackPanel _live = new() { Spacing = 2 };
    private readonly TextBlock _livePartial = LunetUi.Text("", LunetTokens.Type.Label);
    private readonly Avalonia.Threading.DispatcherTimer _liveTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _liveTimerHooked;

    /// <summary>How many times the whole conversation was rebuilt (tests: streaming must not rebuild it).</summary>
    public int FullRenders { get; private set; }

    private void ScheduleLive()
    {
        if (!_liveTimerHooked) { _liveTimerHooked = true; _liveTimer.Tick += (_, _) => { _liveTimer.Stop(); RenderLive(); }; }
        if (!_liveTimer.IsEnabled) _liveTimer.Start();
    }

    /// <summary>Updates only the live block (steps and the text so far); the rest of the conversation stays as built.</summary>
    public void RenderLive()
    {
        if (!Running) return;
        if (!_messages.Children.Contains(_live)) { RenderMessages(); return; }
        _live.Children.Clear();
        _livePartial.TextWrapping = TextWrapping.Wrap;
        foreach (var step in _steps.TakeLast(6))
            _live.Children.Add(LunetUi.Text((step.Status == LunetAiStepStatus.Done ? "✓ " : "… ") + step.Label, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        _livePartial.Text = _partial;
        if (_partial.Length > 0) _live.Children.Add(_livePartial);
        if (_live.Children.Count == 0) _live.Children.Add(LunetUi.Text("Pensando…", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        if (_followTail) _scroll.ScrollToEnd();
    }

    // The reader scrolled up to read: streaming does not pull the view back down until they return to the end.
    private bool _followTail = true;

    private void RenderMessages()
    {
        FullRenders++;
        _liveTimer.Stop();
        _messages.Children.Clear();
        var history = _session?.History ?? [];
        if (history.Count == 0 && !Running)
            _messages.Children.Add(LunetUi.Text("Pergunte sobre o código aberto ou peça uma mudança. Em Estudar a IA só explica; em Fazer ela altera o jogo e você pode desfazer.",
                LunetTokens.Type.Label, LunetTokens.Color.TextSecondary));
        foreach (var turn in history)
        {
            if (turn.Pending) continue;
            _messages.Children.Add(turn.Role == "user" ? UserBubble(turn) : AssistantMessage(turn));
        }
        if (Running && _shell.Workspace.Project?.Manifest.GameId != _sessionGame)
            _messages.Children.Add(LunetUi.Notice($"A IA ainda trabalha em {_sessionName}. Ela termina lá (ou pare-a aqui); depois esta conversa passa para o jogo aberto.", LunetTokens.Color.Warning));
        if (Running)
        {
            _messages.Children.Add(_live);
            RenderLive();
        }
        _followTail = true;
        _scroll.ScrollToEnd();
    }

    private static Control UserBubble(LunetAiTurn turn) => new Border
    {
        HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 520, Padding = new Thickness(LunetTokens.Space.S),
        CornerRadius = new CornerRadius(LunetTokens.Radius.L), Background = LunetTokens.Brush(LunetTokens.Color.PrimarySoft),
        Child = LunetUi.Text((turn.Mode == LunetAiMode.Do ? "Fazer · " : "") + turn.Text, LunetTokens.Type.Label),
    };

    private Control AssistantMessage(LunetAiTurn turn)
    {
        var stack = new StackPanel { Spacing = LunetTokens.Space.Xs };
        foreach (var block in ChatMarkdown.Parse(turn.Text))
        {
            if (block.Kind == ChatBlockKind.Code)
                stack.Children.Add(new Border
                {
                    Background = LunetTokens.Brush(LunetTokens.Color.Background), CornerRadius = new CornerRadius(LunetTokens.Radius.S), Padding = new Thickness(LunetTokens.Space.S),
                    Child = new SelectableTextBlock { Text = block.Text, FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label, Foreground = LunetTokens.Brush(LunetTokens.Color.Code), TextWrapping = TextWrapping.Wrap },
                });
            else
            {
                var prefix = block.Kind switch { ChatBlockKind.Bullet => "• ", ChatBlockKind.Numbered => "", ChatBlockKind.Quote => "│ ", _ => "" };
                var text = LunetUi.Text(prefix + string.Concat(ChatMarkdown.Inline(block.Text).Select(s => s.Text)),
                    block.Kind == ChatBlockKind.Heading ? LunetTokens.Type.Callout : LunetTokens.Type.Label,
                    weight: block.Kind == ChatBlockKind.Heading ? FontWeight.SemiBold : FontWeight.Normal);
                stack.Children.Add(text);
            }
        }
        if (_session?.CheckpointFor(turn) is { IsEmpty: false } checkpoint) stack.Children.Add(Changes(checkpoint));
        else if (_session?.CheckpointFor(turn) is { IsReverted: true }) stack.Children.Add(LunetUi.Text("Alterações desfeitas.", LunetTokens.Type.Caption, LunetTokens.Color.Success));
        return stack;
    }

    /// <summary>What a reply changed: each file with its diff on a tap, and Undo for all of it (the on-disk checkpoint).</summary>
    private Control Changes(LunetAiCheckpoint checkpoint)
    {
        var card = new StackPanel { Spacing = 2 };
        var changes = checkpoint.Changes();
        card.Children.Add(LunetUi.Text($"Alterou {changes.Count} arquivo(s)", LunetTokens.Type.Caption, LunetTokens.Color.Primary, FontWeight.SemiBold));
        foreach (var change in changes)
        {
            var lines = LunetAiDiff.Lines(change.Before, change.After);
            int added = lines.Count(l => l.Kind == DiffKind.Added), removed = lines.Count(l => l.Kind == DiffKind.Removed);
            var open = _openDiffs.Contains(change.Path);
            var row = LunetUi.Button($"{change.Path}  +{added} −{removed}", ButtonKind.Ghost, LunetIcons.File, () =>
            {
                if (!_openDiffs.Remove(change.Path)) _openDiffs.Add(change.Path);
                RenderMessages();
            });
            row.HorizontalAlignment = HorizontalAlignment.Stretch; row.HorizontalContentAlignment = HorizontalAlignment.Left;
            card.Children.Add(row);
            if (open)
                foreach (var line in lines.Where(l => l.Kind != DiffKind.Same).Take(80))
                    card.Children.Add(new TextBlock
                    {
                        Text = (line.Kind == DiffKind.Added ? "+ " : "− ") + line.Text, FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Caption,
                        Foreground = LunetTokens.Brush(line.Kind == DiffKind.Added ? LunetTokens.Color.Success : LunetTokens.Color.Danger), TextWrapping = TextWrapping.Wrap,
                    });
        }
        var undo = LunetUi.Button("Desfazer estas alterações", ButtonKind.Secondary, LunetIcons.Back, () =>
        {
            checkpoint.Revert();
            _shell.WorkspaceView.ReloadFromDisk();
            _shell.Report("Alterações da IA desfeitas.");
            RenderMessages();
        });
        card.Children.Add(undo);
        return new Border
        {
            Child = card, Padding = new Thickness(LunetTokens.Space.S), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), // depth by tone, not by border
        };
    }
}
