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


    private LunetAiSettingsStore AiStore => _aiStore ??= new LunetAiSettingsStore(
        System.IO.Path.Combine(FilesDir!.AbsolutePath, "ai", "settings.json"),
        new AndroidKeystoreVault(System.IO.Path.Combine(NoBackupFilesDir!.AbsolutePath, "secure")));

    private LunetAiLoadedSettings Ai => _ai ??= AiStore.Load();

    private LunetAiProjectModels ProjectModels => new(
        System.IO.Path.Combine(FilesDir!.AbsolutePath, "ai", "project-models"));

    private LunetAiLoadedSettings ProjectAi
    {
        get
        {
            var global = Ai;
            var model = _project is { } project ? ProjectModels.Get(project.Manifest.GameId, global.Settings) : null;
            return string.IsNullOrWhiteSpace(model) ? global : global with { Settings = global.Settings with { Model = model } };
        }
    }

    private static readonly LunetAiModelCatalog ModelCatalog = new(new HttpClient(
        new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) });

    /// <summary>Dentro do projeto, alterar somente o modelo.</summary>
    private void ShowAiModelPicker()
    {
        var ai = Ai;
        var project = _project;
        if (!ai.IsConfigured || project is null)
        {
            Toast.MakeText(this, "Configure a IA em Início → Lunet IA → ⚙.", ToastLength.Long)?.Show();
            return;
        }

        ShowModelCatalog(LunetAiProviders.Find(ai.Settings.ProviderId), ai.Settings.Endpoint, ai.ApiKey,
            ProjectAi.Settings.Model, model =>
            {
                if (!ReferenceEquals(project, _project)) return;
                try
                {
                    ProjectModels.Set(project.Manifest.GameId, ai.Settings, model);
                    RefreshAiHeader();
                    UpdateAiCompact();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    Toast.MakeText(this, "Não foi possível trocar o modelo: " + ex.Message, ToastLength.Long)?.Show();
                }
            }, ai.Settings.Model);
    }

    private void ShowModelCatalog(LunetAiProvider provider, string endpoint, string? key, string selected,
        Action<string?> apply, string? globalDefault = null)
    {
        var form = LunetUi.Column(this, 8);
        form.SetPadding(Dp(14), Dp(8), Dp(14), Dp(8));
        var search = LunetUi.Input(this, "Buscar por nome, ID ou alias");
        var manual = LunetUi.Input(this, "ID manual", selected);
        manual.SetSingleLine(true);
        form.AddView(search);
        form.AddView(manual);
        var status = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        var list = LunetUi.Column(this, 6);
        var scroll = new ScrollView(this);
        scroll.AddView(list);
        var lifetime = new CancellationTokenSource();
        IReadOnlyList<LunetAiModelInfo> available = [];
        AlertDialog? dialog = null;
        void Pick(string? id)
        {
            apply(id);
            dialog?.Dismiss();
        }

        void Render()
        {
            list.RemoveAllViews();
            var query = search.Text?.Trim() ?? "";
            var matches = available.Where(m => m.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Alias?.Contains(query, StringComparison.OrdinalIgnoreCase) == true).ToArray();
            var shownIds = matches.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var m in matches.Take(50))
            {
                var capabilities = m.Tools switch { true => "com ferramentas", false => "sem ferramentas", _ => "capacidade desconhecida" };
                AddHomeAction(list, m.Id, "Provedor · " + capabilities +
                    (m.Alias is null ? "" : " · " + m.Alias), () => Pick(m.Id));
            }
            if (matches.Length > 50)
                list.AddView(LunetUi.Label(this, $"Exibindo 50 de {matches.Length}. Use a busca para filtrar."));
            foreach (var id in provider.SuggestedModels.Where(id => !shownIds.Contains(id) && id.Contains(query, StringComparison.OrdinalIgnoreCase)))
                AddHomeAction(list, id, "Sugestão · disponibilidade e ferramentas não confirmadas", () => Pick(id));
        }

        async void Load(bool refresh)
        {
            if (string.IsNullOrWhiteSpace(key)) { status.Text = "Adicione uma chave para consultar o provedor."; return; }
            status.Text = "Consultando modelos…";
            try
            {
                var models = await ModelCatalog.ListAsync(provider.Id, endpoint, key, lifetime.Token, refresh);
                if (lifetime.IsCancellationRequested) return;
                available = models;
                status.Text = models.Count == 0 ? "O provedor devolveu uma lista vazia." :
                    $"{models.Count} modelos do provedor." +
                    (selected.Length > 0 && !models.Any(m => m.Id == selected) ? " O ID selecionado não consta na lista." : "");
                Render();
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ArgumentException)
            {
                if (!lifetime.IsCancellationRequested)
                    status.Text = ex is OperationCanceledException ? "Tempo esgotado. O ID manual continua disponível." :
                        "Catálogo indisponível. " + ex.Message;
            }
        }

        var actions = LunetUi.Row(this, 6);
        actions.AddView(LunetUi.Button(this, "Atualizar", () => Load(true)));
        actions.AddView(LunetUi.Button(this, "Usar ID", () =>
        {
            var id = manual.Text?.Trim() ?? "";
            if (id.Length is 0 or > 240 || id.Any(char.IsControl)) { manual.Error = "ID inválido"; return; }
            Pick(id);
        }, ButtonStyle.Primary));
        form.AddView(actions);
        if (globalDefault is not null)
            form.AddView(LunetUi.Button(this, "Usar padrão · " + globalDefault, () => Pick(null), ButtonStyle.Quiet));
        form.AddView(status);
        form.AddView(scroll, LunetUi.FillWidth(Dp(240)));
        search.TextChanged += (_, _) => Render();
        dialog = new AlertDialog.Builder(this)!.SetTitle("Modelos · " + provider.Name)!.SetView(form)!
            .SetNegativeButton("Fechar", (_, _) => { })!.Create()!;
        dialog.DismissEvent += (_, _) => lifetime.Cancel();
        dialog.Show();
        Render();
        Load(false);
    }

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
        var key = LunetUi.Input(this, "Cole a chave da API");
        key.InputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
        form.AddView(key);
        string? SavedKey() => AiStore.KeyFor(selected.Id);

        // Models the provider really serves + a connection check (LUNET-498).
        var checkRow = LunetUi.Row(this, 6);
        var status = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        var catalog = ModelCatalog;
        string CurrentEndpoint() => selected.Id == "custom" ? (endpoint.Text ?? "").Trim() : selected.Endpoint;
        string? KeyForCheck() => (key.Text ?? "").Trim() is { Length: > 0 } typed ? typed : SavedKey();
        checkRow.AddView(LunetUi.Button(this, "Ver modelos do provedor", () =>
        {
            ShowModelCatalog(selected, CurrentEndpoint(), KeyForCheck(), model.Text ?? "", id => model.Text = id ?? "");
        }, ButtonStyle.Secondary));
        checkRow.AddView(LunetUi.Button(this, "Testar conexão", async () =>
        {
            if (KeyForCheck() is not { } k) { key.Error = "Cole a chave para testar"; return; }
            var modelText = (model.Text ?? "").Trim();
            if (modelText.Length == 0) { model.Error = "Escolha um modelo"; return; }
            status.Text = "Testando…";
            try
            {
                var check = await catalog.TestAsync(selected.Id, CurrentEndpoint(), k, modelText, CancellationToken.None);
                status.Text = (check.Ok ? "✓ " : "✕ ") + check.Message;
                status.SetTextColor(check.Ok ? LunetUi.Success : LunetUi.Danger);
            }
            catch (ArgumentException ex) { status.Text = "✕ " + ex.Message; status.SetTextColor(LunetUi.Danger); }
        }, ButtonStyle.Secondary));
        var checkScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        checkScroll.AddView(checkRow);
        form.AddView(checkScroll);
        form.AddView(status);
        var getKey = LunetUi.Button(this, "Criar chave no site do provedor ↗", () =>
        {
            if (selected.KeyUrl.Length > 0) StartActivity(new Intent(Intent.ActionView, AndroidUri.Parse(selected.KeyUrl)));
        }, ButtonStyle.Quiet);
        form.AddView(getKey);

        var writes = new Switch(this) { Text = "A IA pode criar e alterar arquivos (com checkpoint para desfazer)", Checked = current.Settings.AllowWrites };
        writes.SetTextColor(LunetUi.Text);
        form.AddView(writes);
        var web = new Switch(this) { Text = "Pesquisar código de referência na internet", Checked = current.Settings.WebResearch };
        web.SetTextColor(LunetUi.Text);
        form.AddView(web);

        form.AddView(LunetUi.Label(this, "Modo inicial do chat", LunetUi.TextLabel, LunetUi.TextMuted));
        const LunetAiMode defaultMode = LunetAiMode.Teach;
        form.AddView(LunetUi.Label(this, "O chat começa em Estudo. Para editar o jogo, selecione Fazer antes de enviar.",
            LunetUi.TextCaption, LunetUi.TextFaint));

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
            if (selected.Id != provider.Id)
            {
                key.Text = "";
                model.Text = provider.Id == current.Settings.ProviderId ? current.Settings.Model : "";
                endpoint.Text = provider.Id == current.Settings.ProviderId ? current.Settings.Endpoint : provider.Endpoint;
            }
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
            bool saved = AiStore.KeyFor(provider.Id) is not null;
            keyLabel.Text = "Chave da API (" + provider.KeyHint + ")" + (saved ? " · ✓ salva neste aparelho" : "");
            key.Hint = saved ? "•••• chave salva (deixe vazio para manter)" : "Cole a chave da API";
            status.Text = "";
            suggestionRow.RemoveAllViews();
            foreach (var m in provider.SuggestedModels)
                suggestionRow.AddView(LunetUi.Chip(this, m, () => model.Text = m));
        }
        foreach (var provider in LunetAiProviders.All)
        {
            var chip = LunetUi.Chip(this, provider.Name, () => SelectProvider(provider, fillEndpoint: true));
            providerChips.Add((provider, chip));
            providerRow.AddView(chip);
        }
        SelectProvider(selected, fillEndpoint: string.IsNullOrWhiteSpace(current.Settings.Endpoint));

        var scroll = new ScrollView(this);
        scroll.AddView(form);
        var dialog = new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("IA do Lunet")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) => { })!
            .Create();
        if (SavedKey() is not null)
            dialog.SetButton((int)DialogButtonType.Neutral, "Esquecer chave", (_, _) =>
            {
                AiStore.ClearKey(selected.Id);
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
            if (keyText.Length == 0 && SavedKey() is null) { key.Error = "Cole a chave da API"; return; }
            if (keyText.Length is > 0 and < 12) { key.Error = "Chave curta demais"; return; }
            try
            {
                AiStore.Save(new LunetAiSettings(selected.Id, endpointText, modelText, steps, writes.Checked, web.Checked, defaultMode),
                    keyText.Length > 0 ? keyText : null);
            }
            catch (Exception ex)
            {
                Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
                return;
            }
            _ai = null;
            _aiMode = defaultMode;
            PaintAiModes();
            dialog.Dismiss();
            RefreshAiHeader();
            afterSave?.Invoke();
        };
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

    // ---------- Review ----------

    /// <summary>
    /// What one AI run changed, with "Desfazer tudo". Built from the persisted checkpoint, so it is there for every
    /// answer, also after a cancel, a failure, a screen change or an app restart (LUNET-507).
    /// </summary>
    private View BuildChangesCard(LunetAiCheckpoint checkpoint, bool? verifiedRun)
    {
        var card = LunetUi.Column(this, 6);
        card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        card.Background = LunetUi.Shape(this, LunetUi.Background, 14, LunetUi.Border);
        if (checkpoint.IsReverted)
        {
            card.AddView(LunetUi.Label(this, "↶ Desfeito · " + checkpoint.RevertedPaths.Count +
                " arquivo(s) voltaram ao estado de antes deste pedido", LunetUi.TextLabel, LunetUi.TextMuted, bold: true));
            return card;
        }
        var changes = checkpoint.Changes();
        var verified = verifiedRun == true;
        card.AddView(LunetUi.Label(this, (verified ? "✓ Compilado · " : "⚠ Não verificado · ") +
            changes.Count + " arquivo(s) alterado(s)", LunetUi.TextLabel, verified ? LunetUi.Success : LunetUi.Warning, bold: true));
        foreach (var change in changes)
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
            if (change.HasLaterEdits)
                card.AddView(LunetUi.Label(this, "A versão atual diverge da revisão da IA. O desfazer pode ser bloqueado.",
                    LunetUi.TextCaption, LunetUi.Warning));
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
            if (LunetAiRuns.Current is { IsRunning: true } running && running.ProjectId == _project?.Manifest.GameId)
            {
                Toast.MakeText(this, "Cancele a tarefa da IA em andamento antes de desfazer.", ToastLength.Long)?.Show();
                return;
            }
            new AlertDialog.Builder(this)!
                .SetTitle("Desfazer as alterações da IA?")!
                .SetMessage($"{changes.Count} arquivo(s) voltarão ao estado anterior ao pedido, " +
                            "mas somente se nenhum deles tiver sido modificado depois pela pessoa ou por outra tarefa. " +
                            "Se houver conflito, nenhum arquivo será alterado.")!
                .SetPositiveButton("Desfazer tudo", (_, _) =>
                {
                    try
                    {
                        SaveCurrent();
                        var paths = changes.Select(c => c.Path).ToArray();
                        checkpoint.Revert();
                        ReloadOpenFileIfChanged(paths);
                        RefreshExplorer();
                        revert.Text = "Desfeito ✓"; revert.Enabled = false; keep.Visibility = ViewStates.Gone;
                    }
                    catch (Exception ex) { Toast.MakeText(this, "Não foi possível desfazer: " + ex.Message, ToastLength.Long)?.Show(); }
                })!
                .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
        };
        actions.AddView(keep);
        actions.AddView(revert);
        card.AddView(actions);
        return card;
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
            .SetNeutralButton("Abrir arquivo", (_, _) => { CloseAiPanel(); OpenProjectEntry(change.Path); })!
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
            var contentErrors = LunetAiAssetValidation.Validate(project);
            LastErrors = errors.Concat(contentErrors).ToArray();
            return new LunetAiBuildReport(result.Success && LastErrors.Count == 0, LastErrors);
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
