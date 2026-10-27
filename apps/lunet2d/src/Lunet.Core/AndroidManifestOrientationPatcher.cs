using System.Buffers.Binary;
using System.Text;

namespace Lunet.Core;

/// <summary>
/// Updates the existing screenOrientation integer on the standalone Activity
/// in Android's binary AXML; no SDK is required on the phone. Keeps the rest of
/// the manifest unchanged so signing happens after all modifications.
/// </summary>
public static class AndroidManifestOrientationPatcher
{
    public static byte[] Patch(byte[] axml, string orientation)
    {
        ArgumentNullException.ThrowIfNull(axml);
        int value = orientation switch
        {
            "portrait" => 1,  // ActivityInfo.SCREEN_ORIENTATION_PORTRAIT
            "landscape" => 0, // ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE
            _ => throw new ArgumentException("Orientação inválida: use portrait ou landscape.", nameof(orientation))
        };
        if (axml.Length < 36 || U16(axml, 0) != 0x0003 || U16(axml, 2) != 8 ||
            U32(axml, 4) != axml.Length)
            throw new InvalidDataException("AndroidManifest.xml binário inválido.");
        int pool = 8;
        if (U16(axml, pool) != 1) throw new InvalidDataException("AXML sem string pool.");
        int poolHeader = U16(axml, pool + 2);
        int poolSize = U32(axml, pool + 4);
        int count = U32(axml, pool + 8);
        int styles = U32(axml, pool + 12);
        bool utf8 = (U32(axml, pool + 16) & 0x100) != 0;
        int stringStart = U32(axml, pool + 20);
        if (poolHeader < 28 || poolSize > axml.Length - pool || count > 100000 ||
            (long)poolHeader + 4L * (count + styles) > stringStart)
            throw new InvalidDataException("String pool do manifesto inválido.");

        // Resolve the compiler-generated indexes, never hardcode an index that
        // can move when AAPT2 or .NET Android changes its string ordering.
        string[] names = new string[count];
        for (int i = 0; i < count; i++)
        {
            int offset = U32(axml, pool + poolHeader + 4 * i);
            int p = checked(pool + stringStart + offset);
            int end = checked(pool + poolSize);
            if (p < pool + stringStart || p >= end)
                throw new InvalidDataException("Offset de string AXML inválido.");
            if (utf8)
            {
                _ = ReadLength8(axml, ref p, end);
                int bytes = ReadLength8(axml, ref p, end);
                if (bytes < 0 || p + (long)bytes >= end)
                    throw new InvalidDataException("String UTF8 AXML truncada.");
                names[i] = Encoding.UTF8.GetString(axml, p, bytes);
            }
            else
            {
                int chars = ReadLength16(axml, ref p, end);
                if (chars < 0 || p + 2L * chars + 1 >= end)
                    throw new InvalidDataException("String UTF16 AXML truncada.");
                names[i] = Encoding.Unicode.GetString(axml, p, checked(chars * 2));
            }
        }

        var result = (byte[])axml.Clone();
        int changed = 0;
        for (int pos = pool + poolSize; pos < axml.Length;)
        {
            if (pos + 8 > axml.Length) throw new InvalidDataException("Chunk AXML truncado.");
            int type = U16(axml, pos);
            int size = U32(axml, pos + 4);
            if (size < 8 || size > axml.Length - pos)
                throw new InvalidDataException("Chunk AXML inválido.");
            if (type == 0x0102) // RES_XML_START_ELEMENT_TYPE
            {
                if (size < 36 || U16(axml, pos + 2) < 16)
                    throw new InvalidDataException("Elemento AXML inválido.");
                int tagName = U32(axml, pos + 20);
                if (tagName >= 0 && tagName < names.Length && names[tagName] == "activity")
                {
                    int attributeStart = U16(axml, pos + 24);
                    int attributeSize = U16(axml, pos + 26);
                    int attributeCount = U16(axml, pos + 28);
                    if (attributeSize < 20 || pos + 16L + attributeStart + (long)attributeSize * attributeCount > pos + size)
                        throw new InvalidDataException("Atributos do Activity inválidos.");
                    for (int i = 0; i < attributeCount; i++)
                    {
                        int a = checked(pos + 16 + attributeStart + attributeSize * i);
                        int nameIndex = U32(axml, a + 4);
                        if (nameIndex < 0 || nameIndex >= names.Length || names[nameIndex] != "screenOrientation")
                            continue;
                        if (U16(axml, a + 12) != 8 || axml[a + 15] is not (0x10 or 0x11))
                            throw new InvalidDataException("screenOrientation não possui tipo inteiro.");
                        W32(result, a + 16, value);
                        changed++;
                    }
                }
            }
            pos += size;
        }
        if (changed != 1)
            throw new InvalidDataException($"Esperada uma Activity com screenOrientation no modelo; encontradas {changed}.");
        return result;
    }

    private static int U16(byte[] b, int p) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(p, 2));
    private static int U32(byte[] b, int p) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p, 4));
    private static void W32(byte[] b, int p, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(p, 4), v);

    private static int ReadLength8(byte[] b, ref int p, int end)
    {
        if (p >= end) throw new InvalidDataException("Comprimento UTF8 AXML truncado.");
        int n = b[p++];
        if ((n & 0x80) == 0) return n;
        if (p >= end) throw new InvalidDataException("Comprimento UTF8 AXML truncado.");
        return ((n & 0x7f) << 8) | b[p++];
    }

    private static int ReadLength16(byte[] b, ref int p, int end)
    {
        if (p + 2 > end) throw new InvalidDataException("Comprimento UTF16 AXML truncado.");
        int n = U16(b, p);
        p += 2;
        if ((n & 0x8000) == 0) return n;
        if (p + 2 > end) throw new InvalidDataException("Comprimento UTF16 AXML truncado.");
        int low = U16(b, p);
        p += 2;
        return ((n & 0x7fff) << 16) | low;
    }
}
