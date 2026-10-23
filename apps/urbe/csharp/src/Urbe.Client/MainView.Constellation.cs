using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-72 (DEC-0052, ADD-0029): the Constelação, opened from the Área de trabalho or the Explorer's menu — never the base of
/// the app. The positions come from .urbe/constelacao.json (derived from the notes, written only when a note was added or
/// removed); the links, the projects and how recent each note is come from the notes as they are now.
/// </summary>
public sealed partial class MainView
{
    private string? _skyText;
    private ConstellationPanel? _sky;

    /// <summary>The Constelação's screen (made the first time it opens).</summary>
    public ConstellationPanel SkyPanel
    {
        get
        {
            if (_sky is not null) return _sky;
            _sky = new ConstellationPanel { ZIndex = 3 };
            _sky.Sky.NoteChosen += path => { _sky.IsVisible = false; OpenNoteByPath(path); };
            _stage.Children.Add(_sky);
            return _sky;
        }
    }

    /// <summary>Opens the Constelação of the notes on the screen; new notes get their place, written to the vault when it can be.</summary>
    public async Task ShowConstellationAsync(string? focusId = null)
    {
        if (_store is null) return;
        var sky = Constellation.Parse(_skyText, out var future);
        var docs = _store.List();
        if (sky.Update(docs.Select(d => (d.Id, d.Path))) && !future && VaultWritable && _vault is not null)
        {
            var json = sky.Serialize();
            try
            {
                await _vault.SaveConstellationAsync(json);
                _skyText = json;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or NotSupportedException)
            {
                // the sky still opens; the places are worked out again next time
                ShowLifeNotice("As posições da Constelação não foram guardadas: " + ex.Message, Ui.ToastKind.Warning);
            }
        }
        using var index = new KnowledgeIndex(_store);
        var links = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var d in docs)
            links[d.Id] = index.Links(d.Id).Select(x => x.Id).Union(index.Backlinks(d.Id).Select(x => x.Id)).Where(id => id != d.Id).ToHashSet(StringComparer.Ordinal);
        var lately = DateTimeOffset.UtcNow.AddDays(-7);
        var stars = docs.Where(d => sky.Of(d.Id) is not null).Select(d =>
        {
            var (x, y) = sky.Of(d.Id)!.Value;
            bool recent = DateTimeOffset.TryParse(d.Modified ?? d.Created, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var when) && when >= lately;
            return new SkyStar(d.Id, d.Path, d.Title, Constellation.ProjectOf(d.Path), x, y, links[d.Id].Count, recent);
        }).ToList();
        SkyPanel.Sky.Show(stars, sky.Regions, id => links.GetValueOrDefault(id) ?? []);
        if (focusId is not null) SkyPanel.Sky.FocusOn(focusId);
        SkyPanel.IsVisible = true;
    }
}
