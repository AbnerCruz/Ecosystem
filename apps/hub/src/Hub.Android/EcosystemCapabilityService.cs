using Android.App;
using Android.Content;
using Android.OS;
using Hub.Core.Capabilities;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace HubApp;

[Service(
    Exported = true,
    Enabled = true,
    Name = "org.ecosystem.capability.android.HostServiceV1")]
[IntentFilter([EcosystemIpcProtocol.ServiceAction])]
public sealed class EcosystemCapabilityService : Service
{
    private sealed record ChallengeState(
        string Id, string PairId, string PeerKeyHash, string Challenge, DateTimeOffset ExpiresAt);

    readonly ConcurrentDictionary<string, ChallengeState> _challenges = new(StringComparer.Ordinal);
    AndroidInstallationKey? _key;
    EcosystemPairingStore? _pairings;
    AuthenticatedHostGateway? _gateway;
    EcosystemCapabilityBinder? _binder;

    internal AndroidInstallationKey Key => _key ?? throw new InvalidOperationException();
    internal EcosystemPairingStore Pairings => _pairings ?? throw new InvalidOperationException();
    internal AuthenticatedHostGateway Gateway => _gateway ?? throw new InvalidOperationException();

    public override void OnCreate()
    {
        base.OnCreate();
        _key = new AndroidInstallationKey("ecosystem.ipc.provider.v1");
        _pairings = new EcosystemPairingStore(this, _key);
        var root = new LocalContext([new ContextStep("ecosystem", "ecosystem")]);
        _gateway = new AuthenticatedHostGateway(new LocalCapabilityHost(
            [TextInspectionTool.Definition(root)], []));
        _binder = new EcosystemCapabilityBinder(this);
    }

    public override IBinder? OnBind(Intent? intent) =>
        intent?.Action == EcosystemIpcProtocol.ServiceAction ? _binder : null;

    public override void OnDestroy()
    {
        _gateway?.Dispose();
        _challenges.Clear();
        base.OnDestroy();
    }

    internal IpcResponse PairBegin(AndroidPeerIdentity peer, IpcRequest request)
    {
        if (request.Op != "pair.begin" || string.IsNullOrWhiteSpace(request.PublicKey)
            || string.IsNullOrWhiteSpace(request.ClientNonce))
            return Error("PROTOCOL_UNSUPPORTED");

        var result = Pairings.Begin(peer, request.PublicKey, request.ClientNonce);
        if (result.Error is not null) return Error(result.Error);
        if (result.Approved is { } approved)
            return new(true, State: "approved", PairId: approved.PairId,
                ProviderPublicKey: Key.PublicKey);

        var pending = result.Pending!;
        return new(true, State: "pending", PairId: pending.PairId, PairCode: pending.Code,
            ProviderPublicKey: Key.PublicKey, ProviderNonce: pending.ProviderNonce);
    }

    internal IpcResponse Challenge(AndroidPeerIdentity peer, IpcRequest request)
    {
        if (request.Op != "session.challenge" || request.Signature is null) return Error("PROTOCOL_UNSUPPORTED");
        var approved = Pairings.Approved(peer, request.PairId);
        if (approved is null) return Error("PAIRING_REQUIRED");
        if (!AndroidInstallationKey.Verify(approved.PublicKey, EcosystemIpcProtocol.SignBytes(request), request.Signature))
            return Error("SIGNATURE_INVALID");

        CleanupChallenges();
        if (_challenges.Count >= 8) return Error("SESSION_BUSY");
        var id = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var challenge = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var item = new ChallengeState(id, approved.PairId, approved.KeyHash, challenge,
            DateTimeOffset.UtcNow.AddSeconds(30));
        _challenges[id] = item;
        return new(true, State: "challenge", PairId: approved.PairId,
            ProviderPublicKey: Key.PublicKey, ChallengeId: id, Challenge: challenge,
            ProviderSignature: Key.Sign(EcosystemIpcProtocol.ProviderProof(
                approved.PairId, id, challenge, approved.KeyHash)));
    }

    internal IpcResponse Open(AndroidPeerIdentity peer, IpcRequest request)
    {
        if (request.Op != "session.open" || request.Signature is null || request.ContextJson is null
            || !EcosystemIpcProtocol.ValidId(request.ChallengeId))
            return Error("PROTOCOL_UNSUPPORTED");
        var approved = Pairings.Approved(peer, request.PairId);
        if (approved is null) return Error("PAIRING_REQUIRED");
        if (!_challenges.TryRemove(request.ChallengeId!, out var challenge)
            || challenge.PairId != approved.PairId || challenge.PeerKeyHash != approved.KeyHash
            || challenge.ExpiresAt <= DateTimeOffset.UtcNow || request.Challenge != challenge.Challenge)
            return Error("CHALLENGE_INVALID");
        if (!AndroidInstallationKey.Verify(approved.PublicKey, EcosystemIpcProtocol.SignBytes(request), request.Signature))
            return Error("SIGNATURE_INVALID");

        var context = ParseContext(request.ContextJson);
        if (context is null) return Error("PROTOCOL_UNSUPPORTED");
        var actor = $"android:{peer.PackageName}:{approved.KeyHash[..Math.Min(12, approved.KeyHash.Length)]}";
        var opened = Gateway.Open(approved.KeyHash, actor, context, []);
        return opened.Succeeded
            ? new(true, State: "open", PairId: approved.PairId, SessionId: opened.SessionId)
            : new(false, HostError: opened.Error);
    }

    internal IpcResponse Discover(AndroidPeerIdentity peer, IpcRequest request)
    {
        if (!Authenticated(peer, request, "discover", out var approved, out var error)) return error!;
        var result = Gateway.Discover(approved!.KeyHash, request.SessionId!);
        return result.Succeeded
            ? new(true, Capabilities: result.Capabilities
                .Select(x => new IpcCapability(x.Id, x.Version, x.Operation, x.Lifecycle)).ToArray())
            : new(false, HostError: result.Error);
    }

    internal IpcResponse Invoke(AndroidPeerIdentity peer, IpcRequest request)
    {
        if (!Authenticated(peer, request, "invoke", out var approved, out var error)) return error!;
        if (!EcosystemIpcProtocol.ValidId(request.RequestId) || request.InputJson is null
            || string.IsNullOrWhiteSpace(request.Capability) || string.IsNullOrWhiteSpace(request.CapabilityOperation)
            || !Version.TryParse(request.MinimumVersion, out var minimum))
            return Error("PROTOCOL_UNSUPPORTED");
        JsonElement input;
        try { using var doc = JsonDocument.Parse(request.InputJson); input = doc.RootElement.Clone(); }
        catch (JsonException) { return Error("PROTOCOL_UNSUPPORTED"); }

        var deadline = request.DeadlineMs is null ? null : TimeSpan.FromMilliseconds(request.DeadlineMs.Value);
        var result = Gateway.InvokeAsync(approved!.KeyHash, request.SessionId!, request.RequestId!,
            request.Capability!, request.CapabilityOperation!, minimum!, input, deadline)
            .GetAwaiter().GetResult();
        return result.Succeeded
            ? new(true, OutputJson: result.Output!.Value.GetRawText())
            : new(false, TransportError: result.TransportError, HostError: result.HostError);
    }

    internal IpcResponse Cancel(AndroidPeerIdentity peer, IpcRequest request)
    {
        if (!Authenticated(peer, request, "cancel", out var approved, out var error)) return error!;
        if (!EcosystemIpcProtocol.ValidId(request.RequestId)) return Error("PROTOCOL_UNSUPPORTED");
        return Gateway.Cancel(approved!.KeyHash, request.SessionId!, request.RequestId!)
            ? new(true, State: "cancelled")
            : new(false, HostError: "CANCELLED");
    }

    internal IpcResponse Close(AndroidPeerIdentity peer, IpcRequest request)
    {
        if (!Authenticated(peer, request, "close", out var approved, out var error)) return error!;
        return Gateway.Close(approved!.KeyHash, request.SessionId!)
            ? new(true, State: "closed")
            : new(false, HostError: "SESSION_CLOSED");
    }

    bool Authenticated(AndroidPeerIdentity peer, IpcRequest request, string operation,
        out ApprovedPeer? approved, out IpcResponse? error)
    {
        approved = null; error = null;
        if (request.Op != operation || request.Signature is null
            || !EcosystemIpcProtocol.ValidId(request.PairId) || !EcosystemIpcProtocol.ValidId(request.SessionId))
        { error = Error("PROTOCOL_UNSUPPORTED"); return false; }
        approved = Pairings.Approved(peer, request.PairId);
        if (approved is null) { error = Error("PAIRING_REQUIRED"); return false; }
        if (!AndroidInstallationKey.Verify(approved.PublicKey, EcosystemIpcProtocol.SignBytes(request), request.Signature))
        { error = Error("SIGNATURE_INVALID"); return false; }
        return true;
    }

    static LocalContext? ParseContext(string json)
    {
        var context = EcosystemIpcProtocol.Deserialize<IpcContext>(json);
        if (context?.Steps is not { Length: > 0 and <= 5 }) return null;
        try { return new LocalContext(context.Steps.Select(x => new ContextStep(x.Level, x.Id))); }
        catch (ArgumentException) { return null; }
    }

    void CleanupChallenges()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _challenges.ToArray())
            if (pair.Value.ExpiresAt <= now) _challenges.TryRemove(pair.Key, out _);
    }

    internal static IpcResponse Error(string code) => new(false, TransportError: code);

    private sealed class EcosystemCapabilityBinder(EcosystemCapabilityService owner) : Binder
    {
        protected override bool OnTransact(int code, Parcel data, Parcel? reply, int flags)
        {
            if (reply is null) return false;
            IpcResponse response;
            try
            {
                data.EnforceInterface(EcosystemIpcProtocol.Descriptor);
                var json = data.ReadString();
                if (json is null || Encoding.UTF8.GetByteCount(json) > EcosystemIpcProtocol.MaxFrameBytes)
                    response = Error("FRAME_TOO_LARGE");
                else if (EcosystemIpcProtocol.Deserialize<IpcRequest>(json) is not { } request)
                    response = Error("PROTOCOL_UNSUPPORTED");
                else if (AndroidPeerIdentityResolver.Resolve(owner, Binder.CallingUid) is not { } peer)
                    response = Error("PEER_UNTRUSTED");
                else response = code switch
                {
                    EcosystemIpcProtocol.PairBegin => owner.PairBegin(peer, request),
                    EcosystemIpcProtocol.Challenge => owner.Challenge(peer, request),
                    EcosystemIpcProtocol.Open => owner.Open(peer, request),
                    EcosystemIpcProtocol.Discover => owner.Discover(peer, request),
                    EcosystemIpcProtocol.Invoke => owner.Invoke(peer, request),
                    EcosystemIpcProtocol.Cancel => owner.Cancel(peer, request),
                    EcosystemIpcProtocol.Close => owner.Close(peer, request),
                    _ => Error("PROTOCOL_UNSUPPORTED")
                };
            }
            catch (Exception)
            {
                response = Error("PROVIDER_UNAVAILABLE");
            }
            reply.WriteNoException();
            reply.WriteString(EcosystemIpcProtocol.Serialize(response));
            return true;
        }
    }
}
