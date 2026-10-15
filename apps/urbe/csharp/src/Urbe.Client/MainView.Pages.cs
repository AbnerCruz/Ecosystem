using System.Text.Json.Nodes;
using Avalonia.Input.Platform;
using Urbe.Core;
using Urbe.Core.Pages;

namespace Urbe.Client;

/// <summary>
/// Páginas (pages.home, studio.js): the vault's .page.json files listed in their own panel and each shown inside the
/// Urbe as the 1.8.4's page engine writes it (PageEngine, ported and proved byte for byte); "⋯" has Ver página, Abrir
/// como texto (JSON) and Mover para a lixeira. A card opens the page in the studio (MainView.Studio.cs).
/// </summary>
public sealed partial class MainView
{
    private PagesPanel? _pagesPanel;
    private readonly PageEngine _pageEngine = new() { MathHtml = PageMath };

    /// <summary>UrbeMath in the pages (KaTeX in the 1.8.4): the formula drawn as SVG by the native renderer; when it cannot be drawn, its TeX as written.</summary>
    public static string PageMath(string tex, bool display)
    {
        var r = MathRenderer.RenderSvg(tex, display, 18);
        // black is the formula's default: it takes the page's text color, and the size follows the text (em)
        if (r.Success && r.Svg.Length > 0)
            return System.Text.RegularExpressions.Regex.Replace(r.Svg.Replace("'#000000'", "'currentColor'"), "width='([0-9.]+)' height='([0-9.]+)'",
                m => "style='vertical-align:middle;width:" + (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 18).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                     "em;height:" + (double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) / 18).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "em'", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
        return "<code class=\"umath-error\" title=\"" + System.Net.WebUtility.HtmlEncode(r.Diagnostic ?? "") + "\">" + System.Net.WebUtility.HtmlEncode(tex) + "</code>";
    }

    /// <summary>The Páginas panel, made the first time it opens (under the file preview, which shows a page).</summary>
    public PagesPanel Pages => _pagesPanel ??= MakePagesPanel();

    private PagesPanel MakePagesPanel()
    {
        var panel = new PagesPanel();
        panel.PageRequested += path => _ = OpenStudioAsync(path);
        panel.NewRequested += () => _ = NewPageAsync();
        panel.MenuRequested += path => _ = ShowPageMenuAsync(path);
        _stage.Children.Insert(Math.Max(0, _stage.Children.IndexOf(FilePreview)), panel);
        return panel;
    }

    /// <summary>The notes a page shows (documents.get by path, documents.list).</summary>
    private sealed class StoreNotes(DocumentStore? store) : IPageNotes
    {
        private static PageNote Of(UrbeDocument d) => new(d.Path, d.Title, d.Content, d.Modified, d.Tags);
        public PageNote? Get(string path) => store?.Get(path) is { } d ? Of(d) : null;
        public IReadOnlyList<PageNote> List() => store is null ? [] : store.List().Select(Of).ToList();
    }

    /// <summary>pageDocs(): the pages (not templates), the most recently changed first.</summary>
    public IReadOnlyList<PageCard> PageCards()
    {
        if (_store is null) return [];
        return _store.List()
            .Where(d => PageEngine.IsPagePath(d.Path) && !d.Path.EndsWith(".template.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Modified ?? "", Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.InvariantCulture)))
            .Select(d =>
            {
                var spec = _pageEngine.Normalize(d.Content).Spec;
                var meta = (JsonObject)spec["meta"]!;
                var icon = (string?)meta["icon"] is { Length: > 0 } i ? i : "✦";
                return new PageCard(d.Path, (string?)meta["title"] ?? d.Title, icon, spec["sections"]!.AsArray().Count);
            }).ToList();
    }

    /// <summary>pages.home: the panel with the vault's pages.</summary>
    public void ShowPages()
    {
        if (Dialogs.IsOpen) return;
        Pages.Show(PageCards());
    }

    /// <summary>The page as the 1.8.4 renders it, inside the Urbe (the HTML viewer of the platform, or its source).</summary>
    public async Task ShowPageAsync(string path)
    {
        if (_store?.Get(path) is not { } doc) { ShowLifeNotice("Página não encontrada: " + path); return; }
        var html = _pageEngine.Render(doc.Content, new StoreNotes(_store));
        var title = (string?)_pageEngine.Normalize(doc.Content).Spec["meta"]!["title"] ?? doc.Title;
        await FilePreview.OpenGeneratedAsync(title, path, System.Text.Encoding.UTF8.GetBytes(html));
    }

    /// <summary>html(false): the page's HTML as the engine writes it for export (null when the page is gone).</summary>
    private string? PageHtml(string path) => _store?.Get(path) is { } doc ? _pageEngine.Render(doc.Content, new StoreNotes(_store)) : null;

    /// <summary>fileBase(): the page's file name without .page.json.</summary>
    private static string PageFileBase(string path) =>
        System.Text.RegularExpressions.Regex.Replace(path[(path.LastIndexOf('/') + 1)..], @"\.(page|template)\.json$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Where "Baixar HTML" goes (a save dialog); the tests replace it. Null = cancelled.</summary>
    public Func<string, byte[], Task<bool?>>? SavePageFile { get; set; }

    /// <summary>download(): "Baixar HTML" — the page as one .html file, saved where the person chooses.</summary>
    public async Task DownloadPageHtmlAsync(string path)
    {
        if (PageHtml(path) is not { } html) return;
        var name = PageFileBase(path) + ".html";
        try
        {
            var saved = await (SavePageFile ?? SaveHtmlFileAsync)(name, System.Text.Encoding.UTF8.GetBytes(html));
            if (saved == true) ShowLifeNotice("HTML baixado.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { ShowLifeNotice("Não consegui salvar: " + ex.Message); }
    }

    private async Task<bool?> SaveHtmlFileAsync(string name, byte[] bytes)
    {
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
            throw new NotSupportedException("este aparelho não permite salvar arquivos");
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Baixar HTML", SuggestedFileName = name, DefaultExtension = "html",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("HTML") { Patterns = ["*.html"], MimeTypes = ["text/html"] }]
        });
        if (file is null) return null;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
        return true;
    }

    /// <summary>saveHtmlToVault(): the page's HTML next to it (x.page.json → x.html), created or replaced, then the city follows.</summary>
    public async Task SavePageHtmlInVaultAsync(string path)
    {
        if (PageHtml(path) is not { } html || World.City is not { } city || _store is null) return;
        if (!VaultWritable) { await Dialogs.AlertAsync("Salvar HTML no vault", NoVaultMessage); return; }
        var target = System.Text.RegularExpressions.Regex.Replace(path, @"\.page\.json$", ".html", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        try
        {
            if (_store.Get(target) is { } existing)
            {
                await _vault!.SaveExistingTextFileAsync(target, html);
                _store.Upsert(new DocumentInput { Id = existing.Id, Path = existing.Path, Title = existing.Title, Content = html, Created = existing.Created, Modified = existing.Modified }, "pages.export");
                if (Editor.IsVisible && Editor.Current?.Path == target) Editor.Open(new CityNote(target, html));
            }
            else
            {
                await _vault!.CreateTextFileAsync(target, html);
                var doc = _store.Upsert(new DocumentInput { Path = target, Content = html }, "pages.export");
                city.AddDocumentHouse(doc, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                _mapSave?.Schedule();
                _mapSave?.MarkSync();
                World.InvalidateVisual();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException)
        {
            await Dialogs.AlertAsync("Não foi possível salvar o HTML", ex.Message);
            return;
        }
        _contents[target] = html;
        RefreshNotes();
        ShowLifeNotice("Salvo como " + target[(target.LastIndexOf('/') + 1)..] + ".");
    }

    /// <summary>"Copiar HTML".</summary>
    public async Task CopyPageHtmlAsync(string path)
    {
        if (PageHtml(path) is not { } html) return;
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) { ShowLifeNotice("Este aparelho não deixou copiar."); return; }
        await clipboard.SetTextAsync(html);
        CopiedText = html;
        ShowLifeNotice("HTML copiado.");
    }

    /// <summary>The last text copied by the app (tests).</summary>
    public string? CopiedText { get; private set; }

    /// <summary>pageMenu(id): Ver página, the exports (Baixar HTML, Salvar HTML no vault, Copiar HTML), Abrir como texto (JSON), Mover para a lixeira.</summary>
    private Task ShowPageMenuAsync(string path)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var name = path[(path.LastIndexOf('/') + 1)..];
        return Dialogs.MenuAsync(name,
        [
            new DialogItem("Abrir no estúdio", UrbeTheme.Icons.Edit, "Editar seções, tema e página", Run: () => OpenStudioAsync(path)),
            new DialogItem("Ver página", UrbeTheme.Icons.Page, "A página dentro do Urbe.", Run: () => ShowPageAsync(path)),
            new DialogItem("Baixar HTML", UrbeTheme.Icons.Download, "Arquivo único, funciona em qualquer hospedagem", Run: () => DownloadPageHtmlAsync(path)),
            new DialogItem("Salvar HTML no vault", UrbeTheme.Icons.Storage, "Ao lado desta página", Run: () => SavePageHtmlInVaultAsync(path)),
            new DialogItem("Copiar HTML", UrbeTheme.Icons.Copy, Run: () => CopyPageHtmlAsync(path)),
            new DialogItem("Abrir como texto (JSON)", UrbeTheme.Icons.Pencil, Run: () =>
            {
                FilePreview.Close();
                Pages.Close();
                OpenNoteByPath(path);
                return Task.CompletedTask;
            }),
            new DialogItem("Mover para a lixeira", UrbeTheme.Icons.Trash, Danger: true, Run: async () =>
            {
                if (!await Dialogs.ConfirmAsync("Mover para a lixeira?", path, "Mover", danger: true)) return;
                if (!await DeleteNoteAsync(path, confirmed: true)) return;
                if (FilePreview.GeneratedFrom == path) FilePreview.Close();
                if (Pages.IsVisible) Pages.Show(PageCards());
            })
        ]);
    }
}
