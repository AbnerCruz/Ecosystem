using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Editor;

public enum TokenKind { Keyword, ControlKeyword, Type, String, Number, Comment, Preprocessor, Method, Punctuation }

public readonly record struct TokenSpan(int Start, int Length, TokenKind Kind);

/// <summary>Realce de sintaxe C# sem análise semântica (rápido o bastante para rodar a cada pausa na digitação).</summary>
public static class SyntaxHighlighter
{
    public static IReadOnlyList<TokenSpan> Classify(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var spans = new List<TokenSpan>();
        foreach (var token in tree.GetRoot().DescendantTokens(descendIntoTrivia: true))
        {
            AddTrivia(token.LeadingTrivia, spans);
            var kind = KindOf(token);
            if (kind is { } k && token.Span.Length > 0) spans.Add(new TokenSpan(token.SpanStart, token.Span.Length, k));
            AddTrivia(token.TrailingTrivia, spans);
        }
        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    private static void AddTrivia(SyntaxTriviaList trivia, List<TokenSpan> spans)
    {
        foreach (var t in trivia)
        {
            switch (t.Kind())
            {
                case SyntaxKind.SingleLineCommentTrivia:
                case SyntaxKind.MultiLineCommentTrivia:
                case SyntaxKind.SingleLineDocumentationCommentTrivia:
                case SyntaxKind.MultiLineDocumentationCommentTrivia:
                    spans.Add(new TokenSpan(t.SpanStart, t.Span.Length, TokenKind.Comment));
                    break;
                case SyntaxKind.DisabledTextTrivia:
                    spans.Add(new TokenSpan(t.SpanStart, t.Span.Length, TokenKind.Comment));
                    break;
                default:
                    if (t.IsDirective) spans.Add(new TokenSpan(t.SpanStart, t.Span.Length, TokenKind.Preprocessor));
                    break;
            }
        }
    }

    private static TokenKind? KindOf(SyntaxToken token)
    {
        var kind = token.Kind();
        if (SyntaxFacts.IsKeywordKind(kind) || SyntaxFacts.IsContextualKeyword(kind) && token.Parent is not IdentifierNameSyntax)
        {
            return kind switch
            {
                SyntaxKind.IfKeyword or SyntaxKind.ElseKeyword or SyntaxKind.ForKeyword or SyntaxKind.ForEachKeyword
                    or SyntaxKind.WhileKeyword or SyntaxKind.DoKeyword or SyntaxKind.SwitchKeyword or SyntaxKind.CaseKeyword
                    or SyntaxKind.DefaultKeyword or SyntaxKind.BreakKeyword or SyntaxKind.ContinueKeyword
                    or SyntaxKind.ReturnKeyword or SyntaxKind.ThrowKeyword or SyntaxKind.TryKeyword or SyntaxKind.CatchKeyword
                    or SyntaxKind.FinallyKeyword or SyntaxKind.GotoKeyword or SyntaxKind.YieldKeyword => TokenKind.ControlKeyword,
                _ => TokenKind.Keyword,
            };
        }
        switch (kind)
        {
            case SyntaxKind.StringLiteralToken:
            case SyntaxKind.CharacterLiteralToken:
            case SyntaxKind.InterpolatedStringStartToken:
            case SyntaxKind.InterpolatedStringEndToken:
            case SyntaxKind.InterpolatedStringTextToken:
            case SyntaxKind.InterpolatedVerbatimStringStartToken:
            case SyntaxKind.SingleLineRawStringLiteralToken:
            case SyntaxKind.MultiLineRawStringLiteralToken:
            case SyntaxKind.Utf8StringLiteralToken:
                return TokenKind.String;
            case SyntaxKind.NumericLiteralToken:
                return TokenKind.Number;
            case SyntaxKind.IdentifierToken:
                return IdentifierKind(token);
            default:
                return null;
        }
    }

    private static TokenKind? IdentifierKind(SyntaxToken token)
    {
        var parent = token.Parent;
        if (parent is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax) return TokenKind.Type;
        if (parent is MethodDeclarationSyntax or LocalFunctionStatementSyntax or ConstructorDeclarationSyntax) return TokenKind.Method;
        if (parent is GenericNameSyntax g && g.Identifier == token) return IsTypeContext(g) ? TokenKind.Type : TokenKind.Method;
        if (parent is IdentifierNameSyntax name)
        {
            if (IsTypeContext(name)) return TokenKind.Type;
            if (name.Parent is InvocationExpressionSyntax inv && inv.Expression == name) return TokenKind.Method;
            if (name.Parent is MemberAccessExpressionSyntax ma && ma.Name == name && ma.Parent is InvocationExpressionSyntax inv2 && inv2.Expression == ma) return TokenKind.Method;
        }
        return null;
    }

    private static bool IsTypeContext(SyntaxNode node)
    {
        var parent = node.Parent;
        while (parent is QualifiedNameSyntax or NullableTypeSyntax or ArrayTypeSyntax or TypeArgumentListSyntax) { node = parent; parent = parent.Parent; }
        return parent switch
        {
            VariableDeclarationSyntax v => v.Type == node,
            ParameterSyntax p => p.Type == node,
            ObjectCreationExpressionSyntax o => o.Type == node,
            BaseTypeSyntax => true,
            CastExpressionSyntax c => c.Type == node,
            TypeArgumentListSyntax => true,
            TypeOfExpressionSyntax => true,
            DefaultExpressionSyntax => true,
            MethodDeclarationSyntax m => m.ReturnType == node,
            PropertyDeclarationSyntax pr => pr.Type == node,
            DeclarationPatternSyntax => true,
            IsPatternExpressionSyntax => false,
            BinaryExpressionSyntax b => b.IsKind(SyntaxKind.IsExpression) || b.IsKind(SyntaxKind.AsExpression) ? b.Right == node : false,
            ForEachStatementSyntax f => f.Type == node,
            TypeConstraintSyntax => true,
            _ => false,
        };
    }
}
