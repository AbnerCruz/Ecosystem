using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HubApp;

internal static class EcosystemIpcProtocol
{
    internal const string ServiceAction = "org.ecosystem.capability.HOST_V1";
    internal const string Descriptor = "org.ecosystem.capability.android.IHostV1";
    internal const int PairBegin = global::Android.OS.IBinder.FirstCallTransaction + 0;
    internal const int Challenge = global::Android.OS.IBinder.FirstCallTransaction + 1;
    internal const int Open = global::Android.OS.IBinder.FirstCallTransaction + 2;
    internal const int Discover = global::Android.OS.IBinder.FirstCallTransaction + 3;
    internal const int Invoke = global::Android.OS.IBinder.FirstCallTransaction + 4;
    internal const int Cancel = global::Android.OS.IBinder.FirstCallTransaction + 5;
    internal const int Close = global::Android.OS.IBinder.FirstCallTransaction + 6;
    internal const int MaxFrameBytes = 256 * 1024;
    internal const int MaxEncodedKeyBytes = 2048;
    internal const int MaxEncodedSignatureBytes = 2048;
    internal const int MaxIdLength = 128;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    internal static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > MaxFrameBytes) return default;
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return default; }
    }

    internal static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    internal static byte[] SignBytes(IpcRequest r)
    {
        static string H(string? value) => EcosystemIpcProtocol.Hash(value ?? "");
        var canonical = string.Join("|", new[]
        {
            "ecosystem-binder/1", r.Op ?? "", r.PairId ?? "", r.SessionId ?? "", r.RequestId ?? "",
            r.ChallengeId ?? "", H(r.Challenge), H(r.ContextJson), r.Capability ?? "",
            r.CapabilityOperation ?? "", r.MinimumVersion ?? "", r.DeadlineMs?.ToString() ?? "", H(r.InputJson)
        });
        return Encoding.UTF8.GetBytes(canonical);
    }

    internal static byte[] ProviderProof(string pairId, string challengeId, string challenge, string clientKeyHash) =>
        Encoding.UTF8.GetBytes($"provider|{pairId}|{challengeId}|{Hash(challenge)}|{clientKeyHash}");

    internal static string PairCode(string clientPublicKey, string providerPublicKey, string clientNonce, string providerNonce)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"pair|{clientPublicKey}|{providerPublicKey}|{clientNonce}|{providerNonce}"));
        var value = ((uint)hash[0] << 24) | ((uint)hash[1] << 16) | ((uint)hash[2] << 8) | hash[3];
        return (value % 1_000_000).ToString("D6");
    }

    internal static bool ValidId(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxIdLength;
}

internal sealed record IpcRequest(
    string? Op = null,
    string? PairId = null,
    string? SessionId = null,
    string? RequestId = null,
    string? PublicKey = null,
    string? ClientNonce = null,
    string? ChallengeId = null,
    string? Challenge = null,
    string? ContextJson = null,
    string? Capability = null,
    string? CapabilityOperation = null,
    string? MinimumVersion = null,
    int? DeadlineMs = null,
    string? InputJson = null,
    string? Signature = null);

internal sealed record IpcResponse(
    bool Ok,
    string? TransportError = null,
    string? HostError = null,
    string? State = null,
    string? PairId = null,
    string? PairCode = null,
    string? ProviderPublicKey = null,
    string? ProviderNonce = null,
    string? ChallengeId = null,
    string? Challenge = null,
    string? ProviderSignature = null,
    string? SessionId = null,
    string? OutputJson = null,
    IpcCapability[]? Capabilities = null);

internal sealed record IpcCapability(string Id, string Version, string Operation, string Lifecycle);
internal sealed record IpcContext(IpcContextStep[] Steps);
internal sealed record IpcContextStep(string Level, string Id);
