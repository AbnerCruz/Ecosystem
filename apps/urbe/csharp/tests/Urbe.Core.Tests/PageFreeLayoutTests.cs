using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class PageFreeLayoutTests
{
    [Fact]
    public void NormalizerKeepsValidTreeRepairsIdsAndReportsInvalidNodesAndStyles()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[{
                "id":"free1",
                "type":"free",
                "props":{
                  "sheet":"page",
                  "root":{
                    "id":"root",
                    "type":"box",
                    "style":{"display":"row","minCol":"240px","gap":"20px"},
                    "mobile":{"display":"stack"},
                    "children":[
                      {"id":"a","type":"text","content":{"text":"A"}},
                      {"id":"a","type":"heading","content":{"text":"B","level":1},"children":[{"type":"text"}]},
                      {"type":"xpto"},
                      {"id":"box","type":"box","style":{"width":"banana","color":"red;}body{x","gap":".5rem"},"children":[]}
                    ]
                  }
                },
                "style":{}
              }]
            }
            """);

        var result = PageNormalizer.Normalize(page);

        Assert.False(result.IsValid);
        var root = result.Page.Raw["sections"]!.AsArray()[0]!["props"]!["root"]!.AsObject();
        var children = root["children"]!.AsArray();

        Assert.Equal(3, children.Count);
        var ids = children
            .OfType<JsonObject>()
            .Select(node => node["id"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());

        Assert.Null(children[1]!["children"]);
        Assert.Contains(
            result.Warnings,
            issue => issue.Path.EndsWith(".children", StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            issue => issue.Path.EndsWith(".children[2].type", StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            issue => issue.Path.EndsWith(".style.width", StringComparison.Ordinal));
        Assert.Contains(
            result.Errors,
            issue => issue.Path.EndsWith(".style.color", StringComparison.Ordinal));
        Assert.Equal(
            ".5rem",
            children[2]!["style"]!["gap"]!.GetValue<string>());
        Assert.Null(children[2]!["style"]!["width"]);
    }

    [Fact]
    public void ResponsiveLayoutProducesScopedBaseTabletAndMobileCss()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[{
                "id":"s1",
                "type":"free",
                "props":{"root":{
                  "id":"root",
                  "type":"box",
                  "style":{"display":"row","minCol":"240px","gap":"20px"},
                  "mobile":{"display":"stack"},
                  "children":[
                    {
                      "id":"t",
                      "type":"heading",
                      "content":{"text":"Oi","level":1},
                      "style":{"size":"3rem"},
                      "tablet":{"size":"2.4rem"},
                      "mobile":{"size":"2rem","display":"none"}
                    },
                    {
                      "id":"g",
                      "type":"box",
                      "style":{"display":"grid","columns":3},
                      "tablet":{"columns":2},
                      "children":[]
                    }
                  ]
                }},
                "style":{}
              }]
            }
            """);

        var normalized = PageNormalizer.Normalize(page);
        Assert.True(normalized.IsValid);

        var html = PageRenderer.RenderBody(normalized.Page);

        Assert.Contains(
            ".fx-s1-root{display:flex;flex-direction:row;flex-wrap:wrap;gap:20px;--fx-basis:240px}",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            ".fx-s1-root>*{flex:1 1 var(--fx-basis,220px)}",
            html,
            StringComparison.Ordinal);
        Assert.Contains("@media (max-width:900px)", html, StringComparison.Ordinal);
        Assert.Contains("font-size:2.4rem", html, StringComparison.Ordinal);
        Assert.Contains("repeat(2,minmax(0,1fr))", html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:600px)", html, StringComparison.Ordinal);
        Assert.Contains("flex-direction:column", html, StringComparison.Ordinal);
        Assert.Contains("display:none;font-size:2rem", html, StringComparison.Ordinal);
        Assert.Contains("<h1 class='fx fx-s1-t'>Oi</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeCssUrlsClassesAndTextCannotBreakTheGeneratedPage()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[{
                "id":"s1",
                "type":"free",
                "props":{"root":{
                  "type":"box",
                  "style":{
                    "bgImage":"https://x/a.png') ;}</style><script>alert(1)</script>",
                    "className":"a\"><b",
                    "css":"</style><script>alert(2)</script>"
                  },
                  "children":[
                    {"type":"text","content":{"text":"<img src=x onerror=alert(3)>"}},
                    {"type":"heading","content":{"text":"<script>alert(4)</script>"}},
                    {"type":"button","content":{"label":"<b>x","url":"javascript:alert(5)"}}
                  ]
                }},
                "style":{}
              }]
            }
            """);

        var normalized = PageNormalizer.Normalize(page);
        var html = PageRenderer.Render(normalized.Page);

        Assert.DoesNotContain("<script>alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img src=x onerror", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:alert", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("</style><script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class='a'><b", html, StringComparison.Ordinal);
        Assert.Contains("&lt;img src=x onerror=alert(3)&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NewPiecesRenderSafelyAndEmbedOnlyHttps()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[{
                "id":"s1",
                "type":"free",
                "props":{"root":{
                  "type":"box",
                  "children":[
                    {"type":"table","content":{"md":"| A | B |\n|---|---|\n| 1 | 2 |"}},
                    {"type":"code","content":{"code":"<b>x</b>","lang":"js"}},
                    {"type":"formula","content":{"tex":"a^2+b^2=c^2"}},
                    {"type":"badge","content":{"text":"Novo"}},
                    {"type":"embed","content":{"url":"https://example.com/m","title":"Mapa"}},
                    {"type":"embed","content":{"url":"javascript:alert(1)","title":"Ruim"}},
                    {"id":"c","type":"heading","content":{"text":"OK"},"style":{"size":"clamp(1.5rem, 5vw, 3rem)"}},
                    {"id":"x","type":"heading","content":{"text":"X"},"style":{"size":"calc(1px);}body{x:1"}}
                  ]
                }},
                "style":{}
              }]
            }
            """);

        var normalized = PageNormalizer.Normalize(page);
        var html = PageRenderer.RenderBody(normalized.Page);

        Assert.Contains("<table>", html, StringComparison.Ordinal);
        Assert.Contains("fx-badge", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>x</b>", html, StringComparison.Ordinal);
        Assert.Contains("sandbox='allow-scripts allow-same-origin allow-popups allow-forms'", html, StringComparison.Ordinal);
        Assert.Contains("src='https://example.com/m'", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clamp(1.5rem, 5vw, 3rem)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("body{x:1", html, StringComparison.Ordinal);
        Assert.Contains(
            normalized.Errors,
            issue => issue.Path.EndsWith(".style.size", StringComparison.Ordinal));
    }

    [Fact]
    public void VaultNoteAndBookSheetModesUseCoreDocumentsAndPersistedSheetSetting()
    {
        var documents = new DocumentStore();
        documents.Upsert(
            new DocumentInput
            {
                Id = "doc_a",
                Path = "Cap/01 A.md",
                Title = "A",
                Content = "# A\n\nPrimeiro parágrafo."
            });

        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "layout":{"format":"book"},
              "sections":[
                {"id":"full","type":"free","props":{"sheet":"full","root":{"type":"box","children":[
                  {"type":"note","content":{"path":"Cap/01 A.md","showTitle":true}},
                  {"type":"pagebreak"}
                ]}},"style":{}},
                {"id":"flow","type":"free","props":{"sheet":"flow","root":{"type":"box","children":[]}},"style":{}},
                {"id":"page","type":"free","props":{"sheet":"page","root":{"type":"box","children":[]}},"style":{}}
              ]
            }
            """);

        var normalized = PageNormalizer.Normalize(page);
        var html = PageRenderer.RenderBody(normalized.Page, documents);

        Assert.Contains("class='sheet bk-free bk-full bk-bleed'", html, StringComparison.Ordinal);
        Assert.Contains("class='sheet flow'", html, StringComparison.Ordinal);
        Assert.Contains("class='sheet bk-free'", html, StringComparison.Ordinal);
        Assert.Contains("<h2>A</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Primeiro parágrafo.", html, StringComparison.Ordinal);
        Assert.Contains("fx-break", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ClampIsAcceptedButCssBreakingLengthIsRejected()
    {
        var page = PageDocument.Parse(
            """
            {
              "version":1,
              "sections":[{
                "type":"free",
                "props":{"root":{
                  "type":"box",
                  "children":[
                    {"id":"ok","type":"heading","content":{"text":"OK"},"style":{"size":"clamp(1.5rem, 5vw, 3rem)"}},
                    {"id":"bad","type":"heading","content":{"text":"Bad"},"style":{"size":"calc(1px);}body{x:1"}}
                  ]
                }},
                "style":{}
              }]
            }
            """);

        var result = PageNormalizer.Normalize(page);
        var root = result.Page.Raw["sections"]!.AsArray()[0]!["props"]!["root"]!.AsObject();
        var children = root["children"]!.AsArray();

        Assert.Equal(
            "clamp(1.5rem, 5vw, 3rem)",
            children[0]!["style"]!["size"]!.GetValue<string>());
        Assert.Null(children[1]!["style"]);
        Assert.Contains(
            result.Errors,
            issue => issue.Path.EndsWith(".style.size", StringComparison.Ordinal));
    }
}
