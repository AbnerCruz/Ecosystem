namespace Urbe.Client;

/// <summary>
/// Estudo (owner, 04/11/2026: research → sources → writing): the note in the editor and, beside it, the source being
/// studied — a site, a YouTube video, a PDF/EPUB or any file of the vault — in the editor's source pane (a phone: above
/// the note; a wide window: on its left), with a grip between them. Not a second editor: the note is the same editor,
/// saved the same way; the source pane is the one "Fixar em painel" already opens.
/// </summary>
public sealed partial class MainView
{
    /// <summary>The Estudo destination is on the screen: the editor with its source pane.</summary>
    public bool StudyOpen => Editor.IsVisible && Editor.Pinned.IsVisible;

    /// <summary>
    /// Opens Estudo: the note being written (the open one, else the most recent, else a new "Anotações de estudo") and
    /// the source pane ready to choose a site, a video or a file.
    /// </summary>
    public async Task StartStudyAsync()
    {
        if (GraphOpen && !await CloseGraphAsync()) return;
        if (AssistantOpen) ToggleAssistant();
        if (StudyOpen) { UpdateNav(); return; }
        if (!Editor.IsVisible || Editor.Current is null)
        {
            var note = Editor.Current?.Path ?? Editor.Tabs.LastOrDefault(p => _contents.ContainsKey(p))
                ?? _store?.List().Where(d => Urbe.Core.ArtifactModel.IsNote(d.Path) && !d.Path.StartsWith("Tutorial/", StringComparison.Ordinal))
                    .OrderByDescending(d => d.Modified ?? d.Created ?? "", StringComparer.Ordinal).FirstOrDefault()?.Path;
            if (note is null)
            {
                if (_vault is null) { await Dialogs.AlertAsync("Estudo", "Abra uma pasta de notas para estudar com uma nota ao lado da fonte."); return; }
                var created = await ((IAssistantApp)this).CreateNoteAsync("Anotações de estudo.md", "# Anotações de estudo\n\n");
                note = created.Path;
            }
            OpenNoteByPath(note);
        }
        Editor.Pinned.Open();
        UpdateNav();
    }
}
