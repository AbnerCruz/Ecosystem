using System.Buffers.Binary;
using System.Reflection;
using Lunet.Core;

namespace Lunet.Tests;

/// <summary>
/// Guard the Android 11+ installability contract: uncompressed APK entries
/// must have 4-byte aligned data, especially resources.arsc.
/// This runs on Linux without Android SDK; CI also executes zipalign against
/// the actual generated standalone APK.
/// </summary>
public sealed class OfflineApkZipAlignmentTests
{
    [Theory]
    [InlineData("resources.arsc")]
    [InlineData("AndroidManifest.xml")]
    [InlineData("assets/game/Game.dll")]
    [InlineData("assets/game/Assets/sprite.png")]
    public void StoredEntryDataStartsOnFourByteBoundaryForEveryOffset(string name)
    {
        MethodInfo writeStored = typeof(OfflineGameApkBuilder).GetMethod(
            "WriteStoredEntry", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Stored ZIP entry writer missing.");

        for (int leadingBytes = 0; leadingBytes < 8; leadingBytes++)
        {
            using var stream = new MemoryStream();
            stream.Write(new byte[leadingBytes]);
            byte[] central = (byte[])(writeStored.Invoke(null, new object[]
            {
                stream, name, new byte[] { 1, 2, 3, 4 }
            }) ?? throw new InvalidOperationException("ZIP central entry missing."));
            var contents = stream.ToArray();
            int fileStart = leadingBytes;
            Assert.Equal(0x04034b50u, Read32(contents, fileStart));
            Assert.Equal(0, Read16(contents, fileStart + 8)); // stored
            int nameLen = Read16(contents, fileStart + 26);
            int localExtraLen = Read16(contents, fileStart + 28);
            int payloadStart = fileStart + 30 + nameLen + localExtraLen;
            Assert.Equal(0, payloadStart % 4);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, contents.AsSpan(payloadStart, 4).ToArray());
            Assert.Equal(0x02014b50u, Read32(central, 0));
            Assert.Equal((uint)fileStart, Read32(central, 42));
            Assert.Equal(0, Read16(central, 10)); // stored in central directory, too
        }
    }

    private static ushort Read16(byte[] bytes, int index) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index, 2));

    private static uint Read32(byte[] bytes, int index) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index, 4));
}
