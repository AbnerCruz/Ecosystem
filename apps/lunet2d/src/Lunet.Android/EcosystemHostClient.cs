using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Security.Keystore;
using Java.Security;
using Java.Security.Spec;
using System.Security.Cryptography;
using System.Text.Json;

namespace Lunet.Android;

internal sealed class EcosystemHostClient : IDisposable
{
    const string KeyAlias = "ecosystem.ipc.caller.v1";
    const string TrustFile = "ecosystem-ipc-provider-v1.json";
    const string PendingTrustFile = "ecosystem-ipc-provider-pending-v1.json";
    readonly Context _context;
    readonly AndroidInstallationKey _key = new(KeyAlias);
    readonly AndroidBindingTrustPolicy? _trustPolicy;
    readonly LifecycleToken _lifecycleToken = new();
    HostServiceConnection? _connection;
    IBinder? _binder;
    ProviderCandidate? _candidate;
    PairCandidate? _pair;
    string? _sessionId;
    bool _disposed;

    internal EcosystemHostClient(Context context)
    {
        _context = context.ApplicationContext!;
        _trustPolicy = AndroidBindingTrustPolicy.Load(_context);
    }

    internal async Task<HostTestResult> ConnectAndInspectAsync(string text, CancellationToken ct)
    {
        if (_disposed) return new(false, "Cliente encerrado.");
        if (text.Length > 100_000) return new(false, "Texto excede o limite de text.inspect.");

        try
        {
            var candidate = ResolveProvider();
            if (candidate is null) return new(false, "Nenhum provider confiável e compatível foi encontrado.");
            var savedTrust = ReadTrust();
            if (savedTrust is not null &&
                (savedTrust.PackageName != candidate.PackageName || savedTrust.SignerSha256 != candidate.SignerSha256))
                return new(false, "A identidade do provider mudou. Pareamento antigo foi recusado; use Esquecer conexão para parear novamente.");

            await BindAsync(candidate, ct);
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            var pair = Rpc(EcosystemIpcProtocol.PairBegin, new IpcRequest(
                Op: "pair.begin", PublicKey: _key.PublicKey, ClientNonce: nonce));
            if (!pair.Ok || pair.PairId is null || pair.ProviderPublicKey is null)
                return Error(pair);

            _candidate = candidate;
            _pair = new PairCandidate(pair.PairId, pair.ProviderPublicKey);

            var pendingTrust = ReadPendingTrust();
            if (pair.State == "pending")
            {
                if (pair.PairCode is null)
                    return new(false, "Provider não apresentou código de pareamento.");
                var proposed = new PendingProviderTrust(
                    candidate.PackageName, candidate.SignerSha256, pair.ProviderPublicKey, pair.PairId, pair.PairCode);
                if (pendingTrust is not null &&
                    (pendingTrust.PackageName != proposed.PackageName ||
                     pendingTrust.SignerSha256 != proposed.SignerSha256 ||
                     pendingTrust.PublicKey != proposed.PublicKey))
                    return new(false, "O provider mudou durante o pareamento. A conexão foi recusada; use Esquecer conexão para reiniciar.");
                WritePendingTrust(proposed);
                return new(false, $"Pareamento aguardando aprovação. Compare o código {pair.PairCode} no provider e aprove; depois toque novamente.", pair.PairCode);
            }

            if (pair.State != "approved") return new(false, "Estado de pareamento inválido.");
            var alreadyTrusted = savedTrust is not null &&
                savedTrust.PackageName == candidate.PackageName &&
                savedTrust.SignerSha256 == candidate.SignerSha256 &&
                savedTrust.PublicKey == pair.ProviderPublicKey &&
                savedTrust.PairId == pair.PairId;
            var matchesPending = pendingTrust is not null &&
                pendingTrust.PackageName == candidate.PackageName &&
                pendingTrust.SignerSha256 == candidate.SignerSha256 &&
                pendingTrust.PublicKey == pair.ProviderPublicKey &&
                pendingTrust.PairId == pair.PairId;
            if (!alreadyTrusted && !matchesPending)
                return new(false, "A aprovação não corresponde ao provider/código previamente exibido. Pareamento recusado; use Esquecer conexão para reiniciar.");

            var challengeRequest = Signed(new IpcRequest(Op: "session.challenge", PairId: pair.PairId));
            var challenge = Rpc(EcosystemIpcProtocol.Challenge, challengeRequest);
            if (!challenge.Ok || challenge.ChallengeId is null || challenge.Challenge is null
                || challenge.ProviderSignature is null || challenge.ProviderPublicKey != pair.ProviderPublicKey)
                return Error(challenge);

            // Provider uses SHA-256 of raw public-key bytes. Compute the same lowercase hex value.
            var clientKeyHash = Convert.ToHexString(
                SHA256.HashData(Convert.FromBase64String(_key.PublicKey))).ToLowerInvariant();
            if (!AndroidInstallationKey.Verify(pair.ProviderPublicKey,
                EcosystemIpcProtocol.ProviderProof(pair.PairId, challenge.ChallengeId, challenge.Challenge, clientKeyHash),
                challenge.ProviderSignature))
                return new(false, "Prova criptográfica do provider inválida.");

            WriteTrust(new ProviderTrust(candidate.PackageName, candidate.SignerSha256, pair.ProviderPublicKey, pair.PairId));
            ClearPendingTrust();

            var open = Signed(new IpcRequest(
                Op: "session.open", PairId: pair.PairId, ChallengeId: challenge.ChallengeId,
                Challenge: challenge.Challenge));
            var opened = Rpc(EcosystemIpcProtocol.Open, open, _lifecycleToken);
            if (!opened.Ok || opened.SessionId is null) return Error(opened);
            _sessionId = opened.SessionId;

            var discovery = Rpc(EcosystemIpcProtocol.Discover, Signed(new IpcRequest(
                Op: "discover", PairId: pair.PairId, SessionId: _sessionId)));
            if (!discovery.Ok || discovery.Capabilities?.Any(x =>
                x.Id == "text.inspect" && x.Version == "1.0.0" && x.Operation == "inspect") != true)
                return new(false, "Capability text.inspect@1.0.0 não está disponível neste Context.");

            var input = JsonSerializer.Serialize(new { text });
            var requestId = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
            var invoke = Rpc(EcosystemIpcProtocol.Invoke, Signed(new IpcRequest(
                Op: "invoke", PairId: pair.PairId, SessionId: _sessionId, RequestId: requestId,
                Capability: "text.inspect", CapabilityOperation: "inspect", MinimumVersion: "1.0.0",
                DeadlineMs: 10_000, InputJson: input)));
            if (!invoke.Ok || invoke.OutputJson is null) return Error(invoke);

            using var output = JsonDocument.Parse(invoke.OutputJson);
            var root = output.RootElement;
            return new(true,
                $"IPC autenticado: {root.GetProperty("characters").GetInt32()} caracteres, {root.GetProperty("words").GetInt32()} palavras, {root.GetProperty("lines").GetInt32()} linhas.");
        }
        catch (System.OperationCanceledException) { return new(false, "Operação cancelada."); }
        catch (Exception) { return new(false, "Provider indisponível ou protocolo recusado."); }
        finally
        {
            TryClose();
            Unbind();
        }
    }

    IpcRequest Signed(IpcRequest request) =>
        request with { Signature = _key.Sign(EcosystemIpcProtocol.SignBytes(request)) };

    static HostTestResult Error(IpcResponse response) => new(false,
        $"Falha: {response.TransportError ?? response.HostError ?? "resposta inválida"}.");

    IpcResponse Rpc(int code, IpcRequest request, IBinder? lifecycleToken = null)
    {
        if (_binder is null) return new(false, TransportError: "PROVIDER_UNAVAILABLE");
        using var data = Parcel.Obtain();
        using var reply = Parcel.Obtain();
        data.WriteInterfaceToken(EcosystemIpcProtocol.Descriptor);
        var json = EcosystemIpcProtocol.Serialize(request);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > EcosystemIpcProtocol.MaxFrameBytes)
            return new(false, TransportError: "FRAME_TOO_LARGE");
        data.WriteString(json);
        if (lifecycleToken is not null) data.WriteStrongBinder(lifecycleToken);
        if (data.DataSize() > EcosystemIpcProtocol.MaxParcelBytes)
            return new(false, TransportError: "FRAME_TOO_LARGE");
        if (!_binder.Transact(code, data, reply, TransactionFlags.None))
            return new(false, TransportError: "PROVIDER_UNAVAILABLE");
        reply.ReadException();
        return EcosystemIpcProtocol.Deserialize<IpcResponse>(reply.ReadString())
            ?? new(false, TransportError: "PROTOCOL_UNSUPPORTED");
    }

    ProviderCandidate? ResolveProvider()
    {
        var query = new Intent(EcosystemIpcProtocol.ServiceAction);
        var matches = _context.PackageManager?.QueryIntentServices(query, PackageInfoFlags.MatchAll)?
            .Where(x => x.ServiceInfo is { Enabled: true, Exported: true })
            .Select(x => x.ServiceInfo!)
            .ToArray() ?? [];
        if (matches.Length != 1) return null;
        var service = matches[0];
        var package = service.PackageName;
        if (string.IsNullOrWhiteSpace(package) || string.IsNullOrWhiteSpace(service.Name)) return null;
        var signer = Signer(package);
        return signer is null || _trustPolicy?.AllowsProvider(package, signer) != true
            ? null : new ProviderCandidate(package, service.Name!, signer);
    }

    string? Signer(string package)
    {
        try
        {
            var info = _context.PackageManager!.GetPackageInfo(package, PackageInfoFlags.SigningCertificates);
            var signers = info.SigningInfo?.GetApkContentsSigners();
            return signers is { Length: 1 }
                ? Convert.ToHexString(SHA256.HashData(signers[0].ToByteArray()!))
                : null;
        }
        catch (Exception) { return null; }
    }

    async Task BindAsync(ProviderCandidate candidate, CancellationToken ct)
    {
        Unbind();
        var completion = new TaskCompletionSource<IBinder>(TaskCreationOptions.RunContinuationsAsynchronously);
        _connection = new HostServiceConnection(completion);
        var intent = new Intent(EcosystemIpcProtocol.ServiceAction)
            .SetComponent(new ComponentName(candidate.PackageName, candidate.ServiceName));
        if (!_context.BindService(intent, _connection, Bind.AutoCreate))
            throw new InvalidOperationException("Bind recusado.");
        _binder = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
    }

    internal bool Revoke(params string[] permissions)
    {
        if (_binder is null || _pair is null || _sessionId is null || permissions is not { Length: > 0 }) return false;
        try
        {
            var response = Rpc(EcosystemIpcProtocol.Revoke, Signed(new IpcRequest(
                Op: "revoke", PairId: _pair.PairId, SessionId: _sessionId, Permissions: permissions)));
            return response.Ok;
        }
        catch (Exception) { return false; }
    }

    void TryClose()
    {
        if (_binder is null || _pair is null || _sessionId is null) return;
        try { _ = Rpc(EcosystemIpcProtocol.Close, Signed(new IpcRequest(
            Op: "close", PairId: _pair.PairId, SessionId: _sessionId))); }
        catch (Exception) { }
        _sessionId = null;
    }

    void Unbind()
    {
        if (_connection is not null)
        {
            try { _context.UnbindService(_connection); } catch (Exception) { }
            _connection.Dispose();
            _connection = null;
        }
        _binder = null;
    }

    ProviderTrust? ReadTrust()
    {
        try
        {
            var path = Path.Combine(_context.FilesDir!.AbsolutePath, TrustFile);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ProviderTrust>(File.ReadAllText(path), EcosystemIpcProtocol.Json)
                : null;
        }
        catch (Exception) { return null; }
    }

    void WriteTrust(ProviderTrust trust)
    {
        var path = Path.Combine(_context.FilesDir!.AbsolutePath, TrustFile);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(trust, EcosystemIpcProtocol.Json));
        File.Move(temp, path, true);
    }

    PendingProviderTrust? ReadPendingTrust()
    {
        try
        {
            var path = Path.Combine(_context.FilesDir!.AbsolutePath, PendingTrustFile);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<PendingProviderTrust>(File.ReadAllText(path), EcosystemIpcProtocol.Json)
                : null;
        }
        catch (Exception) { return null; }
    }

    void WritePendingTrust(PendingProviderTrust trust)
    {
        var path = Path.Combine(_context.FilesDir!.AbsolutePath, PendingTrustFile);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(trust, EcosystemIpcProtocol.Json));
        File.Move(temp, path, true);
    }

    void ClearPendingTrust()
    {
        try
        {
            var path = Path.Combine(_context.FilesDir!.AbsolutePath, PendingTrustFile);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception) { }
    }

    internal void ResetTrust()
    {
        TryClose();
        Unbind();
        foreach (var file in new[] { TrustFile, PendingTrustFile })
        {
            try
            {
                var path = Path.Combine(_context.FilesDir!.AbsolutePath, file);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception) { }
        }
        _pair = null;
        _candidate = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        TryClose();
        Unbind();
        _lifecycleToken.Dispose();
    }

    private sealed class LifecycleToken : Binder { }

    private sealed class HostServiceConnection(TaskCompletionSource<IBinder> completion)
        : Java.Lang.Object, IServiceConnection
    {
        public void OnServiceConnected(ComponentName? name, IBinder? service)
        {
            if (service is not null) completion.TrySetResult(service);
            else completion.TrySetException(new InvalidOperationException("Binder nulo."));
        }
        public void OnServiceDisconnected(ComponentName? name) =>
            completion.TrySetException(new InvalidOperationException("Provider desconectado."));
        public void OnBindingDied(ComponentName? name) =>
            completion.TrySetException(new InvalidOperationException("Binding morreu."));
        public void OnNullBinding(ComponentName? name) =>
            completion.TrySetException(new InvalidOperationException("Provider sem binding."));
    }

    private sealed record ProviderCandidate(string PackageName, string ServiceName, string SignerSha256);
    private sealed record PairCandidate(string PairId, string ProviderPublicKey);
    private sealed record ProviderTrust(string PackageName, string SignerSha256, string PublicKey, string PairId);
    private sealed record PendingProviderTrust(
        string PackageName, string SignerSha256, string PublicKey, string PairId, string PairCode);
}

internal sealed record HostTestResult(bool Success, string Message, string? PairCode = null);

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
        var store = KeyStore.GetInstance(StoreName) ?? throw new InvalidOperationException();
        store.Load((KeyStore.ILoadStoreParameter?)null);
        return store;
    }

    void Ensure()
    {
        using var store = Store();
        if (store.ContainsAlias(_alias)) return;
        using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, StoreName)
            ?? throw new InvalidOperationException();
        using var curve = new ECGenParameterSpec("secp256r1");
        using var spec = new KeyGenParameterSpec.Builder(_alias, KeyStorePurpose.Sign | KeyStorePurpose.Verify)
            .SetAlgorithmParameterSpec(curve).SetDigests(KeyProperties.DigestSha256).Build();
        generator.Initialize(spec);
        using var pair = generator.GenerateKeyPair();
    }

    internal string PublicKey
    {
        get
        {
            using var store = Store();
            return Convert.ToBase64String(store.GetCertificate(_alias)?.PublicKey?.GetEncoded()
                ?? throw new InvalidOperationException());
        }
    }

    internal string Sign(byte[] data)
    {
        using var store = Store();
        var key = store.GetKey(_alias, null) as IPrivateKey ?? throw new InvalidOperationException();
        using var signature = Java.Security.Signature.GetInstance("SHA256withECDSA") ?? throw new InvalidOperationException();
        signature.InitSign(key);
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
            using var factory = KeyFactory.GetInstance(KeyProperties.KeyAlgorithmEc) ?? throw new InvalidOperationException();
            using var key = factory.GeneratePublic(spec);
            using var signature = Java.Security.Signature.GetInstance("SHA256withECDSA") ?? throw new InvalidOperationException();
            signature.InitVerify(key);
            signature.Update(data);
            return signature.Verify(signatureBytes);
        }
        catch (Exception) { return false; }
    }
}
