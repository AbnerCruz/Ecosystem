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

    public static async Task WriteAndCheckAsync(Func<Task<Stream>> openWrite, Func<Task<Stream>> openRead, byte[] bytes)
    {
        await using (var stream = await openWrite())
        {
            if (stream.CanSeek) stream.SetLength(0);
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();
        }
        byte[] back;
        try
        {
            await using var read = await openRead();
            using var copy = new MemoryStream();
            await read.CopyToAsync(copy);
            back = copy.ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException) { return; }
        if (back.Length != bytes.Length || !SHA256.HashData(back).AsSpan().SequenceEqual(SHA256.HashData(bytes)))
            throw new IOException("O arquivo salvo não confere com o que foi gerado (" + PublicationSizes(back.Length) + " de " + PublicationSizes(bytes.Length) +
                                  "). Salve de novo, de preferência em outra pasta.");
    }

    private static string PublicationSizes(long bytes) => Urbe.Core.PublicationPackage.Size(bytes);
}
