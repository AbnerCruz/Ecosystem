using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PageRichBlockRendererTests
{
    [Fact]
    public void ContentAndStructureBlocksRenderDeterministicallyWithoutHostState()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "kind":"urbe-page",
              "meta":{"title":"Blocos"},
              "sections":[
                {"type":"features","props":{"title":"Recursos","items":[{"icon":"⚡","title":"Rápido","text":"Tudo **agora**."}]}},
                {"type":"cards","props":{"items":[
                  {"title":"Site","text":"Abrir","tag":"Web","url":"https://example.com","image":"https://example.com/a.png"},
                  {"title":"Ruim","url":"javascript:alert(1)"}
                ]}},
                {"type":"gallery","props":{"items":[
                  {"image":"https://example.com/a.png","caption":"A"},
                  {"image":"javascript:alert(1)","caption":"Não"}
                ]}},
                {"type":"video","props":{"url":"https://youtu.be/abcdefgh","caption":"Demo"}},
                {"type":"testimonials","props":{"items":[{"text":"**Ótimo**","author":"Ana","role":"Autora"}]}},
                {"type":"stats","props":{"items":[{"value":"42","label":"notas"}]}},
                {"type":"timeline","props":{"items":[{"date":"2026","title":"Marco","text":"Texto **forte**."}]}},
                {"type":"faq","props":{"openFirst":true,"items":[{"q":"Como?","a":"Assim **mesmo**."}]}},
                {"type":"cta","props":{"title":"Ação","text":"Agora","buttons":[
                  {"label":"Seguro","url":"https://example.com","variant":"primary"},
                  {"label":"Bloqueado","url":"javascript:alert(1)","variant":"primary"}
                ]}},
                {"type":"columns","props":{"items":[{"title":"Coluna","markdown":"Texto **rico**."}]}},
                {"type":"pricing","props":{"items":[{"name":"Pro","price":"R$ 10","period":"/mês","features":"Um\nDois","buttonLabel":"Escolher","url":"https://example.com","highlight":true}]}},
                {"type":"contact","props":{"title":"Contato","email":"a@example.com","phone":"+55 (47) 99999-9999","links":[{"label":"Site","url":"https://example.com"}]}},
                {"type":"countdown","props":{"title":"Lançamento","date":"2030-01-01 09:00","done":"Começou!"}},
                {"type":"code","props":{"title":"Código","language":"html","code":"<script>alert(1)</script>"}},
                {"type":"divider","props":{"style":"dots onmouseover=evil"}},
                {"type":"html","props":{"code":"<aside data-custom='1'>HTML livre</aside>"}},
                {"type":"about","props":{"title":"Sobre","image":"javascript:alert(1)","markdown":"Bio **forte**."}},
                {"type":"copyright","props":{"markdown":"Copyright **2026**"}},
                {"type":"dedication","props":{"kind":"epigraph","markdown":"Para todos.","author":"Pessoa"}},
                {"type":"colophon","props":{"markdown":"Produzido no **Urbe**."}}
              ]
            }
            """);

        var html = PageRenderer.RenderBody(page);

        Assert.Contains("<h2>Recursos</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Tudo <strong>agora</strong>.", html, StringComparison.Ordinal);
        Assert.Contains("href='https://example.com'", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<figcaption>A</figcaption>", html, StringComparison.Ordinal);
        Assert.Contains("youtube-nocookie.com/embed/abcdefgh", html, StringComparison.Ordinal);
        Assert.Contains("“<strong>Ótimo</strong>”", html, StringComparison.Ordinal);
        Assert.Contains("<strong>42</strong><span>notas</span>", html, StringComparison.Ordinal);
        Assert.Contains("Texto <strong>forte</strong>.", html, StringComparison.Ordinal);
        Assert.Contains("<details open>", html, StringComparison.Ordinal);
        Assert.Contains("Assim <strong>mesmo</strong>.", html, StringComparison.Ordinal);
        Assert.Contains(">Seguro</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Bloqueado</a>", html, StringComparison.Ordinal);
        Assert.Contains("Texto <strong>rico</strong>.", html, StringComparison.Ordinal);
        Assert.Contains("<span class='pill'>Mais escolhido</span>", html, StringComparison.Ordinal);
        Assert.Contains("mailto:a@example.com", html, StringComparison.Ordinal);
        Assert.Contains("tel:+5547999999999", html, StringComparison.Ordinal);
        Assert.Contains("data-until='2030-01-01T09:00'", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.Contains("class='divider d-dotsonmouseoverevil'", html, StringComparison.Ordinal);
        Assert.Contains("<aside data-custom='1'>HTML livre</aside>", html, StringComparison.Ordinal);
        Assert.Contains("Bio <strong>forte</strong>.", html, StringComparison.Ordinal);
        Assert.Contains("Copyright <strong>2026</strong>", html, StringComparison.Ordinal);
        Assert.Contains("bk-ded-author", html, StringComparison.Ordinal);
        Assert.Contains("Produzido no <strong>Urbe</strong>.", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://vimeo.com/123456", "https://player.vimeo.com/video/123456")]
    [InlineData("https://example.com/movie.mp4", "<video src='https://example.com/movie.mp4'")]
    public void VideoBlockChoosesExpectedSafeEmbed(string url, string expected)
    {
        var page = PageDocument.Parse(
            $$"""
            {
              "version":1,
              "sections":[{"type":"video","props":{"url":"{{url}}"}}]
            }
            """);

        var html = PageRenderer.RenderBody(page);

        Assert.Contains(expected, html, StringComparison.Ordinal);
    }

    [Fact]
    public void GalleryWithOnlyUnsafeItemsReturnsExplicitMissingState()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[
                {"type":"gallery","props":{"title":"Galeria","items":[{"image":"javascript:alert(1)"}]}}
              ]
            }
            """);

        var html = PageRenderer.RenderBody(page);

        Assert.Contains("<h2>Galeria</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Adicione imagens à galeria.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }
}
