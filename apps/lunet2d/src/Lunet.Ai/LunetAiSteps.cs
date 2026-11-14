using System.Text.Json;
using AgentRuntime;

namespace Lunet.Ai;

public enum LunetAiStepKind { Read, Search, Edit, Create, Compile, Docs, Image, Style, Art }
public enum LunetAiStepStatus { Running, Done, Failed }

/// <summary>
/// One visible thing the agent did (LUNET-490): people need to see which files it read and changed and what it ran,
/// instead of "coding blind". Labels are written for the person, not for logs.
/// </summary>
public sealed record LunetAiStep(string Id, LunetAiStepKind Kind, string Label, string? Detail, LunetAiStepStatus Status, string? Error = null)
{
    /// <summary>"Explorou 2 itens · editou 1 arquivo · compilou 1×" — the collapsed line shown after the run.</summary>
    public static string Summarize(IEnumerable<LunetAiStep> steps)
    {
        var finished = steps.GroupBy(s => s.Id).Select(g => g.Last()).ToArray();
        int explored = finished.Count(s => s.Kind is LunetAiStepKind.Read or LunetAiStepKind.Search or LunetAiStepKind.Docs
                                                 or LunetAiStepKind.Image or LunetAiStepKind.Style);
        int edited = finished.Where(s => s.Kind is LunetAiStepKind.Edit or LunetAiStepKind.Create or LunetAiStepKind.Art && s.Status == LunetAiStepStatus.Done)
            .Select(s => s.Detail).Distinct().Count();
        int compiled = finished.Count(s => s.Kind == LunetAiStepKind.Compile);
        var parts = new List<string>();
        if (explored > 0) parts.Add($"Explorou {explored} {(explored == 1 ? "item" : "itens")}");
        if (edited > 0) parts.Add($"editou {edited} {(edited == 1 ? "arquivo" : "arquivos")}");
        if (compiled > 0) parts.Add($"compilou {compiled}×");
        var text = string.Join(" · ", parts);
        return text.Length == 0 ? "Respondeu sem usar ferramentas" : char.ToUpperInvariant(text[0]) + text[1..];
    }

    internal static (LunetAiStepKind Kind, string Label, string? Detail) Describe(string tool, JsonElement args)
    {
        string Arg(string key) => args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        var path = Arg("path");
        return tool switch
        {
            LunetAiProjectTool.ListName => (LunetAiStepKind.Read, "Listando os arquivos do projeto", null),
            LunetAiProjectTool.ReadName or LunetAiProjectTool.ExcerptName => (LunetAiStepKind.Read, "Lendo " + path, path),
            LunetAiProjectTool.SearchName => (LunetAiStepKind.Search, $"Procurando \"{Arg("query")}\"", Arg("query")),
            LunetAiProjectTool.WriteName => (LunetAiStepKind.Edit, "Escrevendo " + path, path),
            LunetAiProjectTool.ReplaceName => (LunetAiStepKind.Edit, "Editando " + path, path),
            LunetAiBuildTool.Name => (LunetAiStepKind.Compile, "Compilando o projeto", null),
            LunetAiDocsTool.SearchName => (LunetAiStepKind.Docs, $"Estudando a documentação: \"{Arg("query")}\"", Arg("query")),
            LunetAiDocsTool.ReadName => (LunetAiStepKind.Docs, "Lendo " + Arg("id"), Arg("id")),
            LunetAiImageTool.Name => (LunetAiStepKind.Image, "Olhando " + path, path),
            LunetAiStyleTool.Name => (LunetAiStepKind.Style, "Lendo o estilo do projeto", null),
            LunetAiSpriteTool.Name => (LunetAiStepKind.Art, "Desenhando " + path, path),
            _ => (LunetAiStepKind.Read, tool, null),
        };
    }
}

/// <summary>Reports every tool call as a <see cref="LunetAiStep"/> (running → done/failed).</summary>
internal sealed class ReportingTool(ITool inner, IProgress<LunetAiStep> progress) : ITool
{
    public ToolDescriptor Descriptor => inner.Descriptor;

    public async ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        var (kind, label, detail) = LunetAiStep.Describe(Descriptor.CapabilityId, invocation.Arguments);
        var id = invocation.CallId;
        progress.Report(new LunetAiStep(id, kind, label, detail, LunetAiStepStatus.Running));
        try
        {
            var outcome = await inner.InvokeAsync(invocation, context, cancellationToken);
            if (kind == LunetAiStepKind.Compile && !outcome.IsError && outcome.Content.Contains("\"success\":false", StringComparison.Ordinal))
                progress.Report(new LunetAiStep(id, kind, "Compilação com erros", detail, LunetAiStepStatus.Failed));
            else
                progress.Report(new LunetAiStep(id, kind, label, detail, outcome.IsError ? LunetAiStepStatus.Failed : LunetAiStepStatus.Done,
                    outcome.IsError ? (outcome.Content.Length > 160 ? outcome.Content[..160] + "…" : outcome.Content) : null));
            return outcome;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress.Report(new LunetAiStep(id, kind, label, detail, LunetAiStepStatus.Failed, ex.Message));
            throw;
        }
    }
}
