using System.Security.Cryptography;
using System.Text.Json;

namespace Hub.Core;

// Local Hub policy, not a shared capability or an authority for Product build identity.
public sealed record AndroidProductTrust(string ProductId, string PackageId, string Repository,
    string TagPrefix, string CertificateSha256, string ApprovalDecision, string ApprovalOption,
    string IdentitySource, bool PublicDevelopmentKey);
public sealed record AndroidPackageEvidence(string PackageId, long VersionCode, string? VersionName,
    IReadOnlyList<string> CurrentSigners);
public sealed record InstallVerdict(bool Allowed, string Message);

public static class InstallationPolicy
{
    public static string NormalizeFingerprint(string fingerprint) => fingerprint.Replace(":", "").ToUpperInvariant();
    public static AndroidProductTrust? Approved(AndroidProductTrust candidate, Datum<string> decisions)
    {
        // Never accept permission from a cached/stale snapshot or approval of another fingerprint/channel.
        if (decisions.Availability != Availability.Derived || decisions.Value is null ||
            !Valid(candidate)) return null;
        try
        {
            using var doc = JsonDocument.Parse(decisions.Value);
            var matches = doc.RootElement.GetProperty("decisions").EnumerateArray()
                .Where(d => d.GetProperty("id").GetString() == candidate.ApprovalDecision).ToArray();
            if (matches.Length != 1) return null;
            var d = matches[0];
            var prefix = "Alternativa A — " + candidate.ApprovalOption + " (escolhida pelo proprietário pelo portal; Issue ";
            return d.GetProperty("status").GetString() == "decided" &&
                d.GetProperty("record").GetString() == $"docs/governance/responses/{candidate.ApprovalDecision}.md" &&
                d.GetProperty("decision").GetString()?.StartsWith(prefix, StringComparison.Ordinal) == true ? candidate : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { return null; }
    }

    static bool Valid(AndroidProductTrust t) => !string.IsNullOrWhiteSpace(t.ProductId) &&
        System.Text.RegularExpressions.Regex.IsMatch(t.PackageId, @"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$") &&
        NormalizeFingerprint(t.CertificateSha256) is { Length: 64 } f && f.All(Uri.IsHexDigit) &&
        !string.IsNullOrWhiteSpace(t.Repository) && !string.IsNullOrWhiteSpace(t.TagPrefix) &&
        t.ApprovalOption.Contains(t.PackageId, StringComparison.Ordinal) &&
        t.ApprovalOption.Contains(t.Repository, StringComparison.Ordinal) &&
        t.ApprovalOption.Contains(t.TagPrefix, StringComparison.Ordinal) &&
        t.ApprovalOption.Contains(NormalizeFingerprint(t.CertificateSha256), StringComparison.Ordinal);

    public static bool TrustedPackage(AndroidProductTrust trust, AndroidPackageEvidence package) =>
        package.PackageId == trust.PackageId && package.VersionCode > 0 &&
        package.CurrentSigners is { Count: 1 } &&
        NormalizeFingerprint(package.CurrentSigners[0]) == NormalizeFingerprint(trust.CertificateSha256);

    public static InstallVerdict Evaluate(AndroidProductTrust? trust, ArtifactChoice choice,
        AndroidPackageEvidence? candidate, AndroidPackageEvidence? installed, bool explicitConsent)
    {
        if (trust is null) return Denied("Identidade/certificado sem aprovação atual; use o canal independente.");
        if (!explicitConsent) return Denied("Instalação exige ação explícita do usuário.");
        if (ArtifactDownloader.Ineligible(choice) is not null || choice.ProductId != trust.ProductId ||
            choice.Channel.Repository.FullName != trust.Repository || choice.Channel.TagPrefix != trust.TagPrefix ||
            !choice.Tag.StartsWith(trust.TagPrefix, StringComparison.Ordinal))
            return Denied("APK não pertence ao Product/canal aprovado.");
        if (candidate is null || !TrustedPackage(trust, candidate))
            return Denied("Identidade, versão ou assinatura do APK não corresponde à aprovação.");
        if (installed is not null && !TrustedPackage(trust, installed))
            return Denied("App instalado tem assinatura incompatível; não será removido.");
        if (installed is not null && candidate.VersionCode <= installed.VersionCode)
            return Denied("Esta versão já está instalada ou é anterior; downgrade bloqueado.");
        return new(true, trust.PublicDevelopmentKey
            ? "Canal de desenvolvimento com chave pública: o certificado não comprova autoria. O Android pedirá confirmação."
            : "Identidade e certificado conferidos; o Android pedirá confirmação.");
    }

    static InstallVerdict Denied(string reason) => new(false, reason);

    /// <summary>Reconferir/copy limitado dos bytes que serão entregues ao Android. Nunca confia no resultado antigo do download.</summary>
    public static async Task CopyVerifiedAsync(Stream input, Stream output, long expectedBytes, string expectedSha256,
        CancellationToken ct = default)
    {
        if (expectedBytes <= 0 || expectedBytes > ArtifactDownloader.DefaultMaxBytes ||
            expectedSha256.Length != 64 || !expectedSha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Metadados inválidos.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long bytes = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, expectedBytes - bytes + 1)), ct);
            if (read == 0) break;
            bytes += read;
            if (bytes > expectedBytes) throw new InvalidDataException("Tamanho do APK mudou.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (bytes != expectedBytes || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(expectedSha256)))
            throw new InvalidDataException("Bytes do APK mudaram; sessão abandonada.");
        await output.FlushAsync(ct);
    }
}

public static class InstallationResultPolicy
{
    public static bool Matches(int expectedSession, string token, int actualSession, string? actualToken) =>
        expectedSession >= 0 && !string.IsNullOrWhiteSpace(token) && expectedSession == actualSession && token == actualToken;

    public static bool Confirmed(string packageId, long versionCode, string certificate, AndroidPackageEvidence? actual) =>
        actual is not null && versionCode > 0 && actual.PackageId == packageId && actual.VersionCode == versionCode &&
        actual.CurrentSigners is { Count: 1 } &&
        InstallationPolicy.NormalizeFingerprint(actual.CurrentSigners[0]) == InstallationPolicy.NormalizeFingerprint(certificate);
}
