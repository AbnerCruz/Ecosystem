using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Method;
using Android.Text.Style;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Lunet.Ai;
using Lunet.Android.Editor;
using Lunet.Compiler;
using Lunet.Core;
using Lunet.Editor;
using System.Security.Cryptography;
using System.Text;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// LUNET-490: the AI conversation, designed after the best chat and coding-agent apps.
/// - Phones get a full conversation screen (the editor context still travels with every message); wide screens a side panel.
/// - Answers read like documents (no bubbles): Markdown, code cards with C# highlighting, Copy/Insert per block.
/// - The work is visible live (read, search, edit, compile…) and folds into one summary line when done.
/// - Every message has actions (copy, retry, edit); context pills show — and let you remove — what the AI will see.
/// </summary>
public sealed partial class MainActivity
{
    private LinearLayout? _aiPanel;
    private LinearLayout? _aiLog;
    private ScrollView? _aiScroll;
    private EditText? _aiInput;
    private Button? _aiSend;
    private TextView? _aiModelChip;
    private TextView? _aiSubtitle;
    private LinearLayout? _aiPills;
    private LunetAiSession? _aiSession;
    private string? _aiSessionProject;
    private string? _aiSessionDirectory;
    private LunetAiWork? _aiRenderedWork;
    private LinearLayout? _aiLiveSteps;
    private TextView? _aiLiveText;
    private IReadOnlyList<LunetAiStep> _aiRenderedSteps = [];
    private string? _aiPendingSelection;
    private string? _aiPendingSelectionFile;
    private LunetAiDocHit? _aiReference;
    private string? _aiLastPrompt;
    private readonly HashSet<string> _aiExcludedContext = new(StringComparer.Ordinal);

    private bool AiPanelOpen => _aiPanel is { Visibility: ViewStates.Visible };

    private void ShowAiAssistant() => OpenAiPanel(null);

    private void ToggleAiPanel()
    {
        if (AiPanelOpen) CloseAiPanel();
        else OpenAiPanel(null);
    }

    private void OpenAiPanel(string? prefill, bool autoSend = false, bool study = false)
    {
        if (_project is null || _workspaceFrame is null)
        {
            Toast.MakeText(this, "Abra um projeto para usar a IA.", ToastLength.Short)?.Show();
            return;
        }
        if (LunetAiRuns.Current is { IsRunning: true } active && active.ProjectId == _project.Manifest.GameId &&
            !active.BelongsTo(_project))
        {
            Toast.MakeText(this, "A IA ainda trabalha em outra cópia deste jogo.", ToastLength.Long)?.Show();
            return;
        }
        _aiPendingSelection = SelectedEditorText();
        _aiPendingSelectionFile = _openFile;
        if (_aiSessionProject != _project.Manifest.GameId)
        {
            _aiExcludedContext.Clear();
            _aiReference = null;
            _aiMode = LunetAiMode.Teach;
            _aiModeOnce = null;
        }
        if (study)
        {
            _aiMode = LunetAiMode.Teach;
            _aiModeOnce = null;
        }
        EnsureAiSession();
        if (_aiPanel is null || _aiPanel.Parent != _workspaceFrame) BuildChat(_workspaceFrame);
        _aiPanel!.Visibility = ViewStates.Visible;
        _aiPanel.BringToFront();
        RefreshAiHeader();
        PaintAiModes();
        RenderConversation();
        if (prefill is not null && _aiInput is not null)
        {
            _aiInput.Text = prefill;
            _aiInput.SetSelection(_aiInput.Text?.Length ?? 0);
        }
        if (!Ai.IsConfigured) Toast.MakeText(this, "Configure provedor e chave na área de trabalho → Lunet IA.", ToastLength.Long)?.Show();
        else if (autoSend && !string.IsNullOrWhiteSpace(prefill)) SendAi();
        else if (prefill is not null) FocusComposer();
    }

    private void CloseAiPanel()
    {
        if (_aiPanel is null) return;
        _aiPanel.Visibility = ViewStates.Gone;
        (GetSystemService(InputMethodService) as InputMethodManager)?.HideSoftInputFromWindow(_aiPanel.WindowToken, 0);
        _editor?.RequestFocus();
    }

    private void FocusComposer()
    {
        _aiInput?.RequestFocus();
        _aiInput?.Post(() => (GetSystemService(InputMethodService) as InputMethodManager)?.ShowSoftInput(_aiInput, ShowFlags.Implicit));
    }

    private void EnsureAiSession()
    {
        var project = _project!;
        if (_aiSession is not null && _aiSessionProject == project.Manifest.GameId && _aiSessionDirectory == project.Directory) return;
        var active = LunetAiRuns.Current;
        if (active is { IsRunning: true } && active.BelongsTo(project))
        {
            _aiSession = active.Session;
            _aiSessionProject = project.Manifest.GameId;
            _aiSessionDirectory = project.Directory;
            return;
        }
        var identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(project.Manifest.GameId))).ToLowerInvariant();
        _aiSession = new LunetAiSession(project, System.IO.Path.Combine(FilesDir!.AbsolutePath, "ai", identifier + ".json"));
        _aiSessionProject = project.Manifest.GameId;
        _aiSessionDirectory = project.Directory;
        _aiSession.MarkInterruptedIfPending();
        _aiAttachments.Clear();
    }

    // ---------- Layout ----------

    private global::Android.Content.ISharedPreferences AiDrafts =>
        GetSharedPreferences("lunet-ai-drafts", global::Android.Content.FileCreationMode.Private)!;

    private void BuildChat(FrameLayout host)
    {
        bool side = LunetUi.IsExpanded(this);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Elevation = Dp(16) };
        panel.Background = side ? LunetUi.Shape(this, LunetUi.Background, LunetUi.RadiusLarge, LunetUi.Border) : new global::Android.Graphics.Drawables.ColorDrawable(LunetUi.Background);

        // Header: back · title/subtitle · model · more
        var header = LunetUi.Row(this, 4);
        header.SetPadding(Dp(4), Dp(6), Dp(6), Dp(6));
        header.AddView(LunetUi.Icon(this, side ? "✕" : "‹", side ? "Fechar a IA" : "Voltar ao código", CloseAiPanel), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        var titles = new LinearLayout(this) { Orientation = Orientation.Vertical };
        titles.AddView(LunetUi.Label(this, "✦ Lunet IA", 16, LunetUi.Text, bold: true));
        _aiSubtitle = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        _aiSubtitle.SetSingleLine(true);
        _aiSubtitle.Ellipsize = TextUtils.TruncateAt.End;
        titles.AddView(_aiSubtitle);
        header.AddView(titles, LunetUi.Weight());
        _aiModelChip = LunetUi.Chip(this, "", ShowAiModelPicker);
        _aiModelChip.SetMaxWidth(Dp(150));
        _aiModelChip.Ellipsize = TextUtils.TruncateAt.End;
        header.AddView(_aiModelChip);
        header.AddView(LunetUi.Icon(this, "⋯", "Opções da conversa", ShowChatMenu), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        panel.AddView(header);
        panel.AddView(LunetUi.Divider(this));

        // Conversation
        _aiLog = new LinearLayout(this) { Orientation = Orientation.Vertical };
        _aiLog.SetPadding(Dp(16), Dp(12), Dp(16), Dp(24));
        _aiScroll = new ScrollView(this) { FillViewport = true };
        _aiScroll.AddView(_aiLog);
        panel.AddView(_aiScroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        // Composer: context pills + attachments + input box
        var composerArea = new LinearLayout(this) { Orientation = Orientation.Vertical };
        composerArea.SetPadding(Dp(10), Dp(6), Dp(10), Dp(10));
        _aiModeChips.Clear();
        var modeRow = LunetUi.Row(this, 6);
        modeRow.SetPadding(Dp(2), 0, Dp(2), Dp(6));
        foreach (var (mode, label, description) in AiModes)
        {
            var chip = LunetUi.Chip(this, label, () =>
            {
                if (mode == LunetAiMode.Do && (!Ai.Settings.AllowWrites || _aiExcludedContext.Contains("project")))
                {
                    Toast.MakeText(this, "Fazer requer contexto do projeto e permissão de escrita nas configurações globais.", ToastLength.Long)?.Show();
                    return;
                }
                _aiMode = mode;
                PaintAiModes();
            });
            chip.ContentDescription = description;
            _aiModeChips.Add((mode, chip));
            modeRow.AddView(chip);
        }
        PaintAiModes();
        composerArea.AddView(modeRow);
        var pillScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        _aiPills = LunetUi.Row(this, 6);
        _aiPills.SetPadding(Dp(2), 0, Dp(2), Dp(6));
        pillScroll.AddView(_aiPills);
        composerArea.AddView(pillScroll);
        var attachmentScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false, Visibility = ViewStates.Gone };
        _aiAttachmentRow = LunetUi.Row(this, 6);
        _aiAttachmentRow.SetPadding(Dp(2), 0, Dp(2), Dp(6));
        attachmentScroll.AddView(_aiAttachmentRow);
        composerArea.AddView(attachmentScroll);

        var box = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        box.SetGravity(GravityFlags.Bottom);
        box.SetPadding(Dp(4), Dp(4), Dp(4), Dp(4));
        box.Background = LunetUi.Shape(this, LunetUi.Surface, 24, LunetUi.Border);
        var attach = LunetUi.Icon(this, "＋", "Anexar imagem ou arquivo", ShowAttachMenu);
        attach.TextSize = 22;
        box.AddView(attach, new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        _aiInput = new EditText(this)
        {
            Hint = "Peça para construir, corrigir, desenhar ou explicar…",
            TextSize = LunetUi.TextBody,
            Background = null,
        };
        _aiInput.SetTextColor(LunetUi.Text);
        _aiInput.SetHintTextColor(LunetUi.TextFaint);
        _aiInput.SetPadding(Dp(6), Dp(10), Dp(6), Dp(10));
        _aiInput.SetMinLines(1);
        _aiInput.SetMaxLines(6);
        _aiInput.InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagCapSentences;
        var draftKey = "project:" + _aiSessionProject;
        _aiInput.Text = AiDrafts.GetString(draftKey, "") ?? "";
        var draftInput = _aiInput;
        _aiInput.TextChanged += (_, _) =>
        {
            AiDrafts.Edit()!.PutString(draftKey, draftInput.Text ?? "")!.Apply();
            if (ReferenceEquals(_aiInput, draftInput)) RefreshSendButton();
        };
        PaintAiModes();
        box.AddView(_aiInput, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        _aiSend = LunetUi.Icon(this, "↑", "Enviar", SendAi, accent: true);
        _aiSend.Background = LunetUi.Pressable(this, LunetUi.Accent, 99);
        box.AddView(_aiSend, new LinearLayout.LayoutParams(Dp(40), Dp(40)) { BottomMargin = Dp(2), RightMargin = Dp(2) });
        composerArea.AddView(box);
        panel.AddView(composerArea);

        FrameLayout.LayoutParams layout;
        if (side)
        {
            layout = new FrameLayout.LayoutParams(Dp(460), ViewGroup.LayoutParams.MatchParent, GravityFlags.End);
            layout.SetMargins(Dp(8), Dp(8), Dp(8), Dp(8));
        }
        else layout = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        host.AddView(panel, layout);
        panel.Visibility = ViewStates.Gone;
        _aiPanel = panel;
        RefreshAttachmentRow();
        RefreshSendButton();
    }

    // ---------- Teach / Do (LUNET-498) ----------

    private static readonly (LunetAiMode Mode, string Label, string Description)[] AiModes =
    [
        (LunetAiMode.Teach, "📖 Estudo", "Explica como você mesmo faz, sem alterar o projeto"),
        (LunetAiMode.Do, "⚒ Fazer", "A IA implementa no projeto, com checkpoint para desfazer"),
    ];

    private LunetAiMode _aiMode = LunetAiMode.Teach;
    private LunetAiMode? _aiModeOnce;
    private readonly List<(LunetAiMode Mode, TextView Chip)> _aiModeChips = [];

    private void PaintAiModes()
    {
        foreach (var (mode, chip) in _aiModeChips)
        {
            bool on = mode == _aiMode;
            chip.Background = LunetUi.Pressable(this, on ? LunetUi.AccentSoft : LunetUi.SurfaceRaised, 99, on ? LunetUi.Accent : LunetUi.Border);
            chip.SetTextColor(on ? LunetUi.AccentText : LunetUi.TextMuted);
        }
        if (_aiInput is not null)
            _aiInput.Hint = _aiMode switch
            {
                LunetAiMode.Teach => "Pergunte como fazer algo; eu explico passo a passo…",
                LunetAiMode.Do => "Diga o que construir, corrigir ou desenhar…",
                _ => "Peça para construir ou pergunte como fazer…",
            };
    }

    private void RefreshSendButton()
    {
        if (_aiSend is null) return;
        bool running = LunetAiRuns.Current is { IsRunning: true } current &&
            current.BelongsTo(_project);
        bool hasText = !string.IsNullOrWhiteSpace(_aiInput?.Text) || _aiAttachments.Count > 0;
        LunetUi.SetLabel(_aiSend, running ? "■" : "↑");
        _aiSend.ContentDescription = running ? "Parar a IA" : "Enviar";
        _aiSend.Alpha = running || hasText ? 1f : 0.45f;
    }

    private void ShowChatMenu() => ShowActionMenu("Conversa",
    [
        ("Nova conversa", () =>
        {
            if (LunetAiRuns.Current is { IsRunning: true } current && current.BelongsTo(_project))
            {
                Toast.MakeText(this, "Pare a execução antes de iniciar outra conversa.", ToastLength.Short)?.Show();
                return;
            }
            _aiSession?.ClearHistory();
            RenderConversation();
        }),
        ("Trocar modelo", ShowAiModelPicker),
        ("Copiar a conversa inteira", () => CopyToClipboard(string.Join("\n\n", (_aiSession?.History ?? []).Select(t => t.Role + ":\n" + t.Text)))),
        ("O que a IA sabe e consegue fazer", () => OpenAiPanel("O que você consegue fazer neste projeto e o que o Lunet ainda não tem?", autoSend: true)),
    ]);

    // ---------- Header and context ----------

    private void RefreshAiHeader()
    {
        if (_aiModelChip is null) return;
        var ai = ProjectAi;
        _aiModelChip.Text = ai.IsConfigured ? ShortModel(ai.Settings.Model) + "  ▾" : "Modelo ▾";
        if (_aiSubtitle is not null)
            _aiSubtitle.Text = (_project?.Name ?? "") + (ai.IsConfigured ? " · " + LunetAiProviders.Find(ai.Settings.ProviderId).Name : "") +
                               (ai.IsConfigured && !ai.Settings.AllowWrites ? " · só leitura" : "");
        RefreshPills();
    }

    private static string ShortModel(string model)
    {
        var name = model.Contains('/') ? model[(model.LastIndexOf('/') + 1)..] : model;
        return name.Length > 22 ? name[..22] + "…" : name;
    }

    /// <summary>Context pills: what travels with the next message; tap to leave something out (Cursor-style).</summary>
    private void RefreshPills()
    {
        if (_aiPills is null) return;
        _aiPills.RemoveAllViews();
        var context = FullAiContext();
        void Pill(string key, string text)
        {
            bool off = _aiExcludedContext.Contains(key);
            var chip = LunetUi.Chip(this, (off ? "＋ " : "") + text, () =>
            {
                if (!_aiExcludedContext.Remove(key)) _aiExcludedContext.Add(key);
                RefreshPills();
            }, selected: !off);
            chip.TextSize = 12;
            chip.Alpha = off ? 0.55f : 1f;
            chip.ContentDescription = (off ? "Incluir " : "Excluir do contexto: ") + text;
            _aiPills.AddView(chip);
        }
        Pill("project", "Projeto");
        _aiPills.AddView(LunetUi.Chip(this, "Conferir contexto", ShowEffectiveAiContext, selected: false));
        _aiPills.AddView(LunetUi.Chip(this, _aiReference is { } doc ? "📖 " + doc.Title : "＋ Referência offline",
            ChooseAiReference, _aiReference is not null));
        if (context.OpenFile is not null) Pill("file", "📄 " + System.IO.Path.GetFileName(context.OpenFile) + (context.CaretLine > 0 ? ":" + context.CaretLine : ""));
        if (!string.IsNullOrEmpty(context.Selection)) Pill("selection", "✂ seleção · " + context.Selection.Split('\n').Length + " linha(s)");
        int errors = context.Diagnostics.Count(d => d.Contains(": error", StringComparison.Ordinal));
        if (errors > 0) Pill("errors", "⚠ " + errors + " erro(s)");
        if (context.ConsoleTail.Count > 0) Pill("console", "❯ console");
        _aiPills.Visibility = _aiPills.ChildCount == 0 ? ViewStates.Gone : ViewStates.Visible;
    }

    private void ChooseAiReference()
    {
        var input = LunetUi.Input(this, "Buscar na documentação do Lunet");
        var dialog = new AlertDialog.Builder(this)!.SetTitle("Referência para o pedido")!.SetView(input)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetNeutralButton("Remover", (_, _) => { _aiReference = null; RefreshPills(); })!
            .SetPositiveButton("Buscar", (_, _) =>
            {
                var hits = AiKnowledge().Search(input.Text ?? "");
                if (hits.Count == 0)
                {
                    Toast.MakeText(this, "Nenhuma referência encontrada.", ToastLength.Short)?.Show();
                    return;
                }
                new AlertDialog.Builder(this)!.SetTitle("Incluir referência offline")!
                    .SetItems(hits.Select(hit => hit.Title).ToArray(), (_, e) => { _aiReference = hits[e.Which]; RefreshPills(); })!
                    .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
            })!;
        dialog.Show();
    }

    private void ShowEffectiveAiContext()
    {
        var context = CurrentAiContext();
        var text = context.IncludeProject
            ? $"Projeto {_project?.Name} · GameId {_project?.Manifest.GameId}\n" + context.Describe() +
                (_aiAttachments.Count == 0 ? "" : "\nAnexos: " + string.Join(", ", _aiAttachments)) +
                "\nA IA também pode consultar os arquivos do projeto e as referências offline."
            : "Sem arquivos, anexos, histórico do projeto, diagnósticos ou seleção. A documentação offline continua disponível." +
                (_aiReference is { } doc ? "\nReferência escolhida: " + doc.Title : "");
        var scroll = new ScrollView(this);
        var body = LunetUi.Label(this, text);
        body.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
        body.SetTextIsSelectable(true);
        scroll.AddView(body);
        new AlertDialog.Builder(this)!.SetTitle("Contexto do próximo pedido")!.SetView(scroll)!
            .SetPositiveButton("Fechar", (_, _) => { })!.Show();
    }

    private string? SelectedEditorText()
    {
        if (_editor is null) return null;
        var start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
        var end = Math.Max(_editor.SelectionStart, _editor.SelectionEnd);
        if (start < 0 || end <= start) return null;
        var text = _editor.Text ?? "";
        return end <= text.Length ? text[start..end] : null;
    }

    private LunetAiEditorContext FullAiContext()
    {
        int caretLine = 0;
        if (_openNote is null && _editor is not null && _editor.Layout is { } layout && _editor.SelectionStart >= 0)
            caretLine = layout.GetLineForOffset(_editor.SelectionStart) + 1;
        var diagnostics = _problems
            .Where(p => p.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(p => $"{p.FilePath ?? "?"}({p.Line},{p.Column}): {(p.Severity == DiagnosticSeverity.Error ? "error" : "warning")} {p.Id}: {p.Message}")
            .ToArray();
        var path = _openNote ?? _openFile;
        var revision = _openNote is null && _editor is not null ? LunetAiProjectFiles.Hash(_editor.Text ?? "") : null;
        return new LunetAiEditorContext(path, _openNote is null ? SelectedEditorText() ?? (_aiPendingSelectionFile == _openFile ? _aiPendingSelection : null) : null,
            caretLine, diagnostics, _console.TakeLast(30).ToArray(), Revision: revision, ReferenceId: _aiReference?.Id);
    }

    /// <summary>The context actually sent: the full context minus what the person turned off.</summary>
    private LunetAiEditorContext CurrentAiContext()
    {
        var c = FullAiContext();
        if (_aiExcludedContext.Contains("project"))
            return LunetAiEditorContext.Empty with { IncludeProject = false, ReferenceId = _aiReference?.Id };
        return new LunetAiEditorContext(
            _aiExcludedContext.Contains("file") ? null : c.OpenFile,
            _aiExcludedContext.Contains("selection") ? null : c.Selection,
            _aiExcludedContext.Contains("file") ? 0 : c.CaretLine,
            _aiExcludedContext.Contains("errors") ? [] : c.Diagnostics,
            _aiExcludedContext.Contains("console") ? [] : c.ConsoleTail,
            Revision: _aiExcludedContext.Contains("file") ? null : c.Revision, ReferenceId: c.ReferenceId);
    }

    // ---------- Conversation rendering ----------

    private void RenderConversation()
    {
        if (_aiLog is null || _aiSession is null) return;
        var work = LunetAiRuns.Current;
        bool belongs = work is not null && work.BelongsTo(_project);
        int oldY = _aiScroll?.ScrollY ?? 0;
        bool followBottom = _aiScroll is null || _aiLog.Height - oldY - _aiScroll.Height < Dp(96);

        _aiRenderedWork = null;
        _aiLiveText = null;
        _aiLiveSteps = null;
        _aiRenderedSteps = [];
        _aiLog.RemoveAllViews();
        var history = _aiSession.History;
        if (history.Count == 0 && !(belongs && work!.IsRunning)) { RenderEmptyState(); return; }
        for (int i = 0; i < history.Count; i++)
        {
            var turn = history[i];
            if (turn.Role == "Você") { AddUserMessage(turn.Text); continue; }
            // A pending turn of the live run shows its progress below; its changes appear when it ends.
            bool live = belongs && work!.IsRunning && turn.Pending;
            if (live) continue;
            AddAssistantMessage(turn.Text, retryPrompt: i > 0 && history[i - 1].Role == "Você"
                ? StripAttachments(history[i - 1].Text) : null, taught: turn.Mode == LunetAiMode.Teach,
                checkpoint: _aiSession.CheckpointFor(turn), verified: turn.Verified,
                steps: turn.Steps ?? (belongs && !work!.IsRunning && i == history.Count - 1 ? work.Steps : null));
        }
        if (belongs && work!.IsRunning)
        {
            // During the few milliseconds before the service persists the pending turn, show the request.
            if (history.Count == 0 || !history[^1].Pending) AddUserMessage(work.Prompt);
            AddAiWorkStatus(work);
        }
        else if (belongs && work!.Error is { } error)
        {
            _aiLog.AddView(LunetUi.Label(this, error, LunetUi.TextBody, LunetUi.Danger),
                Spaced(LunetUi.FillWidth(), 12));
        }

        if (followBottom) ScrollAiToEnd();
        else _aiScroll?.Post(() => _aiScroll?.ScrollTo(0, oldY));
    }

    private void AddAiWorkStatus(LunetAiWork work)
    {
        var state = LunetUi.Column(this, 8);
        state.AddView(LunetUi.Label(this, "✦ Lunet · trabalhando em segundo plano",
            LunetUi.TextLabel, LunetUi.AccentText, bold: true));
        _aiLiveText = LunetUi.Label(this, "", LunetUi.TextBody, LunetUi.Text);
        _aiLiveText.SetTextIsSelectable(true);
        state.AddView(_aiLiveText);
        _aiLiveSteps = LunetUi.Column(this, 4);
        state.AddView(_aiLiveSteps);
        _aiLog!.AddView(state, Spaced(LunetUi.FillWidth(), 14));
        _aiRenderedWork = work;
        RefreshLiveAi(work);
    }

    private void RefreshLiveAi(LunetAiWork work)
    {
        if (!ReferenceEquals(_aiRenderedWork, work) || _aiLiveText is null || _aiLiveSteps is null)
        {
            RenderConversation();
            return;
        }
        bool follow = _aiScroll is null || _aiLog!.Height - _aiScroll.ScrollY - _aiScroll.Height < Dp(64);
        var text = work.PartialText;
        if (_aiLiveText.Text != text) _aiLiveText.Text = text;
        _aiLiveText.Visibility = text.Length == 0 ? ViewStates.Gone : ViewStates.Visible;
        var steps = work.Steps.TakeLast(12).ToArray();
        if (!_aiRenderedSteps.SequenceEqual(steps))
        {
            _aiLiveSteps.RemoveAllViews();
            foreach (var step in steps) _aiLiveSteps.AddView(StepRow(step));
            _aiRenderedSteps = steps;
        }
        if (_aiLiveSteps.ChildCount == 0)
            _aiLiveSteps.AddView(LunetUi.Label(this, "Aguardando o modelo…", LunetUi.TextBody, LunetUi.TextMuted));
        if (follow) ScrollAiToEnd();
    }

    private static string StripAttachments(string text)
    {
        var index = text.IndexOf("\n📎 ", StringComparison.Ordinal);
        return index < 0 ? text : text[..index];
    }

    private void RenderEmptyState()
    {
        var empty = new LinearLayout(this) { Orientation = Orientation.Vertical };
        empty.SetGravity(GravityFlags.CenterHorizontal);
        empty.SetPadding(Dp(4), Dp(36), Dp(4), Dp(12));
        var moon = LunetUi.Label(this, "☾", 40, LunetUi.AccentText);
        moon.Gravity = GravityFlags.Center;
        empty.AddView(moon, LunetUi.FillWidth());
        var title = LunetUi.Label(this, $"O que vamos criar em {_project?.Name}?", 21, LunetUi.Text, bold: true);
        title.Gravity = GravityFlags.Center;
        title.SetPadding(0, Dp(8), 0, Dp(4));
        empty.AddView(title, LunetUi.FillWidth());
        var subtitle = LunetUi.Label(this, "Eu conheço o Lunet e este projeto: o código, a arte, os erros e as suas notas. " +
            "Tudo o que eu mudar pode ser desfeito.", LunetUi.TextLabel, LunetUi.TextMuted);
        subtitle.Gravity = GravityFlags.Center;
        subtitle.SetPadding(Dp(8), 0, Dp(8), Dp(20));
        empty.AddView(subtitle, LunetUi.FillWidth());

        var suggestions = new List<(string Title, string Detail, string Prompt)>();
        int errors = _problems.Count(p => p.Severity == DiagnosticSeverity.Error);
        if (errors > 0) suggestions.Add(($"Corrigir {errors} erro(s)", "e confirmar que compila", "Corrija os erros de compilação do projeto e confirme que compila."));
        var file = _openFile is null ? "o jogo" : System.IO.Path.GetFileName(_openFile);
        suggestions.Add(($"Explicar {file}", "como o código funciona", $"Explique como {file} funciona, em linguagem simples."));
        suggestions.Add(("Criar uma mecânica", "ex.: pulo, inimigo, power-up", "Implemente no jogo: "));
        var images = _project is null ? [] : ProjectNotes.List(_project).Where(n => n.Kind is NoteKind.Image or NoteKind.Sketch).ToArray();
        suggestions.Add(images.Length > 0
            ? ("Desenho → sprite", images[0].Title, $"Crie um sprite a partir de {images[0].Path}, coerente com o estilo do projeto.")
            : ("Desenhar um sprite", "no estilo do projeto", "Crie um sprite do personagem principal, coerente com o estilo do projeto, e use-o no jogo."));
        suggestions.Add(("O que o Lunet consegue?", "recursos e limites", "O que o Lunet consegue fazer hoje e o que ainda não tem?"));

        var grid = new GridLayout(this) { ColumnCount = 2 };
        foreach (var (titleText, detail, prompt) in suggestions.Take(4))
        {
            var card = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true };
            card.SetPadding(Dp(14), Dp(12), Dp(14), Dp(12));
            card.Background = LunetUi.Pressable(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
            card.AddView(LunetUi.Label(this, titleText, LunetUi.TextLabel, LunetUi.Text, bold: true));
            var detailLabel = LunetUi.Label(this, detail, LunetUi.TextCaption, LunetUi.TextMuted);
            detailLabel.SetMaxLines(2);
            card.AddView(detailLabel);
            card.Click += (_, _) =>
            {
                if (prompt.EndsWith(' ')) { _aiInput!.Text = prompt; _aiInput.SetSelection(prompt.Length); FocusComposer(); }
                else { _aiInput!.Text = prompt; SendAi(); }
            };
            var cell = new GridLayout.LayoutParams(GridLayout.InvokeSpec(GridLayout.Undefined, 1f), GridLayout.InvokeSpec(GridLayout.Undefined, 1f))
            { Width = 0 };
            cell.SetMargins(Dp(4), Dp(4), Dp(4), Dp(4));
            grid.AddView(card, cell);
        }
        empty.AddView(grid, LunetUi.FillWidth());
        _aiLog!.AddView(empty, LunetUi.FillWidth());
    }

    private void AddUserMessage(string text)
    {
        var column = new LinearLayout(this) { Orientation = Orientation.Vertical };
        column.SetGravity(GravityFlags.End);
        var bubble = LunetUi.Label(this, text, LunetUi.TextBody, LunetUi.Text);
        bubble.SetTextIsSelectable(true);
        bubble.SetPadding(Dp(14), Dp(10), Dp(14), Dp(10));
        bubble.Background = LunetUi.Shape(this, LunetUi.SurfaceHigh, 18);
        bubble.SetMaxWidth((int)(Resources!.DisplayMetrics!.WidthPixels * (LunetUi.IsExpanded(this) ? 0.3f : 0.82f)));
        column.AddView(bubble, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { Gravity = GravityFlags.End });
        var edit = LunetUi.Button(this, "Editar", () =>
        {
            _aiInput!.Text = StripAttachments(text);
            _aiInput.SetSelection(_aiInput.Text?.Length ?? 0);
            FocusComposer();
        }, ButtonStyle.Quiet);
        edit.TextSize = 12;
        edit.SetMinHeight(Dp(32)); edit.SetMinimumHeight(Dp(32));
        column.AddView(edit, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)) { Gravity = GravityFlags.End });
        _aiLog!.AddView(column, Spaced(LunetUi.FillWidth(), 6));
    }

    /// <summary>An answer: header, Markdown blocks, optional work summary and changes, then actions.</summary>
    private LinearLayout AddAssistantMessage(string text, string? retryPrompt, IReadOnlyList<LunetAiStep>? steps = null,
        bool taught = false, LunetAiCheckpoint? checkpoint = null, bool? verified = null)
    {
        var message = new LinearLayout(this) { Orientation = Orientation.Vertical };
        message.AddView(LunetUi.Label(this, taught ? "✦ Lunet · explicando (nada foi alterado)" : "✦ Lunet",
            LunetUi.TextCaption, LunetUi.AccentText, bold: true));
        if (steps is { Count: > 0 }) message.AddView(WorkSummary(steps), Spaced(LunetUi.FillWidth(), 6));
        RenderMarkdown(message, text);
        if (checkpoint is not null && (checkpoint.IsReverted || checkpoint.Changes().Count > 0))
            message.AddView(BuildChangesCard(checkpoint, verified), Spaced(LunetUi.FillWidth(), 10));
        var actions = LunetUi.Row(this, 2);
        void Action(string label, string description, Action run)
        {
            var b = LunetUi.Button(this, label, run, ButtonStyle.Quiet, description: description);
            b.TextSize = 12;
            b.SetMinHeight(Dp(32)); b.SetMinimumHeight(Dp(32));
            actions.AddView(b, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        }
        Action("⧉ Copiar", "Copiar a resposta", () => CopyToClipboard(text));
        if (taught && retryPrompt is not null)
            Action("⚒ Fazer por mim", "A IA implementa o que explicou", () =>
            {
                _aiInput!.Text = retryPrompt;
                _aiModeOnce = LunetAiMode.Do;
                SendAi();
            });
        if (retryPrompt is not null) Action("↻ Refazer", "Pedir de novo", () => { _aiInput!.Text = retryPrompt; SendAi(); });
        message.AddView(actions);
        _aiLog!.AddView(message, Spaced(LunetUi.FillWidth(), 14));
        return message;
    }

    private LinearLayout.LayoutParams Spaced(LinearLayout.LayoutParams layout, int topDp)
    {
        layout.TopMargin = Dp(topDp);
        return layout;
    }

    // ---------- Markdown ----------

    private void RenderMarkdown(LinearLayout target, string markdown)
    {
        foreach (var block in ChatMarkdown.Parse(markdown))
        {
            switch (block.Kind)
            {
                case ChatBlockKind.Code:
                    target.AddView(CodeCard(block.Text, block.Language), Spaced(LunetUi.FillWidth(), 8));
                    break;
                case ChatBlockKind.Rule:
                    target.AddView(LunetUi.Divider(this), Spaced(LunetUi.FillWidth(), 10));
                    break;
                case ChatBlockKind.Table:
                    target.AddView(TableView(block.Rows ?? []), Spaced(LunetUi.FillWidth(), 8));
                    break;
                default:
                    var view = new TextView(this) { TextSize = LunetUi.TextBody };
                    view.SetTextColor(LunetUi.Text);
                    view.SetLineSpacing(0, 1.3f);
                    view.SetTextIsSelectable(true);
                    view.MovementMethod = LinkMovementMethod.Instance;
                    var builder = new SpannableStringBuilder();
                    int top = 6;
                    switch (block.Kind)
                    {
                        case ChatBlockKind.Heading:
                            AppendSpans(builder, block.Text);
                            builder.SetSpan(new StyleSpan(TypefaceStyle.Bold), 0, builder.Length(), SpanTypes.ExclusiveExclusive);
                            builder.SetSpan(new RelativeSizeSpan(block.Level <= 1 ? 1.35f : block.Level == 2 ? 1.2f : 1.08f), 0, builder.Length(), SpanTypes.ExclusiveExclusive);
                            top = 14;
                            break;
                        case ChatBlockKind.Bullet:
                        case ChatBlockKind.Numbered:
                            var marker = block.Kind == ChatBlockKind.Bullet ? "•" : block.Level + ".";
                            builder.Append(marker + "  ");
                            AppendSpans(builder, block.Text);
                            builder.SetSpan(new LeadingMarginSpanStandard(Dp(4), Dp(22)), 0, builder.Length(), SpanTypes.ExclusiveExclusive);
                            builder.SetSpan(new ForegroundColorSpan(LunetUi.AccentText), 0, marker.Length, SpanTypes.ExclusiveExclusive);
                            top = 3;
                            break;
                        case ChatBlockKind.Quote:
                            AppendSpans(builder, block.Text);
                            builder.SetSpan(new QuoteSpan(LunetUi.Accent, Dp(3), Dp(12)), 0, builder.Length(), SpanTypes.ExclusiveExclusive);
                            view.SetTextColor(LunetUi.TextMuted);
                            break;
                        default:
                            AppendSpans(builder, block.Text);
                            break;
                    }
                    view.SetText(builder, TextView.BufferType.Spannable);
                    target.AddView(view, Spaced(LunetUi.FillWidth(), top));
                    break;
            }
        }
    }

    private void AppendSpans(SpannableStringBuilder builder, string text)
    {
        foreach (var span in ChatMarkdown.Inline(text))
        {
            int start = builder.Length();
            builder.Append(span.Text);
            int end = builder.Length();
            switch (span.Style)
            {
                case ChatInline.Bold:
                    builder.SetSpan(new StyleSpan(TypefaceStyle.Bold), start, end, SpanTypes.ExclusiveExclusive);
                    break;
                case ChatInline.Italic:
                    builder.SetSpan(new StyleSpan(TypefaceStyle.Italic), start, end, SpanTypes.ExclusiveExclusive);
                    break;
                case ChatInline.Code:
                    builder.SetSpan(new TypefaceSpan("monospace"), start, end, SpanTypes.ExclusiveExclusive);
                    builder.SetSpan(new BackgroundColorSpan(LunetUi.SurfaceHigh), start, end, SpanTypes.ExclusiveExclusive);
                    builder.SetSpan(new ForegroundColorSpan(LunetUi.AccentText), start, end, SpanTypes.ExclusiveExclusive);
                    break;
                case ChatInline.Link:
                    var target = span.Target ?? "";
                    builder.SetSpan(new ChatLinkSpan(this, target), start, end, SpanTypes.ExclusiveExclusive);
                    break;
            }
        }
    }

    /// <summary>Links: project files open in Lunet, docs ids open the offline docs, web links the browser.</summary>
    private sealed class ChatLinkSpan(MainActivity owner, string target) : ClickableSpan
    {
        public override void OnClick(View widget)
        {
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                owner.StartActivity(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(target)));
            else if (target.StartsWith("guide:", StringComparison.Ordinal) || target.StartsWith("api:", StringComparison.Ordinal))
                DocumentationPanel.Open(owner, target.StartsWith("guide:", StringComparison.Ordinal) ? target : "id:T:" + target[4..]);
            else if (owner._project is not null && File.Exists(System.IO.Path.Combine(owner._project.Directory, target)))
            {
                owner.CloseAiPanel();
                owner.OpenProjectEntry(target);
            }
        }

        public override void UpdateDrawState(TextPaint ds)
        {
            ds.Color = LunetUi.AccentText;
            ds.UnderlineText = true;
        }
    }

    /// <summary>A code block as its own card: language, Copy, Insert at cursor, C# highlighting, horizontal scroll.</summary>
    private View CodeCard(string code, string? language)
    {
        var card = new LinearLayout(this) { Orientation = Orientation.Vertical };
        card.Background = LunetUi.Shape(this, AColor.Rgb(16, 19, 27), LunetUi.Radius, LunetUi.Border);
        var head = LunetUi.Row(this, 2);
        head.SetPadding(Dp(12), Dp(2), Dp(4), Dp(2));
        var lang = (language ?? "código").ToLowerInvariant();
        head.AddView(LunetUi.Label(this, lang, LunetUi.TextCaption, LunetUi.TextMuted), LunetUi.Weight());
        void Action(string label, Action run)
        {
            var b = LunetUi.Button(this, label, run, ButtonStyle.Quiet);
            b.TextSize = 12;
            b.SetMinHeight(Dp(32)); b.SetMinimumHeight(Dp(32));
            head.AddView(b, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, Dp(34)));
        }
        Action("⧉ Copiar", () => CopyToClipboard(code));
        if (_editor is not null && _openNote is null) Action("⤓ Inserir", () => { CloseAiPanel(); InsertAtCaret(code); });
        card.AddView(head);
        card.AddView(LunetUi.Divider(this));
        var view = new TextView(this) { TextSize = 13 };
        view.SetTypeface(LunetUi.Mono, TypefaceStyle.Normal);
        view.SetTextColor(AColor.Rgb(226, 232, 242));
        view.SetTextIsSelectable(true);
        view.SetPadding(Dp(12), Dp(10), Dp(12), Dp(12));
        view.SetLineSpacing(0, 1.15f);
        var builder = new SpannableStringBuilder(code);
        if (lang is "csharp" or "cs" or "c#" or "código")
        {
            try
            {
                foreach (var token in SyntaxHighlighter.Classify(code))
                    if (CodeEditText.ColorOf(token.Kind) is { } color && token.Start + token.Length <= code.Length)
                        builder.SetSpan(new ForegroundColorSpan(color), token.Start, token.Start + token.Length, SpanTypes.ExclusiveExclusive);
            }
            catch (Exception) { /* highlighting is cosmetic */ }
        }
        view.SetText(builder, TextView.BufferType.Spannable);
        var scroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        scroll.AddView(view);
        card.AddView(scroll);
        return card;
    }

    private View TableView(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        int columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var widths = Enumerable.Range(0, columns).Select(c => rows.Max(r => c < r.Count ? r[c].Length : 0)).ToArray();
        var text = new StringBuilder();
        for (int r = 0; r < rows.Count; r++)
        {
            text.AppendLine(string.Join("  │  ", Enumerable.Range(0, columns).Select(c => (c < rows[r].Count ? rows[r][c] : "").PadRight(widths[c]))));
            if (r == 0) text.AppendLine(string.Join("──┼──", widths.Select(w => new string('─', w))));
        }
        var view = LunetUi.Label(this, text.ToString().TrimEnd(), 12, LunetUi.Text);
        view.SetTypeface(LunetUi.Mono, TypefaceStyle.Normal);
        view.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        view.SetTextIsSelectable(true);
        var scroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        scroll.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
        scroll.AddView(view);
        return scroll;
    }

    // ---------- Work timeline ----------

    private static string StepGlyph(LunetAiStep step) => step.Status switch
    {
        LunetAiStepStatus.Running => "◌",
        LunetAiStepStatus.Failed => "✕",
        _ => step.Kind switch
        {
            LunetAiStepKind.Edit or LunetAiStepKind.Create => "✎",
            LunetAiStepKind.Compile => "⚙",
            LunetAiStepKind.Art => "▦",
            LunetAiStepKind.Image => "◉",
            LunetAiStepKind.Docs => "📖",
            _ => "✓",
        },
    };

    private View StepRow(LunetAiStep step)
    {
        var row = LunetUi.Row(this, 8);
        row.SetPadding(Dp(2), Dp(3), Dp(2), Dp(3));
        var glyph = LunetUi.Label(this, StepGlyph(step), 13, step.Status switch
        {
            LunetAiStepStatus.Failed => LunetUi.Danger,
            LunetAiStepStatus.Running => LunetUi.AccentText,
            _ => step.Kind is LunetAiStepKind.Edit or LunetAiStepKind.Art ? LunetUi.Success : LunetUi.TextMuted,
        });
        row.AddView(glyph, new LinearLayout.LayoutParams(Dp(20), ViewGroup.LayoutParams.WrapContent));
        var label = LunetUi.Label(this, step.Label + (step.Error is { } e ? " — " + e : ""), 13,
            step.Status == LunetAiStepStatus.Running ? LunetUi.Text : LunetUi.TextMuted);
        label.SetMaxLines(2);
        label.Ellipsize = TextUtils.TruncateAt.End;
        row.AddView(label, LunetUi.Weight());
        if (step.Detail is { } path && _project is not null && File.Exists(System.IO.Path.Combine(_project.Directory, path)))
        {
            row.Clickable = true;
            row.Click += (_, _) => { CloseAiPanel(); OpenProjectEntry(path); };
        }
        return row;
    }

    /// <summary>After the run: one line "Explorou N itens · editou M arquivos", expandable to every step.</summary>
    private View WorkSummary(IReadOnlyList<LunetAiStep> steps)
    {
        var finished = steps.GroupBy(s => s.Id).Select(g => g.Last()).ToArray();
        var box = new LinearLayout(this) { Orientation = Orientation.Vertical };
        box.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        box.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
        var header = LunetUi.Label(this, "▸  " + LunetAiStep.Summarize(finished), 13, LunetUi.TextMuted);
        header.Clickable = true;
        var list = new LinearLayout(this) { Orientation = Orientation.Vertical, Visibility = ViewStates.Gone };
        list.SetPadding(0, Dp(6), 0, 0);
        foreach (var step in finished) list.AddView(StepRow(step));
        header.Click += (_, _) =>
        {
            bool show = list.Visibility != ViewStates.Visible;
            list.Visibility = show ? ViewStates.Visible : ViewStates.Gone;
            header.Text = (show ? "▾  " : "▸  ") + LunetAiStep.Summarize(finished);
        };
        box.AddView(header);
        box.AddView(list);
        return box;
    }

    // ---------- Sending ----------

    private LunetAiWebSession? _aiWeb;
    /// <summary>One research gateway for the app's life (its HTTP client is reused by every request).</summary>
    private LunetAiWebSession AiWeb => _aiWeb ??= new LunetAiWebSession();

    private LunetAiWork? _aiLastAppliedWork;
    private int _aiUiRefreshQueued;
    private long _aiLastFileSyncMs;

    private void SendAi()
    {
        var ongoing = LunetAiRuns.Current;
        if (ongoing is { IsRunning: true })
        {
            if (ongoing.BelongsTo(_project)) ongoing.Cancel();
            else Toast.MakeText(this, "A IA já trabalha em outro projeto. Aguarde ou cancele a tarefa atual.", ToastLength.Long)?.Show();
            return;
        }

        var prompt = (_aiInput?.Text ?? "").Trim();
        if (prompt.Length == 0 && _aiAttachments.Count > 0)
            prompt = "Veja o anexo e me diga como podemos usá-lo no jogo.";
        if (prompt.Length == 0 || _project is null || _aiSession is null) return;
        var ai = ProjectAi;
        if (!ai.IsConfigured) { Toast.MakeText(this, "Configure a IA na área de trabalho → Lunet IA.", ToastLength.Long)?.Show(); return; }

        SaveCurrent();
        var context = CurrentAiContext();
        var attachments = context.IncludeProject ? _aiAttachments.ToArray() : [];
        var project = _project;
        var mode = _aiModeOnce ?? _aiMode;
        if (mode == LunetAiMode.Do && (!ai.Settings.AllowWrites || !context.IncludeProject))
        {
            Toast.MakeText(this, "Fazer requer contexto do projeto e permissão de escrita nas configurações globais.", ToastLength.Long)?.Show();
            return;
        }
        bool? tools = null;
        try { tools = ModelCatalog.KnownTools(ai); }
        catch (ArgumentException) { }
        if (mode == LunetAiMode.Do && tools == false)
        {
            Toast.MakeText(this, "Este modelo não oferece ferramentas. Escolha outro modelo ou use Estudo.", ToastLength.Long)?.Show();
            return;
        }
        var request = new LunetAiRequest(ai.Settings, ai.ApiKey!, context, new AndroidAiBuild(project),
            Knowledge: AiKnowledge(), Images: new AndroidAiImages(), Attachments: attachments, Mode: mode,
            Web: ai.Settings.WebResearch ? AiWeb : null, ModelTools: tools);
        var work = new LunetAiWork(project, _aiSession, prompt, request);
        if (!LunetAiRuns.TryStart(this, work, out var error))
        {
            Toast.MakeText(this, error ?? "Não foi possível iniciar a IA.", ToastLength.Long)?.Show();
            return;
        }

        _aiModeOnce = null;
        _aiMode = LunetAiMode.Teach;
        PaintAiModes();
        _aiPendingSelection = null;
        _aiInput!.Text = "";
        _aiAttachments.Clear();
        RefreshAttachmentRow();
        _aiLastPrompt = prompt;
        RenderConversation();
        RefreshSendButton();
    }

    // Lifecycle-independent run events: no network task captures MainActivity or a chat View.
    private void OnAiRunChanged()
    {
        if (IsDestroyed || System.Threading.Interlocked.Exchange(ref _aiUiRefreshQueued, 1) != 0) return;
        RunOnUiThread(() =>
        {
            System.Threading.Interlocked.Exchange(ref _aiUiRefreshQueued, 0);
            if (IsDestroyed || _project is null) return;
            var work = LunetAiRuns.Current;
            if (work is null || !work.BelongsTo(_project)) return;

            // Avoid reloading the editor on each progress event. Always sync once when done.
            long tick = Environment.TickCount64;
            if (!work.IsRunning || (work.Steps.Any(step =>
                    step.Kind is LunetAiStepKind.Edit or LunetAiStepKind.Create or LunetAiStepKind.Art) &&
                tick - _aiLastFileSyncMs >= 500))
            {
                _aiLastFileSyncMs = tick;
                SyncEditorWithDisk();
            }
            if (!work.IsRunning && !ReferenceEquals(_aiLastAppliedWork, work))
            {
                _aiLastAppliedWork = work;
                RefreshExplorer();
                if (work.Reply is { Changes.Count: > 0 } && work.Request.Build is AndroidAiBuild build && build.LastErrors.Count == 0)
                    _problems = [];
            }

            UpdateAiCompact();
            long now = Environment.TickCount64;
            if (AiPanelOpen && (!work.IsRunning || now - _aiLastConversationRenderMs >= 100))
            {
                _aiLastConversationRenderMs = now;
                if (work.IsRunning) RefreshLiveAi(work);
                else RenderConversation();
            }
            RefreshSendButton();
        });
    }

    private static string ExplainAiError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("401") || message.Contains("auth", StringComparison.OrdinalIgnoreCase))
            return "O provedor recusou a chave (401). Confira a chave em ⋯ → Configurar.";
        if (message.Contains("404"))
            return "O provedor não encontrou o modelo ou o endpoint (404). Confira o id do modelo.";
        if (message.Contains("429"))
            return "Limite ou saldo do provedor esgotado (429). Tente de novo em instantes ou verifique o saldo.";
        if (ex is HttpRequestException or TaskCanceledException)
            return "Sem conexão com o provedor. Verifique a internet e tente de novo.";
        return "Não foi possível concluir: " + message;
    }

    private void ReloadOpenFileIfChanged(IEnumerable<string> paths)
    {
        var changed = paths.ToHashSet(StringComparer.Ordinal);
        foreach (var path in changed.Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            try { _assistant?.SetFile(path, _project!.ReadText(path)); } catch (IOException) { }
        }
        if (_openFile is { } opened && changed.Contains(opened))
        {
            if (_editor is null || _session.IsDirty(_editor.Text ?? "")) return;
            CaptureDocument();
            _session.Unload();
            _openFile = null;
            OpenFile(opened);
        }
    }

    /// <summary>
    /// The AI wrote the open file: reload it in place when the person has no unsaved edits. With edits, nothing is
    /// overwritten either way; the next save asks which version to keep (see <see cref="ShowEditorConflict"/>).
    /// </summary>
    private void SyncEditorWithDisk()
    {
        if (_project is null || _editor is null || _openFile is null || _openNote is not null) return;
        if (_preview is not null) return; // the editor view is not shown; it reloads when the workspace returns
        if (!_session.ChangedOnDisk(_project) || _session.IsDirty(_editor.Text ?? "")) return;
        try
        {
            var text = _project.ReadText(_openFile);
            var view = CurrentEditorViewport(_editor);
            _editor.LoadText(text);
            _session.Load(_openFile, text);
            RestoreDocumentViewport(view);
            try { _assistant?.SetFile(_openFile, text); } catch (IOException) { }
        }
        catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException) { }
    }

    private void ScrollAiToEnd()
    {
        if (_aiScroll is { } scroll && _aiLog is { } log)
            scroll.Post(() => scroll.ScrollTo(0, log.Height));
    }

#if LUNET_UI_DEMO
    /// <summary>Screenshot content: a finished exchange with steps, code and changes summary.</summary>
    private void DemoConversation()
    {
        _aiLog?.RemoveAllViews();
        AddUserMessage("Faça o herói pular quando tocar na tela, com um sprite novo para o salto.");
        var steps = new List<LunetAiStep>
        {
            new("1", LunetAiStepKind.Style, "Lendo o estilo do projeto", null, LunetAiStepStatus.Done),
            new("2", LunetAiStepKind.Read, "Lendo Game.cs", "Game.cs", LunetAiStepStatus.Done),
            new("3", LunetAiStepKind.Docs, "Estudando a documentação: \"GestureRecognizer\"", "GestureRecognizer", LunetAiStepStatus.Done),
            new("4", LunetAiStepKind.Art, "Desenhando Assets/heroi-pulo", "Assets/heroi-pulo", LunetAiStepStatus.Done),
            new("5", LunetAiStepKind.Edit, "Editando Game.cs", "Game.cs", LunetAiStepStatus.Done),
            new("6", LunetAiStepKind.Compile, "Compilando o projeto", null, LunetAiStepStatus.Done),
        };
        AddAssistantMessage(
            "Pronto! O herói agora **pula ao tocar** na tela, e criei o sprite `Assets/heroi-pulo` no estilo do projeto (paleta e 16×16).\n\n" +
            "## O que mudou\n1. Gravidade constante e impulso para cima no toque\n2. Só pula quando está no chão\n3. Animação de salto com 3 frames\n\n" +
            "```csharp\nif (Input.Touch.Pressed && _onGround)\n    _velocity.Y = -JumpSpeed;\n_velocity.Y += Gravity * dt;\n```\n\n" +
            "> Compilei o projeto: nenhum erro. Toque em ▶ Rodar para testar.", "Faça o herói pular", steps);
    }
#endif
}
