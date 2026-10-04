using Android.Content;
using Android.Content.PM;
using Android.Security.Keystore;
using Java.Security;
using Java.Security.Spec;
using System.Security.Cryptography;
using System.Text.Json;

namespace HubApp;

internal sealed record AndroidPeerIdentity(int Uid, string PackageName, string SignerSha256);

internal static class AndroidPeerIdentityResolver
{
    internal static AndroidPeerIdentity? Resolve(Context context, int uid)
    {
        try
        {
            var packages = context.PackageManager?.GetPackagesForUid(uid)?
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray() ?? [];
            if (packages.Length != 1) return null;
            var info = context.PackageManager!.GetPackageInfo(packages[0], PackageInfoFlags.SigningCertificates);
            var signers = info.SigningInfo?.GetApkContentsSigners();
            if (signers is not { Length: 1 }) return null;
            var fingerprint = Convert.ToHexString(SHA256.HashData(signers[0].ToByteArray()!));
            return new(uid, packages[0], fingerprint);
        }
        catch (Exception) { return null; }
    }
}

internal sealed class AndroidInstallationKey
{
    const string StoreName = "AndroidKeyStore";
    readonly string _alias;

    internal AndroidInstallationKey(string alias)
    {
        _alias = alias;
        Ensure();
    }

    KeyStore Store()
    {
        var store = KeyStore.GetInstance(StoreName) ?? throw new InvalidOperationException("Keystore indisponível.");
        store.Load((KeyStore.ILoadStoreParameter?)null);
        return store;
    }

    void Ensure()
    {
        using var store = Store();
        if (store.ContainsAlias(_alias)) return;
        using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, StoreName)
            ?? throw new InvalidOperationException("Gerador EC indisponível.");
        using var curve = new ECGenParameterSpec("secp256r1");
        using var spec = new KeyGenParameterSpec.Builder(_alias, KeyStorePurpose.Sign | KeyStorePurpose.Verify)
            .SetAlgorithmParameterSpec(curve)
            .SetDigests(KeyProperties.DigestSha256)
            .Build();
        generator.Initialize(spec);
        using var pair = generator.GenerateKeyPair();
    }

    internal string PublicKey
    {
        get
        {
            using var store = Store();
            var bytes = store.GetCertificate(_alias)?.PublicKey?.GetEncoded()
                ?? throw new InvalidOperationException("Chave pública indisponível.");
            return Convert.ToBase64String(bytes);
        }
    }

    internal string Sign(byte[] data)
    {
        using var store = Store();
        using var entry = store.GetEntry(_alias, null) as KeyStore.PrivateKeyEntry
            ?? throw new InvalidOperationException("Chave privada indisponível.");
        using var signature = Java.Security.Signature.GetInstance("SHA256withECDSA")
            ?? throw new InvalidOperationException("ECDSA indisponível.");
        signature.InitSign(entry.PrivateKey);
        signature.Update(data);
        return Convert.ToBase64String(signature.Sign()!);
    }

    internal static bool Verify(string publicKey, byte[] data, string encodedSignature)
    {
        try
        {
            var keyBytes = Convert.FromBase64String(publicKey);
            var signatureBytes = Convert.FromBase64String(encodedSignature);
            if (keyBytes.Length > EcosystemIpcProtocol.MaxEncodedKeyBytes ||
                signatureBytes.Length > EcosystemIpcProtocol.MaxEncodedSignatureBytes) return false;
            using var spec = new X509EncodedKeySpec(keyBytes);
            using var factory = KeyFactory.GetInstance(KeyProperties.KeyAlgorithmEc)
                ?? throw new InvalidOperationException();
            using var key = factory.GeneratePublic(spec);
            using var signature = Java.Security.Signature.GetInstance("SHA256withECDSA")
                ?? throw new InvalidOperationException();
            signature.InitVerify(key);
            signature.Update(data);
            return signature.Verify(signatureBytes);
        }
        catch (Exception) { return false; }
    }
}

internal sealed record ApprovedPeer(
    string PairId, string KeyHash, string PublicKey, string PackageName, string SignerSha256, DateTimeOffset ApprovedAt);
internal sealed record PendingPeer(
    string PairId, string KeyHash, string PublicKey, string PackageName, string SignerSha256,
    string ClientNonce, string ProviderNonce, string Code, DateTimeOffset ExpiresAt);
internal sealed record PairingState(ApprovedPeer[] Approved, PendingPeer[] Pending);

internal sealed class EcosystemPairingStore
{
    const int PendingLimit = 4;
    static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(2);
    static readonly object Sync = new();
    readonly string _path;
    readonly AndroidInstallationKey _providerKey;

    internal EcosystemPairingStore(Context context, AndroidInstallationKey providerKey)
    {
        _path = Path.Combine(context.FilesDir!.AbsolutePath, "ecosystem-ipc-pairings-v1.json");
        _providerKey = providerKey;
    }

    PairingState Load()
    {
        try
        {
            if (!File.Exists(_path)) return new([], []);
            return JsonSerializer.Deserialize<PairingState>(File.ReadAllText(_path), EcosystemIpcProtocol.Json)
                ?? new([], []);
        }
        catch (Exception) { return new([], []); }
    }

    void Save(PairingState state)
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, EcosystemIpcProtocol.Json));
        File.Move(temp, _path, true);
    }

    PairingState Clean(PairingState state, DateTimeOffset now) =>
        state with { Pending = state.Pending.Where(x => x.ExpiresAt > now).Take(PendingLimit).ToArray() };

    internal (ApprovedPeer? Approved, PendingPeer? Pending, string? Error) Begin(
        AndroidPeerIdentity identity, string publicKey, string clientNonce)
    {
        if (!EcosystemIpcProtocol.ValidId(clientNonce)) return (null, null, "PROTOCOL_UNSUPPORTED");
        byte[] publicBytes;
        try { publicBytes = Convert.FromBase64String(publicKey); } catch (FormatException) { return (null, null, "PROTOCOL_UNSUPPORTED"); }
        if (publicBytes.Length is 0 or > EcosystemIpcProtocol.MaxEncodedKeyBytes) return (null, null, "PROTOCOL_UNSUPPORTED");
        var keyHash = Convert.ToHexString(SHA256.HashData(publicBytes)).ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        lock (Sync)
        {
            var state = Clean(Load(), now);
            var approved = state.Approved.SingleOrDefault(x =>
                x.KeyHash == keyHash && x.PublicKey == publicKey && x.PackageName == identity.PackageName &&
                x.SignerSha256 == identity.SignerSha256);
            if (approved is not null) { Save(state); return (approved, null, null); }

            var pending = state.Pending.SingleOrDefault(x =>
                x.KeyHash == keyHash && x.PackageName == identity.PackageName && x.SignerSha256 == identity.SignerSha256);
            if (pending is null)
            {
                if (state.Pending.Length >= PendingLimit) { Save(state); return (null, null, "PAIRING_PENDING"); }
                var providerNonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                pending = new PendingPeer(
                    Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                    keyHash, publicKey, identity.PackageName, identity.SignerSha256, clientNonce, providerNonce,
                    EcosystemIpcProtocol.PairCode(publicKey, _providerKey.PublicKey, clientNonce, providerNonce),
                    now.Add(PendingLifetime));
                state = state with { Pending = [.. state.Pending, pending] };
            }
            Save(state);
            return (null, pending, null);
        }
    }

    internal PendingPeer[] Pending()
    {
        lock (Sync)
        {
            var state = Clean(Load(), DateTimeOffset.UtcNow);
            Save(state);
            return state.Pending;
        }
    }

    internal bool Approve(string pairId)
    {
        lock (Sync)
        {
            var state = Clean(Load(), DateTimeOffset.UtcNow);
            var pending = state.Pending.SingleOrDefault(x => x.PairId == pairId);
            if (pending is null) { Save(state); return false; }
            var approved = new ApprovedPeer(pending.PairId, pending.KeyHash, pending.PublicKey,
                pending.PackageName, pending.SignerSha256, DateTimeOffset.UtcNow);
            state = state with
            {
                Approved = [.. state.Approved.Where(x =>
                    !(x.PackageName == pending.PackageName && x.SignerSha256 == pending.SignerSha256)), approved],
                Pending = state.Pending.Where(x => x.PairId != pairId).ToArray()
            };
            Save(state);
            return true;
        }
    }

    internal bool Reject(string pairId)
    {
        lock (Sync)
        {
            var state = Clean(Load(), DateTimeOffset.UtcNow);
            var next = state.Pending.Where(x => x.PairId != pairId).ToArray();
            if (next.Length == state.Pending.Length) { Save(state); return false; }
            Save(state with { Pending = next });
            return true;
        }
    }

    internal ApprovedPeer? Approved(AndroidPeerIdentity identity, string? pairId)
    {
        if (!EcosystemIpcProtocol.ValidId(pairId)) return null;
        lock (Sync)
        {
            var state = Clean(Load(), DateTimeOffset.UtcNow);
            Save(state);
            return state.Approved.SingleOrDefault(x =>
                x.PairId == pairId && x.PackageName == identity.PackageName && x.SignerSha256 == identity.SignerSha256);
        }
    }

    internal ApprovedPeer[] Approved()
    {
        lock (Sync)
        {
            var state = Clean(Load(), DateTimeOffset.UtcNow);
            Save(state);
            return state.Approved;
        }
    }

    internal bool Revoke(string pairId)
    {
        lock (Sync)
        {
            var state = Clean(Load(), DateTimeOffset.UtcNow);
            var next = state.Approved.Where(x => x.PairId != pairId).ToArray();
            if (next.Length == state.Approved.Length) { Save(state); return false; }
            Save(state with { Approved = next });
            return true;
        }
    }
}
