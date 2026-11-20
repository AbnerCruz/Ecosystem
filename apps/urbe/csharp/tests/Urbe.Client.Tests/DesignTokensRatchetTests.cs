using System.Text.RegularExpressions;

namespace Urbe.Client.Tests;

/// <summary>
/// #1005 (design system): the interface's colours and type sizes come from <c>UrbeTokens</c>/<c>UrbeTheme</c>. Many
/// screens still write them by hand; this ratchet lets that number fall and never grow. When a screen moves to the
/// tokens, lower the baseline here to the new count, in the same change.
/// </summary>
public sealed partial class DesignTokensRatchetTests
{
    private const int HexColoursBaseline = 87, FontSizesBaseline = 300;

    private static IEnumerable<string> ClientSources()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Urbe.Client"));
        Assert.True(Directory.Exists(root), root);
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .Where(f => Path.GetFileName(f) is not ("UrbeTheme.cs" or "UrbeTokens.cs"));
    }

    [Fact]
    public void HandWrittenColoursAndTypeSizesDoNotGrow()
    {
        int hex = 0, sizes = 0;
        foreach (var file in ClientSources())
        {
            var text = File.ReadAllText(file);
            hex += Hex().Count(text);
            sizes += Size().Count(text);
        }
        Assert.True(hex <= HexColoursBaseline, hex + " cores escritas à mão (máximo " + HexColoursBaseline + "): use UrbeTokens.Color");
        Assert.True(sizes <= FontSizesBaseline, sizes + " tamanhos de fonte escritos à mão (máximo " + FontSizesBaseline + "): use UrbeTokens.Type");
    }

    [GeneratedRegex("\"#[0-9a-fA-F]{6,8}\"")] private static partial Regex Hex();
    [GeneratedRegex(@"FontSize = [0-9]+(\.[0-9]+)?\b")] private static partial Regex Size();
}
