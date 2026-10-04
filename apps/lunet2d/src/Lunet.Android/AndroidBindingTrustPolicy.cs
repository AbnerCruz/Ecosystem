using Android.Content;
using System.Text.Json;

namespace Lunet.Android;

internal sealed record AndroidBindingTrustEndpoint(
    string PackageId, string CertificateSha256, bool PublicDevelopmentKey);

internal sealed record AndroidBindingTrustDocument(
    int SchemaVersion,
    string Binding,
    string ServiceAction,
    AndroidBindingTrustEndpoint Provider,
    AndroidBindingTrustEndpoint[] Callers);

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
                document.Callers.Any(x => !Valid(x)))
                return null;
            return new AndroidBindingTrustPolicy(document);
        }
        catch (Exception) { return null; }
    }

    internal bool AllowsProvider(string packageName, string signerSha256) =>
        _document.Provider.PackageId == packageName &&
        Normalize(_document.Provider.CertificateSha256) == Normalize(signerSha256);

    static bool Valid(AndroidBindingTrustEndpoint endpoint) =>
        !string.IsNullOrWhiteSpace(endpoint.PackageId) &&
        Normalize(endpoint.CertificateSha256) is { Length: 64 } fingerprint &&
        fingerprint.All(Uri.IsHexDigit);

    static string Normalize(string value) => value.Replace(":", "").ToUpperInvariant();
}
