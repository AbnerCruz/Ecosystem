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

    private View BuildChangesCard(LunetAiReply reply)
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
