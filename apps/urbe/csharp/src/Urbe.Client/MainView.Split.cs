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
        if (_store?.Get(path) is not { } doc) return false;
        _split = (true, Editor.Current?.Path ?? doc.Path, doc.Path);
        RenderSplit();
        return true;
    }

    /// <summary>close().</summary>
    public void CloseSplit()
    {
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
        if (_split.Secondary is not { } s || _store?.Get(s) is not { } doc) { CloseSplit(); return; }
        Editor.Split.Show(doc.Path, doc.Title, _contents.GetValueOrDefault(doc.Path, doc.Content));
    }

    /// <summary>editor.split.open without a note: "Abrir ao lado" — the notes other than the open one, most recent first.</summary>
    public async Task ChooseSplitAsync()
    {
        if (_store is null || Dialogs.IsOpen) return;
        var active = Editor.Current?.Path;
        var list = _store.List()
            .Where(d => d.Path != active && System.Text.RegularExpressions.Regex.IsMatch(d.Path, @"\.(md|markdown|txt)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .OrderBy(d => d.Modified ?? "", Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.InvariantCulture)))
            .Take(60).ToList();
        if (list.Count == 0) return;
        var pick = await Dialogs.ChooseAsync("Abrir ao lado", list.Select(d => new DialogItem(d.Title, UrbeTheme.Icons.Notes, d.Path)).ToList(),
            "Escolha a nota para ver ao lado da que está aberta.");
        if (pick is { } i) OpenSplit(list[i].Path);
    }

    private void WireSplit()
    {
        Editor.Split.CloseRequested += CloseSplit;
        Editor.Split.SwapRequested += SwapSplit;
        // document:updated of the secondary redraws the pane
        NoteSaved += path => { if (_split.Enabled && _split.Secondary == path) RenderSplit(); };
    }
}
