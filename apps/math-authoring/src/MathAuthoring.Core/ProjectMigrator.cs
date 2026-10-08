using System.Text;
using System.Text.Json;

namespace MathAuthoring.Core;

/// <summary>
/// Opt-in upgrade pipeline. No legacy format is implicitly supported:
/// migrations are trusted Product code, never loaded from an imported project.
/// A migration only returns a candidate, which must pass the current v1 parser.
/// </summary>
public static class ProjectMigrator
{
    public static DocumentReadResult Open(string source, params DocumentMigration[] steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (source is null)
            return Error("json.required", "JSON não pode ser nulo.");

        var current = source;
        for (var attempts = 0; attempts <= 16; attempts++)
        {
            if (Encoding.UTF8.GetByteCount(current) > ProjectJson.MaxBytes)
                return Error("json.too-large", "Documento ultrapassa o limite de 1 MiB.");

            var inspected = ReadVersion(current);
            if (inspected.Problem is not null)
                return new DocumentReadResult(null, new[] { inspected.Problem });
            var version = inspected.Version;
            if (version == ProductIdentity.CurrentSchemaVersion)
                return ProjectJson.Parse(current);

            // Future inputs are never downgraded or overwritten.
            if (version < 0 || version > ProductIdentity.CurrentSchemaVersion)
                return Error("schema.unsupported", $"Versão {version} não suportada.");

            if (attempts == 16)
                return Error("migration.limit", "Cadeia de migrações excede 16 passos.");
            var applicable = steps.Where(s => s.FromVersion == version).ToArray();
            if (applicable.Length == 0)
                return Error("migration.missing", $"Não há migração registrada para schema {version}.");
            if (applicable.Length > 1)
                return Error("migration.ambiguous", "Há duas migrações concorrentes para a mesma versão.");
            var step = applicable[0];
            if (step.ToVersion != version + 1 || step.Upgrade is null)
                return Error("migration.invalid-step", "Migrações devem avançar exatamente uma versão.");

            try
            {
                current = step.Upgrade(current);
                if (current is null)
                    return Error("migration.failed", "Migração retornou documento nulo.");
            }
            catch (Exception e)
            {
                // Trusted code can fail, but the original input is untouched.
                return Error("migration.failed", $"Migração falhou: {e.GetType().Name}.");
            }
        }
        return Error("migration.limit", "Cadeia de migrações excedeu o limite.");
    }

    private static (int Version, ValidationProblem? Problem) ReadVersion(string json)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 64,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                return (0, new("json.type", "Raiz precisa ser objeto."));
            // Detect duplicate keys across all nesting levels, not only at root:
            // a migration must not be able to erase ambiguous source data.
            var duplicate = DuplicateProperty(parsed.RootElement);
            if (duplicate is not null)
                return (0, new("json.duplicate-key", "Chave duplicada.", duplicate));
            if (!parsed.RootElement.TryGetProperty("schemaVersion", out var element) ||
                element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var version))
                return (0, new("json.schema.required", "schemaVersion inteiro é obrigatório."));
            return (version, null);
        }
        catch (JsonException e)
        {
            return (0, new("json.syntax", $"JSON inválido: {e.Message}"));
        }
    }

    private static string? DuplicateProperty(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!seen.Add(property.Name)) return property.Name;
                    var nested = DuplicateProperty(property.Value);
                    if (nested is not null) return nested;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = DuplicateProperty(item);
                    if (nested is not null) return nested;
                }
                break;
        }
        return null;
    }

    private static DocumentReadResult Error(string code, string message) =>
        new(null, new[] { new ValidationProblem(code, message) });
}

/// <summary>A trusted, explicit one-version upgrade shipped in the application binary.</summary>
public sealed record DocumentMigration(
    int FromVersion,
    int ToVersion,
    Func<string, string> Upgrade);
