using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-141: the HTML being edited shown as a page, with the vault's files it uses, as a Live Server would.</summary>
public sealed class LivePreviewTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private static string Build(string html, Dictionary<string, string> texts, Dictionary<string, byte[]>? files = null, string path = "Site/index.html") =>
        LivePreview.Build(path, html, p => texts.GetValueOrDefault(p), p => files?.GetValueOrDefault(p) ?? (texts.TryGetValue(p, out var t) ? Encoding.UTF8.GetBytes(t) : null), texts.Keys);

    [Fact]
    public void StylesheetsAreInlinedWithTheirOwnUrls()
    {
        var page = Build("<html><head><link rel=\"stylesheet\" href=\"css/estilo.css\"></head><body></body></html>",
            new() { ["Site/css/estilo.css"] = "body{background:url('../img/fundo.png')}" }, new() { ["Site/img/fundo.png"] = Png });
        Assert.Contains("<style data-urbe-src=\"css/estilo.css\">", page);
        Assert.Contains("url(\"data:image/png;base64," + Convert.ToBase64String(Png) + "\")", page);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", page);
    }

    [Fact]
    public void ScriptsAreInlinedAndCannotCloseTheirTag()
    {
        var page = Build("<body><script src=\"app.js\"></script></body>", new() { ["Site/app.js"] = "document.title = '</script>';" });
        Assert.Contains("<script data-urbe-src=\"app.js\">document.title = '<\\/script>';</script>", page);
    }

    [Fact]
    public void ModulesImportEachOtherThroughEmbeddedCopies()
    {
        var page = Build("<script type=\"module\" src=\"main.js\"></script>", new()
        {
            ["Site/main.js"] = "import { soma } from './lib/soma.js';\nconsole.log(soma(1, 2));",
            ["Site/lib/soma.js"] = "import './ciclo.js';\nexport const soma = (a, b) => a + b;",
            ["Site/lib/ciclo.js"] = "import './soma.js';"
        });
        Assert.Contains("type=\"module\"", page);
        Assert.DoesNotContain("'./lib/soma.js'", page);
        Assert.Contains("data:text/javascript;base64,", page);
    }

    [Fact]
    public void PicturesAndMediaBecomeDataUrls()
    {
        var page = Build("<img src=\"foto.png\" alt=\"x\"><video poster=\"foto.png\"></video><img srcset=\"foto.png 1x, foto.png 2x\">", new(), new() { ["Site/foto.png"] = Png });
        var data = "data:image/png;base64," + Convert.ToBase64String(Png);
        Assert.Contains("src=\"" + data + "\"", page);
        Assert.Contains("poster=\"" + data + "\"", page);
        Assert.Contains("srcset=\"" + data + " 1x, " + data + " 2x\"", page);
    }

    [Fact]
    public void AddressesOutsideTheVaultAndMissingFilesAreLeftAsTheyAre()
    {
        var html = "<img src=\"https://exemplo.com/a.png\"><img src=\"data:image/png;base64,AA==\"><a href=\"#topo\">x</a><img src=\"sumiu.png\">";
        var page = Build(html, new());
        Assert.Contains("src=\"https://exemplo.com/a.png\"", page);
        Assert.Contains("src=\"data:image/png;base64,AA==\"", page);
        Assert.Contains("href=\"#topo\"", page);
        Assert.Contains("src=\"sumiu.png\"", page);
    }

    [Fact]
    public void FetchOfAFileOfTheFolderIsAnsweredFromTheVault()
    {
        var page = Build("<script>fetch('dados.json').then(r => r.json())</script>", new() { ["Site/dados.json"] = "{\"a\":1}", ["Outro/fora.json"] = "{}" });
        Assert.Contains("\"Site/dados.json\":\"data:application/json;base64,", page);
        Assert.DoesNotContain("fora.json", page);   // only the page's own folder
    }

    [Fact]
    public void ErrorsOfThePageAreShownInsideIt()
    {
        var page = Build("<html><head><title>t</title></head><body><p>oi</p></body></html>", new());
        var at = page.IndexOf("urbe-live", StringComparison.Ordinal);
        Assert.True(at >= 0 && at < page.IndexOf("<title>", StringComparison.Ordinal));   // before the page's own scripts
        Assert.Contains("window.addEventListener('error'", page);
        Assert.Contains("<p>oi</p>", page);
    }

    [Fact]
    public void APageWithoutHeadOrHtmlStillWorks()
    {
        var page = Build("<h1>Olá</h1>", new());
        Assert.Contains("<h1>Olá</h1>", page);
        Assert.Contains("urbe-live", page);
    }

    [Fact]
    public void PathsGoUpFoldersAndAreDecoded()
    {
        Assert.Equal("Site/img/a b.png", LivePreview.Resolve("Site/paginas/x.html", "../img/a%20b.png?v=2#c"));
        Assert.Equal("img/a.png", LivePreview.Resolve("Site/x.html", "/img/a.png"));
        Assert.Null(LivePreview.Resolve("Site/x.html", "../../fora.png"));
        Assert.Null(LivePreview.Resolve("Site/x.html", "mailto:a@b"));
    }
}
