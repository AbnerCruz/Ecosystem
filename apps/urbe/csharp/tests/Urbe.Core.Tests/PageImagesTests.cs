using System.Text;
using Urbe.Core.Pages;

namespace Urbe.Core.Tests;

/// <summary>
/// CST-059 (owner: an image added to a page did not show): the page's HTML is shown by a WebView that reaches nothing
/// outside it, and is downloaded as a single file, so an image named by its place in the vault must travel inside it.
/// </summary>
public sealed class PageImagesTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
    private static readonly Dictionary<string, byte[]> Vault = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Imagens/foto.png"] = Png,
        ["Projetos/Site/capa.jpg"] = [0xFF, 0xD8, 0xFF, 9],
        ["Imagens/minha foto.webp"] = [1, 1, 1],
        ["Imagens/dupla.png"] = [2], ["Outra/dupla.png"] = [3],
        ["Notas/texto.md"] = Encoding.UTF8.GetBytes("# não é imagem"),
    };

    private static Task<string> Inline(string html, string page = "Projetos/Site/Site.page.json") =>
        PageImages.InlineAsync(html, page, Vault.Keys.ToList(), p => Task.FromResult(Vault.TryGetValue(p, out var b) ? b : null));

    private static string Data(string mime, byte[] bytes) => "data:" + mime + ";base64," + Convert.ToBase64String(bytes);

    [Fact]
    public async Task AVaultPathBecomesTheImageItself()
    {
        var html = await Inline("<figure><img src=\"Imagens/foto.png\" alt=\"x\" loading=\"lazy\"></figure>");
        Assert.Contains("src=\"" + Data("image/png", Png) + "\"", html);
    }

    [Fact]
    public async Task PathsResolveFromThePagesFolderThenTheVaultThenAUniqueName()
    {
        Assert.Contains(Data("image/jpeg", [0xFF, 0xD8, 0xFF, 9]), await Inline("<img src=\"capa.jpg\">"));
        Assert.Contains(Data("image/jpeg", [0xFF, 0xD8, 0xFF, 9]), await Inline("<img src=\"./capa.jpg\">"));
        Assert.Contains(Data("image/png", Png), await Inline("<img src=\"../../Imagens/foto.png\">"));
        Assert.Contains(Data("image/png", Png), await Inline("<img src=\"/Imagens/foto.png\">"));
        Assert.Contains(Data("image/png", Png), await Inline("<img src=\"foto.png\">"));
        Assert.Contains(Data("image/webp", [1, 1, 1]), await Inline("<img src=\"Imagens/minha%20foto.webp\">"));
        Assert.Contains(Data("image/webp", [1, 1, 1]), await Inline("<img src=\"Imagens/minha foto.webp\">"));
        // two files with that name and none at the page: nothing is guessed
        Assert.Contains("src=\"dupla.png\"", await Inline("<img src=\"dupla.png\">"));
    }

    [Fact]
    public async Task BackgroundsAreResolvedToo()
    {
        var html = await Inline("<section style=\"background-image:url('Imagens/foto.png')\"></section>");
        Assert.Contains("url('" + Data("image/png", Png) + "')", html);
    }

    [Fact]
    public async Task WebAddressesDataAndOtherFilesAreLeftAlone()
    {
        const string html = "<img src=\"https://exemplo.com/a.png\"><img src=\"data:image/png;base64,AAAA\"><img src=\"Notas/texto.md\"><img src=\"nada.png\"><a href=\"Imagens/foto.png\">x</a>";
        Assert.Equal(html, await Inline(html));
    }

    [Fact]
    public async Task AnImageUsedTwiceIsReadOnce()
    {
        int reads = 0;
        var html = await PageImages.InlineAsync("<img src=\"Imagens/foto.png\"><img src=\"Imagens/foto.png\">", "Site.page.json", Vault.Keys.ToList(),
            p => { reads++; return Task.FromResult(Vault.GetValueOrDefault(p)); });
        Assert.Equal(1, reads);
        Assert.Equal(2, html.Split(Data("image/png", Png)).Length - 1);
    }
}
