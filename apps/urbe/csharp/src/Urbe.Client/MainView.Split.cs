namespace Urbe.Client;

/// <summary>
/// editor.split (editor/split.js): {enabled, primary, secondary, ratio}. "Abrir em painel dividido" (Ctrl+\) asks which
/// note to see beside the open one (the most recent first, up to 60), "Fechar painel dividido" and "Trocar painéis"
/// swap primary and secondary as the 1.8.4 does; the pane shows the secondary and follows its saves.
/// </summary>
public sealed partial class MainView
{
    private (bool Enabled, string? Primary, string? Secondary) _split;

    /// <summary>snapshot().</summary>
    public (bool Enabled, string? Primary, string? Secondary) SplitState => _split;

    /// <summary>openSecondary(idOrPath).</summary>
    public bool OpenSplit(string path)
    {
        if (_attachments.Contains(path) || _contents.ContainsKey(path) && !Urbe.Core.ArtifactModel.IsNote(path))
        { _ = OpenSplitFileAsync(path); return true; }
        if (_store?.Get(path) is not { } doc) return false;
        if (Editor.Split.Preview.IsVisible) { _ = OpenSplitNoteAsync(path); return true; }
        _split = (true, Editor.Current?.Path ?? doc.Path, doc.Path);
        RenderSplit();
        return true;
    }

    /// <summary>close().</summary>
    public void CloseSplit()
    {
        if (Editor.Split.Preview.IsVisible) { _ = CloseSplitAsync(); return; }
        _split = (false, _split.Primary, null);
        RenderSplit();
    }

    /// <summary>swap(): primary and secondary change places (the pane shows the new secondary).</summary>
    public void SwapSplit()
    {
        if (!_split.Enabled) return;
        _split = (true, _split.Secondary, _split.Primary);
        RenderSplit();
    }

    /// <summary>render(): hidden when off; the secondary's title and text; closed when that note is gone.</summary>
    private void RenderSplit()
    {
        if (!_split.Enabled) { Editor.Split.Hide(); return; }
        if (_split.Secondary is { } file && Editor.Split.Preview.IsVisible && Editor.Split.Preview.Current == file) return;
        if (_split.Secondary is not { } s || _store?.Get(s) is not { } doc) { CloseSplit(); return; }
        Editor.Split.Show(doc.Path, doc.Title, _contents.GetValueOrDefault(doc.Path, doc.Content));
    }

    /// <summary>editor.split.open without a note: "Abrir ao lado" — the notes other than the open one, most recent first.</summary>
    public async Task ChooseSplitAsync()
    {
        if (_store is null || Dialogs.IsOpen) return;
        var active = Editor.Current?.Path;
        var list = _store.List()
            .Where(d => d.Path != active)
            .OrderBy(d => d.Modified ?? "", Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.InvariantCulture)))
            .ToList();
        if (list.Count == 0 && _attachments.Count == 0) return;
        // the 60 most recent at hand; any other through the search of the whole vault (DEC-0059: none is out of reach)
        var recent = list.Take(60).ToList();
        var items = recent.Select(d => new DialogItem(d.Title, UrbeTheme.Icons.Notes, d.Path)).ToList();
        items.Add(new DialogItem("Escolher qualquer arquivo…", UrbeTheme.Icons.FileOther, "Imagens, PDF, áudio, vídeo, código e outros formatos"));
        if (list.Count > recent.Count) items.Insert(0, new DialogItem("Buscar entre todas as notas…", UrbeTheme.Icons.Search, list.Count + " notas"));
        var pick = await Dialogs.ChooseAsync("Abrir ao lado", items, "Escolha a nota para ver ao lado da que está aberta.");
        if (pick is not { } i) return;
        if (list.Count > recent.Count && i == 0) { Search.PickNote(path => OpenSplit(path)); return; }
        if (i == items.Count - 1)
        {
            if (await Dialogs.ChooseFileAsync(_contents.Keys.Concat(_attachments)) is { } path) OpenSplit(path);
            return;
        }
        OpenSplit(recent[list.Count > recent.Count ? i - 1 : i].Path);
    }

    private void WireSplit()
    {
        Editor.Split.Preview.Storage = _vault;
        Editor.Split.Preview.Dialogs = Dialogs;
        Editor.Split.Preview.QuoteRequested += quote => _ = InsertReaderQuoteAsync(quote);
        Editor.Split.CloseRequested += CloseSplit;
        Editor.Split.SwapRequested += SwapSplit;
        // document:updated of the secondary redraws the pane
        NoteSaved += path => { if (_split.Enabled && _split.Secondary == path) RenderSplit(); };
    }

    private async Task OpenSplitFileAsync(string path)
    {
        if (!await Editor.Split.Preview.TryCloseAsync()) return;
        _split = (true, Editor.Current?.Path, path);
        Editor.Split.ShowFile(path);
        await Editor.Split.Preview.OpenAsync(path);
    }

    private async Task OpenSplitNoteAsync(string path)
    {
        if (await Editor.Split.Preview.TryCloseAsync()) OpenSplit(path);
    }

    private async Task CloseSplitAsync()
    {
        if (await Editor.Split.Preview.TryCloseAsync()) CloseSplit();
    }
}
