using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Objetivos, a support panel of the workspace (anexo A §6.4 / H6): the Aprender mission linked to the open game
/// (by GameId), its checklist saved in the same progress file as Aprender, the acceptance criteria, the exact offline
/// reference and a hint in Estudo mode that never writes code. Shown only when the game is linked to a challenge.
/// </summary>
public sealed class ObjectivesView : UserControl
{
    private readonly LunetShell _shell;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M) };

    public ObjectivesView(LunetShell shell)
    {
        _shell = shell;
        Content = new ScrollViewer { Content = _page };
    }

    /// <summary>The challenges linked to the open game (more than one game may share a mission; one game, several).</summary>
    public static IReadOnlyList<LearningProject> ChallengesOf(LunetShell shell, out LearningProgressStore? progress)
    {
        progress = null;
        if (shell.Workspace.Project is not { } project) return [];
        try { progress = new LearningProgressStore(shell.LearningProgressPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { return []; }
        return progress.LinkedProjectIdsForGameId(project.Manifest.GameId).Select(LearningCatalog.FindProject).OfType<LearningProject>().ToList();
    }

    public void Refresh()
    {
        _page.Children.Clear();
        var challenges = ChallengesOf(_shell, out var progress);
        if (progress is null || challenges.Count == 0)
        {
            _page.Children.Add(LunetUi.Secondary("Este jogo não está ligado a um desafio de Aprender. Em Aprender, abra um desafio e crie ou ligue o jogo."));
            return;
        }
        foreach (var challenge in challenges)
        {
            _page.Children.Add(LunetUi.Text(challenge.Title, LunetTokens.Type.TitleSmall, weight: Avalonia.Media.FontWeight.SemiBold));
            _page.Children.Add(LunetUi.Secondary(challenge.Mission));
            var summary = LunetUi.Text("", LunetTokens.Type.Callout);
            void Update() => summary.Text = $"{progress.CompletedCountFor(challenge.Id)} de {challenge.Steps.Count} objetivos";
            Update();
            _page.Children.Add(summary);
            foreach (var step in challenge.Steps)
            {
                var row = new DockPanel();
                var hint = LunetUi.IconButton(LunetIcons.Code, "Pedir uma pista (sem escrever código)", () => Hint(challenge, step));
                hint.Name = "Hint-" + step.Id;
                DockPanel.SetDock(hint, Dock.Right);
                row.Children.Add(hint);
                var check = new CheckBox
                {
                    Content = LunetUi.Text(step.Objective, LunetTokens.Type.BodySmall), MinHeight = LunetTokens.MinTouch,
                    IsChecked = progress.Completed(challenge.Id, step.Id), Name = "Objective-" + step.Id, VerticalContentAlignment = VerticalAlignment.Center,
                };
                AutomationProperties.SetName(check, "Declarar feito: " + step.Objective);
                var restoring = false;
                check.IsCheckedChanged += (_, _) =>
                {
                    if (restoring) return;
                    try { progress.SetCompleted(challenge.Id, step.Id, check.IsChecked == true); Update(); Haptics.Confirm?.Invoke(); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        restoring = true; check.IsChecked = progress.Completed(challenge.Id, step.Id); restoring = false; // never pretend it was saved
                        summary.Text = "Não foi possível salvar: " + e.Message;
                    }
                };
                row.Children.Add(check);
                _page.Children.Add(row);
            }
            _page.Children.Add(LunetUi.Text("Critérios de aceite", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary));
            _page.Children.Add(LunetUi.Secondary(challenge.Acceptance));
            var reference = LunetUi.Button("Referência", ButtonKind.Secondary, LunetIcons.Book, () =>
            {
                _shell.WorkspaceView.Support.Docs.Navigate(challenge.ReferenceTarget);
                _shell.State.ShowPanel(SupportPanel.Docs);
            });
            reference.Name = "ObjectiveReference";
            reference.HorizontalAlignment = HorizontalAlignment.Left;
            _page.Children.Add(reference);
            _page.Children.Add(LunetUi.Divider());
        }
        _page.Children.Add(LunetUi.Text("Marque depois de testar no jogo rodando. A marca é sua declaração, não um teste automático.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
    }

    /// <summary>Opens the AI in Estudo with a hint request for the objective (the person sends it; nothing is written).</summary>
    public void Hint(LearningProject challenge, LearningStep step)
    {
        var ai = _shell.WorkspaceView.Support.Ai;
        _shell.State.ShowPanel(SupportPanel.Ai);
        ai.AskForHint($"Estou no desafio \"{challenge.Title}\". Objetivo: {step.Objective}\nMe dê uma pista do caminho, sem escrever o código da solução.");
    }
}
