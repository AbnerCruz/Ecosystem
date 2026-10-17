using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using EcosystemAi.Product;
using EcosystemAi.ProjectStore;
using System.Globalization;
using System.Text;
using AColor = Android.Graphics.Color;

namespace EcosystemAi.Android;

[Activity(Label = "Ecosystem AI", MainLauncher = true, Exported = true,
    ConfigurationChanges = global::Android.Content.PM.ConfigChanges.Orientation |
        global::Android.Content.PM.ConfigChanges.ScreenSize | global::Android.Content.PM.ConfigChanges.KeyboardHidden,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public sealed partial class MainActivity : Activity
{
    private const int ImportCode = 501;
    private const int ExportCode = 502;
    private readonly AColor _background = AColor.ParseColor("#0B1220");
    private readonly AColor _card = AColor.ParseColor("#172339");
    private readonly AColor _ink = AColor.ParseColor("#E9F0FC");
    private readonly AColor _muted = AColor.ParseColor("#A8BDD9");
    private readonly AColor _accent = AColor.ParseColor("#52E0C4");
    private string _catalog = "";
    private string _journal = "";
    private string _projectRoot = "";
    private LocalProjectStore _store = null!;
    private LinearLayout _content = null!;
    private string? _projectId;
    private string? _sessionId;
    private string _assignee = "default";
    private ProductModelSettings? _settings;
    private ProductExecutionHost? _execution;
    private CancellationTokenSource? _active;
    private EditText? _message;
    private EditText? _requiredText;
    private string? _pendingExport;
    private TextView? _status;
    private readonly SessionDraftCache _drafts = new();

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            // Impede captura do campo de token e histórico pelo snapshot do SO.
            Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
            // Android can expose FilesDir through /data/data or /data/user/0 aliases.
            // Resolve only the OS-owned private root; stores still reject links below it.
            var originalRoot = FilesDir!.AbsolutePath;
            var root = FilesDir.CanonicalPath;
            _catalog = System.IO.Path.Combine(root, "catalog");
            _journal = System.IO.Path.Combine(root, "runs");
            _projectRoot = System.IO.Path.Combine(root, "projects");
            _store = new LocalProjectStore(_catalog);
            RebindPrivateWorkspaces(originalRoot);
            LoadModelPreferences();
            _projectId = savedInstanceState?.GetString("projectId");
            _sessionId = savedInstanceState?.GetString("sessionId");
            Render();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            ShowStartupFailure(error);
        }
    }

    private void RebindPrivateWorkspaces(string originalRoot)
    {
        var originalProjects = System.IO.Path.Combine(originalRoot, "projects");
        foreach (var project in _store.Read().Projects)
        {
            // Earlier APKs persisted different OS aliases. Rebind only when the
            // resolved target is exactly our private GUID directory, never outside it.
            var name = System.IO.Path.GetFileName(project.WorkspaceDirectory);
            if (!Guid.TryParseExact(name, "N", out _)) continue;
            var expected = System.IO.Path.Combine(_projectRoot, name);
            if (project.WorkspaceDirectory == expected) continue;
            using var directory = new Java.IO.File(project.WorkspaceDirectory);
            if (directory.CanonicalPath != expected)
            {
                if (project.WorkspaceDirectory == System.IO.Path.Combine(originalProjects, name))
                    throw new IOException("O workspace privado não corresponde ao diretório esperado.");
                continue; // An unrelated external workspace is never rebound.
            }
            _store.RebindWorkspace(project.Id, project.WorkspaceDirectory, expected);
        }
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        if (_projectId is not null) outState.PutString("projectId", _projectId);
        if (_sessionId is not null) outState.PutString("sessionId", _sessionId);
        // Não salvar token, configuração de gasto ou texto privado no Bundle.
        base.OnSaveInstanceState(outState);
    }

    protected override void OnPause()
    {
        _active?.Cancel(); // Sem gasto continuado em segundo plano nesta prévia.
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        _active?.Cancel();
        _settings = null;
        base.OnDestroy();
    }

    private void ShowStartupFailure(Exception error)
    {
        var cause = error;
        while (cause.InnerException is not null) cause = cause.InnerException;
        var fingerprintInput = $"{cause.GetType().FullName}|{cause.HResult}|{cause.StackTrace}";
        var diagnosticId = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)))[..12];
        var explanation = cause switch
        {
            InvalidDataException => "Os dados locais parecem inválidos ou inconsistentes.",
            System.Text.Json.JsonException => "O catálogo local não pôde ser interpretado.",
            UnauthorizedAccessException => "O Android bloqueou o acesso aos dados locais.",
            _ => "Ocorreu uma falha inesperada durante a inicialização."
        };

        var screen = Column();
        screen.SetBackgroundColor(_background);
        screen.SetPadding(Dp(24), Dp(48), Dp(24), Dp(24));
        screen.AddView(Text("Ecosystem AI não iniciou", 24, true));
        screen.AddView(Text(explanation, 17));
        screen.AddView(Text($"Diagnóstico: {cause.GetType().Name} • {diagnosticId}", 14));
        screen.AddView(Text("Se enviar esse código, consigo localizar a falha sem expor conversa, token ou caminhos do aparelho.", 14));
        SetContentView(screen);
    }

    private int Dp(int n) => (int)(n * Resources!.DisplayMetrics!.Density);
    private LinearLayout Column() => new(this) { Orientation = Orientation.Vertical };
    private TextView Text(string value, float size = 16, bool title = false)
    {
        var view = new TextView(this) { Text = value, TextSize = size };
        view.SetTextColor(title ? _ink : _muted);
        view.SetPadding(0, Dp(6), 0, Dp(6));
        if (title) view.SetTypeface(Typeface.Default, TypefaceStyle.Bold);
        return view;
    }
    private Button Button(string label, Action action, bool accent = false)
    {
        var button = new Button(this) { Text = label, Enabled = _active is null };
        button.SetAllCaps(false);
        button.SetTextColor(accent ? _background : _ink);
        var shape = new GradientDrawable();
        shape.SetColor(accent ? _accent : _card);
        shape.SetCornerRadius(Dp(12));
        button.Background = shape;
        var layout = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(52));
        layout.SetMargins(0, Dp(5), 0, Dp(5));
        button.LayoutParameters = layout;
        button.Click += (_, _) => Guard(action);
        return button;
    }
    private EditText Input(string hint, string value = "", bool multi = false, bool secret = false)
    {
        var edit = new EditText(this) { Hint = hint, Text = value, TextSize = 16, SaveEnabled = false };
        edit.SetTextColor(_ink); edit.SetHintTextColor(_muted);
        edit.InputType = secret ? InputTypes.ClassText | InputTypes.TextVariationPassword
            : InputTypes.ClassText | (multi ? InputTypes.TextFlagMultiLine : InputTypes.TextFlagCapSentences);
        edit.SetSingleLine(!multi);
        edit.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
        if (multi) edit.SetMinLines(3);
        return edit;
    }
    private void Panel(LinearLayout panel)
    {
        panel.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
        var shape = new GradientDrawable(); shape.SetColor(_card); shape.SetCornerRadius(Dp(14));
        panel.Background = shape;
        var layout = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        layout.SetMargins(0, Dp(8), 0, Dp(8));
        _content.AddView(panel, layout);
    }
    private void Dialog(string title, LinearLayout body, string action, Action submit)
    {
        if (_active is not null) throw new InvalidOperationException("Cancele ou aguarde a execução atual.");
        body.SetPadding(Dp(16), Dp(10), Dp(16), Dp(10));
        var scroll = new ScrollView(this); scroll.AddView(body);
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle(title); dialog.SetView(scroll);
        dialog.SetNegativeButton("Cancelar", (_, _) => { });
        dialog.SetPositiveButton(action, (_, _) => Guard(submit)); dialog.Show();
    }
    private void Prompt(string title, string hint, Action<string> submit)
    {
        var body = Column(); var value = Input(hint); body.AddView(value);
        Dialog(title, body, "Salvar", () => { submit(value.Text ?? ""); Render(); });
    }
    private void Notice(string title, string value)
    {
        var dialog = new AlertDialog.Builder(this); dialog.SetTitle(title); dialog.SetMessage(value);
        dialog.SetPositiveButton("Fechar", (_, _) => { }); dialog.Show();
    }
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or IOException
            or KeyNotFoundException or UnauthorizedAccessException or FormatException or OverflowException
            or Java.Lang.SecurityException)
        { Notice("Operação não concluída", e.Message); }
    }

    private ProjectEntry Project() => _store.Read().Projects.Single(p => p.Id == _projectId);
    private SessionEntry Session() => Project().Sessions.Single(s => s.Id == _sessionId);
    private void Render()
    {
        var scroll = new ScrollView(this); scroll.SetFitsSystemWindows(true); scroll.SetBackgroundColor(_background);
        _content = Column(); _content.SetPadding(Dp(18), Dp(26), Dp(18), Dp(24));
        scroll.AddView(_content); SetContentView(scroll);
        _content.AddView(Text("Ecosystem AI", 28, true));
        _content.AddView(Text("Seus projetos. Seus agentes. Seu controle.", 14));
        _content.AddView(Button("Configurações de IA", ShowAiSettings));
        var snapshot = _store.Read();
        var project = snapshot.Projects.SingleOrDefault(p => p.Id == _projectId);
        if (project is null) { _projectId = null; _sessionId = null; ShowProjects(snapshot); return; }
        var session = project.Sessions.SingleOrDefault(s => s.Id == _sessionId);
        _content.AddView(Button("← " + (session is null ? "Projetos" : project.Name), () =>
        { if (_sessionId is not null) _sessionId = null; else _projectId = null; Render(); }));
        _content.AddView(Text(project.Name, 22, true));
        if (session is null) { _sessionId = null; ShowProject(project); }
        else ShowSession(project, session);
    }

    private void ShowProjects(ProjectCatalog snapshot)
    {
        _content.AddView(Text("Prévia Android • dados locais no aparelho", 13));
        _content.AddView(Button("+ Novo projeto", () => Prompt("Novo projeto", "Nome do projeto", name =>
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120) throw new ArgumentException("Nome de 1 a 120 caracteres.");
            var workspace = System.IO.Path.Combine(_projectRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try { _projectId = _store.CreateProject(name, workspace).Id; }
            catch { Directory.Delete(workspace); throw; }
        }), true));
        if (snapshot.Projects.Count == 0)
        {
            var empty = Column(); empty.AddView(Text("Comece com um projeto", 20, true));
            empty.AddView(Text("Crie um projeto e uma conversa. Você pode organizar tudo offline; configurar IA só é necessário para executar tarefas."));
            Panel(empty);
        }
        foreach (var project in snapshot.Projects)
        {
            var p = Column(); p.AddView(Text(project.Name, 20, true));
            p.AddView(Text($"{project.Sessions.Count} conversas • {project.Sessions.Sum(s => s.Runs.Count)} execuções", 14));
            p.AddView(Button("Abrir projeto", () => { _projectId = project.Id; _sessionId = null; _assignee = "default"; Render(); })); Panel(p);
        }
    }

    private void ShowProject(ProjectEntry project)
    {
        _content.AddView(Button("+ Nova conversa", () => Prompt("Nova conversa", "Título", title =>
            _sessionId = _store.CreateSession(project.Id, title).Id), true));
        _content.AddView(Button("Adicionar arquivo ao projeto", () =>
        {
            var intent = new Intent(Intent.ActionOpenDocument); intent.SetType("*/*");
            intent.AddCategory(Intent.CategoryOpenable); StartActivityForResult(intent, ImportCode);
        }));
        _content.AddView(Button("Execuções sem recibo neste projeto", () => { _ = ShowProjectRunRecoveryAsync(project.Id); }));
        _content.AddView(Text("Arquivos", 18, true));
        foreach (var file in Directory.GetFiles(project.WorkspaceDirectory).Take(40))
            _content.AddView(Text(System.IO.Path.GetFileName(file), 14));
        _content.AddView(Button("+ Agente", () => CreateAgent(project.Id)));
        _content.AddView(Button("+ Equipe de produtor e revisor", () => CreateTeam(project.Id)));
        var roster = new LocalAgentRosterStore(_catalog).Read();
        _content.AddView(Text($"{roster.Agents.Count(a => a.ProjectId == project.Id)} agentes • {roster.Teams.Count(t => t.ProjectId == project.Id)} equipes", 14));
        foreach (var session in project.Sessions.OrderByDescending(s => s.CreatedAt))
        {
            var box = Column(); box.AddView(Text(session.Title, 20, true));
            box.AddView(Text($"{session.Turns.Count} mensagens • {session.Runs.Count} execuções", 14));
            box.AddView(Button("Abrir conversa", () => { _sessionId = session.Id; Render(); })); Panel(box);
        }
    }

    private async Task ShowProjectRunRecoveryAsync(string projectId)
    {
        try
        {
            var recovery = await Task.Run(() => ProductRunRecovery.Read(_store.Read(), projectId, _journal));
            if (IsFinishing || IsDestroyed || _projectId != projectId || _sessionId is not null) return;
            if (recovery.JournalUnavailable)
            { Notice("Journal indisponível", "Não foi possível conferir o journal. Nenhum dado foi alterado."); return; }
            var text = new StringBuilder("Execuções deste projeto sem recibo no catálogo. A conversa de origem não é inferida.\n");
            text.AppendLine("Abrir esta lista não retoma nem reenvia chamadas. Confira o provedor antes de executar novamente.\n");
            if (recovery.Truncated) text.AppendLine("Consulta parcial: limite de arquivos ou bytes atingido. Pode haver outras execuções.");
            if (recovery.UnavailableFiles > 0) text.AppendLine($"Arquivos não auditáveis no journal: {recovery.UnavailableFiles}.");
            if (recovery.Runs.Count == 0) text.AppendLine("Nenhuma execução sem recibo encontrada no trecho consultado.");
            foreach (var run in recovery.Runs)
            {
                text.AppendLine($"\n{run.RunId}\n{run.At:dd/MM HH:mm:ss} • {run.Status} • verificado: {run.Verified}");
                text.AppendLine($"Passos: {run.Steps} • ferramentas: {run.ToolCalls}");
                text.AppendLine(run.RecordedCost is { } cost
                    ? $"Valor registrado: {cost.Minor} {cost.Currency} em unidade mínima; não garante a fatura."
                    : "Custo desconhecido; não significa chamada gratuita.");
            }
            Notice("Execuções sem recibo", text.ToString());
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!IsFinishing && !IsDestroyed && _projectId == projectId && _sessionId is null)
                Notice("Consulta indisponível", "Não foi possível conferir as execuções. Nenhum dado foi alterado.");
        }
    }

    private void ShowSession(ProjectEntry project, SessionEntry session)
    {
        _content.AddView(Text(session.Title, 21, true));
        _content.AddView(Button(_settings is null ? "Selecionar modelo" : "Modelo: " + _settings.Model, ChooseSavedModel));
        _content.AddView(Button(_execution is null ? "Autorizar gasto" : "Orçamento da instância", AuthorizeBudget));
        var remaining = _execution is null ? "Sem autorização de gasto configurada"
            : $"Autorização restante: {_execution.RemainingCents} centavos USD • {_execution.RemainingRuns} execuções";
        _content.AddView(Text(remaining, 13));
        _content.AddView(Button("Agente / equipe: " + AssigneeName(project.Id), () => SelectAssignee(project.Id)));
        _content.AddView(Button("Histórico de execuções e custos", () => ShowRuns(project.Id, session.Id)));
        _content.AddView(Text("Conversa", 18, true));
        if (session.Turns.Count > 40) _content.AddView(Text("Mostrando as últimas 40 mensagens.", 13));
        for (var index = Math.Max(0, session.Turns.Count - 40); index < session.Turns.Count; index++)
        {
            var turn = session.Turns[index]; var box = Column();
            box.AddView(Text(turn.Role == "user" ? "VOCÊ" : "AGENTE", 12, true));
            var text = Text(turn.Text, 16); text.SetTextColor(_ink); text.SetTextIsSelectable(true); box.AddView(text);
            if (turn.Role == "assistant")
            {
                var turnIndex = index;
                box.AddView(Button("Salvar resposta .md", () => Export(turn.Text)));
                box.AddView(Button("Salvar no projeto", () => ReviewProjectFile(project.Id, session.Id, turnIndex)));
            }
            Panel(box);
        }
        var draft = _drafts.Get(project.Id, session.Id);
        var messageInput = _message = Input("O que você quer produzir ou investigar?", draft.Goal, multi: true);
        var requiredInput = _requiredText = Input("Trecho exato obrigatório na resposta (opcional)", draft.RequiredText);
        messageInput.SetFilters([new InputFilterLengthFilter(ProductExecutionHost.MaxText)]);
        requiredInput.SetFilters([new InputFilterLengthFilter(SessionDraftCache.MaxRequiredText)]);
        // Capturar projeto, conversa e campos desta tela: uma atualização atrasada
        // nunca deve copiar o texto para outra conversa aberta na mesma Activity.
        void SaveDraft() => _drafts.Save(project.Id, session.Id, messageInput.Text, requiredInput.Text);
        messageInput.TextChanged += (_, _) => SaveDraft();
        requiredInput.TextChanged += (_, _) => SaveDraft();
        messageInput.Enabled = _active is null; requiredInput.Enabled = _active is null;
        _content.AddView(messageInput); _content.AddView(requiredInput);
        _status = Text(_active is null ? "O agente lê os arquivos deste projeto; não sobrescreve nem exclui." : "Executando…", 13);
        _content.AddView(_status);
        _content.AddView(Button("Enviar tarefa", () => StartRun(messageInput.Text ?? "", requiredInput.Text, null), true));
        if (_active is not null)
        {
            var cancel = Button("Cancelar execução", () => _active?.Cancel());
            cancel.Enabled = true; _content.AddView(cancel);
        }
        _content.AddView(Button("+ Planejar tarefa sem executar IA", () => PlanTask(project.Id, session.Id)));
        foreach (var task in new LocalPlannedTaskStore(_catalog).Read().Tasks
            .Where(t => t.ProjectId == project.Id && t.SessionId == session.Id))
        {
            var box = Column(); box.AddView(Text(task.Title, 18, true));
            box.AddView(Text(task.Goal)); box.AddView(Text("Critério: " + task.Acceptance, 13));
            box.AddView(Text(task.Cancelled ? "Cancelada" : $"{task.Attempts.Count} tentativas • confira os recibos", 13));
            if (!task.Cancelled && task.Attempts.Count < 20)
            {
                box.AddView(Button("Executar agora", () => StartRun(ProductTaskRequest.Compose(task),
                    task.Acceptance.StartsWith("response-contains:", StringComparison.Ordinal)
                        ? task.Acceptance["response-contains:".Length..] : null, task)));
                box.AddView(Button("Cancelar planejamento", () => { new LocalPlannedTaskStore(_catalog).Cancel(task.Id); Render(); }));
            }
            Panel(box);
        }
        ShowReviews(project.Id, session.Id);
    }

    private void StartRun(string goal, string? required, PlannedTaskEntry? planned)
    {
        if (_active is not null) throw new InvalidOperationException("Uma execução já está ativa.");
        if (_settings is null) { ChooseSavedModel(); return; }
        if (_execution is null) { AuthorizeBudget(); return; }
        if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Escreva uma solicitação.");
        var projectId = _projectId!; var sessionId = _sessionId!;
        var settings = _settings; var execution = _execution;
        var assignee = planned?.Assignee ?? _assignee;
        var criterion = string.IsNullOrWhiteSpace(required) ? null : required;
        var cancel = new CancellationTokenSource(); _active = cancel;
        Render();
        _ = RunAsync();
        async Task RunAsync()
        {
            string? message = null;
            bool clearDraft = false;
            try
            {
                await Task.Run(async () =>
                {
                    ProductRunResult result; ProductTeamResult? team = null;
                    if (assignee.StartsWith("team:", StringComparison.Ordinal))
                    {
                        team = await execution.ExecuteTeamAsync(settings, projectId, sessionId, goal,
                            assignee[5..], criterion, cancel.Token); result = team.Producer;
                    }
                    else result = await execution.ExecuteAsync(settings, projectId, sessionId, goal, criterion,
                        assignee.StartsWith("agent:", StringComparison.Ordinal) ? assignee[6..] : null, cancel.Token);
                    if (planned is null && result.Receipt.Status == "succeeded")
                        clearDraft = true;
                    if (planned is not null)
                    {
                        var ids = team?.Reviewer is null ? new[] { result.Receipt.RunId }
                            : new[] { result.Receipt.RunId, team.Reviewer.Receipt.RunId };
                        var recordedReview = new LocalTeamReviewStore(_catalog).Read().Reviews.Any(r =>
                            r.ProjectId == projectId && r.SessionId == sessionId && r.ProducerRunId == ids[0]
                            && ids.Length == 2 && r.ReviewerRunId == ids[1]);
                        var outcome = recordedReview ? "review_recorded"
                            : team is null && result.Receipt.Status == "succeeded" && result.Receipt.Verified
                                ? "response_verified" : "failed";
                        new LocalPlannedTaskStore(_catalog).RecordAttempt(planned.Id, outcome, ids);
                    }
                    if (team is not null && (team.Reviewer is null || !team.Reviewer.Receipt.Verified))
                        message = "O produtor executou, mas a revisão não foi concluída. Consulte os recibos; não há retry automático.";
                });
            }
            catch (System.OperationCanceledException) { message = "Execução interrompida. Confira a auditoria antes de reenviar."; }
            catch (Exception) { message = "Execução não concluída. Confira a configuração e os recibos; nenhuma tentativa automática foi iniciada."; }
            finally
            {
                _active = null; cancel.Dispose();
                if (!IsFinishing && !IsDestroyed) RunOnUiThread(() =>
                {
                    if (clearDraft) _drafts.Clear(projectId, sessionId);
                    Guard(Render); if (message is not null) Notice("Execução", message);
                });
            }
        }
    }

    private void CreateAgent(string projectId)
    {
        var body = Column(); var name = Input("Nome"); var instructions = Input("Papel e instruções", multi: true);
        body.AddView(name); body.AddView(instructions);
        Dialog("Novo agente", body, "Criar", () =>
        { new LocalAgentRosterStore(_catalog).CreateAgent(projectId, name.Text ?? "", instructions.Text ?? ""); Render(); });
    }
    private Spinner Select(IReadOnlyList<string> names)
    {
        var spinner = new Spinner(this);
        spinner.Adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, names.ToArray());
        return spinner;
    }
    private void CreateTeam(string projectId)
    {
        var agents = new LocalAgentRosterStore(_catalog).Read().Agents.Where(a => a.ProjectId == projectId).ToArray();
        if (agents.Length < 2) throw new InvalidOperationException("Crie dois agentes diferentes primeiro.");
        var body = Column(); var name = Input("Nome da equipe"); body.AddView(name);
        var producer = Select(agents.Select(a => a.Name).ToArray()); var reviewer = Select(agents.Select(a => a.Name).ToArray());
        reviewer.SetSelection(1);
        body.AddView(Text("Produtor")); body.AddView(producer); body.AddView(Text("Revisor independente")); body.AddView(reviewer);
        Dialog("Nova equipe", body, "Criar", () =>
        { new LocalAgentRosterStore(_catalog).CreateTeam(projectId, name.Text ?? "", agents[producer.SelectedItemPosition].Id, agents[reviewer.SelectedItemPosition].Id); Render(); });
    }
    private string AssigneeName(string projectId)
    {
        var roster = new LocalAgentRosterStore(_catalog).Read();
        return roster.Agents.FirstOrDefault(a => "agent:" + a.Id == _assignee && a.ProjectId == projectId)?.Name
            ?? roster.Teams.FirstOrDefault(t => "team:" + t.Id == _assignee && t.ProjectId == projectId)?.Name ?? "Assistente";
    }
    private void SelectAssignee(string projectId)
    {
        var roster = new LocalAgentRosterStore(_catalog).Read();
        var choices = new List<(string Id, string Name)> { ("default", "Assistente") };
        choices.AddRange(roster.Agents.Where(a => a.ProjectId == projectId).Select(a => ("agent:" + a.Id, "Agente: " + a.Name)));
        choices.AddRange(roster.Teams.Where(t => t.ProjectId == projectId).Select(t => ("team:" + t.Id, "Equipe: " + t.Name + " (2 execuções)")));
        var dialog = new AlertDialog.Builder(this); dialog.SetTitle("Quem executa?");
        dialog.SetItems(choices.Select(c => c.Name).ToArray(), (_, e) => { _assignee = choices[e.Which].Id; Guard(Render); });
        dialog.Show();
    }
    private void PlanTask(string projectId, string sessionId)
    {
        var body = Column(); var title = Input("Título"); var goal = Input("Objetivo", multi: true);
        var exact = Input("Trecho obrigatório (opcional)"); body.AddView(title); body.AddView(goal); body.AddView(exact);
        body.AddView(Text("Planejar não chama IA. Executor: " + AssigneeName(projectId), 13));
        Dialog("Planejar tarefa", body, "Planejar", () =>
        {
            var acceptance = string.IsNullOrWhiteSpace(exact.Text) ? "Resposta presente; avaliação de conteúdo manual."
                : "response-contains:" + exact.Text;
            new LocalPlannedTaskStore(_catalog).Create(projectId, sessionId, title.Text ?? "", goal.Text ?? "", acceptance, _assignee);
            Render();
        });
    }
    private void ShowRuns(string projectId, string sessionId)
    {
        var session = _store.Read().Projects.Single(p => p.Id == projectId)
            .Sessions.Single(s => s.Id == sessionId);
        var runs = session.Runs.TakeLast(50).Reverse().ToArray();
        if (runs.Length == 0) { Notice("Execuções da conversa", "Nenhuma execução registrada."); return; }
        var labels = runs.Select(r => $"{r.Status} • {r.CostMinor} {r.Currency} • {r.At:dd/MM HH:mm}\n{r.RunId}").ToArray();
        var dialog = new AlertDialog.Builder(this);
        dialog.SetTitle("Execuções • selecione para auditar");
        dialog.SetItems(labels, (sender, args) => { _ = ShowRunAuditAsync(projectId, sessionId, runs[args.Which].RunId); });
        dialog.SetNegativeButton("Fechar", (_, _) => { });
        dialog.Show();
    }

    private async Task ShowRunAuditAsync(string projectId, string sessionId, string runId)
    {
        try
        {
            var audit = await Task.Run(async () => await ProductRunAudit.ReadAsync(
                _store.Read(), projectId, sessionId, runId, _journal));
            if (IsFinishing || IsDestroyed || _projectId != projectId || _sessionId != sessionId) return;
            if (audit.Availability == RunAuditAvailability.Missing)
            { Notice("Auditoria indisponível", "Journal ausente. O recibo sozinho não comprova o replay da execução."); return; }
            if (audit.Availability == RunAuditAvailability.Unavailable)
            { Notice("Auditoria indisponível", "Não foi possível conferir o journal desta execução. Nenhum dado foi alterado."); return; }
            var text = new StringBuilder();
            if (audit.Availability == RunAuditAvailability.ReceiptMismatch)
                text.AppendLine("ATENÇÃO: o recibo diverge do journal. Confira os dados antes de reenviar.\n");
            text.AppendLine($"Estado no Runtime: {audit.Status} • verificado: {audit.Verified}");
            text.AppendLine($"Passos: {audit.Steps} • chamadas de ferramenta: {audit.ToolCalls}");
            text.AppendLine(audit.RecordedCost is { } cost
                ? $"Valor registrado: {cost.Minor} {cost.Currency} em unidade mínima{(audit.CostEstimated ? " (estimativa)" : "")}."
                : "Custo não informado no journal; não significa chamada gratuita.");
            text.AppendLine(audit.Tokens is { } tokens
                ? $"Tokens das respostas: {tokens.InputTokens} de entrada • {tokens.OutputTokens} de saída."
                : "Tokens não disponíveis para todas as respostas deste run.");
            text.AppendLine("Valores registrados não garantem a fatura do provedor. Nenhuma chamada ou retomada foi iniciada.");
            text.AppendLine($"\nEventos: {audit.TotalEvents}. Mostrando os últimos {audit.Events!.Count}.");
            foreach (var item in audit.Events)
                text.AppendLine($"#{item.Sequence} • {item.At:HH:mm:ss} • {item.Kind}");
            Notice("Auditoria da execução", text.ToString());
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (!IsFinishing && !IsDestroyed && _projectId == projectId && _sessionId == sessionId)
                Notice("Auditoria indisponível", "Não foi possível conferir a execução. Nenhum dado foi alterado.");
        }
    }
    private void ShowReviews(string projectId, string sessionId)
    {
        foreach (var review in new LocalTeamReviewStore(_catalog).Read().Reviews.Where(r => r.ProjectId == projectId && r.SessionId == sessionId))
        {
            var panel = Column(); panel.AddView(Text("Parecer da equipe", 18, true));
            panel.AddView(Text("Produtor: " + review.ProducerRunId + "\nRevisor: " + review.ReviewerRunId, 12));
            panel.AddView(Text("Decisão: " + review.Decision + " • nenhuma integração de arquivos", 13));
            if (review.Decision == "pending_owner")
            {
                panel.AddView(Button("Aceitar parecer", () => Decide("owner_accepted")));
                panel.AddView(Button("Rejeitar parecer", () => Decide("owner_rejected")));
                void Decide(string decision) => Prompt("Justificativa da decisão", "Por quê?", note =>
                    new LocalTeamReviewStore(_catalog).Decide(review.Id, decision, note));
            }
            Panel(panel);
        }
    }
    private void Export(string value)
    {
        _pendingExport = value;
        var intent = new Intent(Intent.ActionCreateDocument); intent.SetType("text/markdown");
        intent.AddCategory(Intent.CategoryOpenable); intent.PutExtra(Intent.ExtraTitle, "resposta-ecosystem-ai.md");
        StartActivityForResult(intent, ExportCode);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (resultCode != Result.Ok || data?.Data is null) { if (requestCode == ExportCode) _pendingExport = null; return; }
        var uri = data.Data;
        Guard(() =>
        {
            if (requestCode == ExportCode)
            {
                var value = _pendingExport ?? throw new InvalidOperationException("Abra a conversa e exporte novamente.");
                using var output = ContentResolver!.OpenOutputStream(uri) ?? throw new IOException("Não foi possível abrir o destino.");
                var bytes = new UTF8Encoding(false).GetBytes(value); output.Write(bytes); output.Flush(); _pendingExport = null;
                Toast.MakeText(this, "Resposta salva.", ToastLength.Short)!.Show();
            }
            else if (requestCode == ImportCode)
            {
                var project = Project();
                using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Arquivo não acessível.");
                using var memory = new MemoryStream(); var buffer = new byte[8192]; int count;
                while ((count = input.Read(buffer)) > 0)
                {
                    if (memory.Length + count > 64 * 1024) throw new IOException("Importe arquivo textual de até 64 KiB.");
                    memory.Write(buffer, 0, count);
                }
                var value = new UTF8Encoding(false, true).GetString(memory.ToArray());
                if (value.Contains('\0')) throw new IOException("Arquivo binário não é aceito.");
                string display = "arquivo.txt";
                using (var cursor = ContentResolver.Query(uri, null, null, null, null))
                    if (cursor?.MoveToFirst() == true)
                    {
                        var index = cursor.GetColumnIndex(global::Android.Provider.OpenableColumns.DisplayName);
                        if (index >= 0) display = cursor.GetString(index) ?? display;
                    }
                var name = System.Text.RegularExpressions.Regex.Replace(System.IO.Path.GetFileName(display), "[^a-zA-Z0-9._-]", "_");
                if (name.Length == 0 || name.Length > 96 || name.StartsWith('.')) name = "arquivo-" + Guid.NewGuid().ToString("N") + ".txt";
                using var file = new FileStream(System.IO.Path.Combine(project.WorkspaceDirectory, name), FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(file, new UTF8Encoding(false)); writer.Write(value); writer.Flush(); file.Flush(true);
                Render(); Toast.MakeText(this, "Arquivo adicionado ao projeto.", ToastLength.Short)!.Show();
            }
        });
    }
}
