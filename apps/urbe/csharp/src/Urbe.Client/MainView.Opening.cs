using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-78 (observation of 22/10/2026: "a abertura está devagar, deveria ser instantânea"): the tree and the Área de
/// trabalho of the last opening appear at once, from the names kept in the app's own data, while the folder is read.
/// Nothing is changed meanwhile: a note tapped opens as soon as the folder is read, and every other action waits.
/// </summary>
public sealed partial class MainView
{
    /// <summary>The names of the last vault opened (tests give their own file).</summary>
    public OpeningNames OpeningNames { get; set; } = new(UrbeApp.OpeningNamesPath);

    private OpeningNames.Names? _lastNames;
    private string? _openWhenRead;

    /// <summary>True while the screen shows the names of the last opening, before the folder is read.</summary>
    public bool ShowingLastNames => _lastNames is not null;

    /// <summary>The names of the last opening on the screen, marked "Atualizando…" (none kept: the screen says it is opening).</summary>
    private void ShowLastNames()
    {
        if (OpeningNames.Read() is not { } names) return;
        _lastNames = names;
        LoadArea(names.Area);   // presentation only; the vault's own replaces it
        Explorer.HomeTitle = names.Vault;
        Workspace.Title = names.Vault;
        Explorer.Status = "Atualizando…";
        RefreshNotes();
    }

    /// <summary>RefreshNotes while the last names are shown: the tree and the boxes from them.</summary>
    private bool RefreshLastNames()
    {
        if (_lastNames is not { } names) return false;
        Explorer.SetNotes(names.Notes.Select(n => new CityNote(n, "")), names.Folders);
        Explorer.SetFiles(names.Files);
        Workspace.SetIndex(new WorkspaceIndex(names.Notes.Select(n => new WorkspaceNote(n, null, "", null)), names.Files, names.Folders));
        return true;
    }

    /// <summary>The folder was read (or could not be): the names leave the screen.</summary>
    private void LeaveLastNames()
    {
        if (_lastNames is null) return;
        _lastNames = null;
        if (Explorer.Status == "Atualizando…") Explorer.Status = _snapshot is null ? "Nenhuma pasta aberta" : null;
        if (_snapshot is null) { LoadArea(null); Explorer.HomeTitle = Workspace.Title = "Urbe"; }
        RefreshNotes();
    }

    /// <summary>
    /// A note tapped before the folder is read: it opens as soon as it is (true), instead of the last session's. False
    /// once the folder is read.
    /// </summary>
    private bool OpenWhenRead(string path)
    {
        if (_lastNames is null) return false;
        _openWhenRead = path;
        CloseNotesOverEditor();
        ShowLifeNotice("Abre assim que a pasta terminar de ser lida.");
        return true;
    }

    /// <summary>The note tapped while the names were shown, if it is still in the vault; null otherwise.</summary>
    private string? TakeOpenWhenRead()
    {
        var path = _openWhenRead;
        _openWhenRead = null;
        return path is not null && _contents.ContainsKey(path) ? path : null;
    }

    /// <summary>An action that changes the vault or reads a note, asked before the folder is read: it waits.</summary>
    private bool StillReading()
    {
        if (_lastNames is null) return false;
        ShowLifeNotice("Lendo a pasta… um instante.");
        return true;
    }

    /// <summary>The names of the vault on the screen, for the next opening (only those of a real vault).</summary>
    private void RememberNames()
    {
        if (_lastNames is not null || _snapshot is null || _vault?.VaultName is not { Length: > 0 } vault) return;
        var files = new HashSet<string>(_attachments, StringComparer.Ordinal);
        files.ExceptWith(_contents.Keys);
        OpeningNames.Remember(new OpeningNames.Names(vault, [.. _contents.Keys], [.. _library?.Folders ?? []], [.. files], _area.Serialize()));
    }
}
