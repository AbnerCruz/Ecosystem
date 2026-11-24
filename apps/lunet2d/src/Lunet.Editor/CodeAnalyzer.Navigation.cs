using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Editor;

public sealed record CallParameter(string Name, string Display);
public sealed record CallSignature(string Label, IReadOnlyList<CallParameter> Parameters, int? ActiveParameter);
public sealed record CallSignatureHelp(int CallStart, IReadOnlyList<CallSignature> Signatures, int Selected);
public sealed record ProjectSymbol(string Name, string Display, DefinitionLocation Location);

public sealed partial class CodeAnalyzer
{
    public CallSignatureHelp? GetCallSignatures(string path, int position)
    {
        if (!_trees.TryGetValue(path, out var tree) || position <= 0 || position > tree.Length) return null;
        var root = tree.GetRoot();
        var args = root.FindToken(position - 1, findInsideTrivia: true).Parent?.AncestorsAndSelf()
            .OfType<ArgumentListSyntax>().FirstOrDefault(a => a.OpenParenToken.Span.End <= position &&
                (a.CloseParenToken.IsMissing ? position <= a.FullSpan.End : position <= a.CloseParenToken.SpanStart));
        if (args?.Parent is not ExpressionSyntax call) return null;

        var model = _compilation.GetSemanticModel(tree);
        var info = model.GetSymbolInfo(call);
        var chosen = info.Symbol as IMethodSymbol;
        IEnumerable<IMethodSymbol> candidates = call switch
        {
            InvocationExpressionSyntax invocation => model.GetMemberGroup(invocation.Expression).OfType<IMethodSymbol>(),
            BaseObjectCreationExpressionSyntax creation when model.GetTypeInfo(creation).Type is INamedTypeSymbol type =>
                type.InstanceConstructors.Where(c => model.IsAccessible(position, c)),
            _ => [],
        };
        if (call is InvocationExpressionSyntax invocationCall &&
            model.GetTypeInfo(invocationCall.Expression).Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke })
            candidates = candidates.Append(invoke);
        if (call is InvocationExpressionSyntax { Expression: GenericNameSyntax generic })
            candidates = BindExplicitTypes(candidates, generic, model);
        if (call is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax member } })
            candidates = BindExplicitTypes(candidates, member, model);

        var methods = candidates.Concat(info.CandidateSymbols.OfType<IMethodSymbol>())
            .GroupBy(m => m.OriginalDefinition, SymbolEqualityComparer.Default).Select(g => g.First()).ToList();
        if (chosen is not null)
        {
            methods.RemoveAll(m => SymbolEqualityComparer.Default.Equals(m.OriginalDefinition, chosen.OriginalDefinition));
            methods.Insert(0, chosen);
        }
        if (methods.Count == 0) return null;

        int slot = args.Arguments.GetSeparators().Count(separator => separator.Span.End <= position);
        string? name = slot < args.Arguments.Count ? args.Arguments[slot].NameColon?.Name.Identifier.ValueText : null;
        var signatures = methods.Select(m =>
        {
            var parameters = m.Parameters.Select(p => new CallParameter(p.Name, p.ToMinimalDisplayString(model, position))).ToArray();
            int index = name is null
                ? slot < parameters.Length ? slot : m.Parameters.LastOrDefault()?.IsParams == true ? parameters.Length - 1 : -1
                : Array.FindIndex(parameters, p => p.Name == name);
            var label = m.MethodKind == MethodKind.Constructor ? m.ContainingType.Name : m.Name;
            if (m.IsGenericMethod)
                label += "<" + string.Join(", ", m.TypeArguments.Select(t => t.ToMinimalDisplayString(model, position))) + ">";
            return new CallSignature(label, parameters, index >= 0 ? index : null);
        }).ToArray();
        var selected = chosen is not null ? 0 : Math.Max(0, Array.FindIndex(signatures, s => s.ActiveParameter.HasValue));
        return new CallSignatureHelp(args.SpanStart, signatures, selected);
    }

    private static IEnumerable<IMethodSymbol> BindExplicitTypes(IEnumerable<IMethodSymbol> candidates, GenericNameSyntax name, SemanticModel model)
    {
        var types = name.TypeArgumentList.Arguments.Select(arg => model.GetTypeInfo(arg).Type).ToArray();
        foreach (var method in candidates.Where(m => m.Arity == types.Length))
            yield return types.All(t => t is not null && t.TypeKind != TypeKind.Error)
                ? method.ConstructedFrom.Construct(types.Select(t => t!).ToArray()) : method;
    }

    public IReadOnlyList<ProjectSymbol> FindProjectSymbols(string query)
    {
        query = query.Trim();
        if (query.Length == 0) return [];
        return _compilation.GetSymbolsWithName(name => name.Contains(query, StringComparison.OrdinalIgnoreCase), SymbolFilter.TypeAndMember)
            .Where(symbol => !symbol.IsImplicitlyDeclared)
            .SelectMany(symbol => symbol.Locations.Where(l => l.IsInSource).Select(location =>
            {
                var span = location.GetLineSpan();
                return new ProjectSymbol(symbol.Name, symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    new(span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1));
            }))
            .OrderByDescending(s => s.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Location.FilePath, StringComparer.Ordinal)
            .ThenBy(s => s.Location.Line).ThenBy(s => s.Location.Column).ToArray();
    }
}
