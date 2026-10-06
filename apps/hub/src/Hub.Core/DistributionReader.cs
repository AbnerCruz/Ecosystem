using System.Text.Json;

namespace Hub.Core;

/// <summary>Resolve somente o perfil current. Não transforma a direção target em um canal operacional.</summary>
public static class DistributionReader
{
    public const string ProfilePath = "docs/distribution/current.profile.json";

    public static Datum<ReleaseChannel> ReleaseChannelFor(ProductSummary product, Datum<string> profile, RepositoryRef ecosystem)
    {
        var source = $"{profile.Source}#entries.{product.Id}.channels";
        if (profile.Value is null) return Datum<ReleaseChannel>.Missing(source, profile.Note ?? "perfil de distribuição indisponível");
        try
        {
            using var d = JsonDocument.Parse(profile.Value);
            var root = d.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Str(root, "status") != "current"
                || !root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return Datum<ReleaseChannel>.Missing(source, "perfil de distribuição não operacional ou inválido");
            var matches = entries.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object && Str(e, "component") == product.Id).ToList();
            if (matches.Count != 1) return Datum<ReleaseChannel>.Missing(source, matches.Count == 0 ? "distribuição não declarada" : "distribuição ambígua");
            var entry = matches[0];
            if (Str(entry, "availability") == "unavailable") return Datum<ReleaseChannel>.Missing(source, "Product não distribuído neste perfil");
            if (!entry.TryGetProperty("channels", out var channels) || channels.ValueKind != JsonValueKind.Array)
                return Datum<ReleaseChannel>.Missing(source, "canal de releases não declarado");
            var releases = channels.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.Object
                && Str(c, "kind") == "github-release" && Str(c, "role") == "primary").ToList();
            if (releases.Count != 1) return Datum<ReleaseChannel>.Missing(source,
                releases.Count == 0 ? "canal GitHub Releases primário não declarado" : "canal GitHub Releases primário ambíguo");
            var location = Str(releases[0], "locationFrom") switch
            {
                "source.repository" => product.Repository.Value,
                "ecosystem.repository" => $"https://github.com/{ecosystem.FullName}",
                "publicUrl" => product.PublicUrl.Value,
                _ => null,
            };
            var repo = Uri.TryCreate(location, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                ? RepositoryRef.TryParse(location) : null;
            if (repo is null) return Datum<ReleaseChannel>.Missing(source, "localização declarada não resolve um repositório GitHub");
            var ownRepo = repo.FullName.Equals(ecosystem.FullName, StringComparison.OrdinalIgnoreCase);
            return Datum<ReleaseChannel>.From(new(repo, $"https://github.com/{repo.FullName}/releases", ownRepo ? product.Id + "-v" : null), source);
        }
        catch (JsonException) { return Datum<ReleaseChannel>.Missing(source, "perfil de distribuição inválido"); }
    }

    static string? Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
