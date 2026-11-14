using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Lunet.Networking;

/// <summary>A value a game sends over the network. Write and read the same fields in the same order.</summary>
public interface INetSerializable
{
    void Serialize(NetWriter writer);
    void Deserialize(NetReader reader);
}

/// <summary>
/// Compact little-endian writer for network messages (LUNET-501): variable-length integers, floats, strings, vectors and
/// quantized values. Grows as needed up to <see cref="MaxSize"/>, so a bug cannot allocate unbounded memory.
/// </summary>
public sealed class NetWriter
{
    public const int MaxSize = 64 * 1024;
    private byte[] _buffer;
    public int Length { get; private set; }

    public NetWriter(int capacity = 256) => _buffer = new byte[Math.Clamp(capacity, 16, MaxSize)];

    public ReadOnlySpan<byte> Span => _buffer.AsSpan(0, Length);
    public byte[] ToArray() => _buffer.AsSpan(0, Length).ToArray();
    public void Reset() => Length = 0;

    private Span<byte> Take(int count)
    {
        if (Length + count > MaxSize) throw new InvalidOperationException($"Mensagem maior que {MaxSize} bytes.");
        if (Length + count > _buffer.Length) Array.Resize(ref _buffer, Math.Min(MaxSize, Math.Max(_buffer.Length * 2, Length + count)));
        var span = _buffer.AsSpan(Length, count);
        Length += count;
        return span;
    }

    public void Write(byte value) => Take(1)[0] = value;
    public void Write(bool value) => Write(value ? (byte)1 : (byte)0);
    public void Write(short value) => BinaryPrimitives.WriteInt16LittleEndian(Take(2), value);
    public void Write(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(Take(2), value);
    public void Write(int value) => BinaryPrimitives.WriteInt32LittleEndian(Take(4), value);
    public void Write(uint value) => BinaryPrimitives.WriteUInt32LittleEndian(Take(4), value);
    public void Write(long value) => BinaryPrimitives.WriteInt64LittleEndian(Take(8), value);
    public void Write(float value) => BinaryPrimitives.WriteSingleLittleEndian(Take(4), value);
    public void Write(double value) => BinaryPrimitives.WriteDoubleLittleEndian(Take(8), value);
    public void Write(Vector2 value) { Write(value.X); Write(value.Y); }

    /// <summary>Unsigned LEB128: small numbers take one byte.</summary>
    public void WriteVarUInt(uint value)
    {
        while (value >= 0x80) { Write((byte)(value | 0x80)); value >>= 7; }
        Write((byte)value);
    }

    /// <summary>Zig-zag signed varint.</summary>
    public void WriteVarInt(int value) => WriteVarUInt((uint)((value << 1) ^ (value >> 31)));

    public void Write(string value)
    {
        var bytes = Encoding.UTF8.GetByteCount(value ?? "");
        if (bytes > 16_384) throw new InvalidOperationException("Texto grande demais para uma mensagem.");
        WriteVarUInt((uint)bytes);
        Encoding.UTF8.GetBytes(value ?? "", Take(bytes));
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        WriteVarUInt((uint)bytes.Length);
        bytes.CopyTo(Take(bytes.Length));
    }

    /// <summary>A float in [min,max] stored in 16 bits (precision (max-min)/65535): positions and angles cheaply.</summary>
    public void WriteQuantized(float value, float min, float max)
    {
        var t = Math.Clamp((value - min) / (max - min), 0, 1);
        Write((ushort)MathF.Round(t * ushort.MaxValue));
    }

    public void Write<T>(T value) where T : INetSerializable => value.Serialize(this);
}

/// <summary>Bounds-checked reader matching <see cref="NetWriter"/>; malformed data throws <see cref="InvalidDataException"/>.</summary>
public sealed class NetReader
{
    private readonly ReadOnlyMemory<byte> _data;
    public int Position { get; private set; }
    public int Remaining => _data.Length - Position;

    public NetReader(ReadOnlyMemory<byte> data) => _data = data;

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || Position + count > _data.Length) throw new InvalidDataException("Mensagem de rede truncada.");
        var span = _data.Span.Slice(Position, count);
        Position += count;
        return span;
    }

    public byte ReadByte() => Take(1)[0];
    public bool ReadBool() => ReadByte() switch { 0 => false, 1 => true, _ => throw new InvalidDataException("Booleano inválido.") };
    public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
    public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));
    public float ReadSingle()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(Take(4));
        if (!float.IsFinite(value)) throw new InvalidDataException("Número inválido na mensagem.");
        return value;
    }
    public double ReadDouble()
    {
        var value = BinaryPrimitives.ReadDoubleLittleEndian(Take(8));
        if (!double.IsFinite(value)) throw new InvalidDataException("Número inválido na mensagem.");
        return value;
    }
    public Vector2 ReadVector2() => new(ReadSingle(), ReadSingle());

    public uint ReadVarUInt()
    {
        uint result = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            byte b = ReadByte();
            result |= (uint)(b & 0x7f) << shift;
            if ((b & 0x80) == 0) return result;
        }
        throw new InvalidDataException("Varint longo demais.");
    }

    public int ReadVarInt()
    {
        uint raw = ReadVarUInt();
        return (int)(raw >> 1) ^ -(int)(raw & 1);
    }

    public string ReadString()
    {
        var length = ReadVarUInt();
        if (length > 16_384) throw new InvalidDataException("Texto grande demais.");
        return Encoding.UTF8.GetString(Take((int)length));
    }

    public byte[] ReadBytes()
    {
        var length = ReadVarUInt();
        if (length > NetWriter.MaxSize) throw new InvalidDataException("Bloco grande demais.");
        return Take((int)length).ToArray();
    }

    public float ReadQuantized(float min, float max) => min + ReadUInt16() / (float)ushort.MaxValue * (max - min);

    public T Read<T>() where T : INetSerializable, new()
    {
        var value = new T();
        value.Deserialize(this);
        return value;
    }
}
