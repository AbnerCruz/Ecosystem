using System.Security.Cryptography;
using System.Text;

namespace Lunet.Git;

/// <summary>Tipos de objeto do Git.</summary>
public enum ObjectType { Commit = 1, Tree = 2, Blob = 3, Tag = 4 }

/// <summary>Identificador SHA-1 de um objeto (20 bytes).</summary>
public readonly struct ObjectId : IEquatable<ObjectId>, IComparable<ObjectId>
{
    public const int Length = 20;
    private readonly byte[]? _bytes;

    public ObjectId(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Length) throw new ArgumentException("Um ObjectId tem 20 bytes.", nameof(bytes));
        _bytes = bytes.ToArray();
    }

    public static ObjectId Zero { get; } = new(new byte[Length]);

    public bool IsZero => _bytes is null || _bytes.All(b => b == 0);

    public ReadOnlySpan<byte> Bytes => _bytes ?? Zero._bytes!;

    public static ObjectId Parse(string hex)
    {
        if (!TryParse(hex, out var id)) throw new FormatException($"'{hex}' não é um SHA-1 válido.");
        return id;
    }

    public static bool TryParse(ReadOnlySpan<char> hex, out ObjectId id)
    {
        id = default;
        if (hex.Length != Length * 2) return false;
        var bytes = new byte[Length];
        for (var i = 0; i < Length; i++)
        {
            var hi = Nibble(hex[2 * i]);
            var lo = Nibble(hex[2 * i + 1]);
            if (hi < 0 || lo < 0) return false;
            bytes[i] = (byte)(hi << 4 | lo);
        }
        id = new ObjectId(bytes);
        return true;
    }

    private static int Nibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    /// <summary>SHA-1 do objeto: "tipo tamanho\0" seguido do conteúdo.</summary>
    public static ObjectId Compute(ObjectType type, ReadOnlySpan<byte> data)
    {
        var header = Encoding.ASCII.GetBytes($"{TypeName(type)} {data.Length}\0");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(header);
        hash.AppendData(data);
        return new ObjectId(hash.GetHashAndReset());
    }

    public static string TypeName(ObjectType type) => type switch
    {
        ObjectType.Commit => "commit",
        ObjectType.Tree => "tree",
        ObjectType.Blob => "blob",
        ObjectType.Tag => "tag",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static ObjectType ParseType(string name) => name switch
    {
        "commit" => ObjectType.Commit,
        "tree" => ObjectType.Tree,
        "blob" => ObjectType.Blob,
        "tag" => ObjectType.Tag,
        _ => throw new FormatException($"Tipo de objeto desconhecido: {name}"),
    };

    public string Hex => Convert.ToHexString(Bytes).ToLowerInvariant();

    public string Short => Hex[..7];

    public override string ToString() => Hex;

    public bool Equals(ObjectId other) => Bytes.SequenceEqual(other.Bytes);

    public override bool Equals(object? obj) => obj is ObjectId other && Equals(other);

    public override int GetHashCode() => BitConverter.ToInt32(Bytes[..4]);

    public int CompareTo(ObjectId other) => Bytes.SequenceCompareTo(other.Bytes);

    public static bool operator ==(ObjectId left, ObjectId right) => left.Equals(right);

    public static bool operator !=(ObjectId left, ObjectId right) => !left.Equals(right);
}

/// <summary>Erro de operação Git, com mensagem em português para mostrar ao usuário.</summary>
public sealed class GitException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Autor ou committer de um commit.</summary>
public sealed record GitSignature(string Name, string Email, DateTimeOffset When)
{
    public string Format() => $"{Name} <{Email}> {When.ToUnixTimeSeconds()} {(When.Offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(When.Offset.Hours):00}{Math.Abs(When.Offset.Minutes):00}";

    public static GitSignature Parse(string text)
    {
        var open = text.LastIndexOf('<');
        var close = text.LastIndexOf('>');
        if (open < 0 || close < open) throw new FormatException("Assinatura inválida: " + text);
        var name = text[..open].Trim();
        var email = text[(open + 1)..close];
        var parts = text[(close + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var when = DateTimeOffset.UnixEpoch;
        if (parts.Length >= 1 && long.TryParse(parts[0], out var seconds))
        {
            var offset = TimeSpan.Zero;
            if (parts.Length >= 2 && parts[1].Length == 5 && int.TryParse(parts[1][1..3], out var h) && int.TryParse(parts[1][3..], out var m))
                offset = new TimeSpan(h, m, 0) * (parts[1][0] == '-' ? -1 : 1);
            when = DateTimeOffset.FromUnixTimeSeconds(seconds).ToOffset(offset);
        }
        return new GitSignature(name, email, when);
    }
}
