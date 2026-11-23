using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Ai;

namespace Lunet.Android;

/// <summary>
/// Non-modal cowork surface: the editor remains usable while the agent runs.
/// Reuses the existing session, permission gate, foreground service and undo checkpoints.
/// The full conversation is available on demand, never the default surface.
/// </summary>
public sealed partial class MainActivity
{
    private LinearLayout? _aiCompactDock;
    private TextView? _aiCompactStatus;
    private LinearLayout? _aiQuickPopup;
    private TextView? _aiQuickStatus;
    private EditText? _aiQuickInput;
    private Button? _aiQuickAction;
    private long _aiLastConversationRenderMs;

    private void BuildAiCompact(FrameLayout host)
    {
        _aiQuickPopup = null;
        _aiQuickStatus = null;
        _aiQuickInput = null;
        _aiQuickAction = null;

        var dock = LunetUi.Row(this, 6);
        dock.SetPadding(Dp(10), Dp(4), Dp(8), Dp(4));
        dock.Elevation = Dp(10);
        dock.Background = LunetUi.Shape(this, LunetUi.SurfaceRaised, LunetUi.RadiusLarge, LunetUi.Border);
        var status = LunetUi.Label(this, "✦ Perguntar à IA", LunetUi.TextCaption, LunetUi.Text, bold: true);
        status.SetSingleLine(true);
        status.Ellipsize = TextUtils.TruncateAt.End;
        dock.AddView(status, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        dock.AddView(LunetUi.Icon(this, "›", "Abrir assistente compacto", ShowQuickAssistant),
            new LinearLayout.LayoutParams(Dp(38), Dp(40)));
        dock.Clickable = true;
        dock.ContentDescription = "Abrir o assistente sem sair do editor";
        dock.Click += (_, _) => ShowQuickAssistant();
        host.AddView(dock, new FrameLayout.LayoutParams(Dp(226), Dp(48),
            GravityFlags.End | GravityFlags.Bottom)
        { RightMargin = Dp(8), BottomMargin = Dp(66) });
        _aiCompactDock = dock;
        _aiCompactStatus = status;
        UpdateAiCompact();
    }

    private void ShowQuickAssistant()
    {
        if (_workspaceFrame is null || _project is null) return;
        EnsureAiSession();
        if (_aiQuickPopup is not null && _aiQuickPopup.Parent == _workspaceFrame)
        {
            _aiQuickPopup.Visibility = ViewStates.Visible;
            _aiQuickPopup.BringToFront();
            UpdateAiCompact();
            return;
        }
        // SendAi already owns permission, history, build and checkpoint logic. The
        // compact composer is only a second entry point into the same run.
        if (_aiPanel is null || _aiPanel.Parent != _workspaceFrame)
            BuildChat(_workspaceFrame);
        var popup = LunetUi.Column(this, 8);
        popup.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        popup.Elevation = Dp(14);
        popup.Clickable = true;
        popup.Background = LunetUi.Shape(this, LunetUi.Background, LunetUi.RadiusLarge, LunetUi.Border);
        var head = LunetUi.Row(this, 6);
        head.AddView(LunetUi.Label(this, "✦ IA · cowork", LunetUi.TextLabel, LunetUi.AccentText, bold: true),
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        head.AddView(LunetUi.Icon(this, "✕", "Fechar pop-up", () => popup.Visibility = ViewStates.Gone),
            new LinearLayout.LayoutParams(Dp(38), Dp(38)));
        popup.AddView(head);
        _aiQuickStatus = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        _aiQuickStatus.SetMaxLines(3);
        _aiQuickStatus.Ellipsize = TextUtils.TruncateAt.End;
        popup.AddView(_aiQuickStatus);
        var input = new EditText(this)
        {
            Hint = "Peça para criar, corrigir ou analisar…",
            TextSize = LunetUi.TextBody,
        };
        input.SetTextColor(LunetUi.Text);
        input.SetHintTextColor(LunetUi.TextFaint);
        input.SetMinLines(1);
        input.SetMaxLines(3);
        input.InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagCapSentences;
        popup.AddView(input);
        _aiQuickInput = input;

        var actions = LunetUi.Row(this, 8);
        _aiQuickAction = LunetUi.Button(this, "Enviar", () =>
        {
            var ongoing = LunetAiRuns.Current;
            if (ongoing is { IsRunning: true })
            {
                if (ongoing.ProjectId == _project?.Manifest.GameId) ongoing.Cancel();
                return;
            }
            var request = (input.Text ?? "").Trim();
            if (request.Length == 0) return;
            if (_aiInput is null) return;
            _aiInput.Text = request;
            if (!Ai.IsConfigured)
            {
                ShowAiSettings(() =>
                {
                    SendAi();
                    input.Text = "";
                    popup.Visibility = ViewStates.Gone;
                });
                return;
            }
            SendAi();
            if (LunetAiRuns.Current is { IsRunning: true } run && run.ProjectId == _project?.Manifest.GameId)
            {
                input.Text = "";
                popup.Visibility = ViewStates.Gone;
            }
        }, ButtonStyle.Primary);
        actions.AddView(_aiQuickAction, new LinearLayout.LayoutParams(0, Dp(46), 1f));
        actions.AddView(LunetUi.Button(this, "Expandir", () =>
        {
            popup.Visibility = ViewStates.Gone;
            OpenAiPanel(null);
        }, ButtonStyle.Secondary), new LinearLayout.LayoutParams(0, Dp(46), 1f));
        popup.AddView(actions);
        _workspaceFrame.AddView(popup, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent,
            GravityFlags.Bottom)
        { LeftMargin = Dp(8), RightMargin = Dp(8), BottomMargin = Dp(122) });
        _aiQuickPopup = popup;
        UpdateAiCompact();
    }

    /// <summary>Only small labels change on every event; Markdown history is never rebuilt while compact.</summary>
    private void UpdateAiCompact()
    {
        if (_aiCompactStatus is null || _project is null) return;
        var work = LunetAiRuns.Current;
        bool same = work is not null && work.ProjectId == _project.Manifest.GameId;
        string headline, details;
        if (same && work!.IsRunning)
        {
            var step = work.Steps.LastOrDefault(x => x.Status == LunetAiStepStatus.Running)
                ?? work.Steps.LastOrDefault();
            headline = "✦ Trabalhando";
            details = step?.Label ?? "Preparando execução…";
        }
        else if (same && work!.Error is { } error)
        {
            headline = "✦ Falha da IA";
            details = error;
        }
        else if (same && work!.Reply is { } reply)
        {
            headline = reply.Status == AgentRuntime.RunStatus.Succeeded ? "✦ Trabalho concluído" : "✦ Verificar trabalho";
            details = reply.Text;
        }
        else
        {
            headline = "✦ Perguntar à IA";
            details = "Converse e acompanhe sem sair do editor.";
        }
        _aiCompactStatus.Text = same && work!.IsRunning
            ? headline + " · " + details : headline;
        if (_aiQuickStatus is not null) _aiQuickStatus.Text = details;
        if (_aiQuickAction is not null) _aiQuickAction.Text =
            same && work!.IsRunning ? "Parar" : "Enviar";
    }
}
