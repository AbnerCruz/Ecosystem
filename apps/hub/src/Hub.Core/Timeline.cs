using System.Text.Json;

namespace Hub.Core;

public enum EntryKind { RoadmapItem, Gate, Decision, Handoff, HumanValidation, PullRequest, Issue, Branch, Release, CiRun }

/// <summary>Uma entrada de Past/Now/Next com a fonte de onde foi derivada. Nada aqui é digitado no Hub.</summary>
public sealed record TimelineEntry(EntryKind Kind, string Id, string Title, string Source, string? Detail = null);

/// <summary>PAST: o que já aconteceu · NOW: o que está acontecendo ou aguarda alguém · NEXT: o que o ROADMAP/decisões declaram como em seguida (MANIFEST §18).</summary>
/// <param name="Notes">Fontes que não puderam ser lidas (ex.: GitHub indisponível). Nunca esconde a falta de dado.</param>
public sealed record PastNowNext(IReadOnlyList<TimelineEntry> Past, IReadOnlyList<TimelineEntry> Now, IReadOnlyList<TimelineEntry> Next, IReadOnlyList<string>? Notes = null)
{
    public IReadOnlyList<string> Notes { get; init; } = Notes ?? [];
}

/// <summary>
/// Deriva Past / Now / Next das fontes de verdade do repositório (ROADMAP, decisions.json, handoffs). Só lê.
/// O Next é derivado — apenas itens abertos do ROADMAP e gates ainda não aprovados; o Hub não fabrica o Next.
/// Issues, PRs, branches e CI dependem da leitura do GitHub (P3-6) e não entram aqui.
/// </summary>
public static class TimelineBuilder
{
    const string RoadmapSource = "ROADMAP.md";
    const string DecisionsSource = "docs/governance/decisions.json";

    /// <param name="handoffs">Pares (caminho relativo, JSON) dos handoffs.</param>
    public static PastNowNext Build(string roadmap, string decisionsJson, IEnumerable<(string Path, string Json)> handoffs)
    {
        var past = new List<TimelineEntry>();
        var now = new List<TimelineEntry>();
        var next = new List<TimelineEntry>();

        var items = RoadmapReader.ReadItems(roadmap);
        var done = items.Where(i => i.State == ItemState.Done).Select(i => i.Id).ToHashSet();

        foreach (var i in items)
        {
            var e = new TimelineEntry(EntryKind.RoadmapItem, i.Id, i.Title, RoadmapSource);
            switch (i.State)
            {
                case ItemState.Done: past.Add(e); break;
                case ItemState.Verified: now.Add(e with { Detail = "aguardando validação/revisão" }); break;
                default:
                    var waiting = i.DependsOn.Where(d => !done.Contains(d)).ToList();
                    next.Add(e with { Detail = waiting.Count == 0 ? "pronta" : "depende de " + string.Join(", ", waiting) });
                    break;
            }
        }

        foreach (var g in RoadmapReader.ReadGates(roadmap))
        {
            var e = new TimelineEntry(EntryKind.Gate, $"gate-fase-{g.Phase}", $"Gate da Fase {g.Phase} — {g.Title}", RoadmapSource);
            switch (g.State)
            {
                case GateState.Approved: past.Add(e); break;
                case GateState.Waiting: now.Add(e with { Detail = "aguardando aprovação" }); break;
                default:
                    // Só entram no Next os gates de fases que o ROADMAP já detalha com itens; fases futuras sem itens não são fabricadas.
                    if (items.Any(i => i.Id.StartsWith($"P{g.Phase}-"))) next.Add(e with { Detail = "não iniciado" });
                    break;
            }
        }

        foreach (var d in ReadDecisions(decisionsJson))
        {
            var e = new TimelineEntry(EntryKind.Decision, d.Id, d.Title, DecisionsSource);
            if (d.Status == "decided") past.Add(e with { Detail = d.DecidedAt });
            else if (d.Status == "pending") now.Add(e with { Detail = d.Blocking ? "aguardando o proprietário (bloqueante)" : "aguardando o proprietário" });
        }

        foreach (var (path, json) in handoffs)
        {
            if (ReadHandoff(json) is not { } h) continue;
            var e = new TimelineEntry(EntryKind.Handoff, h.MessageId, $"{h.TaskId}: {h.State}", path);
            if (h.State == "done") past.Add(e);
            else if (h.State is "working" or "claimed" or "waiting" or "blocked" or "verifying" or "review") now.Add(e);
            foreach (var v in h.PendingHuman)
                now.Add(new TimelineEntry(EntryKind.HumanValidation, h.MessageId, v, path, "validação humana pendente"));
        }

        return new PastNowNext(past, now, next);
    }

    sealed record Decision(string Id, string Status, string Title, string? DecidedAt, bool Blocking);
    sealed record Handoff(string MessageId, string TaskId, string State, IReadOnlyList<string> PendingHuman);

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static IEnumerable<Decision> ReadDecisions(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); } catch (JsonException) { yield break; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("decisions", out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;
            foreach (var d in arr.EnumerateArray())
            {
                if (d.ValueKind != JsonValueKind.Object) continue;
                if (Str(d, "id") is not { } id || Str(d, "status") is not { } status) continue;
                yield return new Decision(id, status, Str(d, "title") ?? id, Str(d, "decidedAt"),
                    d.TryGetProperty("blocking", out var b) && b.ValueKind == JsonValueKind.True);
            }
        }
    }

    static Handoff? ReadHandoff(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            if (Str(r, "message_id") is not { } id || Str(r, "state") is not { } state) return null;
            var pending = new List<string>();
            if (r.TryGetProperty("verification", out var ver) && ver.ValueKind == JsonValueKind.Array)
                foreach (var v in ver.EnumerateArray())
                    if (v.ValueKind == JsonValueKind.Object && Str(v, "kind") == "human" && Str(v, "result") == "pending")
                        pending.Add(Str(v, "check") ?? "validação humana");
            return new Handoff(id, Str(r, "task_id") ?? "?", state, pending);
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>Acrescenta ao Past/Now/Next o que vem do GitHub (P3-6). Só lê; o que não pôde ser lido vira nota, nunca é inventado.</summary>
public static class TimelineGitHub
{
    public static PastNowNext AddGitHub(PastNowNext baseline, GitHubSnapshot gh, string defaultBranch = "main")
    {
        var past = baseline.Past.ToList();
        var now = baseline.Now.ToList();
        var notes = baseline.Notes.ToList();

        void Note(string what, string source, string? why) => notes.Add($"GitHub ({gh.Repository.FullName}): {what} indisponível{(why is null ? "" : $" — {why}")} [{source}]");

        if (gh.PullRequests is { Availability: Availability.Derived, Value: { } prs })
            foreach (var p in prs)
                now.Add(new TimelineEntry(EntryKind.PullRequest, $"PR #{p.Number}", p.Title, p.Url, p.Draft ? $"rascunho · {p.HeadRef}" : p.HeadRef));
        else Note("PRs", gh.PullRequests.Source, gh.PullRequests.Note);

        if (gh.Issues is { Availability: Availability.Derived, Value: { } issues })
            foreach (var i in issues)
                now.Add(new TimelineEntry(EntryKind.Issue, $"#{i.Number}", i.Title, i.Url, i.State is null ? "sem state:" : $"state:{i.State}"));
        else Note("Issues", gh.Issues.Source, gh.Issues.Note);

        if (gh.Branches is { Availability: Availability.Derived, Value: { } branches })
            foreach (var b in branches.Where(b => b.Name != defaultBranch))
                now.Add(new TimelineEntry(EntryKind.Branch, b.Name, b.Name, gh.Branches.Source));
        else Note("branches", gh.Branches.Source, gh.Branches.Note);

        if (gh.CiRuns is { Availability: Availability.Derived, Value: { } runs })
            foreach (var r in runs.GroupBy(r => r.Name).Select(g => g.First())) // a mais recente de cada workflow
                now.Add(new TimelineEntry(EntryKind.CiRun, r.Name, r.Name, r.Url, r.Conclusion ?? r.Status));
        else Note("CI", gh.CiRuns.Source, gh.CiRuns.Note);

        if (gh.Releases is { Availability: Availability.Derived, Value: { } rels })
            foreach (var r in rels)
                past.Add(new TimelineEntry(EntryKind.Release, r.Tag, r.Name ?? r.Tag, r.Url, r.PublishedAt));
        else Note("releases", gh.Releases.Source, gh.Releases.Note);

        return baseline with { Past = past, Now = now, Notes = notes };
    }
}
