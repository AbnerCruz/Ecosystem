using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// Aprender: 60 cursos descobríveis, T01 completamente utilizável.
/// Progressos são declarações do aluno, não resultados de um avaliador automático.
/// </summary>
public sealed partial class MainActivity
{
    private LearningProgressStore? _learningProgress;
    private string? _learningProgressError;
    private int _learningVisibleCount = 15;
    private string LearningProgressPath => System.IO.Path.Combine(FilesDir!.AbsolutePath, "learning", "progress.json");

    private LearningProgressStore? LearningProgress()
    {
        if (_learningProgress is not null) return _learningProgress;
        try
        {
            _learningProgress = new LearningProgressStore(LearningProgressPath);
            _learningProgressError = null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _learningProgressError = "O progresso não pôde ser lido. Arquivo preservado: " + e.Message;
        }
        return _learningProgress;
    }

    private void ShowLearning()
    {
        EnterLauncherSurface("learn");
        var root = Vertical();
        root.SetBackgroundColor(LunetUi.Background);
        root.SetPadding(Dp(16), Dp(12), Dp(16), Dp(8));
        root.AddView(LunetUi.Label(this, "Aprender", LunetUi.TextHero, LunetUi.Text, bold: true));
        root.AddView(LunetUi.Label(this,
            "Aprenda a desenvolver jogos criando jogos. Primeira trilha: 5 cursos e 10 desafios disponíveis.",
            LunetUi.TextBody, LunetUi.TextMuted));

        var scroll = new ScrollView(this) { FillViewport = true };
        var content = LunetUi.Column(this, 10);
        content.SetPadding(0, Dp(12), 0, Dp(18));
        var progress = LearningProgress();
        if (_learningProgressError is not null)
        {
            content.AddView(LunetUi.Label(this, _learningProgressError, LunetUi.TextLabel, LunetUi.Warning));
            if (LearningProgressStore.HasRecovery(LearningProgressPath))
                content.AddView(LunetUi.Button(this, "Restaurar cópia do progresso", RestoreLearningProgress, ButtonStyle.Secondary));
        }

        if (progress is not null)
        {
            int total = LearningCatalog.Projects.Sum(p => p.Steps.Count);
            content.AddView(LunetUi.Label(this, "SUA TRILHA INICIAL", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
            content.AddView(LunetUi.Label(this,
                $"{progress.OverallCompletedCount} / {total} objetivos declarados · sem avaliação automática",
                LunetUi.TextBody, LunetUi.Text, bold: true));
            var bar = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
            { Max = total, Progress = progress.OverallCompletedCount };
            content.AddView(bar, LunetUi.FillWidth(Dp(12)));
            var next = LearningCatalog.Projects.FirstOrDefault(p => progress.CompletedCountFor(p.Id) < p.Steps.Count)
                ?? LearningCatalog.Projects[0];
            AddHomeAction(content, "Continuar · " + next.Title,
                next.Id + " · " + progress.CompletedCountFor(next.Id) + "/4 objetivos",
                () => ShowLearningProject(next.Id));
        }

        content.AddView(LunetUi.Label(this, "CATÁLOGO DE CURSOS", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        var list = LunetUi.Column(this, 6);
        content.AddView(list);
        var more = LunetUi.Button(this, "Mostrar mais cursos", () =>
        {
            _learningVisibleCount = Math.Min(LearningCatalog.Courses.Count, _learningVisibleCount + 15);
            ShowLearning();
        }, ButtonStyle.Quiet);
        content.AddView(more, LunetUi.FillWidth(Dp(48)));

        string? track = null;
        for (int i = 0; i < Math.Min(LearningCatalog.Courses.Count, _learningVisibleCount); i++)
        {
            var course = LearningCatalog.Courses[i];
            if (track != course.Track)
            {
                track = course.Track;
                var label = LunetUi.Label(this, track.ToUpperInvariant(),
                    LunetUi.TextCaption, LunetUi.TextMuted, bold: true);
                label.SetPadding(Dp(3), Dp(14), 0, Dp(4));
                list.AddView(label);
            }

            int completed = progress is null ? 0 : LearningCatalog.Projects
                .Where(p => p.CourseId == course.Id).Sum(p => progress.CompletedCountFor(p.Id));
            AddHomeAction(list, course.Id + " · " + course.Title,
                course.Available
                    ? "2 projetos disponíveis · " + completed + "/8 objetivos declarados"
                    : "Disponível em breve",
                () =>
                {
                    if (course.Available) ShowLearningCourse(course.Id);
                    else new AlertDialog.Builder(this)!.SetTitle(course.Title)!
                        .SetMessage("Disponível em breve. Os projetos deste curso ainda não foram implementados.")!
                        .SetPositiveButton("Entendi", (_, _) => { })!.Show();
                });
        }

        more.Visibility = _learningVisibleCount < LearningCatalog.Courses.Count
            ? ViewStates.Visible : ViewStates.Gone;
        scroll.AddView(content);
        root.AddView(scroll, Fill(1));
        SetLauncherContent(root, "learn", scroll);
    }

    private void ShowLearningCourse(string courseId)
    {
        var course = LearningCatalog.Find(courseId);
        if (course is null || !course.Available) { ShowLearning(); return; }

        EnterLauncherSurface("learn");
        var root = Vertical();
        root.SetBackgroundColor(LunetUi.Background);
        root.SetPadding(Dp(16), Dp(12), Dp(16), Dp(8));
        root.AddView(LunetUi.Button(this, "← Aprender", ShowLearning, ButtonStyle.Quiet));
        root.AddView(LunetUi.Label(this, course.Title, LunetUi.TextHero, LunetUi.Text, bold: true));
        root.AddView(LunetUi.Label(this, course.Id + " · Trilha 1 · 2 projetos",
            LunetUi.TextCaption, LunetUi.AccentText));

        var scroll = new ScrollView(this) { FillViewport = true };
        var content = LunetUi.Column(this, 12);
        content.SetPadding(0, Dp(18), 0, Dp(16));
        content.AddView(LunetUi.Label(this,
            "Cada desafio resulta em um jogo C# normal, aberto no editor e no Preview. Você decide como implementar.",
            LunetUi.TextBody, LunetUi.Text));
        var progress = LearningProgress();
        foreach (var project in LearningCatalog.Projects.Where(p => p.CourseId == courseId))
        {
            int done = progress?.CompletedCountFor(project.Id) ?? 0;
            AddHomeAction(content, project.Title,
                project.Id + " · " + done + "/4 objetivos declarados · Disponível",
                () => ShowLearningProject(project.Id));
            content.AddView(LunetUi.Label(this, project.Mission,
                LunetUi.TextBody, LunetUi.TextMuted));
            var bar = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
            { Max = project.Steps.Count, Progress = done };
            content.AddView(bar, LunetUi.FillWidth(Dp(8)));
        }
        content.AddView(LunetUi.Label(this,
            "Os objetivos são verificáveis no Preview, mas as caixas registram apenas sua declaração. " +
            "O segundo projeto de cada curso exige aplicar o conhecimento com mais autonomia.",
            LunetUi.TextCaption, LunetUi.TextMuted));
        scroll.AddView(content);
        root.AddView(scroll, Fill(1));
        _restoreLauncherChild = () => ShowLearningCourse(courseId);
        _backFromLauncherChild = ShowLearning;
        SetLauncherContent(root, "learn", scroll, "learn-course-" + courseId);
    }

    private void ShowLearningFirstProject() => ShowLearningProject(LearningCatalog.FirstProjectId);

    private void ShowLearningProject(string projectId)
    {
        var challenge = LearningCatalog.FindProject(projectId);
        if (challenge is null) { ShowLearning(); return; }
        EnterLauncherSurface("learn");
        var root = Vertical();
        root.SetBackgroundColor(LunetUi.Background);
        root.SetPadding(Dp(16), Dp(12), Dp(16), Dp(8));
        root.AddView(LunetUi.Button(this, "← Curso", () => ShowLearningCourse(challenge.CourseId), ButtonStyle.Quiet));
        root.AddView(LunetUi.Label(this, challenge.Title, LunetUi.TextHero, LunetUi.Text, bold: true));
        root.AddView(LunetUi.Label(this,
            challenge.Id + " · " + (challenge.Id.EndsWith("P1", StringComparison.Ordinal)
                ? "Projeto 1 de 2" : "Projeto 2 de 2"),
            LunetUi.TextCaption, LunetUi.AccentText));

        var scroll = new ScrollView(this) { FillViewport = true };
        var body = LunetUi.Column(this, 12);
        body.SetPadding(0, Dp(18), 0, Dp(16));
        body.AddView(LunetUi.Label(this, "MISSÃO", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        body.AddView(LunetUi.Label(this, challenge.Mission, LunetUi.TextBody, LunetUi.Text));

        var progress = LearningProgress();
        if (progress is null)
        {
            body.AddView(LunetUi.Label(this, _learningProgressError ?? "Progresso indisponível",
                LunetUi.TextBody, LunetUi.Warning));
        }
        else
        {
            var count = progress.CompletedCountFor(challenge.Id);
            var summary = LunetUi.Label(this,
                count + " / " + challenge.Steps.Count + " objetivos declarados",
                LunetUi.TextBody, LunetUi.Text, bold: true);
            body.AddView(summary);
            var bar = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
            { Max = challenge.Steps.Count, Progress = count };
            body.AddView(bar, LunetUi.FillWidth(Dp(12)));

            foreach (var step in challenge.Steps)
            {
                var check = new CheckBox(this)
                {
                    Text = step.Objective,
                    Checked = progress.Completed(challenge.Id, step.Id),
                };
                check.SetTextColor(LunetUi.Text);
                check.SetPadding(Dp(2), Dp(6), Dp(2), Dp(6));
                check.ContentDescription = "Declarar objetivo concluído: " + step.Objective;
                bool restoring = false;
                check.CheckedChange += (_, args) =>
                {
                    if (restoring) return;
                    try
                    {
                        progress.SetCompleted(challenge.Id, step.Id, args.IsChecked);
                        int updated = progress.CompletedCountFor(challenge.Id);
                        summary.Text = updated + " / " + challenge.Steps.Count + " objetivos declarados";
                        bar.Progress = updated;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        Toast.MakeText(this, "Não foi possível salvar: " + e.Message, ToastLength.Long)?.Show();
                        restoring = true;
                        check.Checked = progress.Completed(challenge.Id, step.Id);
                        restoring = false;
                    }
                };
                body.AddView(check, LunetUi.FillWidth());
            }
        }

        body.AddView(LunetUi.Label(this,
            "Marque depois de testar no Preview. A marcação é sua declaração, não um teste automático de jogabilidade.",
            LunetUi.TextCaption, LunetUi.TextMuted));
        body.AddView(LunetUi.Label(this, "PESQUISAR", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        body.AddView(LunetUi.Label(this, challenge.Investigate, LunetUi.TextBody, LunetUi.Text));
        body.AddView(LunetUi.Button(this, "Abrir referência da missão", () => DocumentationPanel.Open(this, challenge.ReferenceTarget), ButtonStyle.Secondary),
            LunetUi.FillWidth(Dp(48)));
        body.AddView(LunetUi.Label(this, "CRITÉRIOS DE ACEITE", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        body.AddView(LunetUi.Label(this, challenge.Acceptance, LunetUi.TextBody, LunetUi.Text));
        body.AddView(LunetUi.Label(this, "DESAFIO EXTRA", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        body.AddView(LunetUi.Label(this, challenge.Transfer, LunetUi.TextBody, LunetUi.TextMuted));

        RefreshLauncherCatalog();
        var linkedName = LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, progress?.ProjectGameIdFor(challenge.Id));
        bool linked = linkedName is not null;

        if (linked)
        {
            body.AddView(LunetUi.Button(this, "Continuar no editor · " + linkedName,
                () => OpenLearningEditor(challenge.Id, linkedName!), ButtonStyle.Primary), LunetUi.FillWidth(Dp(52)));
            body.AddView(LunetUi.Button(this, "Pedir uma pista da IA (opcional)", () =>
            {
                OpenLearningEditor(challenge.Id, linkedName!);
                if (_project?.Manifest.GameId == progress?.ProjectGameIdFor(challenge.Id))
                    AskLearningHint(challenge);
            }, ButtonStyle.Secondary));
        }
        else if (progress is not null)
        {
            if (progress.ProjectGameIdFor(projectId) is not null)
                body.AddView(LunetUi.Label(this, "O jogo vinculado está ausente ou tem identidade duplicada. " +
                    "Reimporte o jogo original ou associe outro. As marcações do jogo anterior ficam guardadas.",
                    LunetUi.TextBody, LunetUi.TextMuted));
            body.AddView(LunetUi.Button(this, "Criar projeto para este desafio",
                () => CreateLearningProject(challenge.Id), ButtonStyle.Primary),
                LunetUi.FillWidth(Dp(52)));
        }

        if (progress is not null)
            body.AddView(LunetUi.Button(this, "Associar um projeto existente",
                () => AssociateLearningProject(challenge.Id), ButtonStyle.Quiet));

        scroll.AddView(body);
        root.AddView(scroll, Fill(1));
        _restoreLauncherChild = () => ShowLearningProject(projectId);
        _backFromLauncherChild = () => ShowLearningCourse(challenge.CourseId);
        SetLauncherContent(root, "learn", scroll, "learn-project-" + projectId);
    }

    private void RestoreLearningProgress()
    {
        new AlertDialog.Builder(this)!.SetTitle("Restaurar a gravação anterior?")!
            .SetMessage("A cópia pode não conter a última marcação. O arquivo atual fica preservado.")!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Restaurar", (_, _) =>
            {
                try
                {
                    LearningProgressStore.RestoreBackup(LearningProgressPath);
                    _learningProgress = null;
                    ShowLearning();
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
                { Toast.MakeText(this, e.Message, ToastLength.Long)?.Show(); }
            })!.Show();
    }

    private void AskLearningHint(LearningProject challenge)
    {
        if (_project is null) return;
        var game = _project;
        var progress = LearningProgress();
        var choices = challenge.Steps.Select(step =>
            (Label: (progress?.Completed(challenge.Id, step.Id) == true ? "✓ " : "") + step.Objective,
             Objective: step.Objective)).Append((Label: "Desafio extra", Objective: challenge.Transfer)).ToArray();
        new AlertDialog.Builder(this)!.SetTitle("Pista para qual objetivo?")!
            .SetItems(choices.Select(choice => choice.Label).ToArray(), (_, e) =>
            {
                if (!ReferenceEquals(game, _project)) return;
                OpenAiPanel($"Estou estudando {challenge.Id} ({challenge.Title}). " +
                    $"Objetivo: {choices[e.Which].Objective} " +
                    $"Arquivo atual: {_openFile ?? "nenhum"}. Referência offline: {challenge.ReferenceTarget}. " +
                    "Dê uma pista conceitual para eu implementar e testar. Evite entregar a solução inteira.", study: true);
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void CreateLearningProject(string projectId)
    {
        var challenge = LearningCatalog.FindProject(projectId);
        var progress = LearningProgress();
        if (challenge is null || progress is null) return;
        try
        {
            var existing = _store.List();
            string name = "Aprender" + challenge.Id.Replace("-", "", StringComparison.Ordinal);
            string baseName = name;
            int suffix = 2;
            while (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
                name = baseName + suffix++;
            var project = _store.Create(name, ProjectTemplate.Blank, challenge.Orientation);
            progress.LinkProject(projectId, name, project.Manifest.GameId);
            OpenLearningEditor(projectId, name);
        }
        catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Toast.MakeText(this, "Não foi possível criar o desafio: " + e.Message, ToastLength.Long)?.Show();
        }
    }

    private void AssociateLearningProject(string projectId)
    {
        if (LearningCatalog.FindProject(projectId) is null) return;
        var progress = LearningProgress();
        if (progress is null) return;
        RefreshLauncherCatalog();
        var names = _launcherCatalog.Where(entry =>
                LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, entry.GameId) == entry.FolderName)
            .Select(entry => entry.FolderName).ToArray();
        if (names.Length == 0)
        {
            Toast.MakeText(this, "Crie ou restaure um projeto com identidade única.", ToastLength.Short)?.Show();
            return;
        }
        new AlertDialog.Builder(this)!
            .SetTitle("Associe seu jogo a " + projectId)!
            .SetItems(names, (_, e) =>
            {
                try
                {
                    var project = _store.Open(names[e.Which]);
                    progress.LinkProject(projectId, names[e.Which], project.Manifest.GameId);
                    OpenLearningEditor(projectId, names[e.Which]);
                }
                catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }
}
