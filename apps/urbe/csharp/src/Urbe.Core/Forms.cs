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
        if (depth > 4 || Version != 1 || string.IsNullOrWhiteSpace(Title) || Fields.Count == 0 || Fields.Count > 128)
            throw new ArgumentException("Formulário inválido: versão 1, título e 1 a 128 campos são necessários.");
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
        return new() { Title = title, Fields = NoteTemplateEngine.InputFields(template).Select(id => new FormField { Id = id, Label = id }).ToList() };
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
        var answers = Answers();
        foreach (var f in Definition.Fields.Where(Visible))
        {
            var v = answers[f.Id];
            var text = v?.ToString() ?? "";
            if (v is null || string.IsNullOrWhiteSpace(text) || v is JsonArray { Count: 0 })
            { if (f.Required) errors[f.Id] = "Preencha este campo."; continue; }
            bool ok = f.Type switch
            {
                "integer" => Number(v, out var n) && n == Math.Truncate(n) && InRange(n, f),
                "number" or "scale" or "calculated" => Number(v, out var n) && InRange(n, f),
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
            if (!ok) errors[f.Id] = "Valor inválido para " + f.Label + ".";
        }
        return errors;
    }
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
