using System.Text;
using System.Text.Json;

namespace MathAuthoring.Core;

/// <summary>
/// The v1 textual representation is strictly data. No polymorphic type names,
/// executable payloads, permissive fallbacks, or second document authority.
/// </summary>
public static class ProjectJson
{
    public const int MaxBytes = 1024 * 1024;
    public const int MaxExpressionDepth = 32;
    public const int MaxExpressions = 10000;
    public const int MaxVariables = 512;
    public const int MaxScenes = 128;
    public const int MaxProfiles = 32;
    public const int MaxObjectIdsPerScene = 4096;

    public static DocumentReadResult Parse(string source)
    {
        if (source is null)
            return Error("json.required", "JSON não pode ser nulo.");
        if (Encoding.UTF8.GetByteCount(source) > MaxBytes)
            return Error("json.too-large", "Documento ultrapassa o limite de 1 MiB.");

        try
        {
            using var json = JsonDocument.Parse(source, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
            var reader = new Reader();
            var document = reader.ReadDocument(json.RootElement);
            var problems = ProjectValidator.Validate(document);
            return problems.Count == 0
                ? new DocumentReadResult(document, Array.Empty<ValidationProblem>())
                : new DocumentReadResult(null, problems);
        }
        catch (JsonException e)
        {
            return Error("json.syntax", $"JSON inválido: {e.Message}");
        }
        catch (DocumentFormatException e)
        {
            return Error(e.Code, e.Message, e.Subject);
        }
        catch (FormatException)
        {
            return Error("json.number.invalid", "Número inválido no JSON.");
        }
        catch (OverflowException)
        {
            return Error("json.number.invalid", "Número fora do intervalo permitido.");
        }
    }

    /// <summary>Produces stable field order while preserving semantic array order.</summary>
    public static string Format(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var problems = ProjectValidator.Validate(document);
        if (problems.Count != 0)
            throw new ArgumentException($"Documento inválido: {problems[0].Code}", nameof(document));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", document.SchemaVersion);
            writer.WriteString("projectId", document.ProjectId);
            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            writer.WriteString("title", document.Metadata.Title);
            if (document.Metadata.Description is null) writer.WriteNull("description");
            else writer.WriteString("description", document.Metadata.Description);
            writer.WriteEndObject();

            writer.WritePropertyName("variables");
            writer.WriteStartArray();
            foreach (var variable in document.Variables)
            {
                writer.WriteStartObject();
                writer.WriteString("id", variable.Id);
                writer.WriteString("name", variable.Name);
                writer.WriteNumber("initialValue", variable.InitialValue);
                writer.WritePropertyName("definition");
                WriteExpression(writer, variable.Definition);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WritePropertyName("scenes");
            writer.WriteStartArray();
            foreach (var scene in document.Scenes)
            {
                writer.WriteStartObject();
                writer.WriteString("id", scene.Id);
                writer.WriteString("name", scene.Name);
                writer.WritePropertyName("objectIds");
                writer.WriteStartArray();
                foreach (var id in scene.ObjectIds) writer.WriteStringValue(id);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WritePropertyName("exportProfiles");
            writer.WriteStartArray();
            foreach (var profile in document.ExportProfiles)
            {
                writer.WriteStartObject();
                writer.WriteString("id", profile.Id);
                writer.WriteString("name", profile.Name);
                writer.WriteNumber("width", profile.Width);
                writer.WriteNumber("height", profile.Height);
                writer.WriteNumber("framesPerSecond", profile.FramesPerSecond);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
        }
        if (stream.Length > MaxBytes)
            throw new ArgumentException("Documento serializado excede o limite de 1 MiB.", nameof(document));
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteExpression(Utf8JsonWriter writer, MathExpression? expression)
    {
        if (expression is null) { writer.WriteNullValue(); return; }
        writer.WriteStartObject();
        switch (expression)
        {
            case ConstantExpression number:
                writer.WriteString("kind", "constant");
                writer.WriteNumber("version", 1);
                writer.WriteNumber("value", number.Value);
                break;
            case VariableReferenceExpression reference:
                writer.WriteString("kind", "variable");
                writer.WriteNumber("version", 1);
                writer.WriteString("variableId", reference.VariableId);
                break;
            case BinaryExpression binary:
                writer.WriteString("kind", "binary");
                writer.WriteNumber("version", 1);
                writer.WriteString("operator", binary.Operator switch
                {
                    BinaryOperator.Add => "add",
                    BinaryOperator.Subtract => "subtract",
                    BinaryOperator.Multiply => "multiply",
                    BinaryOperator.Divide => "divide",
                    BinaryOperator.Power => "power",
                    _ => throw new ArgumentException("Operador desconhecido.")
                });
                writer.WritePropertyName("left");
                WriteExpression(writer, binary.Left);
                writer.WritePropertyName("right");
                WriteExpression(writer, binary.Right);
                break;
            default:
                throw new ArgumentException("Tipo de expressão desconhecido.");
        }
        writer.WriteEndObject();
    }

    private static DocumentReadResult Error(string code, string message, string? subject = null) =>
        new(null, new[] { new ValidationProblem(code, message, subject) });

    private sealed class Reader
    {
        private int _expressionCount;

        public ProjectDocument ReadDocument(JsonElement element)
        {
            Object(element, "$", "schemaVersion", "projectId", "metadata", "variables", "scenes", "exportProfiles");
            var schema = Integer(element, "schemaVersion", "$");
            if (schema != ProductIdentity.CurrentSchemaVersion)
                throw new DocumentFormatException("schema.unsupported",
                    $"Versão {schema} não suportada; documento não foi modificado.", "$.schemaVersion");
            var meta = Field(element, "metadata", "$");
            Object(meta, "$.metadata", "title", "description");
            return new ProjectDocument(
                schema,
                String(element, "projectId", "$"),
                new ProjectMetadata(String(meta, "title", "$.metadata"),
                    OptionalString(meta, "description", "$.metadata")),
                List(element, "variables", "$", MaxVariables, ReadVariable),
                List(element, "scenes", "$", MaxScenes, ReadScene),
                List(element, "exportProfiles", "$", MaxProfiles, ReadProfile));
        }

        private VariableDefinition ReadVariable(JsonElement value, string path)
        {
            Object(value, path, "id", "name", "initialValue", "definition");
            var expr = Field(value, "definition", path);
            return new VariableDefinition(
                String(value, "id", path),
                String(value, "name", path),
                Number(value, "initialValue", path),
                expr.ValueKind == JsonValueKind.Null ? null : ReadExpression(expr, path + ".definition", 0));
        }

        private SceneDocument ReadScene(JsonElement value, string path)
        {
            Object(value, path, "id", "name", "objectIds");
            return new SceneDocument(
                String(value, "id", path),
                String(value, "name", path),
                List(value, "objectIds", path, MaxObjectIdsPerScene, (v, p) => Text(v, p)));
        }

        private ExportProfile ReadProfile(JsonElement value, string path)
        {
            Object(value, path, "id", "name", "width", "height", "framesPerSecond");
            return new ExportProfile(String(value, "id", path), String(value, "name", path),
                Integer(value, "width", path), Integer(value, "height", path),
                Integer(value, "framesPerSecond", path));
        }

        private MathExpression ReadExpression(JsonElement element, string path, int depth)
        {
            if (++_expressionCount > MaxExpressions || depth >= MaxExpressionDepth)
                throw new DocumentFormatException("expression.limit", "Expressão excede o limite de profundidade ou nós.", path);
            Object(element, path, "kind", "version", "value", "variableId", "operator", "left", "right");
            var version = Integer(element, "version", path);
            if (version != 1)
                throw new DocumentFormatException("expression.version.unsupported", "Versão de expressão desconhecida.", path);
            var kind = String(element, "kind", path);
            switch (kind)
            {
                case "constant":
                    ExactFields(element, path, "kind", "version", "value");
                    return new ConstantExpression(Number(element, "value", path));
                case "variable":
                    ExactFields(element, path, "kind", "version", "variableId");
                    return new VariableReferenceExpression(String(element, "variableId", path));
                case "binary":
                    ExactFields(element, path, "kind", "version", "operator", "left", "right");
                    var op = String(element, "operator", path) switch
                    {
                        "add" => BinaryOperator.Add,
                        "subtract" => BinaryOperator.Subtract,
                        "multiply" => BinaryOperator.Multiply,
                        "divide" => BinaryOperator.Divide,
                        "power" => BinaryOperator.Power,
                        _ => throw new DocumentFormatException("expression.operator.unknown", "Operador desconhecido.", path)
                    };
                    return new BinaryExpression(op,
                        ReadExpression(Field(element, "left", path), path + ".left", depth + 1),
                        ReadExpression(Field(element, "right", path), path + ".right", depth + 1));
                default:
                    throw new DocumentFormatException("expression.kind.unknown", "Tipo de expressão desconhecido.", path);
            }
        }

        private static IReadOnlyList<T> List<T>(JsonElement parent, string name, string path, int limit,
            Func<JsonElement, string, T> parse)
        {
            var array = Field(parent, name, path);
            if (array.ValueKind != JsonValueKind.Array)
                throw new DocumentFormatException("json.type", "Esperado array.", path + "." + name);
            if (array.GetArrayLength() > limit)
                throw new DocumentFormatException("json.limit", "Array ultrapassa o limite.", path + "." + name);
            var items = new List<T>();
            foreach (var item in array.EnumerateArray())
                items.Add(parse(item, $"{path}.{name}[{items.Count}]"));
            return items.AsReadOnly();
        }

        private static void Object(JsonElement element, string path, params string[] allowed)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new DocumentFormatException("json.type", "Esperado objeto.", path);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new DocumentFormatException("json.duplicate-key", "Chave duplicada.", path + "." + property.Name);
                if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                    throw new DocumentFormatException("json.unknown-field", "Campo desconhecido.", path + "." + property.Name);
            }
        }

        private static void ExactFields(JsonElement element, string path, params string[] required)
        {
            if (element.EnumerateObject().Count() != required.Length)
                throw new DocumentFormatException("json.unknown-field", "Campos incompatíveis com o tipo de expressão.", path);
            foreach (var name in required) _ = Field(element, name, path);
        }

        private static JsonElement Field(JsonElement element, string name, string path)
        {
            if (!element.TryGetProperty(name, out var property))
                throw new DocumentFormatException("json.missing-field", "Campo obrigatório ausente.", path + "." + name);
            return property;
        }

        private static int Integer(JsonElement element, string name, string path)
        {
            var value = Field(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
                throw new DocumentFormatException("json.type", "Esperado inteiro.", path + "." + name);
            return number;
        }

        private static double Number(JsonElement element, string name, string path)
        {
            var value = Field(element, name, path);
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number))
                throw new DocumentFormatException("json.number.invalid", "Esperado número finito.", path + "." + name);
            return number;
        }

        private static string String(JsonElement element, string name, string path) =>
            Text(Field(element, name, path), path + "." + name);

        private static string? OptionalString(JsonElement element, string name, string path)
        {
            var value = Field(element, name, path);
            return value.ValueKind == JsonValueKind.Null ? null : Text(value, path + "." + name);
        }

        private static string Text(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.String || element.GetString() is not { } text)
                throw new DocumentFormatException("json.type", "Esperado texto.", path);
            if (text.Length > 4096)
                throw new DocumentFormatException("json.limit", "Texto excede 4096 caracteres.", path);
            return text;
        }
    }

    private sealed class DocumentFormatException(string code, string message, string subject)
        : Exception(message)
    {
        public string Code { get; } = code;
        public string Subject { get; } = subject;
    }
}

public sealed record DocumentReadResult(
    ProjectDocument? Document,
    IReadOnlyList<ValidationProblem> Problems)
{
    public bool Success => Document is not null && Problems.Count == 0;
}
