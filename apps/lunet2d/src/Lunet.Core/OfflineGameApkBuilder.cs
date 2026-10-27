using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Lunet.Core;

/// <summary>Signer backed by an app-owned per-game key. Never use the IDE certificate for games.</summary>
public interface IGameApkSigner
{
    byte[] CertificateDer { get; }
    byte[] PublicKeyDer { get; }
    byte[] Sign(byte[] signedData);
}

/// <summary>
/// Builds an independent Android APK entirely offline from a precompiled Lunet game host.
/// Preserves the template's aligned local ZIP entries, injects the compiled game and assets,
/// replaces binary Android resources, then produces a new APK Signature Scheme v2 block.
/// No Android SDK, AAPT2, network, GitHub or external executable is used on the device.
/// </summary>
public static class OfflineGameApkBuilder
{
    private const uint EocdMagic = 0x06054b50;
    private const uint CentralMagic = 0x02014b50;
    private const uint LocalMagic = 0x04034b50;
    private const uint SigIdV2 = 0x7109871a;
    private const uint RsaSha256 = 0x0103;
    private static readonly byte[] SigMagic = Encoding.ASCII.GetBytes("APK Sig Block 42");
    private const int MaxGameFile = 128 * 1024 * 1024;
    private const long MaxGameTotal = 512L * 1024 * 1024;

    /// <summary>
    /// Package ID is the project's explicit PackageId, never a hidden global identifier.
    /// New build overwrites an earlier installation only if Android sees the same signing key.
    /// </summary>
    public static byte[] Build(byte[] template, IReadOnlyDictionary<string, byte[]> gameFiles,
        string packageId, string title, IGameApkSigner signer, string orientation = "portrait")
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(gameFiles);
        ArgumentNullException.ThrowIfNull(signer);
        if (!ValidPackage(packageId)) throw new ArgumentException("Identificador Android inválido.", nameof(packageId));
        if (string.IsNullOrWhiteSpace(title) || title.Length > 90) throw new ArgumentException("Nome de jogo inválido.", nameof(title));
        if (signer.CertificateDer.Length == 0 || signer.PublicKeyDer.Length == 0)
            throw new ArgumentException("Assinatura sem certificado/chave pública.", nameof(signer));
        if (gameFiles.Count == 0 || !gameFiles.ContainsKey("game/Game.dll"))
            throw new ArgumentException("Falta o assembly game/Game.dll.", nameof(gameFiles));

        var replacement = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["AndroidManifest.xml"] = null!,
            ["resources.arsc"] = null!
        };
        long gameSize = 0;
        foreach (var (name, data) in gameFiles)
        {
            if (!name.StartsWith("game/", StringComparison.Ordinal) || !ValidZipPath(name))
                throw new ArgumentException("Caminho inválido de asset: " + name, nameof(gameFiles));
            if (data is null || data.Length > MaxGameFile || (gameSize += data.Length) > MaxGameTotal)
                throw new ArgumentException("Assets do jogo excedem o limite de tamanho.", nameof(gameFiles));
            if (!replacement.TryAdd("assets/" + name, data))
                throw new ArgumentException("Asset duplicado.", nameof(gameFiles));
        }

        // Decode resources with the stock ZIP reader; only assets of our sealed template are trusted.
        using (var zip = new ZipArchive(new MemoryStream(template, writable: false), ZipArchiveMode.Read))
        {
            replacement["AndroidManifest.xml"] = PatchManifest(ReadEntry(zip, "AndroidManifest.xml"), packageId, title, orientation);
            replacement["resources.arsc"] = PatchResources(ReadEntry(zip, "resources.arsc"), packageId);
        }

        int eocd = FindEocd(template);
        int cdStart = CheckedOffset(U32(template, eocd + 16), template.Length);
        int cdSize = CheckedOffset(U32(template, eocd + 12), template.Length);
        if (cdStart + (long)cdSize > eocd)
            throw new InvalidDataException("Diretório central do APK inválido.");
        int localEnd = cdStart;
        if (cdStart >= 24 && template.AsSpan(cdStart - 16, 16).SequenceEqual(SigMagic))
        {
            ulong size = U64(template, cdStart - 24);
            if (size < 24 || size > (ulong)(cdStart - 8))
                throw new InvalidDataException("Bloco de assinatura do modelo inválido.");
            localEnd = cdStart - 8 - checked((int)size);
        }

        var oldCentral = new List<byte[]>();
        int pos = cdStart;
        int count = 0;
        while (pos < cdStart + cdSize)
        {
            if (U32(template, pos) != CentralMagic || pos + 46 > eocd)
                throw new InvalidDataException("Entrada ZIP central inválida.");
            int n = U16(template, pos + 28), extra = U16(template, pos + 30), comment = U16(template, pos + 32);
            int size = checked(46 + n + extra + comment);
            if (pos + (long)size > cdStart + cdSize) throw new InvalidDataException("ZIP central truncado.");
            string name = Encoding.UTF8.GetString(template, pos + 46, n);
            if (!replacement.ContainsKey(name) &&
                !name.StartsWith("assets/game/", StringComparison.Ordinal) &&
                !name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                oldCentral.Add(template.AsSpan(pos, size).ToArray());
            pos += size;
            count++;
        }
        if (count != U16(template, eocd + 10)) throw new InvalidDataException("Contagem ZIP inconsistente.");

        using var unsigned = new MemoryStream();
        unsigned.Write(template, 0, localEnd);
        var newCentral = new List<byte[]>();
        foreach (var (name, data) in replacement.OrderBy(x => x.Key, StringComparer.Ordinal))
            newCentral.Add(WriteStoredEntry(unsigned, name, data));
        int directoryOffset = checked((int)unsigned.Position);
        foreach (var item in oldCentral) unsigned.Write(item);
        foreach (var item in newCentral) unsigned.Write(item);
        int directorySize = checked((int)unsigned.Position - directoryOffset);
        int entryCount = oldCentral.Count + newCentral.Count;
        if (entryCount > ushort.MaxValue) throw new InvalidDataException("Muitos arquivos no APK.");
        var end = new byte[22];
        W32(end, 0, EocdMagic);
        W16(end, 8, (ushort)entryCount);
        W16(end, 10, (ushort)entryCount);
        W32(end, 12, (uint)directorySize);
        W32(end, 16, (uint)directoryOffset);
        unsigned.Write(end);
        return SignV2(unsigned.ToArray(), directoryOffset, directorySize, signer);
    }

    /// <summary>APK package segments must start with ASCII letter or underscore.</summary>
    public static bool ValidPackage(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || !value.Contains('.')) return false;
        foreach (var s in value.Split('.'))
        {
            if (s.Length == 0 || !(char.IsLetter(s[0]) && s[0] <= 127 || s[0] == '_')) return false;
            if (s.Any(c => !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')))
                return false;
        }
        return true;
    }

    private static byte[] SignV2(byte[] apk, int cdOffset, int cdSize, IGameApkSigner signer)
    {
        int eo = cdOffset + cdSize;
        var digest = ApkDigest(apk.AsSpan(0, cdOffset), apk.AsSpan(cdOffset, cdSize), apk.AsSpan(eo));
        var digestRecord = LengthPrefixed(Concat(U32Bytes(RsaSha256), LengthPrefixed(digest)));
        var signedData = Concat(LengthPrefixed(digestRecord),
            LengthPrefixed(LengthPrefixed(signer.CertificateDer)),
            LengthPrefixed(Array.Empty<byte>()));
        var signature = signer.Sign(signedData);
        var signatureRecord = LengthPrefixed(Concat(U32Bytes(RsaSha256), LengthPrefixed(signature)));
        var signerData = Concat(LengthPrefixed(signedData),
            LengthPrefixed(signatureRecord),
            LengthPrefixed(signer.PublicKeyDer));
        var v2Value = LengthPrefixed(LengthPrefixed(signerData));
        using var block = new MemoryStream();
        long sizeOfBlock = 24L + 12L + v2Value.Length;
        Write64(block, (ulong)sizeOfBlock);
        Write64(block, checked((ulong)(v2Value.Length + 4)));
        block.Write(U32Bytes(SigIdV2));
        block.Write(v2Value);
        Write64(block, (ulong)sizeOfBlock);
        block.Write(SigMagic);
        var signing = block.ToArray();
        if (signing.Length != sizeOfBlock + 8) throw new InvalidDataException("Tamanho de assinatura inconsistente.");

        var result = new byte[checked(apk.Length + signing.Length)];
        Buffer.BlockCopy(apk, 0, result, 0, cdOffset);
        Buffer.BlockCopy(signing, 0, result, cdOffset, signing.Length);
        Buffer.BlockCopy(apk, cdOffset, result, cdOffset + signing.Length, apk.Length - cdOffset);
        W32(result, checked(eo + signing.Length + 16), checked((uint)(cdOffset + signing.Length)));
        return result;
    }

    private static byte[] ApkDigest(ReadOnlySpan<byte> locals, ReadOnlySpan<byte> directory, ReadOnlySpan<byte> eocd)
    {
        var chunks = new List<byte[]>();
        static void Include(ReadOnlySpan<byte> bytes, List<byte[]> output)
        {
            for (int offset = 0; offset < bytes.Length; offset += 1024 * 1024)
            {
                var chunk = bytes.Slice(offset, Math.Min(1024 * 1024, bytes.Length - offset));
                var temp = new byte[5 + chunk.Length];
                temp[0] = 0xa5;
                W32(temp, 1, (uint)chunk.Length);
                chunk.CopyTo(temp.AsSpan(5));
                output.Add(SHA256.HashData(temp));
            }
        }
        Include(locals, chunks);
        Include(directory, chunks);
        // EOCD already points to the APK signing block start (pre-insertion offset).
        Include(eocd, chunks);
        var aggregate = new byte[checked(5 + chunks.Count * 32)];
        aggregate[0] = 0x5a;
        W32(aggregate, 1, (uint)chunks.Count);
        for (int i = 0; i < chunks.Count; i++) chunks[i].CopyTo(aggregate, 5 + i * 32);
        return SHA256.HashData(aggregate);
    }

    private static byte[] WriteStoredEntry(Stream output, string name, byte[] data)
    {
        var encoded = Encoding.UTF8.GetBytes(name);
        if (encoded.Length > ushort.MaxValue) throw new InvalidDataException("Caminho excessivo.");
        uint crc = Crc32(data);
        int offset = checked((int)output.Position);

        // Android 11+ rejects targetSdk >= 30 APKs unless resources.arsc is STORED
        // and its actual payload begins at a 4-byte ZIP offset. Other stored entries
        // benefit from the same alignment (mmap). Do not zipalign AFTER APK signing:
        // it would invalidate the v2 digest.
        //
        // Use a well-formed local-only ZIP extra field with an unknown tag; its data
        // is padding. A minimum four-byte header ensures even 1-3 bytes of padding
        // remain valid ZIP extra-field structure for strict parsers.
        int headerLength = checked(30 + encoded.Length);
        int extraLength = 4 + ((4 - ((offset + headerLength + 4) & 3)) & 3);
        var local = new byte[headerLength + extraLength];
        W32(local, 0, LocalMagic);
        W16(local, 4, 20);
        W16(local, 6, 0x800); // UTF-8
        W16(local, 10, 0); // STORED, never compress resources.arsc
        W16(local, 12, 0x0021); // DOS date Jan 1 1980
        W32(local, 14, crc);
        W32(local, 18, (uint)data.Length);
        W32(local, 22, (uint)data.Length);
        W16(local, 26, (ushort)encoded.Length);
        W16(local, 28, (ushort)extraLength);
        encoded.CopyTo(local, 30);
        W16(local, headerLength, 0xA11E); // local ZIP padding marker
        W16(local, headerLength + 2, (ushort)(extraLength - 4));
        output.Write(local);
        output.Write(data);

        var central = new byte[46 + encoded.Length];
        W32(central, 0, CentralMagic);
        W16(central, 4, 20);
        W16(central, 6, 20);
        W16(central, 8, 0x800);
        W16(central, 14, 0x0021);
        W32(central, 16, crc);
        W32(central, 20, (uint)data.Length);
        W32(central, 24, (uint)data.Length);
        W16(central, 28, (ushort)encoded.Length);
        W32(central, 42, (uint)offset);
        encoded.CopyTo(central, 46);
        return central;
    }

    private static byte[] PatchManifest(byte[] bytes, string packageId, string title, string orientation)
    {
        var modified = AndroidStringPoolPatcher.Replace(bytes, "io.lunet.game.smoke", packageId);
        // The .NET Android startup provider is process-internal but its authority
        // must still be globally unique. Patching only the manifest's package
        // attribute produces INSTALL_FAILED_CONFLICTING_PROVIDER on game #2.
        // Keep the provider class/name intact; change only its authority.
        modified = AndroidStringPoolPatcher.Replace(modified,
            "io.lunet.game.smoke.mono.MonoRuntimeProvider.__mono_init__",
            packageId + ".mono.MonoRuntimeProvider.__mono_init__");
        // The activity label is a manifest string; the template displays an editable game name.
        modified = AndroidStringPoolPatcher.Replace(modified, "Lunet Game", title, mustFind: false);
        return AndroidManifestOrientationPatcher.Patch(modified, orientation);
    }

    private static byte[] PatchResources(byte[] bytes, string packageId)
    {
        // RES_TABLE_PACKAGE_TYPE: fixed 128 UTF-16 code units at chunk offset + 12.
        var result = (byte[])bytes.Clone();
        var oldName = Encoding.Unicode.GetBytes("io.lunet.game.smoke");
        var newName = Encoding.Unicode.GetBytes(packageId);
        if (newName.Length > 254) throw new ArgumentException("Package ID excede tabela Android.");
        for (int i = 0; i <= result.Length - 256; i += 2)
        {
            if (!result.AsSpan(i, oldName.Length).SequenceEqual(oldName)) continue;
            if (i < 12 || U16(result, i - 12) != 0x0200) continue;
            result.AsSpan(i, 256).Clear();
            newName.CopyTo(result, i);
        }
        // aapt2 can omit package-name section for a package with no resources.
        return result;
    }

    private static byte[] ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException("Modelo de APK sem " + name);
        if (entry.Length > 24 * 1024 * 1024) throw new InvalidDataException("Recurso de template excessivo: " + name);
        using var input = entry.Open();
        using var memory = new MemoryStream();
        input.CopyTo(memory);
        return memory.ToArray();
    }

    private static bool ValidZipPath(string path)
    {
        if (path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || path.Length > 240) return false;
        return path.Split('/').All(part => part.Length > 0 && part is not ("." or ".."));
    }

    private static int FindEocd(byte[] zip)
    {
        for (int i = zip.Length - 22; i >= Math.Max(0, zip.Length - 65557); i--)
            if (U32(zip, i) == EocdMagic && i + 22 + U16(zip, i + 20) == zip.Length) return i;
        throw new InvalidDataException("Modelo APK sem EOCD ZIP válido.");
    }
    private static int CheckedOffset(uint value, int length) => value > length ? throw new InvalidDataException("Offset APK inválido.") : (int)value;
    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = ~0u;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int n = 0; n < 8; n++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0u);
        }
        return ~crc;
    }
    private static byte[] U32Bytes(uint x) { var a = new byte[4]; W32(a, 0, x); return a; }
    private static byte[] LengthPrefixed(byte[] a) => Concat(U32Bytes((uint)a.Length), a);
    private static byte[] Concat(params byte[][] blocks)
    {
        using var memory = new MemoryStream();
        foreach (var b in blocks) memory.Write(b);
        return memory.ToArray();
    }
    private static void Write64(Stream stream, ulong x) { var a = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(a, x); stream.Write(a); }
    private static ushort U16(byte[] b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(offset, 2));
    private static uint U32(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset, 4));
    private static ulong U64(byte[] b, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(offset, 8));
    private static void W16(byte[] b, int offset, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(offset, 2), v);
    private static void W32(byte[] b, int offset, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset, 4), v);
}

/// <summary>Rewrites only the Android binary XML string pool, retaining string indexes and all other chunks.</summary>
internal static class AndroidStringPoolPatcher
{
    public static byte[] Replace(byte[] axml, string from, string to, bool mustFind = true)
    {
        if (axml.Length < 36 || Read16(axml, 0) != 0x0003 || Read16(axml, 2) != 8)
            throw new InvalidDataException("AndroidManifest.xml não é XML binário AXML.");
        int xmlHeader = Read16(axml, 2);
        int pool = xmlHeader;
        if (Read16(axml, pool) != 0x0001) throw new InvalidDataException("AXML sem string pool.");
        int headerSize = Read16(axml, pool + 2);
        int poolSize = checked((int)Read32(axml, pool + 4));
        int count = checked((int)Read32(axml, pool + 8));
        int styles = checked((int)Read32(axml, pool + 12));
        int flags = checked((int)Read32(axml, pool + 16));
        int start = checked((int)Read32(axml, pool + 20));
        int stylesStart = checked((int)Read32(axml, pool + 24));
        if (headerSize < 28 || count > 100000 || poolSize > axml.Length - pool || headerSize + 4L * (count + styles) > start)
            throw new InvalidDataException("String pool AXML inválido.");
        bool utf8 = (flags & 0x100) != 0;
        using var strings = new MemoryStream();
        var offsets = new uint[count];
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            int offset = checked((int)Read32(axml, pool + headerSize + 4 * i));
            int p = checked(pool + start + offset);
            if (p < pool + start || p >= pool + poolSize) throw new InvalidDataException("Offset de string inválido.");
            string value;
            int end;
            if (utf8)
            {
                _ = ReadLength8(axml, ref p);
                int byteCount = ReadLength8(axml, ref p);
                if (p + byteCount >= axml.Length) throw new InvalidDataException("String AXML truncada.");
                value = Encoding.UTF8.GetString(axml, p, byteCount);
                end = p + byteCount + 1;
            }
            else
            {
                int charCount = ReadLength16(axml, ref p);
                int byteCount = checked(charCount * 2);
                if (p + byteCount + 1 >= axml.Length) throw new InvalidDataException("String AXML truncada.");
                value = Encoding.Unicode.GetString(axml, p, byteCount);
                end = p + byteCount + 2;
            }
            offsets[i] = checked((uint)strings.Position);
            if (value == from)
            {
                value = to;
                found = true;
                if (utf8)
                {
                    WriteLength8(strings, value.Length);
                    var newBytes = Encoding.UTF8.GetBytes(value);
                    WriteLength8(strings, newBytes.Length);
                    strings.Write(newBytes);
                    strings.WriteByte(0);
                }
                else
                {
                    WriteLength16(strings, value.Length);
                    strings.Write(Encoding.Unicode.GetBytes(value));
                    strings.WriteByte(0);
                    strings.WriteByte(0);
                }
            }
            else strings.Write(axml, checked(pool + start + offset), end - checked(pool + start + offset));
        }
        if (!found) { if (mustFind) throw new InvalidDataException("Package ID não encontrado no APK modelo."); return axml; }
        while ((strings.Position & 3) != 0) strings.WriteByte(0);
        int previousDataEnd = stylesStart > 0 ? stylesStart : poolSize;
        int newStylesStart = stylesStart == 0 ? 0 : checked(start + (int)strings.Length);
        int newPoolSize = checked(start + (int)strings.Length + poolSize - previousDataEnd);
        var result = new byte[checked(axml.Length + newPoolSize - poolSize)];
        Buffer.BlockCopy(axml, 0, result, 0, pool + start);
        Write32(result, pool + 4, (uint)newPoolSize);
        Write32(result, pool + 24, (uint)newStylesStart);
        for (int i = 0; i < count; i++) Write32(result, pool + headerSize + 4 * i, offsets[i]);
        strings.ToArray().CopyTo(result, pool + start);
        Buffer.BlockCopy(axml, pool + previousDataEnd, result, pool + start + (int)strings.Length, poolSize - previousDataEnd);
        Buffer.BlockCopy(axml, pool + poolSize, result, pool + newPoolSize, axml.Length - pool - poolSize);
        Write32(result, 4, (uint)result.Length);
        return result;
    }
    private static ushort Read16(byte[] a, int i) => BinaryPrimitives.ReadUInt16LittleEndian(a.AsSpan(i, 2));
    private static uint Read32(byte[] a, int i) => BinaryPrimitives.ReadUInt32LittleEndian(a.AsSpan(i, 4));
    private static void Write32(byte[] a, int i, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(a.AsSpan(i, 4), v);
    private static int ReadLength8(byte[] a, ref int p) { int n = a[p++]; return (n & 0x80) == 0 ? n : ((n & 0x7f) << 8) | a[p++]; }
    private static int ReadLength16(byte[] a, ref int p)
    {
        int n = Read16(a, p); p += 2;
        if ((n & 0x8000) == 0) return n;
        int low = Read16(a, p); p += 2;
        return ((n & 0x7fff) << 16) | low;
    }
    private static void WriteLength8(Stream s, int len)
    {
        if (len > 0x7fff) throw new InvalidDataException("String longa demais.");
        if (len > 127) s.WriteByte((byte)((len >> 8) | 0x80));
        s.WriteByte((byte)len);
    }
    private static void WriteLength16(Stream s, int len)
    {
        if (len > 0x7fff) throw new InvalidDataException("String longa demais.");
        s.WriteByte((byte)len); s.WriteByte((byte)(len >> 8));
    }
}
