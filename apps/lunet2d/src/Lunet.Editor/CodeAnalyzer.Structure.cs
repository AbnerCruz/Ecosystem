using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Editor;

/// <summary>Item da estrutura (outline) do arquivo. <see cref="Line"/> começa em 1.</summary>
public sealed record OutlineItem(string Name, string Kind, string Detail, int Line, int Start, int Length, IReadOnlyList<OutlineItem> Children);

/// <summary>Região dobrável: as linhas <c>StartLine+1..EndLine</c> somem quando dobrada. Linhas começam em 1.</summary>
public sealed record FoldRegion(int StartLine, int EndLine, string Kind, string Placeholder);

/// <summary>Detalhes de um símbolo (inspeção de símbolos).</summary>
public sealed record SymbolDetails(
    string Name, string Kind, string Signature, string? ContainingType, string? Namespace, string Accessibility,
    IReadOnlyList<string> Modifiers, string? Type, string? BaseType, IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> Attributes, IReadOnlyList<string> Members, bool IsFromSource, string? DocumentationId);

public sealed partial class CodeAnalyzer
{
    /// <summary>Estrutura hierárquica do arquivo: namespaces, tipos e membros. Usa só a sintaxe, então é rápida.</summary>
    public IReadOnlyList<OutlineItem> GetOutline(string path)
    {
        if (!_trees.TryGetValue(path, out var tree)) return [];
        var text = tree.GetText();
        return OutlineOf(tree.GetRoot().ChildNodes(), text);
    }

    private static List<OutlineItem> OutlineOf(IEnumerable<SyntaxNode> nodes, Microsoft.CodeAnalysis.Text.SourceText text)
    {
        var items = new List<OutlineItem>();
        foreach (var node in nodes)
        {
            switch (node)
            {
                case BaseNamespaceDeclarationSyntax ns:
                    items.Add(Item(ns.Name.ToString(), "namespace", "", ns, ns.Name.Span, text, OutlineOf(ns.Members, text)));
                    break;
                case BaseTypeDeclarationSyntax type:
                    var kind = type switch
                    {
                        ClassDeclarationSyntax => "class",
                        StructDeclarationSyntax => "struct",
                        InterfaceDeclarationSyntax => "interface",
                        EnumDeclarationSyntax => "enum",
                        RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
                        _ => "type",
                    };
                    var children = type is EnumDeclarationSyntax e
                        ? e.Members.Select(m => Item(m.Identifier.Text, "enum value", "", m, m.Identifier.Span, text, [])).ToList()
                        : OutlineOf(type.ChildNodes().Where(c => c is MemberDeclarationSyntax), text);
                    items.Add(Item(type.Identifier.Text, kind, "", type, type.Identifier.Span, text, children));
                    break;
                case MethodDeclarationSyntax m:
                    items.Add(Item(m.Identifier.Text, "method", m.ParameterList.ToString() + " : " + m.ReturnType, m, m.Identifier.Span, text, []));
                    break;
                case ConstructorDeclarationSyntax c:
                    items.Add(Item(c.Identifier.Text, "constructor", c.ParameterList.ToString(), c, c.Identifier.Span, text, []));
                    break;
                case PropertyDeclarationSyntax p:
                    items.Add(Item(p.Identifier.Text, "property", p.Type.ToString(), p, p.Identifier.Span, text, []));
                    break;
                case EventDeclarationSyntax ev:
                    items.Add(Item(ev.Identifier.Text, "event", ev.Type.ToString(), ev, ev.Identifier.Span, text, []));
                    break;
                case EventFieldDeclarationSyntax ef:
                    foreach (var v in ef.Declaration.Variables)
                        items.Add(Item(v.Identifier.Text, "event", ef.Declaration.Type.ToString(), ef, v.Identifier.Span, text, []));
                    break;
                case FieldDeclarationSyntax f:
                    foreach (var v in f.Declaration.Variables)
                        items.Add(Item(v.Identifier.Text, "field", f.Declaration.Type.ToString(), f, v.Identifier.Span, text, []));
                    break;
                case DelegateDeclarationSyntax d:
                    items.Add(Item(d.Identifier.Text, "delegate", d.ParameterList.ToString() + " : " + d.ReturnType, d, d.Identifier.Span, text, []));
                    break;
                case GlobalStatementSyntax:
                    break;
            }
        }
        return items;
    }

    private static OutlineItem Item(string name, string kind, string detail, SyntaxNode node, Microsoft.CodeAnalysis.Text.TextSpan nameSpan,
        Microsoft.CodeAnalysis.Text.SourceText text, IReadOnlyList<OutlineItem> children) =>
        new(name, kind, detail, text.Lines.GetLineFromPosition(nameSpan.Start).LineNumber + 1, nameSpan.Start, nameSpan.Length, children);

    /// <summary>Regiões que podem ser dobradas: tipos, métodos, blocos, #region, comentários e grupos de usings.</summary>
    public IReadOnlyList<FoldRegion> GetFoldRegions(string path)
    {
        if (!_trees.TryGetValue(path, out var tree)) return [];
        var root = tree.GetRoot();
        var text = tree.GetText();
        var regions = new List<FoldRegion>();

        void Add(int startPosition, int endPosition, string kind, string placeholder)
        {
            var start = text.Lines.GetLineFromPosition(startPosition).LineNumber + 1;
            var end = text.Lines.GetLineFromPosition(Math.Max(startPosition, endPosition - 1)).LineNumber + 1;
            if (end > start) regions.Add(new FoldRegion(start, end, kind, placeholder));
        }

        foreach (var node in root.DescendantNodes())
        {
            switch (node)
            {
                case NamespaceDeclarationSyntax ns:
                    Add(ns.SpanStart, ns.Span.End, "namespace", "{ … }");
                    break;
                case BaseTypeDeclarationSyntax type when type.OpenBraceToken != default:
                    Add(type.SpanStart + LeadingTriviaLength(type), type.Span.End, "type", "{ … }");
                    break;
                case BaseMethodDeclarationSyntax { Body: { } body } method:
                    Add(method.SpanStart, body.Span.End, "method", "{ … }");
                    break;
                case AccessorDeclarationSyntax { Body: { } accessorBody } accessor:
                    Add(accessor.SpanStart, accessorBody.Span.End, "accessor", "{ … }");
                    break;
                case PropertyDeclarationSyntax { AccessorList: { } accessors } property:
                    Add(property.SpanStart, accessors.Span.End, "property", "{ … }");
                    break;
                case BlockSyntax block when block.Parent is not (BaseMethodDeclarationSyntax or AccessorDeclarationSyntax):
                    Add(block.Parent!.SpanStart, block.Span.End, "block", "{ … }");
                    break;
                case SwitchStatementSyntax sw:
                    Add(sw.SpanStart, sw.Span.End, "switch", "{ … }");
                    break;
                case InitializerExpressionSyntax init:
                    Add(init.Parent!.SpanStart, init.Span.End, "initializer", "{ … }");
                    break;
            }
        }

        // Grupos de usings.
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(u => u.Parent is CompilationUnitSyntax).ToList();
        if (usings.Count > 1) Add(usings[0].SpanStart, usings[^1].Span.End, "usings", "using …");

        // Comentários de bloco, documentação e linhas seguidas de //.
        SyntaxTrivia? runStart = null;
        var runEndLine = -1;
        int? runStartPosition = null;
        void FlushRun()
        {
            if (runStartPosition is { } startPos && runStart is { } first)
            {
                var startLine = text.Lines.GetLineFromPosition(startPos).LineNumber;
                if (runEndLine > startLine) regions.Add(new FoldRegion(startLine + 1, runEndLine + 1, "comment", "// …"));
                _ = first;
            }
            runStart = null;
            runStartPosition = null;
        }
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                FlushRun();
                Add(trivia.SpanStart, trivia.Span.End, "comment", "/* … */");
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                var line = text.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
                if (runStartPosition is not null && line == runEndLine + 1) runEndLine = line;
                else
                {
                    FlushRun();
                    runStart = trivia;
                    runStartPosition = trivia.SpanStart;
                    runEndLine = line;
                }
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                FlushRun();
                Add(trivia.SpanStart, trivia.Span.End, "doc", "/// …");
            }
        }
        FlushRun();

        // #region … #endregion
        var stack = new Stack<(int Position, string Name)>();
        foreach (var directive in root.DescendantTrivia().Where(t => t.IsDirective).Select(t => t.GetStructure()))
        {
            if (directive is RegionDirectiveTriviaSyntax region)
                stack.Push((region.SpanStart, region.EndOfDirectiveToken.LeadingTrivia.ToString().Trim()));
            else if (directive is EndRegionDirectiveTriviaSyntax end && stack.Count > 0)
            {
                var (position, name) = stack.Pop();
                Add(position, end.Span.End, "region", name.Length > 0 ? name : "#region");
            }
        }

        return regions
            .DistinctBy(r => (r.StartLine, r.EndLine))
            .OrderBy(r => r.StartLine).ThenByDescending(r => r.EndLine)
            .ToList();
    }

    private static int LeadingTriviaLength(SyntaxNode node) => node.GetLeadingTrivia().Span.Length;

    /// <summary>Informações detalhadas do símbolo sob o cursor; nulo se não houver símbolo.</summary>
    public SymbolDetails? GetSymbolDetails(string path, int position)
    {
        if (!_trees.TryGetValue(path, out var tree)) return null;
        var token = tree.GetRoot().FindToken(Math.Clamp(position, 0, Math.Max(0, tree.Length - 1)));
        if (token.Parent is null || !(token.IsKind(SyntaxKind.IdentifierToken) || SyntaxFacts.IsPredefinedType(token.Kind()))) return null;
        var model = _compilation.GetSemanticModel(tree);
        var symbol = model.GetSymbolInfo(token.Parent).Symbol ?? model.GetDeclaredSymbol(token.Parent);
        if (symbol is null) return null;

        var modifiers = new List<string>();
        if (symbol.IsStatic) modifiers.Add("static");
        if (symbol.IsAbstract) modifiers.Add("abstract");
        if (symbol.IsVirtual) modifiers.Add("virtual");
        if (symbol.IsOverride) modifiers.Add("override");
        if (symbol.IsSealed) modifiers.Add("sealed");
        if (symbol is IFieldSymbol { IsReadOnly: true } or IPropertySymbol { IsReadOnly: true }) modifiers.Add("readonly");
        if (symbol is IFieldSymbol { IsConst: true }) modifiers.Add("const");

        var type = symbol switch
        {
            IFieldSymbol f => f.Type.ToDisplayString(),
            IPropertySymbol p => p.Type.ToDisplayString(),
            IMethodSymbol m => m.ReturnType.ToDisplayString(),
            ILocalSymbol l => l.Type.ToDisplayString(),
            IParameterSymbol p => p.Type.ToDisplayString(),
            IEventSymbol e => e.Type.ToDisplayString(),
            _ => null,
        };

        string? baseType = null;
        var interfaces = new List<string>();
        var members = new List<string>();
        if (symbol is INamedTypeSymbol named)
        {
            baseType = named.BaseType is { SpecialType: not SpecialType.System_Object } b ? b.ToDisplayString() : null;
            interfaces.AddRange(named.Interfaces.Select(i => i.ToDisplayString()));
            members.AddRange(named.GetMembers()
                .Where(m => m.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected && !m.IsImplicitlyDeclared && m.CanBeReferencedByName)
                .Select(m => m.ToMinimalDisplayString(model, token.SpanStart)).Take(40));
        }

        var attributes = symbol.GetAttributes().Select(a => a.AttributeClass?.Name ?? "?").ToList();
        return new SymbolDetails(
            symbol.Name, symbol.Kind.ToString(), symbol.ToMinimalDisplayString(model, token.SpanStart),
            symbol.ContainingType?.ToDisplayString(), symbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : null,
            symbol.DeclaredAccessibility.ToString().ToLowerInvariant(), modifiers, type, baseType, interfaces, attributes, members,
            symbol.Locations.Any(l => l.IsInSource), symbol.OriginalDefinition.GetDocumentationCommentId());
    }
}
