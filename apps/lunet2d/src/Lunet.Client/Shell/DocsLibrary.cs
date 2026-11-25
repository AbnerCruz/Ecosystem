using Lunet.Docs;

namespace Lunet.Client.Shell;

/// <summary>
/// The offline documentation of the shell (P3): the generated API reference, the manual and the guides, read from the
/// client's embedded resources — the same files the previous Android screens ship as assets. Works without internet.
/// </summary>
public static class DocsLibrary
{
    private static readonly Lazy<DocumentationBrowser> Shared = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>A browser with its own history over the shared, loaded documentation.</summary>
    public static DocumentationBrowser NewBrowser() => Shared.Value.NewNavigation();

    /// <summary>Loads the documentation in the background (it is a few MB of JSON), so the first open is instant.</summary>
    public static Task WarmUpAsync() => Task.Run(() => _ = Shared.Value);

    private static DocumentationBrowser Load()
    {
        var assembly = typeof(DocsLibrary).Assembly;
        string Read(string name)
        {
            using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Recurso ausente: " + name);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        var guides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames())
            if (name.StartsWith("lunet/guides/", StringComparison.Ordinal) && name.EndsWith(".md", StringComparison.Ordinal))
                guides[Path.GetFileNameWithoutExtension(name["lunet/guides/".Length..])] = Read(name);
        return new DocumentationBrowser(ApiDocumentation.FromJson(Read("lunet/api.json")), guides, ManualCatalog.Read(Read("lunet/manual-catalog.json")));
    }
}
