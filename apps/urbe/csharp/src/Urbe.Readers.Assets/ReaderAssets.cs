namespace Urbe.Readers.Assets;

/// <summary>The bundled reader engines, opened only by the reader that needs them.</summary>
public static class ReaderAssets
{
    public static Stream Open(string name) =>
        typeof(ReaderAssets).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException("Recurso de leitura não empacotado: " + name);
}
