using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>LUNET-520: cursos mostrados como catálogo, somente o primeiro desafio é executável.
/// Estado do aluno é local e independente de ProjectStore.</summary>
public sealed partial class MainActivity
{
    private LearningProgressStore? _learningProgress;
    private string? _learningProgressError;
    private int _learningVisibleCount = 15;

    private LearningProgressStore? LearningProgress()
    {
        if (_learningProgress is not null) return _learningProgress;
        try
        {
            _learningProgress = new LearningProgressStore(System.IO.Path.Combine(FilesDir!.AbsolutePath, "learning", "progress.json"));
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
        root.AddView(LunetUi.Label(this, "Aprenda a criar jogos, criando jogos. 60 cursos planejados em 12 trilhas.", LunetUi.TextBody, LunetUi.TextMuted));
        var scroll = new ScrollView(this) { FillViewport = true };
        var content = LunetUi.Column(this, 10);
        content.SetPadding(0, Dp(12), 0, Dp(18));
        var progress = LearningProgress();
        if (_learningProgressError is not null)
            content.AddView(LunetUi.Label(this, _learningProgressError, LunetUi.TextLabel, LunetUi.Warning));
        if (progress is not null)
        {
            content.AddView(LunetUi.Label(this, "SEU PRIMEIRO PROJETO", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
            var percentage = progress.CompletedCount * 25;
            content.AddView(LunetUi.Label(this, $"{percentage}% · {progress.CompletedCount} de 4 objetivos declarados", LunetUi.TextBody, LunetUi.Text, bold: true));
            var bar = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
            { Max = 4, Progress = progress.CompletedCount };
            content.AddView(bar, LunetUi.FillWidth(Dp(12)));
            AddHomeAction(content, "Continuar: Aprendendo movimento", "Disponível · desenhe, mova, alcance o objetivo", ShowLearningFirstProject);
        }

        content.AddView(LunetUi.Label(this, "CATÁLOGO DE CURSOS", LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        var list = LunetUi.Column(this, 6);
        content.AddView(list);
        var more = LunetUi.Button(this, "Mostrar mais cursos", () =>
        {
            _learningVisibleCount = Math.Min(60, _learningVisibleCount + 15);
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
                var section = LunetUi.Label(this, track.ToUpperInvariant(), LunetUi.TextCaption, LunetUi.TextMuted, bold: true);
                section.SetPadding(Dp(3), Dp(14), 0, Dp(4));
                list.AddView(section);
            }
            AddHomeAction(list, $"{course.Id} · {course.Title}",
                course.Available ? "Primeiro projeto disponível · outros em breve" : "Disponível em breve",
                () =>
                {
                    if (course.Available) ShowLearningFirstProject();
                    else new AlertDialog.Builder(this)!.SetTitle(course.Title)!
                        .SetMessage("Disponível em breve. Este curso está planejado, mas seus projetos ainda não foram implementados.")!
                        .SetPositiveButton("Entendi", (_, _) => { })!.Show();
                });
        }
        more.Visibility = _learningVisibleCount < LearningCatalog.Courses.Count ? ViewStates.Visible : ViewStates.Gone;
        scroll.AddView(content);
        root.AddView(scroll, Fill(1));
        root.AddView(BuildLauncherNavigation("learn"));
        SetContentView(root);
    }

    private void ShowLearningFirstProject()
    {
        EnterLauncherSurface("learn");
        var root = Vertical();
        root.SetBackgroundColor(LunetUi.Background);
        root.SetPadding(Dp(16), Dp(12), Dp(16), Dp(8));
        var header = LunetUi.Row(this, 8);
        header.AddView(LunetUi.Button(this, "← Aprender", ShowLearning, ButtonStyle.Quiet));
        root.AddView(header);
        root.AddView(LunetUi.Label(this, "Aprendendo movimento", LunetUi.TextHero, LunetUi.Text, bold: true));
        root.AddView(LunetUi.Label(this, "LRN-001 · Projeto 1 de 2 · iniciante", LunetUi.TextCaption, LunetUi.AccentText));
        var scroll = new ScrollView(this) { FillViewport = true };
        var body = LunetUi.Column(this, 12);
        body.SetPadding(0, Dp(18), 0, Dp(16));
        body.AddView(LunetUi.Label(this,
            "Missão: crie um pequeno jogo em C# no qual o jogador controla um quadrado e vence ao tocar um círculo. " +
            "Você decide como construir. Não existe solução pronta nem dependência da IA.",
            LunetUi.TextBody, LunetUi.Text));
        var progress = LearningProgress();
        if (progress is null)
        {
            body.AddView(LunetUi.Label(this, _learningProgressError ?? "Progresso indisponível", LunetUi.TextBody, LunetUi.Warning));
        }
        else
        {
            var summary = LunetUi.Label(this, $"{progress.CompletedCount} / 4 objetivos declarados", LunetUi.TextBody, LunetUi.Text, bold: true);
            body.AddView(summary);
            foreach (var step in LearningCatalog.FirstProjectSteps)
            {
                var check = new CheckBox(this)
                {
                    Text = step.Objective,
                    Checked = progress.Completed(step.Id),
                };
                check.SetTextColor(LunetUi.Text);
                check.SetPadding(Dp(2), Dp(5), Dp(2), Dp(5));
                check.ContentDescription = "Declarar objetivo concluído: " + step.Objective;
                bool restoring = false;
                check.CheckedChange += (_, args) =>
                {
                    if (restoring) return;
                    try
                    {
                        progress.SetCompleted(step.Id, args.IsChecked);
                        summary.Text = $"{progress.CompletedCount} / 4 objetivos declarados";
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        Toast.MakeText(this, "Não foi possível salvar o progresso: " + e.Message, ToastLength.Long)?.Show();
                        restoring = true;
                        check.Checked = progress.Completed(step.Id);
                        restoring = false;
                    }
                };
                body.AddView(check, LunetUi.FillWidth());
            }
        }
        body.AddView(LunetUi.Label(this,
            "Marque os objetivos somente depois de testar seu jogo. A marcação é sua declaração, não uma verificação automática de código.",
            LunetUi.TextCaption, LunetUi.TextMuted));
        var linkedName = progress?.ProjectName;
        bool linked = false;
        if (linkedName is not null && _store.List().Contains(linkedName, StringComparer.Ordinal))
        {
            try { linked = _store.Open(linkedName).Manifest.GameId == progress?.ProjectGameId; }
            catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException)
            {
                linked = false;
            }
        }
        if (linked)
        {
            body.AddView(LunetUi.Button(this, "Continuar no editor · " + linkedName, () => OpenProject(linkedName!), ButtonStyle.Primary),
                LunetUi.FillWidth(Dp(52)));
            body.AddView(LunetUi.Button(this, "Pedir ajuda da IA (pistas)", () =>
            {
                OpenProject(linkedName!);
                OpenAiPanel("Estou fazendo o primeiro desafio do Lunet Aprender: desenhar um quadrado, mover por toque, desenhar um círculo e vencer ao encostar. Dê somente uma pista conceitual, sem alterar meus arquivos.");
            }, ButtonStyle.Secondary));
        }
        else
        {
            body.AddView(LunetUi.Button(this, "Criar projeto para o desafio", CreateLearningProject, ButtonStyle.Primary),
                LunetUi.FillWidth(Dp(52)));
        }
        body.AddView(LunetUi.Button(this, "Associar um projeto existente", AssociateLearningProject, ButtonStyle.Quiet));
        body.AddView(LunetUi.Label(this, "Próximo projeto: Caça ao alvo · Disponível em breve", LunetUi.TextCaption, LunetUi.TextMuted));
        scroll.AddView(body);
        root.AddView(scroll, Fill(1));
        root.AddView(BuildLauncherNavigation("learn"));
        SetContentView(root);
    }

    private void CreateLearningProject()
    {
        var progress = LearningProgress();
        if (progress is null) return;
        try
        {
            var existing = _store.List();
            string name = "AprendendoMovimento";
            int suffix = 2;
            while (existing.Contains(name, StringComparer.OrdinalIgnoreCase))
                name = "AprendendoMovimento" + suffix++;
            var project = _store.Create(name, ProjectTemplate.Blank);
            progress.LinkProject(name, project.Manifest.GameId);
            OpenProject(name);
        }
        catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            Toast.MakeText(this, "Não foi possível criar desafio: " + e.Message, ToastLength.Long)?.Show();
        }
    }

    private void AssociateLearningProject()
    {
        var progress = LearningProgress();
        if (progress is null) return;
        var names = _store.List().ToArray();
        if (names.Length == 0)
        {
            Toast.MakeText(this, "Crie um projeto primeiro.", ToastLength.Short)?.Show();
            return;
        }
        new AlertDialog.Builder(this)!
            .SetTitle("Escolha seu projeto para Aprendendo movimento")!
            .SetItems(names, (_, e) =>
            {
                try
                {
                    var project = _store.Open(names[e.Which]);
                    progress.LinkProject(names[e.Which], project.Manifest.GameId);
                    ShowLearningFirstProject();
                }
                catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }
}
