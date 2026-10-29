namespace Urbe.Core.Assistant;

/// <summary>
/// CST-115: what the question card of ask_user shows besides the question. The counts and the understanding are the
/// agent's own declaration (an estimate, never a measurement); the doubts still open are its short list; the change is
/// read against the previous question of the same conversation.
/// </summary>
public sealed record QuestionProgress(int Resolved, int Total, int? Understanding, IReadOnlyList<string> Pending, string? Change)
{
    public string Summary =>
        Resolved + " de " + Total + (Total == 1 ? " dúvida esclarecida" : " dúvidas esclarecidas") +
        (Understanding is { } u ? " · compreensão declarada " + u + "%" : "");

    public static QuestionProgress From(System.Text.Json.Nodes.JsonObject input, System.Text.Json.Nodes.JsonObject? previous)
    {
        var (resolved, total, understanding, pending) = Read(input);
        string? change = null;
        if (previous is not null)
        {
            var (before, _, _, open) = Read(previous);
            var parts = new List<string>();
            if (resolved > before) parts.Add((resolved - before) + (resolved - before == 1 ? " esclarecida" : " esclarecidas"));
            var fresh = pending.Where(p => !open.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
            if (fresh.Count > 0) parts.Add(fresh.Count + (fresh.Count == 1 ? " nova" : " novas") + " (" + string.Join(", ", fresh) + ")");
            if (parts.Count > 0) change = "Desde a pergunta anterior: " + string.Join(" · ", parts);
        }
        return new QuestionProgress(resolved, total, understanding, pending, change);
    }

    private static (int Resolved, int Total, int? Understanding, List<string> Pending) Read(System.Text.Json.Nodes.JsonObject o)
    {
        int total = Math.Max(1, (int)LegacyCustomize.JsToNumber(o["total"]));
        int resolved = Math.Clamp((int)LegacyCustomize.JsToNumber(o["resolved"]), 0, total);
        int? understanding = o["understanding"] is null ? null : Math.Clamp((int)LegacyCustomize.JsToNumber(o["understanding"]), 0, 100);
        var pending = (o["pending"] as System.Text.Json.Nodes.JsonArray ?? [])
            .Select(x => LegacyCustomize.JsTrim(x is null ? "" : LegacyCustomize.JsToString(x)))
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
        return (resolved, total, understanding, pending);
    }
}
