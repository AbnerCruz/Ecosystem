using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Editor;

/// <summary>
/// Formata um documento C#: reindenta (4 espaços) pela estrutura da sintaxe, apara espaços no fim das linhas e garante
/// uma quebra de linha final. Não move tokens nem mexe em literais de várias linhas (raw strings, verbatim, interpoladas).
/// Se as chaves estão desbalanceadas devolve o texto original, para nunca estragar código em edição.
/// </summary>
public static class CodeFormatter
{
    public static string Format(string text)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest));
        var root = tree.GetRoot();
        var source = tree.GetText();

        var tokens = root.DescendantTokens().Where(t => !t.IsMissing).ToList();
        var depthBefore = new int[tokens.Count];
        var depth = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            depthBefore[i] = depth;
            if (tokens[i].IsKind(SyntaxKind.OpenBraceToken)) depth++;
            else if (tokens[i].IsKind(SyntaxKind.CloseBraceToken)) depth--;
            if (depth < 0) return text;
        }
        if (depth != 0) return text;

        var lineCount = source.Lines.Count;
        var firstTokenOfLine = new int[lineCount];
        Array.Fill(firstTokenOfLine, -1);
        var protectedLine = new bool[lineCount];
        for (var i = 0; i < tokens.Count; i++)
        {
            var startLine = source.Lines.GetLineFromPosition(tokens[i].SpanStart).LineNumber;
            var endLine = source.Lines.GetLineFromPosition(Math.Max(tokens[i].SpanStart, tokens[i].Span.End - 1)).LineNumber;
            if (firstTokenOfLine[startLine] < 0) firstTokenOfLine[startLine] = i;
            if (endLine > startLine && IsTextToken(tokens[i]))
                for (var l = startLine + 1; l <= endLine; l++) protectedLine[l] = true;
        }
        // Strings interpoladas de várias linhas: inclusive o código dentro das chaves fica intacto.
        foreach (var node in root.DescendantNodes().Where(n => n is InterpolatedStringExpressionSyntax))
        {
            var startLine = source.Lines.GetLineFromPosition(node.SpanStart).LineNumber;
            var endLine = source.Lines.GetLineFromPosition(Math.Max(node.SpanStart, node.Span.End - 1)).LineNumber;
            for (var l = startLine + 1; l <= endLine; l++) protectedLine[l] = true;
        }

        // Comentários /* */ de várias linhas: as linhas seguintes andam junto com a primeira.
        var blockFirstLine = new Dictionary<int, int>();
        foreach (var trivia in root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.MultiLineCommentTrivia)))
        {
            var start = source.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
            var end = source.Lines.GetLineFromPosition(Math.Max(trivia.SpanStart, trivia.Span.End - 1)).LineNumber;
            for (var l = start + 1; l <= end; l++) blockFirstLine[l] = start;
        }

        var levelOfLine = new int[lineCount];
        Array.Fill(levelOfLine, -1);
        for (var line = 0; line < lineCount; line++)
        {
            if (firstTokenOfLine[line] >= 0) levelOfLine[line] = LevelOf(tokens, depthBefore, firstTokenOfLine[line]);
        }

        var result = new System.Text.StringBuilder(text.Length + 64);
        var newIndent = new int[lineCount];
        for (var line = 0; line < lineCount; line++)
        {
            var raw = source.Lines[line].ToString();
            if (protectedLine[line]) { newIndent[line] = -1; continue; }
            var trimmed = raw.Trim();
            if (trimmed.Length == 0) { newIndent[line] = 0; continue; }
            if (trimmed.StartsWith('#')) { newIndent[line] = 0; continue; }
            if (blockFirstLine.ContainsKey(line)) { newIndent[line] = -2; continue; }

            var level = levelOfLine[line];
            if (level < 0)
            {
                // Linha só com comentário: acompanha o próximo token.
                var next = NextTokenIndex(tokens, source.Lines[line].End);
                level = next < 0 ? 0 : tokens[next].IsKind(SyntaxKind.CloseBraceToken) ? depthBefore[next] : LevelOf(tokens, depthBefore, next);
            }
            newIndent[line] = level * IndentationService.Indent.Length;
        }

        for (var line = 0; line < lineCount; line++)
        {
            var raw = source.Lines[line].ToString();
            string output;
            if (newIndent[line] == -1) output = raw;
            else if (newIndent[line] == -2)
            {
                var first = blockFirstLine[line];
                var delta = newIndent[first] - LeadingSpaces(source.Lines[first].ToString());
                var current = LeadingSpaces(raw);
                output = new string(' ', Math.Max(0, current + delta)) + raw.TrimStart().TrimEnd();
            }
            else if (raw.Trim().Length == 0) output = "";
            else output = new string(' ', newIndent[line]) + raw.Trim();
            result.Append(output);
            if (line < lineCount - 1) result.Append('\n');
        }

        var formatted = result.ToString().TrimEnd('\n', ' ', '\t');
        return formatted.Length == 0 ? "" : formatted + "\n";
    }

    private static int LevelOf(List<SyntaxToken> tokens, int[] depthBefore, int index)
    {
        var token = tokens[index];
        var level = depthBefore[index];
        if (token.IsKind(SyntaxKind.CloseBraceToken)) level--;

        // Comandos dentro de "case ...:" ficam um nível abaixo dos rótulos.
        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            if (node is SwitchSectionSyntax section && section.Statements.Count > 0 && token.SpanStart >= section.Statements.Span.Start)
                level++;
        }

        if (index > 0 && IsContinuation(tokens[index - 1], token)) level++;
        return Math.Max(0, level);
    }

    private static bool IsContinuation(SyntaxToken previous, SyntaxToken token)
    {
        if (token.IsKind(SyntaxKind.OpenBraceToken) || token.IsKind(SyntaxKind.CloseBraceToken)) return false;
        switch (previous.Kind())
        {
            case SyntaxKind.SemicolonToken:
            case SyntaxKind.OpenBraceToken:
            case SyntaxKind.CloseBraceToken:
                return false;
            case SyntaxKind.ColonToken:
                return !(previous.Parent is SwitchLabelSyntax);
            case SyntaxKind.CloseBracketToken:
                return previous.Parent is not AttributeListSyntax;
            case SyntaxKind.CommaToken:
                return !(previous.Parent is InitializerExpressionSyntax or EnumDeclarationSyntax or AnonymousObjectCreationExpressionSyntax
                    or CollectionExpressionSyntax or SwitchExpressionSyntax or PropertyPatternClauseSyntax);
        }
        // Início de uma diretiva de pré-processador ou primeiro token do arquivo.
        return true;
    }

    private static int NextTokenIndex(List<SyntaxToken> tokens, int position)
    {
        var low = 0;
        var high = tokens.Count - 1;
        var found = -1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (tokens[mid].SpanStart >= position) { found = mid; high = mid - 1; }
            else low = mid + 1;
        }
        return found;
    }

    private static bool IsTextToken(SyntaxToken token) => token.Kind() is
        SyntaxKind.StringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken or SyntaxKind.InterpolatedStringTextToken
        or SyntaxKind.InterpolatedMultiLineRawStringStartToken or SyntaxKind.InterpolatedStringStartToken
        or SyntaxKind.Utf8StringLiteralToken or SyntaxKind.Utf8MultiLineRawStringLiteralToken
        or SyntaxKind.InterpolatedVerbatimStringStartToken or SyntaxKind.CharacterLiteralToken;

    private static int LeadingSpaces(string line)
    {
        var n = 0;
        foreach (var c in line)
        {
            if (c == ' ') n++;
            else if (c == '\t') n += IndentationService.Indent.Length;
            else break;
        }
        return n;
    }
}
