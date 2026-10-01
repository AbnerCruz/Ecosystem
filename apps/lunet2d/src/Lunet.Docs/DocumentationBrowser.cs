namespace Lunet.Docs;

public enum DocBlockKind { Title, Heading, Paragraph, Code, Bullet, Link, Signature, Note }

/// <summary>Bloco de uma página de documentação. Blocos <see cref="DocBlockKind.Link"/> levam a <see cref="Target"/>.</summary>
public sealed record DocBlock(DocBlockKind Kind, string Text, string? Target = null, string? Detail = null);

public sealed record DocPage(string Target, string Title, IReadOnlyList<DocBlock> Blocks);

/// <summary>
/// Monta as páginas do painel Documentation e guarda o histórico de navegação. Alvos (targets):
/// <c>home</c>, <c>guide:nome</c>, <c>id:T:...</c> (tipo ou membro), <c>ns:Lunet.Graphics</c>, <c>search:texto</c>.
/// </summary>
public sealed class DocumentationBrowser
{
    private readonly Stack<string> _history = new();
    private readonly IReadOnlyDictionary<string, string> _guides;

    public DocumentationBrowser(ApiDocumentation api, IReadOnlyDictionary<string, string> guides)
    {
        Api = api;
        _guides = guides;
    }

    public ApiDocumentation Api { get; }

    public IEnumerable<string> GuideNames => _guides.Keys.OrderBy(k => k, StringComparer.Ordinal);

    public string? Current { get; private set; }

    public bool CanGoBack => _history.Count > 0;

    public DocPage Navigate(string target)
    {
        var page = Resolve(target);
        if (Current is not null && Current != page.Target) _history.Push(Current);
        Current = page.Target;
        return page;
    }

    public DocPage? Back()
    {
        if (_history.Count == 0) return null;
        var previous = _history.Pop();
        Current = previous;
        return Resolve(previous);
    }

    /// <summary>Alvo para abrir a documentação do símbolo do Roslyn (mesmo identificador), ou uma busca pelo nome se não existir.</summary>
    public string TargetForSymbol(string? documentationId, string fallbackName) =>
        documentationId is not null && Api.TryFind(documentationId, out _, out _) ? "id:" + documentationId : "search:" + fallbackName;

    public DocPage Resolve(string target)
    {
        if (target == "home") return Home();
        var colon = target.IndexOf(':');
        var kind = colon < 0 ? target : target[..colon];
        var rest = colon < 0 ? "" : target[(colon + 1)..];
        return kind switch
        {
            "guide" => Guide(rest),
            "id" => ById(rest),
            "ns" => Namespace(rest),
            "search" => Search(rest),
            _ => NotFound(target),
        };
    }

    private DocPage Home()
    {
        var blocks = new List<DocBlock>
        {
            new(DocBlockKind.Paragraph, "Documentação do Lunet, funciona sem internet. Comece pelos guias; a referência da API traz cada tipo e membro com exemplos."),
            new(DocBlockKind.Heading, "Guias"),
        };
        foreach (var name in GuideNames)
            blocks.Add(new DocBlock(DocBlockKind.Link, GuideTitle(name), "guide:" + name));
        blocks.Add(new DocBlock(DocBlockKind.Heading, "Referência da API"));
        foreach (var group in Api.Types.GroupBy(t => t.Namespace).OrderBy(g => g.Key, StringComparer.Ordinal))
            blocks.Add(new DocBlock(DocBlockKind.Link, group.Key, "ns:" + group.Key, $"{group.Count()} tipos"));
        return new DocPage("home", "Documentação", blocks);
    }

    private DocPage Namespace(string ns)
    {
        var blocks = new List<DocBlock>();
        foreach (var type in Api.Types.Where(t => t.Namespace == ns).OrderBy(t => t.Name, StringComparer.Ordinal))
            blocks.Add(new DocBlock(DocBlockKind.Link, $"{type.Name} ({type.Kind})", "id:" + type.Id, FirstSentence(type.Summary)));
        return new DocPage("ns:" + ns, ns, blocks);
    }

    private DocPage Guide(string name)
    {
        if (!_guides.TryGetValue(name, out var markdown)) return NotFound("guide:" + name);
        var blocks = new List<DocBlock>();
        foreach (var block in MarkdownLite.Parse(markdown))
        {
            blocks.Add(block.Kind switch
            {
                MarkdownBlockKind.Heading => new DocBlock(block.Level == 1 ? DocBlockKind.Title : DocBlockKind.Heading, block.Text),
                MarkdownBlockKind.Code => new DocBlock(DocBlockKind.Code, block.Text),
                MarkdownBlockKind.Bullet => new DocBlock(DocBlockKind.Bullet, block.Text),
                _ => new DocBlock(DocBlockKind.Paragraph, block.Text),
            });
        }
        return new DocPage("guide:" + name, GuideTitle(name), blocks);
    }

    private DocPage ById(string id)
    {
        if (!Api.TryFind(id, out var type, out var member)) return NotFound("id:" + id);
        return member is null ? TypePage(type) : MemberPage(type, member);
    }

    private DocPage TypePage(TypeDoc type)
    {
        var blocks = new List<DocBlock>
        {
            new(DocBlockKind.Signature, type.Signature),
            new(DocBlockKind.Note, $"{type.Namespace} · desde {type.Since}"),
        };
        AddText(blocks, type.Summary);
        AddText(blocks, type.Remarks);
        AddExamples(blocks, type.Examples);

        foreach (var group in type.Members.GroupBy(m => m.Kind).OrderBy(g => KindOrder(g.Key)))
        {
            blocks.Add(new DocBlock(DocBlockKind.Heading, KindTitle(group.Key)));
            foreach (var member in group)
                blocks.Add(new DocBlock(DocBlockKind.Link, member.Signature, "id:" + member.Id, FirstSentence(member.Summary)));
        }
        AddRelated(blocks, type.Related);
        return new DocPage("id:" + type.Id, type.Name, blocks);
    }

    private DocPage MemberPage(TypeDoc type, MemberDoc member)
    {
        var blocks = new List<DocBlock>
        {
            new(DocBlockKind.Signature, member.Signature),
            new(DocBlockKind.Link, $"{type.Name} ({type.Namespace})", "id:" + type.Id),
        };
        AddText(blocks, member.Summary);
        if (member.Parameters.Count > 0)
        {
            blocks.Add(new DocBlock(DocBlockKind.Heading, "Parâmetros"));
            foreach (var p in member.Parameters)
                blocks.Add(new DocBlock(DocBlockKind.Bullet, $"`{p.Name}` ({p.Type}): {(p.Description.Length > 0 ? p.Description : "—")}"));
        }
        if (member.Returns.Length > 0)
        {
            blocks.Add(new DocBlock(DocBlockKind.Heading, "Retorno"));
            AddText(blocks, member.Returns);
        }
        if (member.Remarks.Length > 0)
        {
            blocks.Add(new DocBlock(DocBlockKind.Heading, "Observações"));
            AddText(blocks, member.Remarks);
        }
        AddExamples(blocks, member.Examples);
        AddRelated(blocks, member.Related);
        blocks.Add(new DocBlock(DocBlockKind.Note, $"Desde {member.Since}"));
        return new DocPage("id:" + member.Id, $"{type.Name}.{member.Name}", blocks);
    }

    private DocPage Search(string query)
    {
        var blocks = new List<DocBlock>();
        var results = Api.Search(query);
        foreach (var result in results)
            blocks.Add(new DocBlock(DocBlockKind.Link, result.Title, "id:" + result.Id, $"{result.Kind} · {FirstSentence(result.Summary)}"));
        foreach (var guide in _guides.Where(g => g.Value.Contains(query, StringComparison.OrdinalIgnoreCase) || GuideTitle(g.Key).Contains(query, StringComparison.OrdinalIgnoreCase)))
            blocks.Add(new DocBlock(DocBlockKind.Link, "Guia: " + GuideTitle(guide.Key), "guide:" + guide.Key));
        if (blocks.Count == 0) blocks.Add(new DocBlock(DocBlockKind.Paragraph, $"Nada encontrado para \"{query}\"."));
        return new DocPage("search:" + query, $"Busca: {query}", blocks);
    }

    private DocPage NotFound(string target) =>
        new(target, "Não encontrado", [new DocBlock(DocBlockKind.Paragraph, $"Não há documentação para \"{target}\".")]);

    private string GuideTitle(string name)
    {
        if (!_guides.TryGetValue(name, out var markdown)) return name;
        var heading = MarkdownLite.Parse(markdown).FirstOrDefault(b => b.Kind == MarkdownBlockKind.Heading);
        return heading?.Text ?? name;
    }

    private static void AddText(List<DocBlock> blocks, string text)
    {
        if (text.Length == 0) return;
        foreach (var block in MarkdownLite.Parse(text))
            blocks.Add(block.Kind == MarkdownBlockKind.Code
                ? new DocBlock(DocBlockKind.Code, block.Text)
                : block.Kind == MarkdownBlockKind.Bullet ? new DocBlock(DocBlockKind.Bullet, block.Text) : new DocBlock(DocBlockKind.Paragraph, block.Text));
    }

    private static void AddExamples(List<DocBlock> blocks, IReadOnlyList<string> examples)
    {
        if (examples.Count == 0) return;
        blocks.Add(new DocBlock(DocBlockKind.Heading, examples.Count == 1 ? "Exemplo" : "Exemplos"));
        foreach (var example in examples) AddText(blocks, example);
    }

    private void AddRelated(List<DocBlock> blocks, IReadOnlyList<string> related)
    {
        var links = related.Where(r => Api.TryFind(r, out _, out _)).ToList();
        if (links.Count == 0) return;
        blocks.Add(new DocBlock(DocBlockKind.Heading, "Veja também"));
        foreach (var id in links)
        {
            Api.TryFind(id, out var type, out var member);
            blocks.Add(new DocBlock(DocBlockKind.Link, member is null ? type.Name : $"{type.Name}.{member.Name}", "id:" + id));
        }
    }

    private static string FirstSentence(string text)
    {
        if (text.Length == 0) return "";
        var end = text.IndexOf(". ", StringComparison.Ordinal);
        var sentence = end > 0 ? text[..(end + 1)] : text;
        return sentence.Split('\n')[0];
    }

    private static int KindOrder(string kind) => kind switch
    {
        "constructor" => 0, "field" => 1, "enum value" => 1, "property" => 2, "event" => 3, "method" => 4, _ => 5,
    };

    private static string KindTitle(string kind) => kind switch
    {
        "constructor" => "Construtores", "field" => "Campos", "enum value" => "Valores", "property" => "Propriedades", "event" => "Eventos", "method" => "Métodos", _ => kind,
    };
}
