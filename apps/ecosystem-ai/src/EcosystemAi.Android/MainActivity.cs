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
public sealed class MainActivity : Activity
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
    private string _draftGoal = "";
    private string _draftRequired = "";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Impede captura do campo de token e histórico pelo snapshot do SO.
        Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
        var root = FilesDir!.AbsolutePath;
        _catalog = System.IO.Path.Combine(root, "catalog");
        _journal = System.IO.Path.Combine(root, "runs");
        _projectRoot = System.IO.Path.Combine(root, "projects");
        _store = new LocalProjectStore(_catalog);
        _projectId = savedInstanceState?.GetString("projectId");
        _sessionId = savedInstanceState?.GetString("sessionId");
        Guard(Render);
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

    private void ShowSession(ProjectEntry project, SessionEntry session)
    {
        _content.AddView(Text(session.Title, 21, true));
        _content.AddView(Button(_settings is null ? "Configurar IA e orçamento" : "IA: " + _settings.Model, Configure));
        var remaining = _execution is null ? "Sem autorização de gasto configurada"
            : $"Autorização restante: {_execution.RemainingCents} centavos USD • {_execution.RemainingRuns} execuções";
        _content.AddView(Text(remaining, 13));
        _content.AddView(Button("Agente / equipe: " + AssigneeName(project.Id), () => SelectAssignee(project.Id)));
        _content.AddView(Button("Histórico de execuções e custos", () => ShowRuns(session)));
        _content.AddView(Text("Conversa", 18, true));
        if (session.Turns.Count > 40) _content.AddView(Text("Mostrando as últimas 40 mensagens.", 13));
        for (var index = Math.Max(0, session.Turns.Count - 40); index < session.Turns.Count; index++)
        {
            var turn = session.Turns[index]; var box = Column();
            box.AddView(Text(turn.Role == "user" ? "VOCÊ" : "AGENTE", 12, true));
            var text = Text(turn.Text, 16); text.SetTextColor(_ink); text.SetTextIsSelectable(true); box.AddView(text);
            if (turn.Role == "assistant")
                box.AddView(Button("Salvar resposta .md", () => Export(turn.Text)));
            Panel(box);
        }
        _message = Input("O que você quer produzir ou investigar?", _draftGoal, multi: true);
        _requiredText = Input("Trecho exato obrigatório na resposta (opcional)", _draftRequired);
        _message.TextChanged += (_, _) => _draftGoal = _message.Text ?? "";
        _requiredText.TextChanged += (_, _) => _draftRequired = _requiredText.Text ?? "";
        _message.Enabled = _active is null; _requiredText.Enabled = _active is null;
        _content.AddView(_message); _content.AddView(_requiredText);
        _status = Text(_active is null ? "O agente lê os arquivos deste projeto; não sobrescreve nem exclui." : "Executando…", 13);
        _content.AddView(_status);
        _content.AddView(Button("Enviar tarefa", () => StartRun(_message.Text ?? "", _requiredText.Text, null), true));
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

    private void Configure()
    {
        var prefs = GetSharedPreferences("model-ui", FileCreationMode.Private)!;
        var body = Column(); body.AddView(Text("O token fica somente em memória. A configuração autoriza os limites abaixo para esta instância.", 14));
        var endpoint = Input("Endpoint HTTPS completo", _settings?.Endpoint ?? prefs.GetString("endpoint", "") ?? "");
        var model = Input("ID do modelo", _settings?.Model ?? prefs.GetString("model", "") ?? "");
        var key = Input("Chave de API", _settings?.ApiKey ?? "", secret: true);
        var run = Input("Centavos USD por execução", (_settings?.RunBudgetCents ?? 20).ToString(CultureInfo.InvariantCulture));
        var call = Input("Centavos USD por chamada", (_settings?.MaxCallCents ?? 5).ToString(CultureInfo.InvariantCulture));
        var total = Input("Autorizar até quantos centavos USD nesta instância?", "100");
        var inputPrice = Input("Preço de entrada USD / milhão de tokens", _settings?.InputUsdPerMillion.ToString(CultureInfo.InvariantCulture) ?? "");
        var outputPrice = Input("Preço de saída USD / milhão de tokens", _settings?.OutputUsdPerMillion.ToString(CultureInfo.InvariantCulture) ?? "");
        var history = new CheckBox(this) { Text = "Reenviar histórico da conversa ao provedor", Checked = _settings?.UseHistory ?? false };
        history.SetTextColor(_ink);
        foreach (var view in new View[] { endpoint, model, key, run, call, total, inputPrice, outputPrice, history }) body.AddView(view);
        Dialog("IA e autorização de gasto", body, "Autorizar", () =>
        {
            decimal Price(EditText e) => decimal.Parse((e.Text ?? "").Replace(',', '.'), CultureInfo.InvariantCulture);
            var settings = new ProductModelSettings
            {
                Endpoint = (endpoint.Text ?? "").Trim(), Model = (model.Text ?? "").Trim(), ApiKey = key.Text,
                RunBudgetCents = long.Parse(run.Text ?? "", CultureInfo.InvariantCulture),
                MaxCallCents = long.Parse(call.Text ?? "", CultureInfo.InvariantCulture),
                InputUsdPerMillion = Price(inputPrice), OutputUsdPerMillion = Price(outputPrice), UseHistory = history.Checked
            };
            settings.Validate();
            if (new Uri(settings.Endpoint).Scheme != "https") throw new ArgumentException("A prévia Android aceita endpoint HTTPS.");
            var limit = long.Parse(total.Text ?? "", CultureInfo.InvariantCulture);
            if (limit < settings.RunBudgetCents) throw new ArgumentException("Teto da instância menor que o orçamento de uma execução.");
            _execution = new ProductExecutionHost(_catalog, _journal, limit);
            _settings = settings;
            // Apenas endpoint/modelo, nunca chave nem nova autorização automática após restart.
            prefs.Edit()!.PutString("endpoint", settings.Endpoint)!.PutString("model", settings.Model)!.Apply();
            key.Text = ""; Render();
        });
    }

    private void StartRun(string goal, string? required, PlannedTaskEntry? planned)
    {
        if (_active is not null) throw new InvalidOperationException("Uma execução já está ativa.");
        if (_settings is null || _execution is null) { Configure(); return; }
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
                    { _draftGoal = ""; _draftRequired = ""; }
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
    private void ShowRuns(SessionEntry session)
    {
        var lines = session.Runs.TakeLast(50).Select(r =>
            $"{r.RunId}\n{r.Status} • verificado: {r.Verified}\n{r.CostMinor} centavos {r.Currency} registrados{(r.CostEstimated ? " (estimativa)" : "")}\n{r.Verification}\n");
        Notice("Recibos da conversa", session.Runs.Count == 0 ? "Nenhuma execução registrada." :
            "Valores registrados no Runtime; confira a fatura do provedor.\n\n" + string.Join("\n", lines));
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
