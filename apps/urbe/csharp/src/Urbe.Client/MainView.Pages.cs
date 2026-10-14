using System.Text.Json.Nodes;
using Urbe.Core;
using Urbe.Core.Pages;

namespace Urbe.Client;

/// <summary>
/// Páginas (pages.home, studio.js): the vault's .page.json files listed in their own panel and each shown inside the
/// Urbe as the 1.8.4's page engine writes it (PageEngine, ported and proved byte for byte); "⋯" has Ver página, Abrir
/// como texto (JSON) and Mover para a lixeira. The studio that creates and edits pages comes with UC-35.
/// </summary>
public sealed partial class MainView
{
    private PagesPanel? _pagesPanel;
    private readonly PageEngine _pageEngine = new();

    /// <summary>The Páginas panel, made the first time it opens (under the file preview, which shows a page).</summary>
    public PagesPanel Pages => _pagesPanel ??= MakePagesPanel();

    private PagesPanel MakePagesPanel()
    {
        var panel = new PagesPanel();
        panel.PageRequested += path => _ = ShowPageAsync(path);
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

    /// <summary>pageMenu(id): Ver página, Abrir como texto (JSON), Mover para a lixeira.</summary>
    private Task ShowPageMenuAsync(string path)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var name = path[(path.LastIndexOf('/') + 1)..];
        return Dialogs.MenuAsync(name,
        [
            new DialogItem("Ver página", UrbeTheme.Icons.Page, "A página dentro do Urbe.", Run: () => ShowPageAsync(path)),
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
