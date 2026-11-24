using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// A missão segue o GameId do jogo que está aberto, em vez de existir apenas
/// na tela do catálogo. O painel reutiliza o dock do Console/Problemas.
/// </summary>
public sealed partial class MainActivity
{
    private string? _selectedLearningProjectId;
    private readonly Dictionary<(string GameId, string MissionId), string> _learningCompileResults = new();

    private void RememberLearningCompilation(string? missionId, string gameId, bool success)
    {
        if (missionId is null) return;
        _learningCompileResults[(gameId, missionId)] = success ? "Compilou" : "Falhou";
    }

    private void ResolveLearningWorkspaceContext()
    {
        if (_project is null) { _selectedLearningProjectId = null; return; }

        var folder = System.IO.Path.GetFileName(_project.Directory);
        if (LauncherProjectCatalog.UniqueNameForId(_launcherCatalog, _project.Manifest.GameId) != folder)
        {
            _selectedLearningProjectId = null;
            if (_layoutStore.Current.PanelTab == "learn") _layoutStore.Current.PanelTab = "problems";
            return;
        }

        var linked = LearningProgress()?.LinkedProjectIdsForGameId(_project.Manifest.GameId);
        if (linked is null || linked.Count == 0)
        {
            _selectedLearningProjectId = null;
            if (_layoutStore.Current.PanelTab == "learn")
                _layoutStore.Current.PanelTab = "problems";
            return;
        }

        if (_selectedLearningProjectId is null || !linked.Contains(_selectedLearningProjectId, StringComparer.Ordinal))
            _selectedLearningProjectId = linked[0];
    }

    /// <summary>Inicia a edição e abre o enunciado sincronizado com este desafio.</summary>
    private void OpenLearningEditor(string projectId, string name)
    {
        var progress = LearningProgress();
        if (progress is null || LearningCatalog.FindProject(projectId) is null) return;
        try
        {
            var game = _store.Open(name);
            if (progress.ProjectGameIdFor(projectId) != game.Manifest.GameId)
            {
                Toast.MakeText(this, "Associação de curso inválida; escolha o projeto novamente.", ToastLength.Long)?.Show();
                ShowLearningProject(projectId);
                return;
            }
        }
        catch (Exception e) when (e is ProjectException or IOException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível abrir o jogo: " + e.Message, ToastLength.Long)?.Show();
            return;
        }

        _selectedLearningProjectId = projectId;
        _layoutStore.Current.OpenPanel("learn");
        _layoutStore.Current.PanelFraction = Math.Max(_layoutStore.Current.PanelFraction, 0.42);
        SaveLayouts();
        OpenProject(name);
    }

    private void OpenLearningWorkbenchPanel()
    {
        if (_selectedLearningProjectId is null || _project is null) return;
        if (_drawer is not null) _drawer.Visibility = ViewStates.Gone;
        if (_drawerScrim is not null) _drawerScrim.Visibility = ViewStates.Gone;
        _layoutStore.Current.OpenPanel("learn");
        SetLearningWorkbenchPanel();
        ApplyWorkspaceLayout();
        SaveLayouts();
        RefreshWorkbenchNavigation();
    }

    private void SetLearningWorkbenchPanel()
    {
        _showConsole = false;
        _showProfiler = false;
        _layoutStore.Current.PanelTab = "learn";
        if (_panelList is null || _project is null || _selectedLearningProjectId is null) return;
        _panelList.RemoveAllViews();

        var challenge = LearningCatalog.FindProject(_selectedLearningProjectId);
        var progress = LearningProgress();
        if (challenge is null || progress is null ||
            progress.ProjectGameIdFor(challenge.Id) != _project.Manifest.GameId)
        {
            _panelList.AddView(LunetUi.Label(this, "Nenhum desafio associado a este jogo.",
                LunetUi.TextBody, LunetUi.TextMuted));
            return;
        }

        _panelList.AddView(LunetUi.Label(this, "APRENDER · " + challenge.Id,
            LunetUi.TextCaption, LunetUi.AccentText, bold: true));
        _panelList.AddView(LunetUi.Label(this, challenge.Title,
            LunetUi.TextTitle, LunetUi.Text, bold: true));
        _panelList.AddView(LunetUi.Label(this, challenge.Mission,
            LunetUi.TextBody, LunetUi.Text));

        var count = progress.CompletedCountFor(challenge.Id);
        var summary = LunetUi.Label(this, count + "/" + challenge.Steps.Count +
            " objetivos declarados", LunetUi.TextLabel, LunetUi.Text, bold: true);
        _panelList.AddView(summary);
        var bar = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
        { Max = challenge.Steps.Count, Progress = count };
        _panelList.AddView(bar, LunetUi.FillWidth(Dp(10)));
        if (_learningCompileResults.TryGetValue((_project.Manifest.GameId, challenge.Id), out var result))
            _panelList.AddView(LunetUi.Label(this,
                "Última compilação nesta sessão: " + result + ". Teste o resultado no Preview.",
                LunetUi.TextCaption, LunetUi.TextMuted));


        foreach (var step in challenge.Steps)
        {
            var check = new CheckBox(this)
            {
                Text = step.Objective,
                Checked = progress.Completed(challenge.Id, step.Id)
            };
            check.SetTextColor(LunetUi.Text);
            check.SetPadding(Dp(3), Dp(6), Dp(3), Dp(6));
            check.ContentDescription = "Declarar objetivo concluído: " + step.Objective;
            bool restoring = false;
            check.CheckedChange += (_, args) =>
            {
                if (restoring) return;
                try
                {
                    progress.SetCompleted(challenge.Id, step.Id, args.IsChecked);
                    var updated = progress.CompletedCountFor(challenge.Id);
                    summary.Text = updated + "/" + challenge.Steps.Count + " objetivos declarados";
                    bar.Progress = updated;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, "Não foi possível salvar o progresso: " + e.Message, ToastLength.Long)?.Show();
                    restoring = true;
                    check.Checked = progress.Completed(challenge.Id, step.Id);
                    restoring = false;
                }
            };
            _panelList.AddView(check, LunetUi.FillWidth());
        }

        _panelList.AddView(LunetUi.Label(this,
            "Marque cada etapa depois de testar no Preview; não há verificação automática.",
            LunetUi.TextCaption, LunetUi.TextMuted));

        _panelList.AddView(LunetUi.Label(this, "PESQUISAR",
            LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        _panelList.AddView(LunetUi.Label(this, challenge.Investigate,
            LunetUi.TextBody, LunetUi.Text));
        _panelList.AddView(LunetUi.Button(this, "Abrir referência da missão", () => DocumentationPanel.Open(this, challenge.ReferenceTarget), ButtonStyle.Secondary),
            LunetUi.FillWidth(Dp(48)));

        _panelList.AddView(LunetUi.Label(this, "COMO TESTAR NO PREVIEW",
            LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        _panelList.AddView(LunetUi.Label(this, challenge.Acceptance,
            LunetUi.TextBody, LunetUi.Text));
        _panelList.AddView(LunetUi.Label(this, "DESAFIO EXTRA",
            LunetUi.TextCaption, LunetUi.TextMuted, bold: true));
        _panelList.AddView(LunetUi.Label(this, challenge.Transfer,
            LunetUi.TextBody, LunetUi.TextMuted));

        _panelList.AddView(LunetUi.Button(this, "Rodar no Preview", Run, ButtonStyle.Primary),
            LunetUi.FillWidth(Dp(48)));
        _panelList.AddView(LunetUi.Button(this, "Pedir uma pista à IA", () => AskLearningHint(challenge),
            ButtonStyle.Secondary), LunetUi.FillWidth(Dp(48)));

        var other = progress.LinkedProjectIdsForGameId(_project.Manifest.GameId);
        if (other.Count > 1)
            _panelList.AddView(LunetUi.Button(this, "Trocar missão associada", () =>
            {
                var labels = other.Select(id =>
                    id + " · " + (LearningCatalog.FindProject(id)?.Title ?? id)).ToArray();
                new AlertDialog.Builder(this)!
                    .SetTitle("Qual missão deste jogo?")
                    .SetItems(labels, (_, e) =>
                    {
                        _selectedLearningProjectId = other[e.Which];
                        SetLearningWorkbenchPanel();
                    })!
                    .SetNegativeButton("Cancelar", (_, _) => { })!
                    .Show();
            }, ButtonStyle.Quiet), LunetUi.FillWidth(Dp(48)));
    }
}
