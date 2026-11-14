using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using Lunet.Ai;
using Lunet.Compiler;
using Lunet.Core;
using System.Security.Cryptography;
using System.Text;
using AColor = Android.Graphics.Color;
using AndroidUri = Android.Net.Uri;

namespace Lunet.Android;

/// <summary>
/// LUNET-484: the AI works beside the code. One tap from the top bar (or "Pedir à IA" on a selection) opens a panel
/// that does not cover the editor on wide screens and keeps it visible on phones. It knows the open file, selection,
/// errors and console; it compiles what it writes; every change can be reviewed and reverted.
/// </summary>
public sealed partial class MainActivity
{
    private LunetAiSettingsStore? _aiStore;
    private LunetAiLoadedSettings? _ai;
    private CancellationTokenSource? _aiRunCancellation;
    private LinearLayout? _aiPanel;
    private LinearLayout? _aiLog;
    private ScrollView? _aiScroll;
    private EditText? _aiInput;
    private Button? _aiSend;
    private TextView? _aiContextLine;
    private TextView? _aiModelChip;
    private TextView? _aiActivity;
    private LunetAiSession? _aiSession;
    private string? _aiSessionProject;
    private string? _aiPendingSelection;

    private LunetAiSettingsStore AiStore => _aiStore ??= new LunetAiSettingsStore(
        System.IO.Path.Combine(FilesDir!.AbsolutePath, "ai", "settings.json"),
        new AndroidKeystoreVault(System.IO.Path.Combine(NoBackupFilesDir!.AbsolutePath, "secure")));

    private LunetAiLoadedSettings Ai => _ai ??= AiStore.Load();

    // ---------- Settings (configure once) ----------

    private void ShowAiSettings(Action? afterSave = null)
    {
        var current = Ai;
        var form = LunetUi.Column(this, 10);
        form.SetPadding(Dp(18), Dp(8), Dp(18), Dp(8));

        var providerIndex = Math.Max(0, LunetAiProviders.All.ToList().FindIndex(p => p.Id == current.Settings.ProviderId));
        var selected = LunetAiProviders.All[providerIndex];
        form.AddView(LunetUi.Label(this, "Provedor", LunetUi.TextLabel, LunetUi.TextMuted));
        var providers = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        var providerRow = LunetUi.Row(this, 8);
        providers.AddView(providerRow);
        form.AddView(providers);

        var endpointLabel = LunetUi.Label(this, "Endpoint (/chat/completions)", LunetUi.TextLabel, LunetUi.TextMuted);
        var endpoint = LunetUi.Input(this, "https://…/v1/chat/completions", current.Settings.Endpoint);
        endpoint.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        form.AddView(endpointLabel);
        form.AddView(endpoint);

        form.AddView(LunetUi.Label(this, "Modelo", LunetUi.TextLabel, LunetUi.TextMuted));
        var model = LunetUi.Input(this, "id do modelo no provedor", current.Settings.Model);
        form.AddView(model);
        var suggestions = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        var suggestionRow = LunetUi.Row(this, 6);
        suggestions.AddView(suggestionRow);
        form.AddView(suggestions);

        var keyLabel = LunetUi.Label(this, "", LunetUi.TextLabel, LunetUi.TextMuted);
        form.AddView(keyLabel);
        var key = LunetUi.Input(this, current.ApiKey is null ? "Cole a chave da API" : "•••• chave salva (deixe vazio para manter)");
        key.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        form.AddView(key);
        var getKey = LunetUi.Button(this, "Criar chave no site do provedor ↗", () =>
        {
            if (selected.KeyUrl.Length > 0) StartActivity(new Intent(Intent.ActionView, AndroidUri.Parse(selected.KeyUrl)));
        }, ButtonStyle.Quiet);
        form.AddView(getKey);

        var writes = new Switch(this) { Text = "A IA pode criar e alterar arquivos (com checkpoint para desfazer)", Checked = current.Settings.AllowWrites };
        writes.SetTextColor(LunetUi.Text);
        form.AddView(writes);

        form.AddView(LunetUi.Label(this, "Limite de passos por pedido", LunetUi.TextLabel, LunetUi.TextMuted));
        var stepsRow = LunetUi.Row(this, 6);
        int steps = current.Settings.MaxSteps;
        var stepChips = new List<(int Value, TextView Chip)>();
        void PaintSteps()
        {
            foreach (var (value, chip) in stepChips)
            {
                chip.Background = LunetUi.Pressable(this, value == steps ? LunetUi.AccentSoft : LunetUi.SurfaceRaised, 99,
                    value == steps ? LunetUi.Accent : LunetUi.Border);
                chip.SetTextColor(value == steps ? LunetUi.AccentText : LunetUi.Text);
            }
        }
        foreach (var value in new[] { 16, 32, 64, 128 })
        {
            var chip = LunetUi.Chip(this, value.ToString(), () => { steps = value; PaintSteps(); });
            stepChips.Add((value, chip));
            stepsRow.AddView(chip);
        }
        PaintSteps();
        form.AddView(stepsRow);
        form.AddView(LunetUi.Label(this,
            "A chave fica cifrada no Android Keystore deste aparelho. Nunca vai para o projeto, o Git ou o APK do jogo. " +
            "O provedor pode cobrar pelo uso.", LunetUi.TextCaption, LunetUi.TextFaint));

        var providerChips = new List<(LunetAiProvider Provider, TextView Chip)>();
        void SelectProvider(LunetAiProvider provider, bool fillEndpoint)
        {
            selected = provider;
            foreach (var (p, chip) in providerChips)
            {
                chip.Background = LunetUi.Pressable(this, p == provider ? LunetUi.AccentSoft : LunetUi.SurfaceRaised, 99,
                    p == provider ? LunetUi.Accent : LunetUi.Border);
                chip.SetTextColor(p == provider ? LunetUi.AccentText : LunetUi.Text);
            }
            bool custom = provider.Id == "custom";
            endpointLabel.Visibility = endpoint.Visibility = custom ? ViewStates.Visible : ViewStates.Gone;
            if (fillEndpoint && !custom) endpoint.Text = provider.Endpoint;
            getKey.Visibility = provider.KeyUrl.Length > 0 ? ViewStates.Visible : ViewStates.Gone;
            keyLabel.Text = "Chave da API (" + provider.KeyHint + ")";
            suggestionRow.RemoveAllViews();
            foreach (var m in provider.SuggestedModels)
                suggestionRow.AddView(LunetUi.Chip(this, m, () => model.Text = m));
            if (fillEndpoint && provider.SuggestedModels.Count > 0 && string.IsNullOrWhiteSpace(model.Text))
                model.Text = provider.SuggestedModels[0];
        }
        foreach (var provider in LunetAiProviders.All)
        {
            var chip = LunetUi.Chip(this, provider.Name, () => SelectProvider(provider, fillEndpoint: true));
            providerChips.Add((provider, chip));
            providerRow.AddView(chip);
        }
        SelectProvider(selected, fillEndpoint: string.IsNullOrWhiteSpace(current.Settings.Endpoint));
        if (string.IsNullOrWhiteSpace(model.Text) && selected.SuggestedModels.Count > 0) model.Text = selected.SuggestedModels[0];

        var scroll = new ScrollView(this);
        scroll.AddView(form);
        var dialog = new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("IA do Lunet")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) => { })!
            .Create();
        if (current.ApiKey is not null)
            dialog.SetButton((int)DialogButtonType.Neutral, "Esquecer chave", (_, _) =>
            {
                AiStore.ClearKey();
                _ai = null;
                RefreshAiHeader();
            });
        dialog.Show();
        dialog.GetButton((int)DialogButtonType.Positive)!.Click += (_, _) =>
        {
            var endpointText = selected.Id == "custom" ? (endpoint.Text ?? "").Trim() : selected.Endpoint;
            try { LunetAiEndpoint.Validate(endpointText); }
            catch (ArgumentException ex) { endpoint.Error = ex.Message; return; }
            var modelText = (model.Text ?? "").Trim();
            if (modelText.Length == 0) { model.Error = "Escolha um modelo"; return; }
            var keyText = (key.Text ?? "").Trim();
            if (keyText.Length == 0 && current.ApiKey is null) { key.Error = "Cole a chave da API"; return; }
            if (keyText.Length is > 0 and < 12) { key.Error = "Chave curta demais"; return; }
            try
            {
                AiStore.Save(new LunetAiSettings(selected.Id, endpointText, modelText, steps, writes.Checked),
                    keyText.Length > 0 ? keyText : null);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
                return;
            }
            _ai = null;
            dialog.Dismiss();
            RefreshAiHeader();
            afterSave?.Invoke();
        };
    }

    // ---------- Panel ----------

    /// <summary>Entry point kept for the menus and the command palette.</summary>
    private void ShowAiAssistant() => OpenAiPanel(null);

    private void ToggleAiPanel()
    {
        if (_aiPanel is { Visibility: ViewStates.Visible }) CloseAiPanel();
        else OpenAiPanel(null);
    }

    private void OpenAiPanel(string? prefill, bool autoSend = false)
    {
        if (_project is null || _workspaceFrame is null)
        {
            Toast.MakeText(this, "Abra um projeto para usar a IA.", ToastLength.Short)?.Show();
            return;
        }
        _aiPendingSelection = SelectedEditorText();
        EnsureAiSession();
        if (_aiPanel is null || _aiPanel.Parent != _workspaceFrame) BuildAiPanel(_workspaceFrame);
        _aiPanel!.Visibility = ViewStates.Visible;
        RefreshAiHeader();
        RenderAiHistory();
        if (prefill is not null && _aiInput is not null)
        {
            _aiInput.Text = prefill;
            _aiInput.SetSelection(_aiInput.Text?.Length ?? 0);
        }
        if (!Ai.IsConfigured) ShowAiSettings(autoSend ? SendAi : null);
        else if (autoSend && !string.IsNullOrWhiteSpace(prefill)) SendAi();
        else _aiInput?.RequestFocus();
    }

    private void CloseAiPanel()
    {
        if (_aiPanel is null) return;
        _aiPanel.Visibility = ViewStates.Gone;
        (GetSystemService(InputMethodService) as InputMethodManager)?.HideSoftInputFromWindow(_aiPanel.WindowToken, 0);
        _editor?.RequestFocus();
    }

    private bool AiPanelOpen => _aiPanel is { Visibility: ViewStates.Visible };

    private void EnsureAiSession()
    {
        var project = _project!;
        if (_aiSession is not null && _aiSessionProject == project.Manifest.GameId) return;
        var identifier = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(project.Manifest.GameId))).ToLowerInvariant();
        _aiSession = new LunetAiSession(project, System.IO.Path.Combine(FilesDir!.AbsolutePath, "ai", identifier + ".json"));
        _aiSessionProject = project.Manifest.GameId;
    }

    private void BuildAiPanel(FrameLayout host)
    {
        bool side = LunetUi.IsExpanded(this);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical, Clickable = true, Elevation = Dp(12) };
        panel.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.RadiusLarge, LunetUi.Border);
        panel.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));

        // Header: title · model chip · new conversation · close
        var header = LunetUi.Row(this, 6);
        var title = LunetUi.Label(this, "✦ IA", LunetUi.TextTitle, LunetUi.Text, bold: true);
        header.AddView(title);
        _aiModelChip = LunetUi.Chip(this, "", () => ShowAiSettings());
        header.AddView(_aiModelChip, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        header.AddView(LunetUi.Icon(this, "⟲", "Nova conversa", () =>
        {
            _aiSession?.ClearHistory();
            RenderAiHistory();
        }), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        header.AddView(LunetUi.Icon(this, "✕", "Fechar IA", CloseAiPanel), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        panel.AddView(header);

        _aiContextLine = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        _aiContextLine.SetPadding(Dp(2), Dp(2), Dp(2), Dp(6));
        panel.AddView(_aiContextLine);

        _aiLog = LunetUi.Column(this, 10);
        _aiLog.SetPadding(0, Dp(4), 0, Dp(4));
        _aiScroll = new ScrollView(this) { FillViewport = true };
        _aiScroll.AddView(_aiLog);
        panel.AddView(_aiScroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _aiActivity = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.AccentText);
        _aiActivity.Visibility = ViewStates.Gone;
        _aiActivity.SetPadding(Dp(2), Dp(4), Dp(2), Dp(4));
        panel.AddView(_aiActivity);

        var quick = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        var quickRow = LunetUi.Row(this, 6);
        quickRow.SetPadding(0, Dp(4), 0, Dp(6));
        foreach (var (label, prompt) in new[]
        {
            ("Corrigir os erros", "Corrija os erros de compilação do projeto e confirme que compila."),
            ("Explicar este código", "Explique o código selecionado (ou o arquivo aberto) de forma simples."),
            ("Melhorar seleção", "Melhore o trecho selecionado mantendo o comportamento; mostre o que mudou."),
            ("Criar mecânica…", "Implemente no jogo: "),
            ("Revisar o projeto", "Revise o projeto e aponte bugs, riscos e melhorias, sem alterar arquivos."),
        })
            quickRow.AddView(LunetUi.Chip(this, label, () =>
            {
                if (_aiInput is null) return;
                _aiInput.Text = prompt;
                _aiInput.SetSelection(prompt.Length);
                if (!prompt.EndsWith(' ')) SendAi();
                else _aiInput.RequestFocus();
            }));
        quick.AddView(quickRow);
        panel.AddView(quick);

        var composer = LunetUi.Row(this, 8);
        _aiInput = LunetUi.Input(this, "Peça para construir, corrigir ou explicar…", singleLine: false);
        _aiInput.SetMinLines(1);
        _aiInput.SetMaxLines(5);
        _aiInput.InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine | InputTypes.TextFlagCapSentences;
        composer.AddView(_aiInput, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        _aiSend = LunetUi.Icon(this, "↑", "Enviar para a IA", SendAi, accent: true);
        composer.AddView(_aiSend, new LinearLayout.LayoutParams(Dp(48), Dp(48)));
        panel.AddView(composer);

        FrameLayout.LayoutParams layout;
        if (side)
        {
            layout = new FrameLayout.LayoutParams(Dp(420), ViewGroup.LayoutParams.MatchParent, GravityFlags.End);
            layout.SetMargins(Dp(8), Dp(64), Dp(8), Dp(8));
        }
        else
        {
            var height = (int)(Resources!.DisplayMetrics!.HeightPixels * 0.58f);
            layout = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, height, GravityFlags.Bottom);
            layout.SetMargins(Dp(6), 0, Dp(6), Dp(6));
        }
        host.AddView(panel, layout);
        panel.Visibility = ViewStates.Gone;
        _aiPanel = panel;
    }

    private void RefreshAiHeader()
    {
        if (_aiModelChip is null) return;
        var ai = Ai;
        _aiModelChip.Text = ai.IsConfigured
            ? LunetAiProviders.Find(ai.Settings.ProviderId).Name + " · " + ai.Settings.Model + (ai.Settings.AllowWrites ? "" : " · só leitura")
            : "Configurar provedor e chave";
        if (_aiContextLine is not null)
        {
            var context = CurrentAiContext();
            var parts = new List<string>();
            if (context.OpenFile is not null) parts.Add(context.OpenFile + (context.CaretLine > 0 ? ":" + context.CaretLine : ""));
            if (!string.IsNullOrEmpty(context.Selection)) parts.Add("seleção de " + context.Selection.Split('\n').Length + " linha(s)");
            var errors = _problems.Count(p => p.Severity == DiagnosticSeverity.Error);
            if (errors > 0) parts.Add(errors + " erro(s)");
            if (_console.Count > 0) parts.Add("console");
            _aiContextLine.Text = parts.Count == 0 ? "Contexto: projeto inteiro" : "Contexto: " + string.Join(" · ", parts);
        }
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

    private LunetAiEditorContext CurrentAiContext()
    {
        int caretLine = 0;
        if (_editor is not null && _editor.Layout is { } layout && _editor.SelectionStart >= 0)
            caretLine = layout.GetLineForOffset(_editor.SelectionStart) + 1;
        var diagnostics = _problems
            .Where(p => p.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(p => $"{p.FilePath ?? "?"}({p.Line},{p.Column}): {(p.Severity == DiagnosticSeverity.Error ? "error" : "warning")} {p.Id}: {p.Message}")
            .ToArray();
        return new LunetAiEditorContext(_openNote ?? _openFile, _openNote is null ? SelectedEditorText() ?? _aiPendingSelection : null,
            _openNote is null ? caretLine : 0, diagnostics,
            _console.TakeLast(30).ToArray());
    }

    private void SendAi()
    {
        if (_aiRunCancellation is not null) { _aiRunCancellation.Cancel(); return; }
        var request = (_aiInput?.Text ?? "").Trim();
        if (request.Length == 0 || _project is null || _aiSession is null) return;
        var ai = Ai;
        if (!ai.IsConfigured) { ShowAiSettings(SendAi); return; }

        SaveCurrent(); // the agent reads and edits what is on disk
        var context = CurrentAiContext();
        _aiPendingSelection = null;
        _aiInput!.Text = "";
        AddAiBubble("Você", request);
        var thinking = AddAiBubble("Lunet", "Pensando…");
        _aiActivity!.Visibility = ViewStates.Visible;
        _aiActivity.Text = "Lendo o projeto…";
        _aiSend!.Text = "■";
        _aiSend.ContentDescription = "Parar a IA";
        var cts = new CancellationTokenSource();
        _aiRunCancellation = cts;
        var project = _project;
        var session = _aiSession;
        var progress = new Progress<string>(line => RunOnUiThread(() =>
        {
            if (_aiActivity is not null) _aiActivity.Text = HumanizeActivity(line);
        }));
        var build = new AndroidAiBuild(project);
        Task.Run(async () =>
        {
            try
            {
                var reply = await session.AskAsync(request, new LunetAiRequest(ai.Settings, ai.ApiKey!, context, build,
                    Progress: progress), cts.Token);
                RunOnUiThread(() => OnAiReply(project, thinking, reply, build.LastErrors));
            }
            catch (OperationCanceledException)
            {
                RunOnUiThread(() => SetBubbleText(thinking, "Parado. Os arquivos alterados até aqui continuam no projeto; use o histórico do Git ou revise abaixo."));
            }
            catch (Exception ex)
            {
                RunOnUiThread(() => SetBubbleText(thinking, ExplainAiError(ex)));
            }
            finally
            {
                RunOnUiThread(() =>
                {
                    cts.Dispose();
                    if (ReferenceEquals(_aiRunCancellation, cts)) _aiRunCancellation = null;
                    if (_aiSend is not null) { _aiSend.Text = "↑"; _aiSend.ContentDescription = "Enviar para a IA"; }
                    if (_aiActivity is not null) _aiActivity.Visibility = ViewStates.Gone;
                });
            }
        });
    }

    private static string HumanizeActivity(string line) =>
        line.Contains("lunet.build.compile", StringComparison.Ordinal) ? "Compilando o projeto…"
        : line.Contains("lunet.files.write", StringComparison.Ordinal) || line.Contains("lunet.files.replace", StringComparison.Ordinal) ? "Editando arquivos…"
        : line.Contains("lunet.files.search", StringComparison.Ordinal) ? "Procurando no projeto…"
        : line.Contains("lunet.files", StringComparison.Ordinal) ? "Lendo arquivos…"
        : line.Contains("Verification", StringComparison.OrdinalIgnoreCase) ? "Verificando…"
        : line;

    private static string ExplainAiError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("401") || message.Contains("auth", StringComparison.OrdinalIgnoreCase))
            return "O provedor recusou a chave (401). Confira a chave nas configurações da IA.";
        if (message.Contains("404"))
            return "O provedor não encontrou o modelo ou o endpoint (404). Confira o id do modelo.";
        if (message.Contains("429"))
            return "Limite ou saldo do provedor esgotado (429). Tente de novo em instantes ou verifique o saldo.";
        if (ex is HttpRequestException or TaskCanceledException)
            return "Sem conexão com o provedor. Verifique a internet e tente de novo.";
        return "Não foi possível concluir: " + message;
    }

    private void OnAiReply(LunetProject project, TextView bubble, LunetAiReply reply, IReadOnlyList<string> buildErrors)
    {
        SetBubbleText(bubble, reply.Text);
        if (reply.Changes.Count > 0)
        {
            AddChangesCard(project, reply);
            ReloadOpenFileIfChanged(reply.Changes.Select(c => c.Path));
            RefreshExplorer();
        }
        if (buildErrors.Count == 0 && reply.Changes.Count > 0) _problems = [];
        RefreshAiHeader();
        ScrollAiToEnd();
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
            _session.Unload();
            _openFile = null;
            OpenFile(opened);
        }
    }

    // ---------- Transcript ----------

    private void RenderAiHistory()
    {
        if (_aiLog is null || _aiSession is null) return;
        _aiLog.RemoveAllViews();
        if (_aiSession.History.Count == 0)
        {
            var hint = LunetUi.Label(this,
                "Converse com a IA sem sair do código. Ela vê o arquivo aberto, a seleção, os erros e o console, " +
                "compila o que escreve e você pode desfazer qualquer mudança.\n\nDica: selecione um trecho e toque em “Pedir à IA”.",
                LunetUi.TextLabel, LunetUi.TextMuted);
            hint.SetPadding(Dp(6), Dp(12), Dp(6), Dp(12));
            _aiLog.AddView(hint);
        }
        foreach (var turn in _aiSession.History) AddAiBubble(turn.Role, turn.Text);
        ScrollAiToEnd();
    }

    private TextView AddAiBubble(string role, string text)
    {
        bool mine = role == "Você";
        var container = new LinearLayout(this) { Orientation = Orientation.Vertical };
        var bubble = new TextView(this) { TextSize = LunetUi.TextBody };
        bubble.SetTextColor(LunetUi.Text);
        bubble.SetTextIsSelectable(true);
        bubble.SetLineSpacing(0, 1.2f);
        bubble.SetPadding(Dp(12), Dp(9), Dp(12), Dp(9));
        bubble.Background = LunetUi.Shape(this, mine ? LunetUi.AccentSoft : LunetUi.SurfaceRaised, 14);
        SetBubbleText(bubble, text);
        var layout = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        { Gravity = mine ? GravityFlags.End : GravityFlags.Start };
        if (mine) layout.LeftMargin = Dp(36); else layout.RightMargin = Dp(12);
        container.AddView(bubble, layout);
        if (!mine && text.Contains("```", StringComparison.Ordinal))
        {
            var actions = LunetUi.Row(this, 6);
            actions.SetPadding(0, Dp(4), 0, 0);
            actions.AddView(LunetUi.Chip(this, "Copiar código", () => CopyToClipboard(FirstCodeBlock(text))));
            actions.AddView(LunetUi.Chip(this, "Inserir no cursor", () => InsertAtCaret(FirstCodeBlock(text))));
            container.AddView(actions);
        }
        _aiLog?.AddView(container);
        ScrollAiToEnd();
        return bubble;
    }

    /// <summary>Markdown-lite: fenced code in monospace on a darker block, **bold** and `inline` code.</summary>
    private void SetBubbleText(TextView bubble, string text)
    {
        var builder = new SpannableStringBuilder();
        var parts = text.Split("```");
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            int start = builder.Length();
            if (i % 2 == 1)
            {
                var newline = part.IndexOf('\n');
                var code = (newline >= 0 && newline < 20 ? part[(newline + 1)..] : part).TrimEnd('\n');
                builder.Append(code);
                int end = builder.Length();
                builder.SetSpan(new TypefaceSpan("monospace"), start, end, SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new BackgroundColorSpan(LunetUi.Background), start, end, SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new ForegroundColorSpan(LunetUi.AccentText), start, end, SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new RelativeSizeSpan(0.92f), start, end, SpanTypes.ExclusiveExclusive);
                builder.Append("\n");
            }
            else AppendInline(builder, part);
        }
        bubble.SetText(builder, TextView.BufferType.Spannable);
    }

    private static void AppendInline(SpannableStringBuilder builder, string text)
    {
        int index = 0;
        while (index < text.Length)
        {
            int bold = text.IndexOf("**", index, StringComparison.Ordinal);
            int code = text.IndexOf('`', index);
            int next = bold < 0 ? code : code < 0 ? bold : Math.Min(bold, code);
            if (next < 0) { builder.Append(text[index..]); break; }
            builder.Append(text[index..next]);
            bool isBold = next == bold;
            int close = isBold ? text.IndexOf("**", next + 2, StringComparison.Ordinal) : text.IndexOf('`', next + 1);
            if (close < 0) { builder.Append(text[next..]); break; }
            var inner = text[(next + (isBold ? 2 : 1))..close];
            int s = builder.Length();
            builder.Append(inner);
            if (isBold) builder.SetSpan(new StyleSpan(TypefaceStyle.Bold), s, builder.Length(), SpanTypes.ExclusiveExclusive);
            else
            {
                builder.SetSpan(new TypefaceSpan("monospace"), s, builder.Length(), SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new ForegroundColorSpan(LunetUi.AccentText), s, builder.Length(), SpanTypes.ExclusiveExclusive);
            }
            index = close + (isBold ? 2 : 1);
        }
    }

    private static string FirstCodeBlock(string text)
    {
        var parts = text.Split("```");
        if (parts.Length < 2) return text;
        var block = parts[1];
        var newline = block.IndexOf('\n');
        return (newline >= 0 && newline < 20 ? block[(newline + 1)..] : block).TrimEnd('\n');
    }

    private void CopyToClipboard(string text)
    {
        (GetSystemService(ClipboardService) as global::Android.Content.ClipboardManager)!.PrimaryClip = ClipData.NewPlainText("Lunet", text);
        Toast.MakeText(this, "Código copiado", ToastLength.Short)?.Show();
    }

    private void InsertAtCaret(string text)
    {
        if (_editor is null) return;
        int start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
        int end = Math.Max(_editor.SelectionStart, _editor.SelectionEnd);
        _editor.Replace(Math.Max(0, start), Math.Max(0, end - start), text);
        Toast.MakeText(this, "Inserido no editor (desfazer com ↶)", ToastLength.Short)?.Show();
    }

    private void ScrollAiToEnd() => _aiScroll?.Post(() => _aiScroll?.FullScroll(FocusSearchDirection.Down));

    // ---------- Review ----------

    private void AddChangesCard(LunetProject project, LunetAiReply reply)
    {
        var card = LunetUi.Column(this, 6);
        card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        card.Background = LunetUi.Shape(this, LunetUi.Background, 14, LunetUi.Border);
        var verified = reply.Status == AgentRuntime.RunStatus.Succeeded;
        card.AddView(LunetUi.Label(this, (verified ? "✓ Compilado · " : "⚠ Não verificado · ") +
            reply.Changes.Count + " arquivo(s) alterado(s)", LunetUi.TextLabel, verified ? LunetUi.Success : LunetUi.Warning, bold: true));
        foreach (var change in reply.Changes)
        {
            var (added, removed) = LunetAiDiff.Count(change.Diff());
            var row = LunetUi.Row(this, 6);
            var name = LunetUi.Label(this, (change.Before is null ? "＋ " : "") + change.Path, LunetUi.TextLabel);
            name.SetSingleLine(true);
            name.Ellipsize = TextUtils.TruncateAt.Middle;
            row.AddView(name, LunetUi.Weight());
            row.AddView(LunetUi.Label(this, $"+{added}", LunetUi.TextCaption, LunetUi.Success));
            row.AddView(LunetUi.Label(this, $"−{removed}", LunetUi.TextCaption, LunetUi.Danger));
            row.AddView(LunetUi.Chip(this, "Ver", () => ShowDiff(change)));
            card.AddView(row);
        }
        var actions = LunetUi.Row(this, 8);
        var keep = LunetUi.Button(this, "Manter", () => { }, ButtonStyle.Selected);
        var revert = LunetUi.Button(this, "Desfazer tudo", () => { }, ButtonStyle.Danger);
        keep.Click += (_, _) =>
        {
            keep.Text = "Mantido ✓"; keep.Enabled = false; revert.Visibility = ViewStates.Gone;
        };
        revert.Click += (_, _) =>
        {
            try
            {
                reply.Checkpoint.Revert();
                ReloadOpenFileIfChanged(reply.Changes.Select(c => c.Path));
                RefreshExplorer();
                revert.Text = "Desfeito ✓"; revert.Enabled = false; keep.Visibility = ViewStates.Gone;
            }
            catch (Exception ex) { Toast.MakeText(this, "Não foi possível desfazer: " + ex.Message, ToastLength.Long)?.Show(); }
        };
        actions.AddView(keep);
        actions.AddView(revert);
        card.AddView(actions);
        _aiLog?.AddView(card, LunetUi.FillWidth());
    }

    private void ShowDiff(LunetAiChange change)
    {
        var builder = new SpannableStringBuilder();
        var diff = change.Diff();
        // Show changed lines with 3 lines of context, like a review hunk.
        var keep = new bool[diff.Count];
        for (int i = 0; i < diff.Count; i++)
            if (diff[i].Kind != DiffKind.Same)
                for (int j = Math.Max(0, i - 3); j <= Math.Min(diff.Count - 1, i + 3); j++) keep[j] = true;
        bool gap = false;
        for (int i = 0; i < diff.Count; i++)
        {
            if (!keep[i]) { if (!gap) { builder.Append("  ⋯\n"); gap = true; } continue; }
            gap = false;
            var line = diff[i];
            int start = builder.Length();
            builder.Append(line.Kind switch { DiffKind.Added => "+ ", DiffKind.Removed => "− ", _ => "  " } + line.Text + "\n");
            if (line.Kind != DiffKind.Same)
            {
                builder.SetSpan(new BackgroundColorSpan(line.Kind == DiffKind.Added ? LunetUi.SuccessSoft : LunetUi.DangerSoft),
                    start, builder.Length(), SpanTypes.ExclusiveExclusive);
                builder.SetSpan(new ForegroundColorSpan(line.Kind == DiffKind.Added ? LunetUi.Success : LunetUi.Danger),
                    start, start + 2, SpanTypes.ExclusiveExclusive);
            }
        }
        var view = new TextView(this) { TextSize = 12 };
        view.SetTypeface(LunetUi.Mono, TypefaceStyle.Normal);
        view.SetTextColor(LunetUi.Text);
        view.SetTextIsSelectable(true);
        view.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        view.SetText(builder, TextView.BufferType.Spannable);
        var horizontal = new HorizontalScrollView(this);
        horizontal.AddView(view);
        var scroll = new ScrollView(this);
        scroll.SetBackgroundColor(LunetUi.Background);
        scroll.AddView(horizontal);
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle(change.Path)!
            .SetView(scroll)!
            .SetPositiveButton("Fechar", (_, _) => { })!
            .SetNeutralButton("Abrir arquivo", (_, _) => { CloseAiPanel(); OpenFile(change.Path); })!
            .Show();
    }

    // ---------- Build service for the agent ----------

    /// <summary>The agent compiles with the same compiler and sources as the Run button.</summary>
    private sealed class AndroidAiBuild(LunetProject project) : ILunetAiBuild
    {
        public IReadOnlyList<string> LastErrors { get; private set; } = [];

        public Task<LunetAiBuildReport> CompileAsync(CancellationToken cancellationToken) => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sources = project.LoadSources().Select(s => new SourceFile(s.Path, s.Text)).ToList();
            var result = SharedCompiler.Value.Compile("LunetAiCheck" + Guid.NewGuid().ToString("N")[..6], sources);
            var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{d.FilePath ?? "?"}({d.Line},{d.Column}): error {d.Id}: {d.Message}").ToArray();
            LastErrors = errors;
            return new LunetAiBuildReport(result.Success, errors);
        }, cancellationToken);
    }

    // ---------- "Pedir à IA" on a text selection ----------

    private sealed class AiSelectionCallback(MainActivity owner) : Java.Lang.Object, ActionMode.ICallback
    {
        private const int AskId = 0x4c41; // "LA"

        public bool OnCreateActionMode(ActionMode? mode, IMenu? menu)
        {
            menu?.Add(0, AskId, 0, "✦ Pedir à IA")?.SetShowAsAction(ShowAsAction.Always);
            return true;
        }

        public bool OnPrepareActionMode(ActionMode? mode, IMenu? menu) => false;

        public bool OnActionItemClicked(ActionMode? mode, IMenuItem? item)
        {
            if (item?.ItemId != AskId) return false;
            owner.OpenAiPanel("");
            mode?.Finish();
            return true;
        }

        public void OnDestroyActionMode(ActionMode? mode) { }
    }
}
