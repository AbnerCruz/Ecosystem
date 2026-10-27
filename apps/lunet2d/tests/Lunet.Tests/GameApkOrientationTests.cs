using System.Buffers.Binary;
using System.Text;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>Binary AXML regression: activity orientation, not a fake text manifest.</summary>
public sealed class GameApkOrientationTests
{
    [Theory]
    [InlineData("portrait", 1)]
    [InlineData("landscape", 0)]
    public void BinaryAndroidManifestActivityOrientationMatchesProject(string orientation, int expected)
    {
        byte[] axml = BuildActivityManifest();
        var patched = AndroidManifestOrientationPatcher.Patch(axml, orientation);
        Assert.Equal(expected, Read32(patched, patched.Length - 4));
        Assert.Equal(1, Read32(axml, axml.Length - 4)); // original was not mutated
    }

    [Theory]
    [InlineData("")]
    [InlineData("horizontal")]
    [InlineData("diagonal")]
    public void UnsupportedOrientationIsRejectedBeforePackaging(string orientation) =>
        Assert.Throws<ArgumentException>(() =>
            AndroidManifestOrientationPatcher.Patch(BuildActivityManifest(), orientation));

    // Minimal structurally valid binary XML with a UTF-8 string pool and one <activity>
    // START_ELEMENT with a typed android:screenOrientation="portrait" attribute.
    private static byte[] BuildActivityManifest()
    {
        string[] strings = ["activity", "screenOrientation"];
        using var data = new MemoryStream();
        var offsets = new List<int>();
        foreach (var name in strings)
        {
            offsets.Add((int)data.Position);
            var bytes = Encoding.UTF8.GetBytes(name);
            data.WriteByte((byte)name.Length);
            data.WriteByte((byte)bytes.Length);
            data.Write(bytes);
            data.WriteByte(0);
        }
        while (data.Length % 4 != 0) data.WriteByte(0);
        int poolSize = 28 + strings.Length * 4 + (int)data.Length;
        int startTagSize = 56;
        var b = new byte[8 + poolSize + startTagSize];
        W16(b, 0, 3); W16(b, 2, 8); W32(b, 4, b.Length);
        int pool = 8;
        W16(b, pool, 1); W16(b, pool + 2, 28);
        W32(b, pool + 4, poolSize); W32(b, pool + 8, strings.Length);
        W32(b, pool + 16, 0x100); // UTF-8 pool
        W32(b, pool + 20, 28 + strings.Length * 4);
        for (int i = 0; i < offsets.Count; i++)
            W32(b, pool + 28 + i * 4, offsets[i]);
        data.ToArray().CopyTo(b, pool + 28 + strings.Length * 4);

        int tag = pool + poolSize;
        W16(b, tag, 0x0102); W16(b, tag + 2, 16); W32(b, tag + 4, startTagSize);
        W32(b, tag + 12, -1); // comment
        W32(b, tag + 16, -1); // ns
        W32(b, tag + 20, 0);  // element: activity
        W16(b, tag + 24, 20); // attributeStart
        W16(b, tag + 26, 20); // attributeSize
        W16(b, tag + 28, 1);  // attributeCount
        int a = tag + 36;
        W32(b, a, -1); W32(b, a + 4, 1); W32(b, a + 8, -1);
        W16(b, a + 12, 8); b[a + 15] = 0x10; W32(b, a + 16, 1);
        return b;
    }

    private static int Read32(byte[] b, int p) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p, 4));
    private static void W16(byte[] b, int p, int value) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p, 2), checked((ushort)value));
    private static void W32(byte[] b, int p, int value) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(p, 4), value);
}
