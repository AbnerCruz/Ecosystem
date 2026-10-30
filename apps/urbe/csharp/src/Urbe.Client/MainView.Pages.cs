using System.Text.Json.Nodes;
using Avalonia.Input.Platform;
using Urbe.Core;
using Urbe.Core.Pages;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// Páginas (pages.home, studio.js): the vault's .page.json files listed in their own panel and each shown inside the
/// Urbe as the 1.8.4's page engine writes it (PageEngine, ported and proved byte for byte); "⋯" has Ver página, Abrir
/// como texto (JSON) and Mover para a lixeira. A card opens the page in the studio (MainView.Studio.cs).
/// </summary>
public sealed partial class MainView
{
    private PagesPanel? _pagesPanel;
    private readonly PageEngine _pageEngine = new() { MathHtml = PageMath, UrbeMarks = true };

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
        panel.ConvertCompositionsRequested += () => _ = ConvertCompositionsAsync();
        panel.MenuRequested += path => _ = ShowPageMenuAsync(path);
        _stage.Children.Insert(Math.Max(0, _stage.Children.IndexOf(FilePreview)), panel);
        return panel;
    }

    /// <summary>The notes a page shows (documents.get by path, documents.list).</summary>
    private sealed class StoreNotes(DocumentStore? store) : IPageNotes
    {
        // a page's queries by tag see the ones inherited from the project (DEC-0058)
        private static PageNote Of(UrbeDocument d, ProjectTags projects) => new(d.Path, d.Title, d.Content, d.Modified, projects.Effective(d));
        public PageNote? Get(string path) => store?.Get(path) is { } d ? Of(d, ProjectTags.Of(store.List())) : null;
        public IReadOnlyList<PageNote> List()
        {
            if (store is null) return [];
            var all = store.List();
            var projects = ProjectTags.Of(all);
            return all.Select(d => Of(d, projects)).ToList();
        }
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
        Pages.Show(PageCards(), CompositionPlan()?.Creates.Count ?? 0);
    }

    /// <summary>The vault's compositions of the 1.8.4 (.urbe/compositions.v2.json or .json), as the converter reads them; null without any.</summary>
    private CompositionFile? Compositions()
    {
        if (_snapshot is null) return null;
        VaultFile? Find(string p) => _snapshot.Files.FirstOrDefault(f => string.Equals(f.Key, p, StringComparison.OrdinalIgnoreCase)).Value;
        if (Find(".urbe/compositions.v2.json") is { } v2)
        {
            try
            {
                // readSide: version 2 is the same content as version 1
                if (JsonNode.Parse(v2.Bytes.Span) is JsonObject root && root["version"] is JsonValue v && v.TryGetValue<int>(out var n) && n == 2)
                {
                    root["version"] = 1;
                    return CompositionFile.Parse(root);
                }
            }
            catch (System.Text.Json.JsonException) { }
            return CompositionFile.Parse(v2.Bytes);
        }
        return Find(".urbe/compositions.json") is { } v1 ? CompositionFile.Parse(v1.Bytes) : null;
    }

    /// <summary>UC-45 (ADR-0009): which compositions still become pages (an already converted one is recognised by its id).</summary>
    private CompositionMigrationPlan? CompositionPlan()
    {
        if (_store is null || Compositions() is not { } file || file.Items.Count == 0) return null;
        var existing = _store.List().Where(d => d.Path.EndsWith(".page.json", StringComparison.OrdinalIgnoreCase))
            .Select(d => new PersistedPage(d.Path, PageDocument.Parse(d.Content))).ToList();
        return CompositionMigrationPlanner.Plan(file, _store, existing);
    }

    /// <summary>"Converter em páginas": each composition written as a page (only new files; the compositions file is kept).</summary>
    public async Task ConvertCompositionsAsync()
    {
        if (!VaultWritable) { await Dialogs.AlertAsync("Converter composições", NoVaultMessage); return; }
        var plan = CompositionPlan();
        if (plan is null || plan.Blocked) { await Dialogs.AlertAsync("Converter composições", plan?.BlockReason ?? "Não há composições para converter."); return; }
        if (plan.Creates.Count == 0) { ShowLifeNotice("As composições já viraram páginas."); return; }
        var missing = plan.Creates.Sum(c => c.MissingDocumentIds.Count);
        if (!await Dialogs.ConfirmAsync("Converter " + plan.Creates.Count + " composição(ões) em páginas?",
                "Cada composição vira uma página em Páginas/Composições, com as mesmas notas na mesma ordem. O arquivo das composições fica no vault, sem mudança." +
                (missing > 0 ? " " + missing + " nota(s) que não existem mais ficam como seções vazias." : "") +
                (plan.Conflicts.Count > 0 ? " " + plan.Conflicts.Count + " composição(ões) não serão convertidas: já existe uma página diferente com o mesmo nome." : ""), "Converter"))
            return;
        int written = 0;
        foreach (var c in plan.Creates)
        {
            try
            {
                await ((IAssistantApp)this).CreateNoteAsync(c.PagePath, c.Page.SerializeForWrite() + "\n");
                written++;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
            {
                await Dialogs.AlertAsync("Não foi possível converter " + c.PagePath, ex.Message);
                break;
            }
        }
        ShowPages();
        ShowLifeNotice(written + " página(s) criada(s) a partir das composições.");
    }

    /// <summary>The page as the 1.8.4 renders it, inside the Urbe (the HTML viewer of the platform, or its source).</summary>
    public async Task ShowPageAsync(string path)
    {
        if (_store?.Get(path) is not { } doc) { ShowLifeNotice("Página não encontrada: " + path); return; }
        var html = await WithVaultImagesAsync(_pageEngine.Render(doc.Content, new StoreNotes(_store)), path);
        var title = (string?)_pageEngine.Normalize(doc.Content).Spec["meta"]!["title"] ?? doc.Title;
        await FilePreview.OpenGeneratedAsync(title, path, System.Text.Encoding.UTF8.GetBytes(html));
    }

    /// <summary>html(false): the page's HTML as the engine writes it for export (null when the page is gone).</summary>
    private async Task<string?> PageHtmlAsync(string path) =>
        _store?.Get(path) is { } doc ? await WithVaultImagesAsync(_pageEngine.Render(doc.Content, new StoreNotes(_store)), path) : null;

    /// <summary>
    /// CST-059: the vault images a page names travel inside its HTML (the platform's viewer reaches nothing outside the
    /// page, and a downloaded page is a single file). <paramref name="cache"/> keeps what the studio's preview already read.
    /// </summary>
    private async Task<string> WithVaultImagesAsync(string html, string pagePath, Dictionary<string, byte[]?>? cache = null)
    {
        if (_vault is not { } vault) return html;
        var files = _attachments.Concat(_contents.Keys).ToList();
        return await Urbe.Core.Pages.PageImages.InlineAsync(html, pagePath, files, async file =>
        {
            if (cache is not null && cache.TryGetValue(file, out var known)) return known;
            var bytes = (await vault.ReadFileAsync(file, Urbe.Core.Pages.PageImages.MaxBytes))?.Bytes;
            if (cache is not null) cache[file] = bytes;
            return bytes;
        });
    }

    /// <summary>fileBase(): the page's file name without .page.json.</summary>
    private static string PageFileBase(string path) =>
        System.Text.RegularExpressions.Regex.Replace(path[(path.LastIndexOf('/') + 1)..], @"\.(page|template)\.json$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Where "Baixar HTML" goes (a save dialog); the tests replace it. Null = cancelled.</summary>
    public Func<string, byte[], Task<bool?>>? SavePageFile { get; set; }

    /// <summary>download(): "Baixar HTML" — the page as one .html file, saved where the person chooses.</summary>
    public async Task DownloadPageHtmlAsync(string path)
    {
        if (await PageHtmlAsync(path) is not { } html) return;
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

    /// <summary>saveHtmlToVault(): the page's HTML next to it (x.page.json → x.html), created or replaced, then its document.</summary>
    public async Task SavePageHtmlInVaultAsync(string path)
    {
        if (await PageHtmlAsync(path) is not { } html || _library is not { } library || _store is null) return;
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
                library.AddNote(target, html, Now, "pages.export");
                NotesChanged();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException)
        {
            await Dialogs.AlertAsync("Não foi possível salvar o HTML", StorageProblems.Explain(ex));
            return;
        }
        _contents[target] = html;
        RefreshNotes();
        ShowLifeNotice("Salvo como " + target[(target.LastIndexOf('/') + 1)..] + ".");
    }

    /// <summary>"Copiar HTML".</summary>
    public async Task CopyPageHtmlAsync(string path)
    {
        if (await PageHtmlAsync(path) is not { } html) return;
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
            new DialogItem("Imprimir ou salvar PDF", UrbeTheme.Icons.Page, "Pela impressão do sistema", Run: () => PrintPageAsync(path)),
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
                if (Pages.IsVisible) Pages.Show(PageCards(), CompositionPlan()?.Creates.Count ?? 0);
            })
        ]);
    }
}
