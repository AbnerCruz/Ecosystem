using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Aprender in the shell (P3): the course catalog, the first track's projects and each challenge with its objectives,
/// research and acceptance. Progress is the student's own declaration, kept in the same file as the previous screens
/// (<see cref="LunetShell.LearningProgressPath"/>), so nothing is lost moving between them. Each challenge is an ordinary
/// game: created and linked by its GameId, opened in the workspace, with the offline reference one tap away.
/// </summary>
public sealed class LearnView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly ScrollViewer _scroll = new();
    private readonly TextBlock _title = LunetUi.Title("Aprender");
    private readonly Button _back;
    private readonly Stack<Action> _history = new();
    private LearningProgressStore? _progress;
    private string? _progressError;
    private Action _current;

    public LearnView(LunetShell shell, Action close)
    {
        _shell = shell;
        _close = close;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        _back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Back());
        _title.VerticalAlignment = VerticalAlignment.Center;
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(_back, Dock.Left);
        bar.Children.Add(_back);
        bar.Children.Add(_title);
        _scroll.Content = new Border { Child = _page, MaxWidth = 720, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xl) };
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(_scroll);
        Content = root;
        _current = ShowCatalog;
        ShowCatalog();
    }

    /// <summary>What is on screen: "catalog", a course id or a project id (tests and screen readers).</summary>
    public string Showing { get; private set; } = "catalog";

    /// <summary>Back inside Aprender; at the catalog it closes Aprender. Always consumed.</summary>
    public bool Back()
    {
        if (_history.Count == 0) { _close(); return true; }
        _current = _history.Pop();
        _current();
        return true;
    }

    private void Go(Action page)
    {
        _history.Push(_current);
        _current = page;
        page();
    }

    private LearningProgressStore? Progress()
    {
        if (_progress is not null) return _progress;
        try { _progress = new LearningProgressStore(_shell.LearningProgressPath); _progressError = null; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _progressError = "O progresso não pôde ser lido. O arquivo foi preservado: " + e.Message;
        }
        return _progress;
    }

    private void Begin(string title, string showing)
    {
        _title.Text = title;
        Showing = showing;
        _page.Children.Clear();
        _scroll.Offset = default;
    }

    // ---- catalog ----

    public void ShowCatalog()
    {
        Begin("Aprender", "catalog");
        _page.Children.Add(LunetUi.Secondary("Aprenda a fazer jogos fazendo jogos. Cada desafio é um jogo seu, em C#, que você roda e muda."));
        var progress = Progress();
        if (_progressError is not null) ProgressProblem();
        if (progress is not null)
        {
            var total = LearningCatalog.Projects.Sum(p => p.Steps.Count);
            var next = LearningCatalog.Projects.FirstOrDefault(p => progress.CompletedCountFor(p.Id) < p.Steps.Count) ?? LearningCatalog.Projects[0];
            var card = new StackPanel { Spacing = LunetTokens.Space.Xs };
            card.Children.Add(LunetUi.Text("SUA TRILHA", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold));
            card.Children.Add(LunetUi.Text($"{progress.OverallCompletedCount} de {total} objetivos", LunetTokens.Type.Title, weight: FontWeight.SemiBold));
            card.Children.Add(Bar(progress.OverallCompletedCount, total));
            var resume = LunetUi.Button("Continuar · " + next.Title, ButtonKind.Primary, LunetIcons.Play, () => Go(() => ShowProject(next.Id)));
            resume.HorizontalAlignment = HorizontalAlignment.Left;
            resume.Name = "Resume";
            card.Children.Add(resume);
            _page.Children.Add(LunetUi.Card(card));
        }
        string? track = null;
        foreach (var course in LearningCatalog.Courses)
        {
            if (course.Track != track)
            {
                track = course.Track;
                var heading = LunetUi.Text(track, LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
                heading.Margin = new Thickness(0, LunetTokens.Space.M, 0, 0);
                _page.Children.Add(heading);
            }
            var done = progress is null ? 0 : LearningCatalog.Projects.Where(p => p.CourseId == course.Id).Sum(p => progress.CompletedCountFor(p.Id));
            var steps = LearningCatalog.Projects.Where(p => p.CourseId == course.Id).Sum(p => p.Steps.Count);
            var c = course;
            _page.Children.Add(Row(course.Title,
                course.Available ? $"{course.Id} · {done} de {steps} objetivos" : $"{course.Id} · em breve",
                course.Available ? () => Go(() => ShowCourse(c.Id)) : null, "Course-" + course.Id));
        }
    }

    // ---- course ----

    public void ShowCourse(string courseId)
    {
        var course = LearningCatalog.Find(courseId);
        if (course is null || !course.Available) { ShowCatalog(); return; }
        Begin(course.Title, courseId);
        _page.Children.Add(LunetUi.Secondary("Cada desafio vira um jogo C# comum, que abre no editor e roda ao lado do código. Você decide como fazer."));
        var progress = Progress();
        foreach (var project in LearningCatalog.Projects.Where(p => p.CourseId == courseId))
        {
            var done = progress?.CompletedCountFor(project.Id) ?? 0;
            var body = new StackPanel { Spacing = LunetTokens.Space.Xxs };
            body.Children.Add(LunetUi.Text(project.Title, LunetTokens.Type.Callout, LunetTokens.Color.Primary, FontWeight.SemiBold));
            body.Children.Add(LunetUi.Text(project.Mission, LunetTokens.Type.Label, LunetTokens.Color.TextSecondary));
            body.Children.Add(Bar(done, project.Steps.Count));
            body.Children.Add(LunetUi.Text($"{done} de {project.Steps.Count} objetivos", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
            var p = project;
            var b = Tappable(body, () => Go(() => ShowProject(p.Id)), project.Title);
            b.Name = "Project-" + project.Id;
            _page.Children.Add(b);
        }
        _page.Children.Add(LunetUi.Text("O segundo desafio de cada curso pede mais autonomia. As caixas marcam o que você declara ter feito; não há correção automática.",
            LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
    }

    // ---- project (challenge) ----

    public void ShowProject(string projectId)
    {
        var challenge = LearningCatalog.FindProject(projectId);
        if (challenge is null) { ShowCatalog(); return; }
        Begin(challenge.Title, projectId);
        _page.Children.Add(Section("Missão"));
        _page.Children.Add(Body(challenge.Mission));

        var progress = Progress();
        var linked = progress is null ? null : LinkedGame(progress.ProjectGameIdFor(challenge.Id));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        if (linked is not null)
        {
            var open = LunetUi.Button("Abrir " + linked, ButtonKind.Primary, LunetIcons.Play, () => OpenChallengeGame(linked, null));
            open.Name = "OpenChallenge";
            actions.Children.Add(open);
        }
        else if (progress is not null)
        {
            var create = LunetUi.Button("Criar o jogo do desafio", ButtonKind.Primary, LunetIcons.Plus, () => CreateChallengeGame(challenge));
            create.Name = "CreateChallenge";
            actions.Children.Add(create);
        }
        var reference = LunetUi.Button("Referência", ButtonKind.Secondary, LunetIcons.Book, () =>
        {
            if (linked is not null) OpenChallengeGame(linked, challenge.ReferenceTarget);
            else Go(() => ShowReference(challenge));
        });
        reference.Name = "Reference";
        actions.Children.Add(reference);
        _page.Children.Add(new ScrollViewer
        {
            Content = actions, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });
        if (linked is null && progress?.ProjectGameIdFor(challenge.Id) is not null)
            _page.Children.Add(LunetUi.Text("O jogo ligado a este desafio não está na pasta de jogos (apagado, renomeado em outro app ou na lixeira). Restaure-o ou crie outro; as marcações dele ficam guardadas.",
                LunetTokens.Type.Caption, LunetTokens.Color.Warning));

        _page.Children.Add(Section("Objetivos"));
        if (progress is null) ProgressProblem();
        else
        {
            var summary = LunetUi.Text("", LunetTokens.Type.Callout, weight: FontWeight.SemiBold);
            var bar = Bar(0, challenge.Steps.Count);
            void Update()
            {
                var count = progress.CompletedCountFor(challenge.Id);
                summary.Text = $"{count} de {challenge.Steps.Count} objetivos";
                bar.Value = count;
            }
            Update();
            _page.Children.Add(summary);
            _page.Children.Add(bar);
            foreach (var step in challenge.Steps)
            {
                var check = new CheckBox
                {
                    Content = LunetUi.Text(step.Objective, LunetTokens.Type.Body), MinHeight = LunetTokens.MinTouch,
                    IsChecked = progress.Completed(challenge.Id, step.Id), Name = "Step-" + step.Id, VerticalContentAlignment = VerticalAlignment.Center,
                };
                AutomationProperties.SetName(check, "Declarar feito: " + step.Objective);
                var restoring = false;
                check.IsCheckedChanged += (_, _) =>
                {
                    if (restoring) return;
                    try { progress.SetCompleted(challenge.Id, step.Id, check.IsChecked == true); Update(); Haptics.Confirm?.Invoke(); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        // Never pretend it was saved: the box goes back and the reason is shown.
                        restoring = true; check.IsChecked = progress.Completed(challenge.Id, step.Id); restoring = false;
                        _page.Children.Insert(_page.Children.IndexOf(check) + 1, LunetUi.Text("Não foi possível salvar: " + e.Message, LunetTokens.Type.Caption, LunetTokens.Color.Danger));
                    }
                };
                _page.Children.Add(check);
            }
            _page.Children.Add(LunetUi.Text("Marque depois de testar no jogo rodando. A marca é sua declaração, não um teste automático.",
                LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        }
        _page.Children.Add(Section("Pesquisar"));
        _page.Children.Add(Body(challenge.Investigate));
        _page.Children.Add(Section("Critérios de aceite"));
        _page.Children.Add(Body(challenge.Acceptance));
        _page.Children.Add(Section("Desafio extra"));
        _page.Children.Add(Body(challenge.Transfer));
        if (progress is not null && _shell.Workspace.Projects().Count > 0)
        {
            var associate = LunetUi.Button("Ligar a um jogo que já existe", ButtonKind.Ghost, null, () => Go(() => ShowAssociate(challenge)));
            associate.HorizontalAlignment = HorizontalAlignment.Left;
            _page.Children.Add(associate);
        }
    }

    /// <summary>The offline reference of a challenge, inside Aprender (no game open yet).</summary>
    private void ShowReference(LearningProject challenge)
    {
        Begin("Referência", challenge.Id + ":ref");
        var docs = new DocsView { MinHeight = 480 };
        docs.Navigate(challenge.ReferenceTarget);
        _page.Children.Add(docs);
    }

    private void ShowAssociate(LearningProject challenge)
    {
        Begin("Ligar a um jogo", challenge.Id + ":link");
        _page.Children.Add(LunetUi.Secondary($"Escolha o jogo em que você está fazendo \"{challenge.Title}\". As marcações seguem o jogo."));
        foreach (var name in _shell.Workspace.Projects())
        {
            var n = name;
            _page.Children.Add(Row(name, "", () =>
            {
                try
                {
                    var project = _shell.Workspace.Store.Open(n);
                    Progress()!.LinkProject(challenge.Id, n, project.Manifest.GameId);
                    _current = () => ShowProject(challenge.Id);
                    _history.TryPop(out _);
                    ShowProject(challenge.Id);
                }
                catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException or ArgumentException)
                {
                    _page.Children.Add(LunetUi.Text(e.Message, LunetTokens.Type.Caption, LunetTokens.Color.Danger));
                }
            }, "Link-" + name));
        }
    }

    /// <summary>The game name whose GameId is the linked one, when exactly one game has it.</summary>
    private string? LinkedGame(string? gameId)
    {
        if (gameId is null) return null;
        string? found = null;
        foreach (var name in _shell.Workspace.Projects())
        {
            try
            {
                if (_shell.Workspace.Store.Open(name).Manifest.GameId != gameId) continue;
                if (found is not null) return null; // duplicated identity: ambiguous, link again
                found = name;
            }
            catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException) { }
        }
        return found;
    }

    private void CreateChallengeGame(LearningProject challenge)
    {
        try
        {
            var name = _shell.Workspace.Store.FreeName("Aprender" + challenge.Id.Replace("-", "", StringComparison.Ordinal));
            var project = _shell.Workspace.Store.Create(name, ProjectTemplate.Blank, challenge.Orientation);
            Progress()!.LinkProject(challenge.Id, name, project.Manifest.GameId);
            OpenChallengeGame(name, null); // Objetivos beside the code; its Referência opens the exact page
        }
        catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _page.Children.Insert(1, LunetUi.Text("Não foi possível criar o jogo: " + e.Message, LunetTokens.Type.Caption, LunetTokens.Color.Danger));
        }
    }

    /// <summary>Opens the challenge's game; with a reference, the Documentação panel opens on it beside the code.</summary>
    private void OpenChallengeGame(string name, string? reference)
    {
        _close();
        _shell.OpenProject(name);
        // Opening a mission lands in its game with the objectives beside the code (anexo A §6.4), not in the catalog.
        if (reference is null) { _shell.State.ShowPanel(Shell.SupportPanel.Objectives); return; }
        _shell.State.ShowPanel(Shell.SupportPanel.Docs);
        _shell.WorkspaceView.Support.Docs.Navigate(reference);
    }

    private void ProgressProblem()
    {
        _page.Children.Add(LunetUi.Text(_progressError ?? "Progresso indisponível.", LunetTokens.Type.Label, LunetTokens.Color.Warning));
        if (!LearningProgressStore.HasRecovery(_shell.LearningProgressPath)) return;
        var restore = LunetUi.Button("Restaurar a cópia do progresso", ButtonKind.Secondary, null, () =>
        {
            try { LearningProgressStore.RestoreBackup(_shell.LearningProgressPath); _progress = null; _current(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { _progressError = e.Message; _current(); }
        });
        restore.HorizontalAlignment = HorizontalAlignment.Left;
        _page.Children.Add(restore);
    }

    // ---- components ----

    private static ProgressBar Bar(int value, int max) => new()
    {
        Minimum = 0, Maximum = Math.Max(1, max), Value = value, Height = 6, MinHeight = 6, CornerRadius = new CornerRadius(3),
        Foreground = LunetTokens.Brush(LunetTokens.Color.Primary), Background = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay),
    };

    private static TextBlock Section(string text) => LunetUi.Section(text);

    private static TextBlock Body(string text)
    {
        var t = LunetUi.Text(text, LunetTokens.Type.Body);
        t.LineHeight = LunetTokens.Type.Body * 1.45;
        return t;
    }

    private static Button Tappable(Control content, Action onClick, string name)
    {
        var b = new Button
        {
            Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S),
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(LunetTokens.Radius.L),
        };
        AutomationProperties.SetName(b, name);
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Control Row(string title, string detail, Action? onClick, string name)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(LunetUi.Text(title, LunetTokens.Type.Callout, onClick is null ? LunetTokens.Color.TextTertiary : LunetTokens.Color.TextPrimary, FontWeight.SemiBold));
        if (detail.Length > 0) stack.Children.Add(LunetUi.Text(detail, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
        if (onClick is null) return new Border { Child = stack, Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S), MinHeight = LunetTokens.MinTouch };
        var b = Tappable(stack, onClick, title);
        b.Name = name;
        return b;
    }
}
