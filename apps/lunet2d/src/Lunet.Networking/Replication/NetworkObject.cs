using System.Numerics;

namespace Lunet.Networking;

/// <summary>How a value type travels and blends between snapshots.</summary>
public static class NetCodec<T>
{
    public static Action<NetWriter, T> Write { get; set; } = Default.Writer();
    public static Func<NetReader, T> Read { get; set; } = Default.Reader();
    /// <summary>Blend for interpolation; null = no blending (switches at the newer snapshot).</summary>
    public static Func<T, T, float, T>? Lerp { get; set; } = Default.Blender();

    private static class Default
    {
        public static Action<NetWriter, T> Writer() => typeof(T) switch
        {
            var t when t == typeof(int) => (w, v) => w.WriteVarInt((int)(object)v!),
            var t when t == typeof(float) => (w, v) => w.Write((float)(object)v!),
            var t when t == typeof(double) => (w, v) => w.Write((double)(object)v!),
            var t when t == typeof(bool) => (w, v) => w.Write((bool)(object)v!),
            var t when t == typeof(byte) => (w, v) => w.Write((byte)(object)v!),
            var t when t == typeof(uint) => (w, v) => w.WriteVarUInt((uint)(object)v!),
            var t when t == typeof(string) => (w, v) => w.Write((string)(object)v! ?? ""),
            var t when t == typeof(Vector2) => (w, v) => w.Write((Vector2)(object)v!),
            var t when typeof(INetSerializable).IsAssignableFrom(t) => (w, v) => ((INetSerializable)v!).Serialize(w),
            _ => (_, _) => throw new NotSupportedException($"Defina NetCodec<{typeof(T).Name}>.Write e Read para sincronizar este tipo."),
        };

        public static Func<NetReader, T> Reader() => typeof(T) switch
        {
            var t when t == typeof(int) => r => (T)(object)r.ReadVarInt(),
            var t when t == typeof(float) => r => (T)(object)r.ReadSingle(),
            var t when t == typeof(double) => r => (T)(object)r.ReadDouble(),
            var t when t == typeof(bool) => r => (T)(object)r.ReadBool(),
            var t when t == typeof(byte) => r => (T)(object)r.ReadByte(),
            var t when t == typeof(uint) => r => (T)(object)r.ReadVarUInt(),
            var t when t == typeof(string) => r => (T)(object)r.ReadString(),
            var t when t == typeof(Vector2) => r => (T)(object)r.ReadVector2(),
            var t when typeof(INetSerializable).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null =>
                r => { var v = (INetSerializable)Activator.CreateInstance(t)!; v.Deserialize(r); return (T)v; },
            _ => _ => throw new NotSupportedException($"Defina NetCodec<{typeof(T).Name}>.Read para sincronizar este tipo."),
        };

        public static Func<T, T, float, T>? Blender() => typeof(T) switch
        {
            var t when t == typeof(float) => (a, b, k) => (T)(object)((float)(object)a! + ((float)(object)b! - (float)(object)a!) * k),
            var t when t == typeof(double) => (a, b, k) => (T)(object)((double)(object)a! + ((double)(object)b! - (double)(object)a!) * k),
            var t when t == typeof(Vector2) => (a, b, k) => (T)(object)Vector2.Lerp((Vector2)(object)a!, (Vector2)(object)b!, k),
            _ => null,
        };
    }
}

/// <summary>Untyped view of a <see cref="NetworkVariable{T}"/> for the replication system.</summary>
public abstract class NetworkVariableBase
{
    internal NetworkObject? Owner { get; set; }
    internal abstract void Write(NetWriter writer);
    internal abstract void Read(NetReader reader, double serverTime);
    internal abstract void Forget();
}

/// <summary>
/// A synchronized value (SPEC §8 NetworkVariable). The server sets <see cref="Value"/>; clients read it. On clients,
/// <see cref="Interpolated"/> gives a smooth value between snapshots (for drawing remote objects).
/// </summary>
public sealed class NetworkVariable<T> : NetworkVariableBase
{
    private readonly List<(double Time, T Value)> _history = new(8);
    private T _value;

    public NetworkVariable(T initial = default!) => _value = initial;

    /// <summary>The latest authoritative value (set on the server; received on clients).</summary>
    public T Value { get => _value; set => _value = value; }

    /// <summary>The value at <paramref name="serverTime"/>, blended between the two surrounding snapshots.</summary>
    public T At(double serverTime)
    {
        if (_history.Count == 0) return _value;
        if (serverTime <= _history[0].Time) return _history[0].Value;
        for (int i = _history.Count - 1; i > 0; i--)
        {
            var (t0, v0) = _history[i - 1];
            var (t1, v1) = _history[i];
            if (serverTime < t0) continue;
            if (serverTime >= t1) return v1;
            var lerp = NetCodec<T>.Lerp;
            return lerp is null ? v0 : lerp(v0, v1, (float)((serverTime - t0) / Math.Max(1e-6, t1 - t0)));
        }
        return _history[^1].Value;
    }

    /// <summary>On clients: the value at "server time minus interpolation delay" (smooth for remote objects). On the server: <see cref="Value"/>.</summary>
    public T Interpolated => Owner is null || Owner.Session is null || Owner.Session.IsServer ? _value
        : At(Owner.Session.Clock.ServerTime - Owner.Session.Options.InterpolationDelay);

    internal override void Write(NetWriter writer) => NetCodec<T>.Write(writer, _value);

    internal override void Read(NetReader reader, double serverTime)
    {
        _value = NetCodec<T>.Read(reader);
        if (_history.Count > 0 && serverTime <= _history[^1].Time) return; // stale or duplicate snapshot
        _history.Add((serverTime, _value));
        if (_history.Count > 32) _history.RemoveAt(0);
    }

    internal override void Forget() => _history.Clear();

    public static implicit operator T(NetworkVariable<T> variable) => variable._value;
}

/// <summary>
/// A replicated object (SPEC §8 NetworkObject/NetworkIdentity): created by the server with <see cref="NetworkReplication.Spawn{T}"/>,
/// appears on every client with the same <see cref="NetworkId"/>, and its <see cref="NetworkVariable{T}"/>s follow the server.
/// Declare variables as fields and list them in <see cref="Variables"/> (fixed order on every device).
/// </summary>
public abstract class NetworkObject
{
    private NetworkVariableBase[]? _variables;

    public int NetworkId { get; internal set; }
    /// <summary>Peer that controls it (for input and prediction); -1 = the server.</summary>
    public int OwnerPeerId { get; internal set; } = -1;
    public string Kind { get; internal set; } = "";
    public NetworkSession? Session { get; internal set; }
    public bool IsServer => Session?.IsServer ?? true;
    public bool IsOwner => Session is not null && OwnerPeerId == Session.LocalPeerId;

    /// <summary>The synchronized variables, in the same order on every device.</summary>
    protected abstract IEnumerable<NetworkVariableBase> Variables { get; }

    internal NetworkVariableBase[] VariableList
    {
        get
        {
            if (_variables is not null) return _variables;
            _variables = Variables.ToArray();
            foreach (var v in _variables) v.Owner = this;
            return _variables;
        }
    }

    /// <summary>Called on every device after the object appears.</summary>
    public virtual void OnSpawned() { }
    /// <summary>Called on every device before the object disappears.</summary>
    public virtual void OnDespawned() { }
}
