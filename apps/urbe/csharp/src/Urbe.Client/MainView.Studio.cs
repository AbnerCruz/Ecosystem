using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Urbe.Core;
using Urbe.Core.Assistant;
using Urbe.Core.Pages;

namespace Urbe.Client;

/// <summary>
/// The page studio inside the app (studio.js open/home/newPage): a card of Páginas opens its page in the studio, "Nova
/// página" makes a page with a free layout ready to write in (quickPage) and opens it, and the studio saves through the
/// vault like a note (written, verified, the city and the store follow). The studio's Exportar and ⋯ menus are here.
/// </summary>
public sealed partial class MainView : IPageStudioHost
{
    private PageStudioView? _studio;

    /// <summary>The studio, made the first time a page opens (above Páginas).</summary>
    public PageStudioView Studio => _studio ??= MakeStudio();

    private PageStudioView MakeStudio()
    {
        var view = new PageStudioView(this);
        _stage.Children.Insert(Math.Max(0, _stage.Children.IndexOf(FilePreview)), view);
        return view;
    }

    /// <summary>open(id): the page in the studio.</summary>
    public async Task OpenStudioAsync(string path)
    {
        if (_store?.Get(path) is not { } doc) { ShowLifeNotice("Página não encontrada: " + path); return; }
        if (!VaultWritable) { await ShowPageAsync(path); return; }
        await Studio.OpenAsync(doc.Path, doc.Content);
    }

    /// <summary>newPage() → quickPage(): "Páginas/Nova página.page.json" with a free layout, opened with its first section selected.</summary>
    public async Task NewPageAsync()
    {
        if (!VaultWritable || _store is null) { await Dialogs.AlertAsync("Nova página", NoVaultMessage); return; }
        var spec = PageStudio.QuickPage(_pageEngine);
        var path = PageStudio.UniquePath("Páginas/Nova página.page.json", ((IPageStudioHost)this).Exists);
        try { await ((IAssistantApp)this).CreateNoteAsync(path, LegacyCustomize.Stringify(_pageEngine.Compact(spec), 2) + "\n"); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Não foi possível criar a página", ex.Message);
            return;
        }
        await OpenStudioAsync(path);
        if (Studio.Model is { Sections.Count: > 0 } st)
        {
            st.Select((string?)st.Sections[0]!["id"]);
            Studio.RenderPanels();
        }
        ShowLifeNotice("Nova página. Dê um nome no topo e use ＋ para inserir o que quiser.");
    }

    /// <summary>pages.open from the palette: the page chosen among the vault's pages opens in the studio.</summary>
    public async Task ChoosePageForStudioAsync()
    {
        var pages = PageCards();
        if (pages.Count == 0) { ShowPages(); return; }
        var pick = await Dialogs.ChooseAsync("Abrir página no estúdio", pages.Select(p => new DialogItem(p.Icon + "  " + p.Title, Detail: p.Path)).ToList());
        if (pick is not null) await OpenStudioAsync(pages[pick.Value].Path);
    }

    // ---------------------------------------------------------------- IPageStudioHost

    UrbeDialogs IPageStudioHost.Dialogs => Dialogs;

    bool IPageStudioHost.PreviewCovered =>
        _assistant?.IsVisible == true || FilePreview.IsVisible || _pluginPanels.Children.Any(c => c.IsVisible);

    string IPageStudioHost.Render(JsonObject spec, bool preview) => _pageEngine.Render(spec, new StoreNotes(_store), preview);

    Task<Avalonia.Controls.Control?> IPageStudioHost.PreviewAsync(string path, byte[] html, Action<string>? messages) =>
        ContentViewers.CreateAsync(new ContentRequest(_vault, path, path[(path.LastIndexOf('/') + 1)..], VaultFileKinds.Html, html) { Messages = messages });

    async Task IPageStudioHost.SaveAsync(string path, string text)
    {
        var doc = _store?.Get(path) ?? throw new InvalidOperationException("A página não está mais no vault: " + path);
        await ((IAssistantApp)this).SaveNoteAsync(doc, text);
    }

    async Task IPageStudioHost.SaveNoteAsync(string path, string text)
    {
        var doc = _store?.Get(path) ?? throw new InvalidOperationException("A nota não está mais no vault: " + path);
        await ((IAssistantApp)this).SaveNoteAsync(doc, text);
    }

    async Task<string> IPageStudioHost.RenameAsync(string path, string newPath)
    {
        var doc = _store?.Get(path) ?? throw new InvalidOperationException("A página não está mais no vault: " + path);
        var moved = await ((IAssistantApp)this).RenameNoteAsync(doc, newPath);
        return moved.Path;
    }

    bool IPageStudioHost.Exists(string path) => _store?.Get(path) is not null || _contents.ContainsKey(path) || _attachments.Contains(path);

    IReadOnlyList<(string Path, string Title)> IPageStudioHost.Notes() =>
        _store is null ? [] : _store.List().Where(d => Regex.IsMatch(d.Path, @"\.(md|markdown|txt)$", RegexOptions.IgnoreCase))
            .OrderBy(d => d.Path, StringComparer.Ordinal).Select(d => (d.Path, d.Title)).ToList();

    PageNote? IPageStudioHost.Note(string path)
    {
        var p = path.Trim();
        var d = _store?.Get(p) ?? _store?.Get(p + ".md") ?? _store?.List().FirstOrDefault(x => x.Title.Length > 0 &&
            string.Equals(x.Title, Regex.Replace(p, @"\.md$", "", RegexOptions.IgnoreCase), StringComparison.OrdinalIgnoreCase));
        return d is null ? null : new PageNote(d.Path, d.Title, d.Content, d.Modified, d.Tags);
    }

    IReadOnlyList<string> IPageStudioHost.Folders()
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var p in (_store?.List().Select(d => d.Path) ?? []).Concat(_attachments))
        {
            var parts = p.Split('/').ToList();
            parts.RemoveAt(parts.Count - 1);
            while (parts.Count > 0) { set.Add(string.Join('/', parts)); parts.RemoveAt(parts.Count - 1); }
        }
        return set.ToList();
    }

    IReadOnlyList<string> IPageStudioHost.Tags() =>
        _store is null ? [] : _store.List().SelectMany(d => d.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    IReadOnlyList<(string Path, string Name, string Description, JsonObject Section)> IPageStudioHost.SavedBlocks()
    {
        var list = new List<(string, string, string, JsonObject)>();
        foreach (var d in _store?.List() ?? [])
        {
            if (!Regex.IsMatch(d.Path, @"^Páginas/Blocos/.+\.block\.json$", RegexOptions.IgnoreCase)) continue;
            JsonObject? raw;
            try { raw = JsonNode.Parse(d.Content) as JsonObject; }
            catch (System.Text.Json.JsonException) { continue; }
            if (raw?["section"] is not JsonObject sec || _pageEngine.BlockInfo((string?)sec["type"]) is not { } b) continue;
            var name = (string?)raw["name"] is { Length: > 0 } n ? n : Regex.Replace(d.Path[(d.Path.LastIndexOf('/') + 1)..], @"\.block\.json$", "", RegexOptions.IgnoreCase);
            list.Add((d.Path, name, (string?)raw["description"] is { Length: > 0 } desc ? desc : b.Label, sec));
        }
        return list;
    }

    IReadOnlyList<(string Path, string Name, string Description, JsonObject Spec)> IPageStudioHost.SavedTemplates()
    {
        var list = new List<(string, string, string, JsonObject)>();
        foreach (var d in _store?.List() ?? [])
        {
            if (!d.Path.EndsWith(".template.json", StringComparison.OrdinalIgnoreCase)) continue;
            JsonObject? raw;
            try { raw = JsonNode.Parse(d.Content) as JsonObject; }
            catch (System.Text.Json.JsonException) { continue; }
            if (raw is null) continue;
            var spec = _pageEngine.Normalize(raw).Spec;
            var tp = raw["template"] as JsonObject;
            list.Add((d.Path, (string?)tp?["name"] is { Length: > 0 } n ? n : (string?)spec["meta"]!["title"] ?? d.Title, (string?)tp?["description"] ?? "", raw));
        }
        return list;
    }

    async Task IPageStudioHost.CreateFileAsync(string path, string text)
    {
        await ((IAssistantApp)this).CreateNoteAsync(path, text);
    }

    /// <summary>exportMenu: Baixar HTML, Salvar HTML no vault, Copiar HTML (Imprimir ou salvar PDF: by the HTML opened in the browser).</summary>
    Task IPageStudioHost.ExportMenuAsync(string path) => Dialogs.MenuAsync("Exportar",
    [
        new DialogItem("Imprimir ou salvar PDF", UrbeTheme.Icons.Page,
            Studio.Model?.IsBook == true ? "Com o tamanho de página e as margens do livro" : "Pela impressão do sistema", Run: () => PrintPageAsync(path)),
        new DialogItem("Baixar HTML", UrbeTheme.Icons.Download, "Arquivo único, funciona em qualquer hospedagem", Run: () => DownloadPageHtmlAsync(path)),
        new DialogItem("Ver página", UrbeTheme.Icons.Page, "A página dentro do Urbe, como ela será publicada", Run: async () => { await ShowPageAsync(path); }),
        new DialogItem("Salvar HTML no vault", UrbeTheme.Icons.Storage, "Ao lado desta página", Run: () => SavePageHtmlInVaultAsync(path)),
        new DialogItem("Copiar HTML", UrbeTheme.Icons.Copy, Run: () => CopyPageHtmlAsync(path))
    ]);

    /// <summary>printPdf(): the page's export HTML to the system's print ("Salvar como PDF" saves the file).</summary>
    public async Task PrintPageAsync(string path)
    {
        if (PageHtml(path) is not { } html) return;
        if (ContentViewers.Printer is not { } print) { ShowLifeNotice("Este aparelho não oferece impressão. Use Baixar HTML e imprima pelo navegador."); return; }
        try
        {
            ShowLifeNotice("Preparando o PDF…");
            await print(PageFileBase(path), html);
            ShowLifeNotice("Escolha “Salvar como PDF” para gerar o arquivo.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { ShowLifeNotice("Não consegui gerar o PDF: " + ex.Message); }
    }

    /// <summary>moreMenu: Salvar como modelo, Duplicar página, Editar JSON, Todas as páginas, Mover página para a lixeira.</summary>
    Task IPageStudioHost.MoreMenuAsync(string path) => Dialogs.MenuAsync("Página",
    [
        new DialogItem("Salvar como modelo", UrbeTheme.Icons.Layers, "Reutilize em novas páginas", Run: () => SaveTemplateAsync(path)),
        new DialogItem("Duplicar página", UrbeTheme.Icons.Copy, Run: () => DuplicatePageAsync(path)),
        new DialogItem("Editar JSON", UrbeTheme.Icons.FileCode, "Ctrl+J", Run: () => { Studio.OpenJson(); return Task.CompletedTask; }),
        new DialogItem("Todas as páginas", UrbeTheme.Icons.Notes, Run: async () => { await Studio.CloseAsync(); ShowPages(); }),
        new DialogItem("Mover página para a lixeira", UrbeTheme.Icons.Trash, Danger: true, Run: () => TrashStudioPageAsync(path))
    ]);

    /// <summary>saveTemplate: Páginas/Modelos/&lt;nome&gt;.template.json with kind urbe-template and its name and description.</summary>
    private async Task SaveTemplateAsync(string path)
    {
        if (Studio.Model is not { } st) return;
        var name = PageStudio.CleanName(await Dialogs.PromptAsync(new PromptOptions("Salvar como modelo", "Nome do modelo", (string?)st.Meta["title"] ?? "", Confirm: "Salvar modelo",
            Validate: v => PageStudio.CleanName(v).Length > 0 ? null : "Use um nome válido.")));
        if (name.Length == 0) return;
        var desc = await Dialogs.PromptAsync(new PromptOptions("Descrição (opcional)", "Para que serve este modelo?", "", Confirm: "Salvar", Required: false));
        var spec = (JsonObject)st.Spec.DeepClone();
        spec["kind"] = "urbe-template";
        spec["template"] = new JsonObject { ["name"] = name, ["description"] = desc ?? "" };
        var target = PageStudio.UniquePath("Páginas/Modelos/" + name + ".template.json", ((IPageStudioHost)this).Exists);
        var compact = _pageEngine.Compact(spec);
        compact["template"] = spec["template"]!.DeepClone();
        try
        {
            await ((IAssistantApp)this).CreateNoteAsync(target, LegacyCustomize.Stringify(compact, 2) + "\n");
            ShowLifeNotice("Modelo “" + name + "” salvo.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Não foi possível salvar o modelo", ex.Message);
        }
    }

    /// <summary>duplicatePage: "x cópia.page.json" with "(cópia)" in the title, opened.</summary>
    private async Task DuplicatePageAsync(string path)
    {
        if (Studio.Model is not { } st) return;
        await Studio.FlushAsync();
        var target = PageStudio.UniquePath(Regex.Replace(path, @"(\.page\.json)$", " cópia$1", RegexOptions.IgnoreCase), ((IPageStudioHost)this).Exists);
        var spec = (JsonObject)st.Spec.DeepClone();
        spec["meta"]!["title"] = ((string?)spec["meta"]!["title"] ?? "") + " (cópia)";
        try { await ((IAssistantApp)this).CreateNoteAsync(target, LegacyCustomize.Stringify(_pageEngine.Compact(spec), 2) + "\n"); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Não foi possível duplicar", ex.Message);
            return;
        }
        await OpenStudioAsync(target);
        ShowLifeNotice("Cópia criada.");
    }

    /// <summary>trashPage: confirmed, the page to the trash and back to Páginas.</summary>
    private async Task TrashStudioPageAsync(string path)
    {
        if (Studio.Model is not { } st) return;
        if (!await Dialogs.ConfirmAsync("Mover para a lixeira?", "“" + ((string?)st.Meta["title"] ?? path) + "” poderá ser restaurada pela Lixeira.", "Mover para a lixeira", danger: true)) return;
        await Studio.FlushAsync();
        if (!await DeleteNoteAsync(path, confirmed: true)) return;
        Studio.Discard();
        ShowPages();
    }

    /// <summary>pickImage + shrink: an image of the device as a data: URL, at most 1600 px (small PNG/GIF and SVG kept as they are).</summary>
    async Task<string?> IPageStudioHost.PickImageAsync()
    {
        if (PickImage is not null) return await PickImage();
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return null;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Escolher imagem", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Imagens") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.svg"], MimeTypes = ["image/*"] }]
        });
        if (files.Count == 0) return null;
        await using var stream = await files[0].OpenReadAsync();
        using var mem = new MemoryStream();
        await stream.CopyToAsync(mem);
        return ImageDataUrl(files[0].Name, mem.ToArray());
    }

    /// <summary>Replaces the image picker (tests): the data: URL, or null.</summary>
    public Func<Task<string?>>? PickImage { get; set; }

    internal static string ImageDataUrl(string name, byte[] bytes)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        string Mime() => ext switch { ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", ".svg" => "image/svg+xml", _ => "image/jpeg" };
        if (ext == ".svg" || (ext is ".png" or ".gif" && bytes.Length < 400_000)) return "data:" + Mime() + ";base64," + Convert.ToBase64String(bytes);
        try
        {
            using var src = new MemoryStream(bytes);
            using var probe = new Bitmap(src);
            var size = probe.PixelSize;
            double k = Math.Min(1, 1600.0 / Math.Max(size.Width, size.Height));
            src.Position = 0;
            using var scaled = k < 1 ? Bitmap.DecodeToWidth(src, Math.Max(1, (int)Math.Round(size.Width * k))) : new Bitmap(src);
            // shrink(): PNG/GIF stay PNG, photos become JPEG at .86 (the 1.8.4 wrote WebP at .86)
            bool png = ext is ".png" or ".gif";
            using var output = new MemoryStream();
            if (png) scaled.Save(output, new PngBitmapEncoderOptions());
            else scaled.Save(output, new JpegBitmapEncoderOptions { Quality = 86 });
            var encoded = output.ToArray();
            if (k >= 1 && bytes.Length <= encoded.Length) return "data:" + Mime() + ";base64," + Convert.ToBase64String(bytes);
            return "data:" + (png ? "image/png" : "image/jpeg") + ";base64," + Convert.ToBase64String(encoded);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException("Formato de imagem não suportado.", ex);
        }
    }

    void IPageStudioHost.Closed()
    {
        if (Pages.IsVisible) Pages.Show(PageCards(), CompositionPlan()?.Creates.Count ?? 0);
    }
}
