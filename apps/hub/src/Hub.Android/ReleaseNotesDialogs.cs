using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Method;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Hub.Core;
using System.Globalization;

namespace HubApp;

/// <summary>Widgets nativos: nunca WebView/HTML executável; dados e estados vêm das projeções do Core.</summary>
sealed class ReleaseNotesDialogs(Activity activity)
{
    AlertDialog? _historyDialog;
    AlertDialog? _detailsDialog;
    LinearLayout? _historyContent;
    ProductReleaseHistory? _history;
    int _visible = 10;

    public void ShowHistory(ProductReleaseHistory history)
    {
        Dismiss();
        _history = history; _visible = 10;
        _historyContent = Column();
        var scroll = new ScrollView(activity); scroll.AddView(_historyContent);
        _historyDialog = new AlertDialog.Builder(activity)
            .SetTitle(history.ProductName + " · Histórico de versões")
            .SetView(scroll).SetNegativeButton("Voltar", (_, _) => { }).Create();
        RenderHistory();
        _historyDialog!.Show();
        _historyDialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }

    public void Refresh(HubSnapshot? snapshot, bool loading)
    {
        if (_historyDialog?.IsShowing != true || _history is null) return;
        _history = ReleaseHistoryBuilder.Build(snapshot, _history.ProductId, _history.InstalledVersion, loading);
        RenderHistory();
    }

    void RenderHistory()
    {
        if (_historyContent is not { } content || _history is not { } history) return;
        content.RemoveAllViews();
        Text(content, $"Versão no SOURCE: {history.SourceVersion.Value ?? "indisponível"}"
            + (history.SourceVersion.Availability == Availability.Stale ? " (último estado)" : ""));
        Text(content, $"Instalada: {history.InstalledVersion.Value ?? "não conhecida — ainda não consultada"}"
            + (history.InstalledVersion.Availability == Availability.Stale ? " (último estado)" : ""));
        Text(content, history.Releases.Count == 0 ? "Mais recente publicada: indisponível"
            : $"Mais recente publicada: {history.Releases[0].Version}" + (history.Releases[0].Release.Prerelease ? " (pré-lançamento)" : ""));
        if (history.Refreshing) Text(content, "Atualizando releases…");
        Text(content, history.State switch
        {
            ReleaseHistoryState.Loading => "Carregando histórico…",
            ReleaseHistoryState.Empty => "Nenhuma release publicada neste canal entre as consultadas.",
            ReleaseHistoryState.Unavailable => "Histórico indisponível: falha na consulta ou canal não declarado; sem cache confiável.",
            ReleaseHistoryState.Stale => "Último histórico conhecido (cache); a fonte não foi confirmada nesta leitura.",
            _ => $"{history.Releases.Count} release(s) consultada(s), até {ReleaseHistoryBuilder.MaxReleases}. Mais recentes por data; versão desempata.",
        });
        if (history.Note is { } note) Text(content, note);
        if (history.Changes.Comparable)
            Button(content, "O que mudou desde " + history.Changes.InstalledVersion, () => ShowAggregate(history));
        else Text(content, history.Changes.Note);
        foreach (var entry in history.Releases.Take(_visible))
            Button(content, entry.Version + (entry.Release.Prerelease ? " · pré-lançamento" : "") + "\n" + Date(entry.Release.PublishedAt),
                () => ShowEntry(history.ProductName, entry, history.State == ReleaseHistoryState.Stale));
        if (history.Releases.Count > _visible)
            Button(content, "Mostrar mais 10 versões", () => { _visible += 10; RenderHistory(); });
        Text(content, "Fonte: " + history.Source);
    }

    void ShowEntry(string productName, ReleaseHistoryEntry entry, bool stale)
    {
        _detailsDialog?.Dismiss();
        var content = Column();
        Text(content, $"{productName} {entry.Version}", 20);
        if (stale) Text(content, "Notas do último estado conhecido (cache), sem confirmação atual da fonte.");
        Text(content, Date(entry.Release.PublishedAt) + (entry.Release.Prerelease ? " · pré-lançamento" : ""));
        if (!string.IsNullOrWhiteSpace(entry.Release.Name)) Text(content, entry.Release.Name!);
        Notes(content, entry.Release.BodyMarkdown);
        var technical = Column();
        Text(technical, "Tag da fonte: " + entry.Release.Tag);
        Text(technical, "Origem: " + entry.Source);
        foreach (var asset in entry.Release.Artifacts ?? [])
        {
            Text(technical, asset.Name + $" · {asset.Size:N0} bytes");
            if (asset.Sha256 is { } sha) Text(technical, "SHA-256 informado pela fonte: " + sha);
        }
        Expand(content, "Detalhes da fonte e integridade", technical);
        if (entry.Release.Artifacts is { Count: > 0 } assets)
        {
            Text(content, "Artefatos publicados", 17);
            foreach (var asset in assets) Link(content, asset.Name, asset.Url);
        }
        else Text(content, entry.Release.Artifacts is null ? "Metadados de artefatos indisponíveis." : "Nenhum artefato publicado nesta release.");
        Link(content, "Ver release original", entry.Release.Url);
        _detailsDialog = Show(content, "Ver mudanças");
    }

    void ShowAggregate(ProductReleaseHistory history)
    {
        _detailsDialog?.Dismiss();
        var content = Column(); Text(content, history.Changes.Note);
        if (history.State == ReleaseHistoryState.Stale) Text(content, "Agregação do último histórico conhecido (cache).");
        if (history.Changes.Releases.Count == 0) Text(content, "Nenhuma release comparável posterior entre as consultadas.");
        foreach (var entry in history.Changes.Releases)
        {
            Text(content, entry.Version + " · " + Date(entry.Release.PublishedAt), 19);
            Notes(content, entry.Release.BodyMarkdown);
            Link(content, "Ver release original", entry.Release.Url);
        }
        _detailsDialog = Show(content, "O que mudou desde " + history.Changes.InstalledVersion);
    }

    void Notes(LinearLayout content, string? markdown)
    {
        var parsed = ReleaseNotesParser.Parse(markdown);
        if (string.IsNullOrWhiteSpace(parsed.Markdown)) { Text(content, "Notas de mudanças não disponíveis nesta leitura da release."); return; }
        if (parsed.Sections.Count == 0) { Markdown(content, parsed.Markdown); return; }
        foreach (var section in parsed.Sections)
        {
            var target = section.Kind == ReleaseNoteKind.Technical ? Column() : content;
            if (!string.IsNullOrEmpty(section.Heading)) Text(target, section.Heading, 18);
            Markdown(target, section.Markdown);
            if (section.Kind == ReleaseNoteKind.Technical) Expand(content, "Detalhes técnicos", target);
        }
    }

    void Markdown(LinearLayout content, string markdown)
    {
        foreach (var block in MarkdownPresentation.Build(markdown))
        {
            var spans = new SpannableStringBuilder();
            foreach (var run in block.Runs)
            {
                int start = spans.Length(); spans.Append(run.Text); int end = spans.Length();
                if (run.Bold) spans.SetSpan(new StyleSpan(TypefaceStyle.Bold), start, end, SpanTypes.ExclusiveExclusive);
                if (run.Italic) spans.SetSpan(new StyleSpan(TypefaceStyle.Italic), start, end, SpanTypes.ExclusiveExclusive);
                if (run.Code) spans.SetSpan(new TypefaceSpan("monospace"), start, end, SpanTypes.ExclusiveExclusive);
                if (run.Url is { } url) spans.SetSpan(new URLSpan(url), start, end, SpanTypes.ExclusiveExclusive);
            }
            var text = new TextView(activity) { TextSize = block.Style == "heading" ? Math.Max(17, 25 - block.Level * 2) : 15 };
            text.SetText(spans, TextView.BufferType.Spannable);
            text.SetTextIsSelectable(true);
            text.MovementMethod = LinkMovementMethod.Instance;
            text.SetPadding(ScreenRenderer.Dp(activity, Math.Max(0, block.Level - 1) * 10), 5, 0, 7);
            if (block.Style is "code" or "quote") text.SetBackgroundColor(Color.Argb(24, 128, 128, 128));
            if (block.Style == "heading") text.SetTypeface(null, TypefaceStyle.Bold);
            content.AddView(text);
        }
    }

    void Expand(LinearLayout parent, string label, LinearLayout details)
    {
        details.Visibility = ViewStates.Gone;
        Button(parent, label + " · expandir", () => details.Visibility = details.Visibility == ViewStates.Gone ? ViewStates.Visible : ViewStates.Gone);
        parent.AddView(details);
    }
    void Link(LinearLayout parent, string label, string? url)
    {
        if (MarkdownPresentation.SafeLink(url) is not { } safe) return;
        Button(parent, label, () =>
        {
            try { activity.StartActivity(new Intent(Intent.ActionView, Android.Net.Uri.Parse(safe))); }
            catch (ActivityNotFoundException) { Toast.MakeText(activity, "Nenhum aplicativo disponível para abrir o link.", ToastLength.Short)?.Show(); }
        });
    }
    void Button(LinearLayout parent, string label, Action click)
    { var button = new Button(activity) { Text = label }; button.Click += (_, _) => click(); parent.AddView(button); }
    void Text(LinearLayout parent, string text, float size = 15)
    { var view = new TextView(activity) { Text = text, TextSize = size }; view.SetTextIsSelectable(true); parent.AddView(view); }
    LinearLayout Column()
    { var column = new LinearLayout(activity) { Orientation = Orientation.Vertical }; int padding = ScreenRenderer.Dp(activity, 14); column.SetPadding(padding, padding, padding, padding); return column; }
    AlertDialog Show(LinearLayout content, string title)
    {
        var scroll = new ScrollView(activity); scroll.AddView(content);
        var dialog = new AlertDialog.Builder(activity).SetTitle(title).SetView(scroll).SetNegativeButton("Voltar", (_, _) => { }).Create()!;
        dialog.Show(); dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent); return dialog;
    }
    static string Date(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
        ? date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "Data não informada pela release";
    public void Dismiss() { _detailsDialog?.Dismiss(); _historyDialog?.Dismiss(); _detailsDialog = null; _historyDialog = null; }
}
