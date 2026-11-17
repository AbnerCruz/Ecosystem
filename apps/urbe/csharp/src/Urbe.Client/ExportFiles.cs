using System.Security.Cryptography;
using Avalonia.Platform.Storage;

namespace Urbe.Client;

/// <summary>
/// CST-180: an exported file is written whole (what was there before is cut, never left at the end) and read back to
/// confirm it is the file that was made. A difference is an error, not a success message; a place that does not let
/// the file be read back right away (some cloud folders) is trusted as written, since nothing better can be known.
/// </summary>
public static class ExportFiles
{
    public static Task WriteAndCheckAsync(IStorageFile file, byte[] bytes) => WriteAndCheckAsync(file.OpenWriteAsync, file.OpenReadAsync, bytes);

    public static Task WriteAndCheckAsync(Func<Task<Stream>> openWrite, Func<Task<Stream>> openRead, byte[] bytes) =>
        WriteAndCheckAsync(openWrite, openRead, () => new MemoryStream(bytes, writable: false));

    /// <summary>The same, streamed: a video is copied and compared without being held in memory.</summary>
    public static async Task WriteAndCheckAsync(Func<Task<Stream>> openWrite, Func<Task<Stream>> openRead, Func<Stream> openSource)
    {
        byte[] made;
        long length;
        await using (var source = openSource())
        await using (var stream = await openWrite())
        {
            if (stream.CanSeek) stream.SetLength(0);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1 << 16];
            length = 0;
            for (int n; (n = await source.ReadAsync(buffer)) > 0; length += n)
            {
                hash.AppendData(buffer, 0, n);
                await stream.WriteAsync(buffer.AsMemory(0, n));
            }
            await stream.FlushAsync();
            made = hash.GetHashAndReset();
        }
        byte[] back;
        long backLength;
        try
        {
            await using var read = await openRead();
            (back, backLength) = await HashAsync(read);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException) { return; }
        if (backLength != length || !back.AsSpan().SequenceEqual(made))
            throw new IOException("O arquivo salvo não confere com o que foi gerado (" + PublicationSizes(backLength) + " de " + PublicationSizes(length) +
                                  "). Salve de novo, de preferência em outra pasta.");
    }

    private static async Task<(byte[] Hash, long Length)> HashAsync(Stream stream)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1 << 16];
        long length = 0;
        for (int n; (n = await stream.ReadAsync(buffer)) > 0; length += n) hash.AppendData(buffer, 0, n);
        return (hash.GetHashAndReset(), length);
    }

    private static string PublicationSizes(long bytes) => Urbe.Core.PublicationPackage.Size(bytes);
}
