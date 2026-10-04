using Android.Content;
using System.Text.Json;

namespace HubApp;

internal sealed record AndroidBindingTrustEndpoint(
    string PackageId, string CertificateSha256, bool PublicDevelopmentKey);

internal sealed record AndroidBindingTrustCaller(
    string PackageId, string CertificateSha256, bool PublicDevelopmentKey,
    IpcContextStep[] Context, string[] Grants);

internal sealed record AndroidBindingTrustDocument(
    int SchemaVersion,
    string Binding,
    string ServiceAction,
    AndroidBindingTrustEndpoint Provider,
    AndroidBindingTrustCaller[] Callers);

internal sealed class AndroidBindingTrustPolicy
{
    readonly AndroidBindingTrustDocument _document;

    AndroidBindingTrustPolicy(AndroidBindingTrustDocument document) => _document = document;

    internal static AndroidBindingTrustPolicy? Load(Context context)
    {
        try
        {
            using var stream = context.Assets?.Open("ecosystem/android-binder-v1.trust.json");
            if (stream is null) return null;
            var document = JsonSerializer.Deserialize<AndroidBindingTrustDocument>(stream, EcosystemIpcProtocol.Json);
            if (document is null || document.SchemaVersion != 1 ||
                document.Binding != "android-binder-v1" ||
                document.ServiceAction != EcosystemIpcProtocol.ServiceAction ||
                document.Provider is null || document.Callers is null ||
                !Valid(document.Provider) || document.Callers.Length == 0 ||
                document.Callers.Any(x => !Valid(x) || !ValidContext(x.Context) || x.Grants is null ||
                    x.Grants.Any(g => string.IsNullOrWhiteSpace(g) || g.Length > 128)))
                return null;
            return new AndroidBindingTrustPolicy(document);
        }
        catch (Exception) { return null; }
    }

    internal AndroidBindingTrustCaller? Caller(AndroidPeerIdentity peer) =>
        _document.Callers.SingleOrDefault(x =>
            x.PackageId == peer.PackageName &&
            Normalize(x.CertificateSha256) == Normalize(peer.SignerSha256));

    internal bool AllowsCaller(AndroidPeerIdentity peer) => Caller(peer) is not null;

    internal AndroidBindingTrustEndpoint Provider => _document.Provider;

    static bool Valid(AndroidBindingTrustEndpoint endpoint) =>
        !string.IsNullOrWhiteSpace(endpoint.PackageId) &&
        Normalize(endpoint.CertificateSha256) is { Length: 64 } fingerprint &&
        fingerprint.All(Uri.IsHexDigit);

    static bool Valid(AndroidBindingTrustCaller caller) =>
        !string.IsNullOrWhiteSpace(caller.PackageId) &&
        Normalize(caller.CertificateSha256) is { Length: 64 } fingerprint &&
        fingerprint.All(Uri.IsHexDigit);

    static bool ValidContext(IpcContextStep[]? steps) =>
        steps is { Length: > 0 and <= 5 } &&
        steps.All(x => !string.IsNullOrWhiteSpace(x.Level) && x.Level.Length <= 64 &&
                       !string.IsNullOrWhiteSpace(x.Id) && x.Id.Length <= 128);

    internal static string Normalize(string value) => value.Replace(":", "").ToUpperInvariant();
}
