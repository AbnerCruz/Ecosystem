using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Compiler;

/// <summary>Resultado da comparação entre duas versões do código do jogo.</summary>
public enum ChangeKind
{
    /// <summary>Nenhuma mudança de comportamento (igual, ou só comentários e formatação).</summary>
    None,
    /// <summary>Só corpos de métodos e propriedades mudaram: seria possível aplicar sem mudar a forma dos tipos.</summary>
    HotReloadPossible,
    /// <summary>Tipos, membros, assinaturas ou inicializadores mudaram: é preciso reiniciar o jogo.</summary>
    RestartRequired,
}

/// <summary>Classificação das mudanças com os motivos, em português, para mostrar ao usuário.</summary>
public sealed record ChangeReport(ChangeKind Kind, IReadOnlyList<string> Reasons);

/// <summary>
/// Classifica mudanças entre "hot reload possível" e "restart required" comparando a estrutura do código
/// (sem prometer mágica: previsibilidade vale mais que marketing).
/// </summary>
public static class ChangeClassifier
{
    private sealed record Member(string Shape, string Full);

    public static ChangeReport Classify(IReadOnlyList<SourceFile> before, IReadOnlyList<SourceFile> after)
    {
        var reasons = new List<string>();
        var oldMembers = Collect(before);
        var newMembers = Collect(after);

        var restart = false;
        foreach (var key in oldMembers.Keys.Except(newMembers.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            restart = true;
            reasons.Add($"removido: {Describe(key)}");
        }
        foreach (var key in newMembers.Keys.Except(oldMembers.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            restart = true;
            reasons.Add($"adicionado: {Describe(key)}");
        }

        var bodyChanges = new List<string>();
        foreach (var (key, member) in newMembers.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!oldMembers.TryGetValue(key, out var old)) continue;
            if (old.Shape != member.Shape)
            {
                restart = true;
                reasons.Add($"assinatura ou inicializador alterado: {Describe(key)}");
            }
            else if (old.Full != member.Full)
            {
                bodyChanges.Add($"corpo alterado: {Describe(key)}");
            }
        }

        if (restart) return new ChangeReport(ChangeKind.RestartRequired, reasons.Concat(bodyChanges).ToList());
        if (bodyChanges.Count > 0) return new ChangeReport(ChangeKind.HotReloadPossible, bodyChanges);
        return new ChangeReport(ChangeKind.None, ["só comentários ou formatação mudaram (ou nada mudou)"]);
    }

    private static string Describe(string key)
    {
        var colon = key.IndexOf(':');
        var kind = key[..colon] switch
        {
            "type" => "tipo",
            "method" => "método",
            "ctor" => "construtor",
            "field" => "campo",
            "property" => "propriedade",
            "event" => "evento",
            "enumvalue" => "valor de enum",
            "usings" => "usings",
            _ => key[..colon],
        };
        return $"{kind} {key[(colon + 1)..]}";
    }

    private static Dictionary<string, Member> Collect(IReadOnlyList<SourceFile> files)
    {
        var members = new Dictionary<string, Member>(StringComparer.Ordinal);
        foreach (var file in files.Where(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var root = CSharpSyntaxTree.ParseText(file.Text, new CSharpParseOptions(LanguageVersion.Latest)).GetRoot();
            var usings = string.Concat(root.DescendantNodes().OfType<UsingDirectiveSyntax>().Select(u => Canonical(u) + "\n"));
            members[$"usings:{file.Path}"] = new Member("", usings);
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case BaseTypeDeclarationSyntax type:
                        AddType(members, type);
                        break;
                    case DelegateDeclarationSyntax del:
                        Put(members, $"type:{QualifiedName(del)}", Canonical(del), Canonical(del));
                        break;
                    case MethodDeclarationSyntax method:
                        Put(members, $"method:{QualifiedName(method)}({ParameterTypes(method.ParameterList)})", Erased(method), Canonical(method));
                        break;
                    case ConstructorDeclarationSyntax ctor:
                        Put(members, $"ctor:{QualifiedName(ctor)}({ParameterTypes(ctor.ParameterList)})", Erased(ctor), Canonical(ctor));
                        break;
                    case FieldDeclarationSyntax field:
                        foreach (var variable in field.Declaration.Variables)
                        {
                            var text = Canonical(field.WithDeclaration(field.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable))));
                            Put(members, $"field:{QualifiedName(field)}.{variable.Identifier.Text}", text, text);
                        }
                        break;
                    case PropertyDeclarationSyntax property:
                        Put(members, $"property:{QualifiedName(property)}.{property.Identifier.Text}", Erased(property), Canonical(property));
                        break;
                    case IndexerDeclarationSyntax indexer:
                        Put(members, $"property:{QualifiedName(indexer)}.this[{ParameterTypes(indexer.ParameterList)}]", Erased(indexer), Canonical(indexer));
                        break;
                    case EventDeclarationSyntax ev:
                        Put(members, $"event:{QualifiedName(ev)}.{ev.Identifier.Text}", Erased(ev), Canonical(ev));
                        break;
                    case EventFieldDeclarationSyntax ef:
                        foreach (var variable in ef.Declaration.Variables)
                            Put(members, $"event:{QualifiedName(ef)}.{variable.Identifier.Text}", Canonical(ef), Canonical(ef));
                        break;
                    case OperatorDeclarationSyntax op:
                        Put(members, $"method:{QualifiedName(op)}.operator {op.OperatorToken.Text}({ParameterTypes(op.ParameterList)})", Erased(op), Canonical(op));
                        break;
                    case EnumMemberDeclarationSyntax value:
                        Put(members, $"enumvalue:{QualifiedName(value)}.{value.Identifier.Text}", Canonical(value), Canonical(value));
                        break;
                }
            }
        }
        return members;
    }

    private static void Put(Dictionary<string, Member> members, string key, string shape, string full) =>
        members[key] = members.TryGetValue(key, out var existing) ? new Member(existing.Shape + "\n" + shape, existing.Full + "\n" + full) : new Member(shape, full);

    private static void AddType(Dictionary<string, Member> members, BaseTypeDeclarationSyntax type)
    {
        var header = type.AttributeLists.ToString() + " " + type.Modifiers + " " + type.Kind() + " " + type.Identifier.Text;
        if (type is TypeDeclarationSyntax generic) header += generic.TypeParameterList?.ToString() + generic.ConstraintClauses.ToString();
        header += type.BaseList?.ToString();
        var text = Normalize(header);
        Put(members, $"type:{QualifiedName(type)}", text, text);
    }

    /// <summary>Nome qualificado: namespaces e tipos que contêm o nó, mais o nome do próprio tipo quando for um.</summary>
    private static string QualifiedName(SyntaxNode node)
    {
        var parts = new List<string>();
        for (var current = node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax ? node : node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case BaseNamespaceDeclarationSyntax ns: parts.Add(ns.Name.ToString()); break;
                case BaseTypeDeclarationSyntax type: parts.Add(type.Identifier.Text); break;
                case DelegateDeclarationSyntax del when ReferenceEquals(current, node): parts.Add(del.Identifier.Text); break;
            }
        }
        parts.Reverse();
        var qualified = string.Join('.', parts);
        return node switch
        {
            MethodDeclarationSyntax m => qualified + "." + m.Identifier.Text,
            ConstructorDeclarationSyntax => qualified + ".ctor",
            _ => qualified,
        };
    }

    private static string ParameterTypes(BaseParameterListSyntax list) =>
        string.Join(',', list.Parameters.Select(p => Normalize((p.Type?.ToString() ?? "?") + (p.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword) || m.IsKind(SyntaxKind.OutKeyword) || m.IsKind(SyntaxKind.InKeyword)) ? "&" : ""))));

    private static string Canonical(SyntaxNode node)
    {
        var withoutComments = node.ReplaceTrivia(
            node.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                                              t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)),
            (_, _) => default);
        return Normalize(withoutComments.WithoutTrivia().NormalizeWhitespace().ToString());
    }

    /// <summary>O mesmo nó com os corpos (chaves ou "=>") trocados por nada: sobra só a "forma" do membro.</summary>
    private static string Erased(SyntaxNode node) => Canonical(new BodyEraser().Visit(node)!);

    private static string Normalize(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed class BodyEraser : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitBlock(BlockSyntax node) => SyntaxFactory.Block();

        public override SyntaxNode? VisitArrowExpressionClause(ArrowExpressionClauseSyntax node) =>
            SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression));

        public override SyntaxNode? VisitEqualsValueClause(EqualsValueClauseSyntax node) => node; // inicializadores fazem parte da forma
    }
}
