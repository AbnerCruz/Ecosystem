using Lunet.Docs;

namespace Lunet.Tests;

public class DocsTests
{
    internal static ApiDocumentation Generate()
    {
        var assembly = typeof(Game).Assembly;
        var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
        Assert.True(File.Exists(xmlPath), "Lunet.Framework.xml deve ser copiado para a saída dos testes");
        using var stream = File.OpenRead(xmlPath);
        return ApiDocGenerator.Generate(assembly, new XmlDocReader(stream), "0.0.1");
    }

    [Fact]
    public void Generates_TypesAndMembersWithSignaturesFromTheFramework()
    {
        var docs = Generate();
        var batch = docs.FindType("SpriteBatch")!;
        Assert.Equal("Lunet.Graphics", batch.Namespace);
        Assert.Equal("class", batch.Kind);
        Assert.Contains("public sealed class SpriteBatch", batch.Signature);
        Assert.False(string.IsNullOrWhiteSpace(batch.Summary));

        var begin = batch.Members.First(m => m.Name == "Begin" && m.Parameters.Count == 4);
        Assert.Equal("method", begin.Kind);
        Assert.Contains("BlendState? blend = null", begin.Signature);
        Assert.Contains("RectangleF? clip = null", begin.Signature);
        Assert.Equal("M:Lunet.Graphics.SpriteBatch.Begin(Lunet.Graphics.BlendState,Lunet.Graphics.SamplerState,Lunet.Graphics.Shader,System.Nullable{Lunet.RectangleF})", begin.Id);
        Assert.Contains("Mistura", begin.Parameters.First(p => p.Name == "blend").Description);
    }

    [Fact]
    public void Members_CoverPropertiesFieldsEventsEnumsAndConstructors()
    {
        var docs = Generate();
        var game = docs.FindType("Game")!;
        Assert.Contains(game.Members, m => m.Kind == "property" && m.Name == "Configuration" && m.Signature.Contains("{ get; }"));
        Assert.Contains(game.Members, m => m.Name == "Update" && m.Signature.StartsWith("protected virtual void Update(GameTime time)"));

        var log = docs.FindType("GameLog")!;
        Assert.Contains(log.Members, m => m.Kind == "event" && m.Name == "Written");

        var mode = docs.FindType("BlendMode")!;
        Assert.Equal("enum", mode.Kind);
        Assert.Contains(mode.Members, m => m.Kind == "enum value" && m.Name == "Additive");

        var texture = docs.FindType("Texture2D")!;
        Assert.Contains(texture.Members, m => m.Name == "CreateSolid" && m.Signature.Contains("static Texture2D CreateSolid"));
        Assert.Contains(docs.FindType("SpriteBatch")!.Members, m => m.Kind == "constructor");
    }

    [Fact]
    public void Ids_MatchTheRoslynDocumentationCommentIds_SoTheEditorCanLinkSymbolsToDocs()
    {
        var docs = Generate();
        var analyzer = new Lunet.Editor.CodeAnalyzer(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        analyzer.SetFile("G.cs", "using Lunet; using Lunet.Graphics; public class G { void M(SpriteBatch b, Texture2D t) { b.Draw(t, new System.Numerics.Vector2(), Color.White); } }");
        var code = "using Lunet; using Lunet.Graphics; public class G { void M(SpriteBatch b, Texture2D t) { b.Draw(t, new System.Numerics.Vector2(), Color.White); } }";
        var hover = analyzer.GetHover("G.cs", code.IndexOf("Draw(", StringComparison.Ordinal) + 1);
        Assert.NotNull(hover?.DocumentationId);
        Assert.True(docs.TryFind(hover!.DocumentationId!, out var type, out var member), hover.DocumentationId);
        Assert.Equal("SpriteBatch", type.Name);
        Assert.Equal("Draw", member!.Name);
    }

    [Fact]
    public void EveryPublicTypeHasASummary_AndMostMembersAreDocumented()
    {
        var docs = Generate();
        var missingTypes = docs.Types.Where(t => string.IsNullOrWhiteSpace(t.Summary)).Select(t => t.Name).ToList();
        Assert.True(missingTypes.Count == 0, "Tipos sem resumo: " + string.Join(", ", missingTypes));

        var members = docs.Types.SelectMany(t => t.Members.Select(m => (Type: t, Member: m))).ToList();
        var missing = members.Where(x => string.IsNullOrWhiteSpace(x.Member.Summary)).Select(x => $"{x.Type.Name}.{x.Member.Name}").ToList();
        var coverage = 1.0 - (double)missing.Count / members.Count;
        Assert.True(coverage >= 0.95, $"Cobertura de resumos {coverage:P0}; faltam: {string.Join(", ", missing.Take(40))}");
    }

    [Fact]
    public void Search_RanksNamesBeforeSummaryMatches()
    {
        var docs = Generate();
        var results = docs.Search("SpriteBatch");
        Assert.Equal("SpriteBatch", results[0].Title);
        Assert.Contains(docs.Search("vibração"), r => r.Title.Contains("Vibrate") || r.Title.Contains("Haptics"));
        Assert.Empty(docs.Search("   "));
        Assert.Contains(docs.Search("Texture2D.CreateCircle"), r => r.Title == "Texture2D.CreateCircle" || r.Title.StartsWith("Texture2D"));
    }

    [Fact]
    public void Json_RoundTripsWithoutLoss()
    {
        var docs = Generate();
        var back = ApiDocumentation.FromJson(docs.ToJson());
        Assert.Equal(docs.Types.Count, back.Types.Count);
        Assert.Equal(docs.ToJson(), back.ToJson());
        Assert.True(back.TryFind("T:Lunet.Game", out var game, out _));
        Assert.Equal("Game", game.Name);
    }

    [Fact]
    public void XmlReader_ConvertsInlineTagsToPlainMarkdown()
    {
        var xml = """
            <doc><members><member name="M:A.B">
              <summary>Usa <see cref="T:Lunet.Game"/> com <paramref name="x"/> e <c>null</c>; veja <see langword="true"/>.</summary>
              <param name="x">O valor.</param>
              <returns>Nada.</returns>
              <remarks><para>Primeiro.</para><para>Segundo.</para></remarks>
              <example>Exemplo:<code>
                  var a = 1;
                  a++;
              </code></example>
              <seealso cref="M:Lunet.Game.Update(Lunet.GameTime)"/>
              <since>0.2.0</since>
            </member></members></doc>
            """;
        var reader = new XmlDocReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)));
        var entry = reader.Get("M:A.B")!;
        Assert.Equal("Usa `Game` com `x` e `null`; veja `true`.", entry.Summary);
        Assert.Equal("O valor.", entry.Parameters["x"]);
        Assert.Equal("Primeiro.\n\nSegundo.", entry.Remarks);
        Assert.Equal("Exemplo:\n\n```\nvar a = 1;\na++;\n```", entry.Examples[0]);
        Assert.Contains("M:Lunet.Game.Update(Lunet.GameTime)", entry.Related);
        Assert.Contains("T:Lunet.Game", entry.Related);
        Assert.Equal("0.2.0", entry.Since);
        Assert.Null(reader.Get("M:Nao.Existe"));
    }

    [Fact]
    public void Formatter_HandlesGenericsArraysNullableAndDefaults()
    {
        Assert.Equal("Dictionary<string, List<int>>", SignatureFormatter.TypeName(typeof(Dictionary<string, List<int>>)));
        Assert.Equal("int[]", SignatureFormatter.TypeName(typeof(int[])));
        Assert.Equal("float?", SignatureFormatter.TypeName(typeof(float?)));
        Assert.Equal("int[,]", SignatureFormatter.TypeName(typeof(int[,])));
        var method = typeof(string).GetMethod("Contains", [typeof(string), typeof(StringComparison)])!;
        Assert.Equal("public bool Contains(string value, StringComparison comparisonType)", SignatureFormatter.Method(method));
        Assert.Equal("M:System.String.Contains(System.String,System.StringComparison)", DocumentationId.For(method));
        Assert.Equal("M:System.Linq.Enumerable.Select``2(System.Collections.Generic.IEnumerable{``0},System.Func{``0,``1})",
            DocumentationId.For(typeof(Enumerable).GetMethods().First(m => m.Name == "Select" && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)));
    }
}

public class DocumentationBrowserTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "docs", "guides"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static Dictionary<string, string> Guides() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "docs", "guides"), "*.md")
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f), File.ReadAllText);

    [Fact]
    public void CommittedApiJson_MatchesTheFramework_SetLUNET_UPDATE_DOCS_ToRegenerate()
    {
        var path = Path.Combine(RepoRoot(), "docs", "api", "lunet-framework.json");
        var json = DocsTests.Generate().ToJson();
        if (Environment.GetEnvironmentVariable("LUNET_UPDATE_DOCS") == "1") File.WriteAllText(path, json);
        Assert.True(File.Exists(path), "Rode com LUNET_UPDATE_DOCS=1 para gerar docs/api/lunet-framework.json");
        Assert.Equal(json.ReplaceLineEndings(), File.ReadAllText(path).ReplaceLineEndings());
    }

    [Fact]
    public void MarkdownLite_ParsesBlocksAndInlineStyles()
    {
        var blocks = MarkdownLite.Parse("# Título\n\nUm **forte** e `code`.\n\n- item\n\n```\nx = 1;\n```");
        Assert.Equal([MarkdownBlockKind.Heading, MarkdownBlockKind.Paragraph, MarkdownBlockKind.Bullet, MarkdownBlockKind.Code], blocks.Select(b => b.Kind));
        Assert.Equal("x = 1;", blocks[3].Text);
        var runs = MarkdownLite.ParseInline("a **b** *c* `d`");
        Assert.Equal([InlineStyle.Normal, InlineStyle.Bold, InlineStyle.Normal, InlineStyle.Italic, InlineStyle.Normal, InlineStyle.Code], runs.Select(r => r.Style));
    }

    [Fact]
    public void Browser_HomeListsGuidesAndNamespaces_AndNavigatesWithHistory()
    {
        var browser = new DocumentationBrowser(DocsTests.Generate(), Guides());
        var home = browser.Navigate("home");
        Assert.Contains(home.Blocks, b => b.Kind == DocBlockKind.Link && b.Target == "guide:primeiros-passos");
        Assert.Contains(home.Blocks, b => b.Target == "ns:Lunet.Graphics");

        var type = browser.Navigate(browser.TargetForSymbol("T:Lunet.Graphics.SpriteBatch", "SpriteBatch"));
        Assert.Equal("SpriteBatch", type.Title);
        Assert.Contains(type.Blocks, b => b.Kind == DocBlockKind.Link && b.Text.Contains("Begin"));

        Assert.True(browser.CanGoBack);
        Assert.Equal("home", browser.Back()!.Target);
    }

    [Fact]
    public void Browser_UnknownSymbolFallsBackToSearch_AndEveryGuideRenders()
    {
        var browser = new DocumentationBrowser(DocsTests.Generate(), Guides());
        Assert.StartsWith("search:", browser.TargetForSymbol("T:Nope", "SpriteBatch"));
        Assert.Contains(browser.Navigate("search:SpriteBatch").Blocks, b => b.Target?.StartsWith("id:") == true);
        foreach (var name in browser.GuideNames)
            Assert.Contains(browser.Resolve("guide:" + name).Blocks, b => b.Kind == DocBlockKind.Title);
    }

    [Fact]
    public void Browser_EveryTypeAndMemberPageResolves()
    {
        var api = DocsTests.Generate();
        var browser = new DocumentationBrowser(api, Guides());
        foreach (var type in api.Types)
        {
            Assert.NotEqual("Não encontrado", browser.Resolve("id:" + type.Id).Title);
            foreach (var member in type.Members) Assert.NotEqual("Não encontrado", browser.Resolve("id:" + member.Id).Title);
        }
    }
}
