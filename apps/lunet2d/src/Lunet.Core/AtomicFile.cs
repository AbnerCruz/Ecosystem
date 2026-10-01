using System.Text;

namespace Lunet.Core;

internal static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Grava em arquivo temporário e troca por rename, para não deixar arquivo pela metade.</summary>
    public static void WriteAllText(string path, string text)
    {
        var temporary = path + ".lunet-tmp";
        File.WriteAllText(temporary, text, Utf8);
        File.Move(temporary, path, overwrite: true);
    }
}
