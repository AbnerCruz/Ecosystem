using Urbe.Core;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>
/// #956: study sessions — a named set of the vault's sources, by id, with objectives. Opening one lists its sources as
/// they are now (renamed ones followed, missing ones said) and opens the chosen one in Estudo beside the note.
/// </summary>
public sealed partial class MainView
{
    public async Task<string?> CreateStudySessionAsync()
    {
        if (!VaultWritable || _store is null) { await Dialogs.AlertAsync("Sessão de estudo", NoVaultMessage); return null; }
        var title = await Dialogs.PromptAsync(new PromptOptions("Nova sessão de estudo", "Título", Placeholder: "Ex.: Física I", Confirm: "Continuar"));
        if (string.IsNullOrWhiteSpace(title)) return null;
        var objective = await Dialogs.PromptAsync(new PromptOptions("Nova sessão de estudo", "Objetivo (opcional)", Confirm: "Escolher fontes", Required: false));
        var session = new StudySession { Title = title.Trim(), Objectives = string.IsNullOrWhiteSpace(objective) ? [] : [objective.Trim()] };
        session = await PickStudySourcesAsync(session);
        var path = "Estudos/" + ArtifactModel.SafeName(session.Title) + StudySession.Suffix;
        for (int n = 2; _store.Get(path) is not null; n++) path = "Estudos/" + ArtifactModel.SafeName(session.Title) + "-" + n + StudySession.Suffix;
        await ((IAssistantApp)this).CreateNoteAsync(path, session.Serialize());
        return path;
    }

    // the vault's files and notes, chosen one by one until Concluir (search in the list from 7 items)
    private async Task<StudySession> PickStudySourcesAsync(StudySession session)
    {
        var all = _store!.List().Where(d => !d.Path.EndsWith(StudySession.Suffix, StringComparison.OrdinalIgnoreCase) && !d.Path.StartsWith("Tutorial/", StringComparison.Ordinal))
            .Select(d => (d.Id, d.Path)).Concat(_attachments.Select(p => (Id: "file:" + p, Path: p))).OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToList();
        while (true)
        {
            var left = all.Where(x => session.Sources.All(s => s.DocumentId != x.Id)).ToList();
            var items = new List<DialogItem> { new("Concluir", Detail: session.Sources.Count == 1 ? "1 fonte" : session.Sources.Count + " fontes") };
            items.AddRange(left.Select(x => new DialogItem(Path.GetFileName(x.Path), Detail: x.Path)));
            var pick = await Dialogs.ChooseAsync("Fontes de " + session.Title, items);
            if (pick is not int i || i == 0) return session;
            session = session.Add(left[i - 1].Id, left[i - 1].Path);
        }
    }

    private string? PathOfSource(string id) =>
        id.StartsWith("file:", StringComparison.Ordinal) ? (_attachments.Contains(id[5..]) ? id[5..] : null) : _store?.Get(id) is { } d && d.Id == id ? d.Path : null;

    public async Task OpenStudySessionAsync(string path)
    {
        if (_store?.Get(path) is not { } file) return;
        StudySession session;
        try { session = StudySession.Parse(file.Content); }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException or NotSupportedException)
        { await Dialogs.AlertAsync("Sessão de estudo", "Não foi possível ler a sessão: " + e.Message); return; }
        var (present, missing) = session.Resolve(PathOfSource);
        var message = (session.Objectives.Count > 0 ? "Objetivo: " + string.Join("; ", session.Objectives) : "") +
            (missing.Count > 0 ? (session.Objectives.Count > 0 ? "\n" : "") + "Não estão mais no vault: " + string.Join(", ", missing.Select(m => Path.GetFileName(m.PathHint))) : "");
        var items = present.Select(p => new DialogItem(Path.GetFileName(p.Path), Detail: p.Path)).Append(new DialogItem("Adicionar fontes")).Append(new DialogItem("Mapa da sessão")).ToList();
        var pick = await Dialogs.ChooseAsync(session.Title, items, message.Length > 0 ? message : null);
        if (pick is not int i) return;
        if (i == present.Count)
        {
            var more = await PickStudySourcesAsync(session);
            if (more.Sources.Count != session.Sources.Count) await SaveNoteAsync(new CityNote(path, more.Serialize()));
            return;
        }
        if (i == present.Count + 1)
        {
            var map = StudyMap.Build(session, present.Select(p => (p.Id, p.Path, ArtifactModel.IsNote(p.Path) ? _contents.GetValueOrDefault(p.Path) : null)).ToList());
            var mapPath = path[..^StudySession.Suffix.Length] + ".grafo.json";
            await SaveNoteAsync(new CityNote(mapPath, map.Serialize()));
            await OpenGraphAsync(mapPath);
            return;
        }
        await StartStudyAsync();
        var source = present[i].Path;
        if (ArtifactModel.IsNote(source) && _contents.ContainsKey(source)) Editor.Pinned.Pin(_contents[source], source);
        else await Editor.Pinned.OpenFileAsync(source);
    }

    /// <summary>The vault's study sessions, to open one (or start a new one).</summary>
    public async Task ShowStudySessionsAsync()
    {
        var sessions = _store?.List().Where(d => d.Path.EndsWith(StudySession.Suffix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        var items = sessions.Select(d => new DialogItem(Path.GetFileName(d.Path)[..^StudySession.Suffix.Length], Detail: d.Path))
            .Append(new DialogItem("Nova sessão de estudo", UrbeTheme.Icons.Plus)).ToList();
        if (await Dialogs.ChooseAsync("Sessões de estudo", items) is not int i) return;
        if (i == sessions.Count) { if (await CreateStudySessionAsync() is { } made) await OpenStudySessionAsync(made); }
        else await OpenStudySessionAsync(sessions[i].Path);
    }
}
