using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Ai;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>One run's actual tool activity. Opening the disclosure mounts only the current page.</summary>
internal sealed class AiActivityView : Border
{
    private const int PageSize = 12;
    private readonly StackPanel _details = new() { Spacing = LunetTokens.Space.Xs };
    private readonly TextBlock _label = LunetUi.Text("", LunetTokens.Type.Label, weight: FontWeight.SemiBold);
    private readonly Border _chevron = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _toggle;
    private IReadOnlyList<LunetAiStep> _steps = [];
    private int? _pageIndex;

    public AiActivityView()
    {
        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_chevron, Dock.Right);
        _chevron.Margin = new Thickness(LunetTokens.Space.S, 0, 0, 0);
        header.Children.Add(_chevron);
        header.Children.Add(_label);
        _toggle = LunetUi.Button("Etapas da execução", ButtonKind.Ghost, null, () => SetOpen(!IsOpen));
        _toggle.Name = "AiActivityToggle";
        _toggle.HorizontalAlignment = HorizontalAlignment.Stretch;
        _toggle.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _toggle.Content = header;
        Child = new StackPanel { Children = { _toggle, _details } };
        Background = LunetTokens.Brush(LunetTokens.Color.Surface);
        CornerRadius = new CornerRadius(LunetTokens.Radius.M);
        SetOpen(false);
    }

    public bool IsOpen { get; private set; }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        _pageIndex = null;
        _chevron.Child = LunetIcons.Create(open ? LunetIcons.ChevronDown : LunetIcons.ChevronRight,
            LunetTokens.Color.TextSecondary, 16);
        UpdateDetails();
    }

    public void Update(IReadOnlyList<LunetAiStep> steps, bool running)
    {
        _steps = steps;
        IsVisible = running || steps.Count > 0;
        var failed = steps.Count(s => s.Status == LunetAiStepStatus.Failed);
        var summary = running
            ? steps.LastOrDefault(s => s.Status == LunetAiStepStatus.Running)?.Label ?? "Preparando resposta…"
            : LunetAiStep.Summarize(steps);
        _label.Text = steps.Count == 0 ? summary : $"{summary} · {steps.Count} {(steps.Count == 1 ? "etapa" : "etapas")}";
        if (failed > 0) _label.Text += $" · {failed} {(failed == 1 ? "falha" : "falhas")}";
        _toggle.IsEnabled = steps.Count > 0;
        AutomationProperties.SetName(_toggle, _label.Text);
        UpdateDetails();
    }

    private void UpdateDetails()
    {
        _details.Children.Clear();
        _details.IsVisible = IsOpen && _steps.Count > 0;
        AutomationProperties.SetHelpText(_toggle, IsOpen ? "Recolher etapas" : "Mostrar etapas");
        if (!_details.IsVisible) return;

        var lastPage = (_steps.Count - 1) / PageSize;
        var page = Math.Min(_pageIndex ?? lastPage, lastPage);
        var start = page * PageSize;
        var end = Math.Min(start + PageSize, _steps.Count);
        if (page > 0)
            _details.Children.Add(PageButton("Etapas anteriores", () => _pageIndex = page - 1));
        for (var i = start; i < end; i++)
            _details.Children.Add(StepRow(_steps[i]));
        if (page < lastPage)
            _details.Children.Add(PageButton("Etapas seguintes", () => _pageIndex = page + 1 == lastPage ? null : page + 1));
    }

    private Button PageButton(string label, Action move) =>
        LunetUi.Button(label, ButtonKind.Ghost, null, () => { move(); UpdateDetails(); });

    private static Control StepRow(LunetAiStep step)
    {
        var (icon, color, status) = step.Status switch
        {
            LunetAiStepStatus.Done => (LunetIcons.Check, LunetTokens.Color.Success, "Concluída"),
            LunetAiStepStatus.Failed => (LunetIcons.Problems, LunetTokens.Color.Danger, "Falhou"),
            _ => (LunetIcons.Pulse, LunetTokens.Color.Info, "Em andamento"),
        };
        var marker = LunetIcons.Create(icon, color, 16);
        marker.Margin = new Thickness(0, 2, LunetTokens.Space.S, 0);
        marker.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(marker, Dock.Left);
        var notes = new StackPanel { Spacing = 2 };
        notes.Children.Add(LunetUi.Text(step.Label, LunetTokens.Type.Label));
        var duration = step.ElapsedMilliseconds is { } elapsed ? $" · {TimeSpan.FromMilliseconds(elapsed).TotalSeconds:0.##} s" : "";
        notes.Children.Add(LunetUi.Text(status + duration, LunetTokens.Type.Caption, color));
        if (!string.IsNullOrWhiteSpace(step.Detail) && !step.Label.Contains(step.Detail, StringComparison.Ordinal))
            notes.Children.Add(LunetUi.Secondary(step.Detail));
        if (!string.IsNullOrWhiteSpace(step.Error))
            notes.Children.Add(LunetUi.Text(step.Error, LunetTokens.Type.Caption, LunetTokens.Color.Danger));
        return new DockPanel
        {
            Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xs),
            Children = { marker, notes },
        };
    }
}
