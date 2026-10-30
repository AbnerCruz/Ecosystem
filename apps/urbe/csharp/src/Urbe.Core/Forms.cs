using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

public sealed class FormField
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
    public string? Description { get; set; }
    public string? Group { get; set; }
    public bool Required { get; set; }
    public JsonNode? Initial { get; set; }
    public List<string> Options { get; set; } = [];
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? VisibleWhen { get; set; }
    public JsonNode? EqualsValue { get; set; }
    public List<string> Sum { get; set; } = [];
    public List<FormField> Fields { get; set; } = [];
}

public sealed class FormDefinition
{
    /// <summary>The most fields of one form; note templates ask the same many (CST-045), so a template and its form never disagree.</summary>
    public const int MaxFields = 128;

    public int Version { get; set; } = 1;
    public string Title { get; set; } = "Formulário";
    public string? Description { get; set; }
    public string? SourcePath { get; set; }
    public List<FormField> Fields { get; set; } = [];
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public static FormDefinition Parse(JsonObject input)
    {
        FormDefinition definition;
        try { definition = input.Deserialize<FormDefinition>(Json) ?? throw new JsonException(); }
        catch (Exception e) when (e is JsonException or NotSupportedException) { throw new ArgumentException("Definição de formulário inválida.", e); }
        definition.Check();
        return definition;
    }
    public void Check(int depth = 0)
    {
        if (depth > 4 || Version != 1 || string.IsNullOrWhiteSpace(Title) || Fields.Count == 0 || Fields.Count > MaxFields)
            throw new ArgumentException("Formulário inválido: versão 1, título e 1 a " + MaxFields + " campos são necessários.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Fields)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(f.Id, @"^[\p{L}_][\p{L}\p{N}_-]{0,63}$") || string.IsNullOrWhiteSpace(f.Label) || !ids.Add(f.Id) ||
                !Types.Contains(f.Type) || f.Min > f.Max || f.Options.Distinct(StringComparer.Ordinal).Count() != f.Options.Count)
                throw new ArgumentException("Campo inválido ou repetido: " + f.Id);
            if (f.Type is "select" or "multi_select" && f.Options.Count == 0)
                throw new ArgumentException("Informe as opções: " + f.Id);
            if (f.VisibleWhen is not null && (f.VisibleWhen == f.Id || !ids.Contains(f.VisibleWhen)))
                throw new ArgumentException("Condição deve referenciar um campo anterior: " + f.Id);
            if (f.Type == "repeat") new FormDefinition { Fields = f.Fields }.Check(depth + 1);
            if (f.Sum.Any(id => id == f.Id || !ids.Contains(id)))
                throw new ArgumentException("Soma deve referenciar campos anteriores: " + f.Id);
        }
    }
    private static readonly HashSet<string> Types = ["text", "long_text", "integer", "number", "date", "time", "select", "multi_select", "checkbox", "toggle", "scale", "list", "note", "folder", "color", "calculated", "repeat"];
    public static FormDefinition FromTemplate(string title, string template)
    {
        const string marker = "<!-- urbe-form ";
        var start = template.IndexOf(marker, StringComparison.Ordinal);
        if (start >= 0 && template.IndexOf("-->", start, StringComparison.Ordinal) is var end && end > start)
        {
            var form = Parse(JsonNode.Parse(template[(start + marker.Length)..end]) as JsonObject ?? throw new ArgumentException("Metadados inválidos."));
            var placeholders = NoteTemplateEngine.InputFields(template);
            if (placeholders.Any(id => !form.Fields.Any(f => f.Id == id)))
                throw new ArgumentException("Os metadados não incluem todos os campos do modelo.");
            return form;
        }
        return new() { Title = title, Fields = NoteTemplateEngine.InputFields(template).Select(id => new FormField { Id = id, Label = NoteTemplateEngine.LabelOf(id) }).ToList() };
    }
}

public sealed class FormSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ConversationId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string? SubmissionMessageId { get; set; }
    public string? ToolCallId { get; set; }
    public string State { get; set; } = "pending";
    public FormDefinition Definition { get; set; } = new();
    public JsonObject Values { get; set; } = new();
    public string? SourceHash { get; set; }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static FormSession Create(FormDefinition definition, string conversation = "", string agent = "", string? source = null)
    {
        definition.Check();
        return new() { Definition = definition, ConversationId = conversation, AgentId = agent,
            SourceHash = source is null ? null : Hash(source),
            Values = new JsonObject(definition.Fields.Select(f => KeyValuePair.Create(f.Id, f.Initial?.DeepClone()))) };
    }
    public bool Visible(FormField field) => field.VisibleWhen is null || JsonNode.DeepEquals(Values[field.VisibleWhen], field.EqualsValue);
    public JsonObject Answers()
    {
        var result = new JsonObject();
        foreach (var f in Definition.Fields)
        {
            if (!Visible(f)) continue;
            if (f.Type == "calculated")
                result[f.Id] = f.Sum.Sum(id => double.TryParse((result[id] ?? Values[id])?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0);
            else result[f.Id] = Values[f.Id]?.DeepClone();
        }
        return result;
    }
    public Dictionary<string, string> Validate(IReadOnlyCollection<string>? notes = null, IReadOnlyCollection<string>? folders = null)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Definition.Fields.Where(Visible))
            if (ErrorFor(f, notes, folders) is { } error) errors[f.Id] = error;
        return errors;
    }

    /// <summary>
    /// What is wrong with a field's value, in words the person can act on (CST-047), or null when it is fine. An empty
    /// field is only an error when it is required.
    /// </summary>
    public string? ErrorFor(FormField f, IReadOnlyCollection<string>? notes = null, IReadOnlyCollection<string>? folders = null)
    {
        var v = Answers()[f.Id];
        var text = v?.ToString() ?? "";
        if (v is null || string.IsNullOrWhiteSpace(text) || v is JsonArray { Count: 0 })
            return f.Required ? "Preencha este campo." : null;
        if (f.Type is "integer" or "number" or "scale" or "calculated")
        {
            if (!Number(v, out var n)) return "Use um número.";
            if (f.Type == "integer" && n != Math.Truncate(n)) return "Use um número inteiro.";
            if (!InRange(n, f))
                return f.Min is { } lo && f.Max is { } hi ? "Use um valor entre " + Show(lo) + " e " + Show(hi) + "."
                    : f.Min is { } min ? "Use um valor a partir de " + Show(min) + "." : "Use um valor até " + Show(f.Max!.Value) + ".";
            return null;
        }
        bool ok = f.Type switch
        {
            "checkbox" or "toggle" => v is JsonValue j && j.TryGetValue<bool>(out _),
            "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "time" => TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "select" => f.Options.Contains(text, StringComparer.Ordinal),
            "multi_select" => v is JsonArray a && a.All(x => x is JsonValue j && j.TryGetValue<string>(out var s) && f.Options.Contains(s, StringComparer.Ordinal)),
            "list" => v is JsonArray a && a.All(x => x is JsonValue j && j.TryGetValue<string>(out _)),
            "repeat" => v is JsonArray a && (f.Min is null || a.Count >= f.Min) && (f.Max is null || a.Count <= f.Max) && a.All(item => item is JsonObject row && new FormSession { Definition = new() { Fields = f.Fields }, Values = row }.Validate(notes, folders).Count == 0),
            "note" => notes is null || notes.Contains(text),
            "folder" => folders is null || folders.Contains(text),
            "color" => text.Length == 7 && text[0] == '#' && int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
            _ => v is JsonValue j && j.TryGetValue<string>(out _) && (f.Min is null || text.Length >= f.Min) && (f.Max is null || text.Length <= f.Max)
        };
        return ok ? null : "Valor inválido para " + f.Label + ".";
    }

    // the person's way of writing numbers (pt-BR): 0,5
    private static string Show(double n) => n.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));

    public Dictionary<string, string> TemplateValues() => Answers().ToDictionary(item => item.Key, item => item.Value switch
    {
        JsonArray list => string.Join("\n", list.Select(x => x?.ToString() ?? "")),
        JsonValue value when value.TryGetValue<bool>(out var flag) => flag ? "Sim" : "Não",
        _ => item.Value?.ToString() ?? ""
    }, StringComparer.Ordinal);
    private static bool Number(JsonNode? value, out double number)
    {
        number = 0;
        return value is JsonValue v && v.GetValueKind() == JsonValueKind.Number &&
            double.TryParse(v.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }
    private static bool InRange(double n, FormField f) => (f.Min is null || n >= f.Min) && (f.Max is null || n <= f.Max);
}

/// <summary>CST-047: what the native form's editors let into a value — numbers only in number fields, every ticked option kept.</summary>
public static class FormInput
{
    /// <summary>The typed text with only what a number may have: digits, one leading minus and (unless integer) one decimal mark.</summary>
    public static string NumberText(string? typed, bool integer)
    {
        var sb = new System.Text.StringBuilder();
        bool mark = false;
        foreach (var c in typed ?? "")
        {
            if (char.IsAsciiDigit(c)) sb.Append(c);
            else if (c == '-' && sb.Length == 0) sb.Append(c);
            else if (!integer && !mark && c is ',' or '.') { sb.Append(c); mark = true; }
        }
        return sb.ToString();
    }

    /// <summary>The number of a field's text (comma or point), or null while it is not one yet ("", "-").</summary>
    public static double? ParseNumber(string? text) =>
        double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;

    /// <summary>Ticks or unticks one option of a multiple choice, keeping the others (in the options' order, never doubled).</summary>
    public static void Toggle(FormSession session, FormField field, string option, bool on)
    {
        var chosen = (session.Values[field.Id] as JsonArray ?? []).Select(x => x?.ToString() ?? "").ToHashSet(StringComparer.Ordinal);
        if (on) chosen.Add(option); else chosen.Remove(option);
        session.Values[field.Id] = new JsonArray([.. field.Options.Where(chosen.Contains).Select(x => (JsonNode?)JsonValue.Create(x))]);
    }
}
