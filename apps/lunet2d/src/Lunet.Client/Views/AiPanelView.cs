using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using IModelProvider = AgentRuntime.IModelProvider;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
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
public sealed record AiHost(LunetAiSettingsStore Store, string HistoryDirectory, IModelProvider? Provider = null, LunetAiModelCatalog? Catalog = null,
    Func<LunetAiKnowledge?>? Knowledge = null, ILunetAiImages? Images = null, ILunetAiWeb? Web = null,
    Func<string, IDisposable?>? KeepAlive = null, Action<string>? Diagnostics = null)
{
    private LunetAiKnowledge? _knowledge;
    private bool _knowledgeLoaded;
    private readonly object _knowledgeLock = new();

    /// <summary>The Framework reference and guides, indexed once on first use (never at startup).</summary>
    public LunetAiKnowledge? LoadKnowledge()
    {
        lock (_knowledgeLock)
        {
            if (!_knowledgeLoaded) { _knowledge = Knowledge?.Invoke(); _knowledgeLoaded = true; }
            return _knowledge;
        }
    }

    /// <summary>Image perception for the AI: the head's decoder, or Avalonia's (works on every head).</summary>
    public ILunetAiImages ImageReader => Images ?? AvaloniaAiImages.Instance;

    private static readonly Lazy<LunetAiModelCatalog> Shared = new(() => new LunetAiModelCatalog(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }));

    /// <summary>The provider's model list (memory cache of a few minutes, per provider, address and key).</summary>
    public LunetAiModelCatalog Models => Catalog ?? Shared.Value;
}

/// <summary>Compiles the open game for the AI's checks with the shell's own compiler (portable).</summary>
internal sealed class ShellAiBuild(Lunet.Host.WorkspaceSession workspace, LunetProject project) : ILunetAiBuild
{
    public IReadOnlyList<string> LastErrors { get; private set; } = [];

    public Task<LunetAiBuildReport> CompileAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = workspace.Compile(project);
        var errors = result.Diagnostics.Where(d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Error).Select(d => d.ToString())
            .Concat(LunetAiAssetValidation.Validate(project)).ToArray();
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
    private readonly ObservableCollection<object> _rows = [];
    private readonly ItemsControl _messages = new()
    {
        Margin = new Thickness(0, LunetTokens.Space.S),
        ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
    };
    private readonly ScrollViewer _scroll = new() { Name = "AiTranscript", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly Button _latest;
    private readonly HashSet<LunetAiTurn> _openActivities = new(ReferenceEqualityComparer.Instance);
    private int _renderedTurns;
    private string? _visibleProject;
    private LunetAiSession? _renderedSession;
    private readonly TextBox _input = new()
    {
        AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 140, MinHeight = LunetTokens.MinTouch,
        PlaceholderText = "Mensagem sobre o jogo", FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Body,
        CornerRadius = new CornerRadius(LunetTokens.Radius.L), Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S),
    };
    private readonly StackPanel _composer = new() { Spacing = LunetTokens.Space.Xs };
    private readonly HashSet<string> _openDiffs = new(StringComparer.Ordinal);
    private LunetAiSession? _session;
    private LunetProject? _sessionProject;
    private Task<LunetAiSession>? _sessionLoad;
    private string? _sessionGame;
    private string? _sessionName;
    private CancellationTokenSource? _run;
    private readonly List<LunetAiStep> _steps = [];
    private string _partial = "";

    public AiPanelView(LunetShell shell)
    {
        _shell = shell;
        AutomationProperties.SetName(_input, "Mensagem para a IA");
        _messages.ItemsSource = _rows;
        _messages.ItemTemplate = new FuncDataTemplate<LunetAiTurn>((turn, _) =>
        {
            if (turn is null) return null;
            try
            {
                var message = turn.Role is "Você" or "user" ? UserBubble(turn) : AssistantMessage(turn, _openActivities.Contains(turn));
                return new Border { Child = message, Margin = new Thickness(0, 0, 0, LunetTokens.Space.L) };
            }
            catch (Exception ex) when (RecoverableUiFailure(ex))
            {
                RecordDiagnostic(_shell.AiHost, "transcript_error=" + ex.GetType().Name);
                return LunetUi.Secondary(turn.Text);
            }
        });
        _scroll.Content = _messages;
        _latest = LunetUi.IconButton(LunetIcons.ChevronDown, "Ir para a última mensagem", JumpToLatest);
        _latest.Focusable = false;
        _latest.IsVisible = false;
        _scroll.ScrollChanged += (_, change) =>
        {
            // An extent change while streaming must not move the reader into or out of follow mode.
            var atEnd = _scroll.Offset.Y >= _scroll.Extent.Height - _scroll.Viewport.Height - 32;
            if (change.OffsetDelta.Y != 0 && change.ExtentDelta.Y == 0 && change.ViewportDelta.Y == 0)
                _followTail = atEnd;
            _latest.IsVisible = !atEnd && !_followTail;
        };
        Content = new DockPanel
        {
            MaxWidth = 780, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.S),
        };
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
    internal Button LatestButton => _latest;

    /// <summary>The last reply (tests).</summary>
    public LunetAiReply? LastReply { get; private set; }

    public void Refresh() => GuardUi(RefreshCore, "Não foi possível abrir o chat");

    private void RefreshCore()
    {
        var root = (DockPanel)Content!;
        if (_shell.AiHost is not { } host)
        {
            root.Children.Clear();
            root.Children.Add(LunetUi.EmptyState(LunetIcons.Code, "IA indisponível aqui", "Esta versão do Lunet não tem onde guardar a chave com segurança."));
            return;
        }
        var loaded = host.Store.Load();
        if (!loaded.IsConfigured && host.Provider is null) { root.Children.Clear(); root.Children.Add(new AiSettingsView(host, Refresh)); return; }
        if (!Running && _shell.Workspace.Project is { } project &&
            (!ReferenceEquals(_sessionProject, project) || _sessionLoad is null))
            _ = RefreshSessionAsync(host, project);
        var installed = root.Children.Contains(_composer);
        var switchedSession = !ReferenceEquals(_renderedSession, _session);
        if (!installed || switchedSession) BuildComposer();
        // Already in place: nothing moves (the message being written keeps its focus and the keyboard).
        if (!installed)
        {
            root.Children.Clear();
            DockPanel.SetDock(_composer, Dock.Bottom);
            root.Children.Add(_composer);
            root.Children.Add(_scroll);
        }
        if (!installed || switchedSession || _visibleProject != _shell.Workspace.Project?.Manifest.GameId)
            RenderMessages(jumpToEnd: switchedSession);
        else if (_followTail)
            FollowTail();
    }

    private Task<LunetAiSession> LoadSession(AiHost host, LunetProject project)
    {
        if (ReferenceEquals(_sessionProject, project) && _sessionLoad is { } existing) return existing;
        _sessionProject = project;
        _session = null;
        _attachments.Clear();
        _picking = false;
        _sessionGame = project.Manifest.GameId;
        _sessionName = project.Name;
        return _sessionLoad = Task.Run(() =>
        {
            // Same history as the legacy route, one conversation per game.
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(project.Manifest.GameId))).ToLowerInvariant();
            var session = new LunetAiSession(project, Path.Combine(host.HistoryDirectory, id + ".json"));
            session.MarkInterruptedIfPending();
            return session;
        });
    }

    private async Task RefreshSessionAsync(AiHost host, LunetProject project)
    {
        var load = LoadSession(host, project);
        try
        {
            var session = await load;
            if (!ReferenceEquals(load, _sessionLoad) || !ReferenceEquals(project, _shell.Workspace.Project)) return;
            _session = session;
            if (!Running) Refresh();
        }
        catch (Exception ex) when (RecoverableUiFailure(ex))
        {
            if (ReferenceEquals(load, _sessionLoad)) _sessionLoad = null;
            ReportFailure("Não foi possível carregar o histórico da IA", ex);
        }
    }

    // ---------------- conversation ----------------

    private readonly WrapPanel _modes = new();
    private readonly WrapPanel _attachmentRow = new() { Name = "AiAttachments" };
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
            DockPanel.SetDock(_sendHost, Dock.Right);
            DockPanel.SetDock(_modes, Dock.Left);
            var actions = new DockPanel { LastChildFill = false, Children = { _sendHost, _modes } };
            var fields = new StackPanel { Children = { _input, actions } };
            _composer.Children.Add(_attachmentRow);
            _composer.Children.Add(new Border
            {
                Background = LunetTokens.Brush(LunetTokens.Color.Surface),
                CornerRadius = new CornerRadius(LunetTokens.Radius.L),
                Padding = new Thickness(LunetTokens.Space.Xs, 0),
                Child = fields,
            });
        }
        BuildAttachments();
        _modes.Children.Clear();
        foreach (var (mode, label, hint) in new[] { (LunetAiMode.Teach, "Estudar", "Explica e responde, não altera nada"), (LunetAiMode.Do, "Fazer", "Altera o jogo, mostra o que mudou e permite desfazer") })
        {
            var b = LunetUi.Button(label, Mode == mode ? ButtonKind.Selected : ButtonKind.Ghost, null, () => { Mode = mode; BuildComposer(); });
            b.Padding = new Thickness(LunetTokens.Space.Xs, 0);
            b.Focusable = false;
            AutomationProperties.SetHelpText(b, hint);
            _modes.Children.Add(b);
        }
        var attach = LunetUi.IconButton(LunetIcons.Paperclip, "Anexar", ToggleAttachPicker);
        attach.Focusable = false; attach.Name = "AiAttach"; attach.IsEnabled = !Running;
        AutomationProperties.SetHelpText(attach, "Anexa uma nota, imagem ou arquivo das Notas do jogo à próxima mensagem");
        _modes.Children.Add(attach);
        var send = Running
            ? LunetUi.IconButton(LunetIcons.Stop, "Parar a IA", Stop, LunetTokens.Color.Danger)
            : LunetUi.IconButton(LunetIcons.Send, "Enviar", () => _ = SendAsync(), LunetTokens.Color.OnAccent);
        send.Background = LunetTokens.Brush(Running ? LunetTokens.Color.DangerSoft : LunetTokens.Color.Accent);
        send.Focusable = false;
        _sendHost.Child = send;
    }

    private readonly List<string> _attachments = [];
    private bool _picking;

    /// <summary>Files of the game attached to the next message (project paths; the AI reads them, images by perception).</summary>
    public IReadOnlyList<string> Attachments => _attachments;

    /// <summary>Attaches a project file to the next message (no duplicates; only files that exist in the game).</summary>
    public bool Attach(string projectPath)
    {
        if (Running || _shell.Workspace.Project is not { } project || _attachments.Contains(projectPath)) return false;
        if (!project.Exists(projectPath)) return false;
        _attachments.Add(projectPath);
        _picking = false;
        BuildComposer();
        return true;
    }

    private void ToggleAttachPicker() { _picking = !_picking; BuildComposer(); }

    private void BuildAttachments()
    {
        _attachmentRow.Children.Clear();
        _attachmentRow.IsVisible = _picking || _attachments.Count > 0;
        foreach (var path in _attachments.ToArray())
        {
            var chip = LunetUi.Button(Path.GetFileName(path) + "  ✕", ButtonKind.Selected, null, () => { _attachments.Remove(path); BuildComposer(); });
            chip.MinHeight = 36; chip.Focusable = false; chip.IsEnabled = !Running;
            AutomationProperties.SetName(chip, "Remover anexo " + Path.GetFileName(path));
            _attachmentRow.Children.Add(chip);
        }
        if (Running || !_picking || _shell.Workspace.Project is not { } project) return;
        var notes = ProjectNotes.List(project).Where(n => !_attachments.Contains(n.Path)).ToList();
        if (notes.Count == 0)
        {
            _attachmentRow.Children.Add(LunetUi.Secondary("Nada nas Notas deste jogo. Importe imagens e arquivos em Notas para anexar."));
            return;
        }
        foreach (var note in notes.Take(40))
        {
            var pick = LunetUi.Button((ProjectNotes.IsImage(note.Path) ? "Imagem · " : "") + Path.GetFileName(note.Path), ButtonKind.Ghost, null, () => Attach(note.Path));
            pick.MinHeight = 36; pick.Focusable = false;
            _attachmentRow.Children.Add(pick);
        }
    }

    /// <summary>Sends the input in the chosen mode (then the mode goes back to Estudo). Saves everything first.</summary>
    public async Task SendAsync()
    {
        var draft = _input.Text ?? "";
        var prompt = draft.Trim();
        if (prompt.Length == 0 || Running || _shell.AiHost is not { } host || _shell.Workspace.Project is not { } project) return;
        if (prompt.Length > 8_000) { _shell.Report("A mensagem pode ter até 8.000 caracteres."); return; }

        using var run = new CancellationTokenSource();
        _run = run;
        var mode = Mode;
        var started = Stopwatch.GetTimestamp();
        IDisposable? alive = null;
        var accepted = false;
        double? firstTokenMs = null;
        double? preparationMs = null;
        LastReply = null;
        try
        {
            var sessionLoad = LoadSession(host, project);
            var attachments = _attachments.ToArray();
            _steps.Clear();
            _partial = "";
            _stepsDirty = true;
            _liveActivity.SetOpen(false);
            BuildComposer();

            if (!await _shell.WorkspaceView.SaveEditorBuffersAsync(project) || run.IsCancellationRequested)
                return;
            if (!ReferenceEquals(project, _shell.Workspace.Project)) return;
            var editor = _shell.WorkspaceView.Code;
            var code = editor.Surface as CodeSurface;
            var context = new LunetAiEditorContext(
                editor.OpenPath,
                code is null || code.SelectionEnd <= code.SelectionStart ? null : code.Text[code.SelectionStart..code.SelectionEnd],
                code?.Editor.TextArea.Caret.Line ?? 0,
                _shell.LiveProblems.Select(d => d.ToString()).ToArray(),
                _shell.WorkspaceView.Support.ConsoleLines.TakeLast(20).ToArray());

            var session = await sessionLoad.WaitAsync(run.Token);
            _session = session;
            var loaded = await Task.Run(host.Store.Load, run.Token);
            if (!loaded.IsConfigured && host.Provider is null) return;
            run.Token.ThrowIfCancellationRequested();

            // Foreground service activation uses the Android head. Release is protected independently of the run.
            alive = host.KeepAlive?.Invoke(project.Name);
            RenderMessages(jumpToEnd: true);

            var steps = new Progress<LunetAiStep>(step =>
            {
                if (!ReferenceEquals(_run, run)) return;
                var i = _steps.FindIndex(s => s.Id == step.Id);
                if (i >= 0) _steps[i] = step;
                else _steps.Add(step);
                _stepsDirty = true;
                ScheduleLive();
            });
            var partial = new Progress<string>(text =>
            {
                if (!ReferenceEquals(_run, run)) return;
                _partial = text;
                ScheduleLive();
            });
            IProgress<TimeSpan> prepared = new Progress<TimeSpan>(_ =>
            {
                if (!ReferenceEquals(_run, run)) return;
                GuardUi(() =>
                {
                    if (_input.Text == draft) _input.Text = "";
                    _attachments.Clear();
                    _picking = false;
                    Mode = LunetAiMode.Teach;
                    BuildComposer();
                    RenderMessages();
                }, "Não foi possível atualizar o chat");
            });
            var uiProbe = Stopwatch.GetTimestamp();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                RecordDiagnostic(host, $"dispatcher_ms={Stopwatch.GetElapsedTime(uiProbe).TotalMilliseconds:F0}"),
                DispatcherPriority.Background);
            LastReply = await Task.Run(async () =>
            {
                var request = new LunetAiRequest(loaded.Settings with { Model = loaded.Settings.Model.Length > 0 ? loaded.Settings.Model : "teste" },
                    loaded.ApiKey ?? "chave-local-de-teste", context, new ShellAiBuild(_shell.Workspace, project), host.Provider,
                    Knowledge: host.LoadKnowledge(), Images: host.ImageReader, Attachments: attachments,
                    Web: loaded.Settings.WebResearch ? host.Web : null, ModelTools: host.Models.KnownTools(loaded),
                    Mode: mode, Steps: steps, PartialText: partial)
                {
                    TurnStarted = new RunProgress<TimeSpan>(elapsed =>
                    {
                        accepted = true;
                        preparationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                        prepared.Report(elapsed);
                    }),
                    FirstToken = new RunProgress<TimeSpan>(_ => firstTokenMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds),
                };
                return await session.AskAsync(prompt, request, run.Token);
            }, run.Token);
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            _shell.Report("IA interrompida. Confira a conversa para ver e desfazer alterações já gravadas.");
        }
        catch (Exception ex) when (RecoverableUiFailure(ex))
        {
            ReportFailure("A IA não concluiu a solicitação", ex);
            if (ReferenceEquals(project, _sessionProject) && _sessionLoad?.IsFaulted == true) _sessionLoad = null;
        }
        finally
        {
            _run = null;
            RecordDiagnostic(host, $"prepare_ms={Metric(preparationMs)} first_token_ms={Metric(firstTokenMs)} elapsed_ms={Metric(Stopwatch.GetElapsedTime(started).TotalMilliseconds)} managed_bytes={GC.GetTotalMemory(false)} status={LastReply?.Status.ToString() ?? (run.IsCancellationRequested ? "cancelled" : "aborted")}");
            GuardUi(() => alive?.Dispose(), "Não foi possível encerrar o serviço da IA");
            var keepActivityOpen = _liveActivity.IsOpen;
            _steps.Clear();
            _partial = "";
            if (ReferenceEquals(project, _shell.Workspace.Project))
            {
                if (accepted)
                {
                    if (_input.Text == draft) _input.Text = "";
                    _attachments.Clear();
                    _picking = false;
                    Mode = LunetAiMode.Teach;
                }
                GuardUi(() =>
                {
                    if (accepted && mode == LunetAiMode.Do) _shell.WorkspaceView.ReloadFromDisk();
                    if (!_shell.State.AiExpanded && LastReply?.Checkpoint is { IsEmpty: false } changed)
                        _shell.WorkspaceView.ReviewAiChanges(changed);
                }, "A conversa foi preservada, mas o editor não conseguiu atualizar os arquivos");
            }
            else if (_shell.Workspace.Project is { } next)
                _ = RefreshSessionAsync(host, next);
            GuardUi(() => { BuildComposer(); RenderMessages(openLatestActivity: keepActivityOpen); },
                "Não foi possível atualizar o chat");
        }
    }

    private static string Metric(double? value) => value?.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) ?? "n/a";

    public void Stop() => GuardUi(() => _run?.Cancel(), "Não foi possível interromper a IA");

    private sealed class RunProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private static bool RecoverableUiFailure(Exception ex) =>
        ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException);

    private static void RecordDiagnostic(AiHost? host, string message)
    {
        try
        {
            if (host?.Diagnostics is { } log) log(message);
            else Trace.TraceInformation("Lunet AI: " + message);
        }
        catch (Exception ex) when (RecoverableUiFailure(ex)) { }
    }

    private void ReportFailure(string message, Exception ex)
    {
        RecordDiagnostic(_shell.AiHost, $"failure={ex.GetType().Name} stack={ex.StackTrace}");
        _shell.Report(message + ".");
    }

    private void GuardUi(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (RecoverableUiFailure(ex)) { ReportFailure(message, ex); }
    }

    // ---- the live part of a run (steps and streamed text) ----
    // Every token and step used to rebuild the whole conversation (Markdown, code blocks, diffs): a long history made
    // the streaming stutter (GOAL H4: "evitar rebuild por token"). Now the history is built at the start and end of a
    // run only; while it runs, one live block is updated in place, at most ten times a second.
    private readonly StackPanel _live = new() { Spacing = 2 };
    private readonly TextBlock _livePartial = LunetUi.Text("", LunetTokens.Type.BodySmall);
    private readonly AiActivityView _liveActivity = new();
    private bool _stepsDirty;
    private readonly Avalonia.Threading.DispatcherTimer _liveTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _liveTimerHooked;

    /// <summary>How many times the whole conversation was rebuilt (tests: streaming must not rebuild it).</summary>
    public int FullRenders { get; private set; }

    private void ScheduleLive()
    {
        if (!_liveTimerHooked) { _liveTimerHooked = true; _liveTimer.Tick += (_, _) => { _liveTimer.Stop(); GuardUi(RenderLive, "Não foi possível atualizar a resposta em andamento"); }; }
        if (!_liveTimer.IsEnabled) _liveTimer.Start();
    }

    /// <summary>Updates only the live block (steps and the text so far); the rest of the conversation stays as built.</summary>
    public void RenderLive()
    {
        if (!Running) return;
        if (_rows.Count == 0 || !ReferenceEquals(_rows[^1], _live)) return;
        if (_stepsDirty)
        {
            _liveActivity.Update(_steps, running: true);
            _stepsDirty = false;
        }
        _livePartial.Text = _partial;
        _livePartial.IsVisible = _partial.Length > 0;
        FollowTail();
    }

    // A person reading an older turn keeps their position while the answer grows.
    private bool _followTail = true;
    private bool _tailQueued;

    private void JumpToLatest()
    {
        _followTail = true;
        _latest.IsVisible = false;
        FollowTail();
    }

    private void FollowTail()
    {
        if (!_followTail || _tailQueued) return;
        _tailQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _tailQueued = false;
            if (_followTail) _scroll.ScrollToEnd();
        }, DispatcherPriority.Loaded);
    }

    private void RenderMessages(bool jumpToEnd = false, bool openLatestActivity = false)
    {
        FullRenders++;
        _liveTimer.Stop();
        if (!ReferenceEquals(_renderedSession, _session))
        {
            _rows.Clear();
            _renderedTurns = 0;
            _openActivities.Clear();
            _openDiffs.Clear();
            _renderedSession = _session;
            jumpToEnd = true;
        }
        if (jumpToEnd)
        {
            _followTail = true;
            _latest.IsVisible = false;
        }
        _visibleProject = _shell.Workspace.Project?.Manifest.GameId;
        while (_rows.Count > _renderedTurns) _rows.RemoveAt(_rows.Count - 1);
        var history = _session?.History ?? [];
        if (openLatestActivity && history.LastOrDefault() is { Pending: false } latest) _openActivities.Add(latest);
        while (_renderedTurns < history.Count && !history[_renderedTurns].Pending)
        {
            _rows.Add(history[_renderedTurns]);
            _renderedTurns++;
        }
        if (history.Count == 0 && !Running)
            _rows.Add(LunetUi.Secondary("Use Estudar para entender o código ou Fazer para pedir uma alteração."));
        if (Running && _visibleProject != _sessionGame)
            _rows.Add(LunetUi.Notice($"A IA ainda está trabalhando em {_sessionName}. O chat volta ao jogo aberto quando terminar.", LunetTokens.Color.Warning));
        if (Running)
        {
            if (_live.Children.Count == 0)
            {
                _live.Children.Add(_liveActivity);
                _live.Children.Add(_livePartial);
            }
            _rows.Add(_live);
            RenderLive();
            ScheduleLive();
        }
        FollowTail();
    }

    private void RefreshTurn(LunetAiTurn turn)
    {
        var index = _rows.IndexOf(turn);
        if (index >= 0) _rows[index] = turn;
    }

    private static Control UserBubble(LunetAiTurn turn)
    {
        var body = new StackPanel { Spacing = LunetTokens.Space.Xxs };
        if (turn.Mode == LunetAiMode.Do)
            body.Children.Add(LunetUi.Text("Fazer", LunetTokens.Type.Caption, LunetTokens.Color.Primary, FontWeight.SemiBold));
        body.Children.Add(LunetUi.Text(turn.Text, LunetTokens.Type.BodySmall));
        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right, MaxWidth = 560, Padding = new Thickness(LunetTokens.Space.S),
            Margin = new Thickness(LunetTokens.Space.L, 0, 0, 0),
            CornerRadius = new CornerRadius(LunetTokens.Radius.L), Background = LunetTokens.Brush(LunetTokens.Color.PrimarySoft),
            Child = body,
        };
    }

    private Control AssistantMessage(LunetAiTurn turn, bool openActivity)
    {
        var stack = new StackPanel { Spacing = LunetTokens.Space.S };
        if (turn.Steps is { Count: > 0 } steps)
        {
            var activity = new AiActivityView();
            activity.Update(steps, running: false);
            if (openActivity) activity.SetOpen(true);
            activity.OpenChanged += open =>
            {
                if (open) _openActivities.Add(turn);
                else _openActivities.Remove(turn);
            };
            stack.Children.Add(activity);
        }
        stack.Children.Add(AiMessageContent.Render(turn.Text, text => _ = Copy(text, "Código copiado.")));
        var copy = LunetUi.IconButton(LunetIcons.Copy, "Copiar resposta em Markdown", () => _ = Copy(turn.Text, "Resposta copiada."));
        copy.Name = "AiCopyResponse";
        stack.Children.Add(copy);
        if (_session?.CheckpointFor(turn) is { IsEmpty: false } checkpoint) stack.Children.Add(Changes(turn, checkpoint));
        else if (_session?.CheckpointFor(turn) is { IsReverted: true }) stack.Children.Add(LunetUi.Text("Alterações desfeitas.", LunetTokens.Type.Caption, LunetTokens.Color.Success));
        return stack;
    }

    private async Task Copy(string text, string feedback)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
        Haptics.Confirm?.Invoke();
        _shell.Report(feedback);
    }

    /// <summary>What a reply changed: each file with its diff on a tap, and Undo for all of it (the on-disk checkpoint).</summary>
    private Control Changes(LunetAiTurn turn, LunetAiCheckpoint checkpoint)
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
                RefreshTurn(turn);
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
        card.Children.Add(LunetUi.Button("Ver no editor", ButtonKind.Ghost, LunetIcons.Code, () =>
        {
            _shell.State.ReduceAi();
            _shell.WorkspaceView.ReviewAiChanges(checkpoint);
        }));
        var undo = LunetUi.Button("Desfazer estas alterações", ButtonKind.Secondary, LunetIcons.Back, () =>
        {
            GuardUi(() =>
            {
                checkpoint.Revert();
                if (ReferenceEquals(_shell.Workspace.Project, _sessionProject)) _shell.WorkspaceView.ReloadFromDisk();
                _shell.Report("Alterações da IA desfeitas.");
                RefreshTurn(turn);
            }, "Não foi possível desfazer as alterações da IA");
        });
        card.Children.Add(undo);
        return new Border
        {
            Child = card, Padding = new Thickness(LunetTokens.Space.S), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), // depth by tone, not by border
        };
    }
}
