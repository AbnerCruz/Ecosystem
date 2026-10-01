using Lunet.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Editor;

public enum CompletionKind { Keyword, Namespace, Class, Struct, Interface, Enum, Method, Property, Field, Event, Local, Parameter, Other }

/// <summary>Sugestão de autocompletar: substitui <see cref="ReplaceLength"/> caracteres a partir de <see cref="ReplaceStart"/> por <see cref="InsertText"/>.</summary>
public sealed record CompletionItem(string Label, CompletionKind Kind, string Detail, string InsertText, int ReplaceStart, int ReplaceLength, int Overloads);

public sealed record HoverInfo(string Signature, string Kind, int Start, int Length, string? DocumentationId = null);

public sealed record DefinitionLocation(string FilePath, int Line, int Column);

/// <summary>
/// Análise de código do projeto para o editor (autocompletar, dicas, ir para definição, diagnósticos ao vivo).
/// Mantém uma compilação e troca só a árvore do arquivo editado. Não é thread-safe: use de uma thread por vez.
/// </summary>
public sealed partial class CodeAnalyzer
{
    private static readonly string[] Keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "class", "const", "continue", "decimal",
        "default", "delegate", "do", "double", "else", "enum", "event", "false", "finally", "float", "for", "foreach", "if",
        "in", "int", "interface", "internal", "is", "long", "namespace", "new", "null", "object", "out", "override", "private",
        "protected", "public", "readonly", "ref", "return", "sealed", "short", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "using", "var", "virtual", "void", "while",
    ];

    private readonly IReferenceProvider _references;
    private readonly Dictionary<string, SyntaxTree> _trees = new(StringComparer.Ordinal);
    private CSharpCompilation _compilation;

    public CodeAnalyzer(IReferenceProvider references)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
        _compilation = NewCompilation();
    }

    private CSharpCompilation NewCompilation() => CSharpCompilation.Create("LunetEditor", _trees.Values, _references.GetReferences(),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable));

    /// <summary>Define o conteúdo de um arquivo (novo ou alterado). Chame a cada pausa na digitação.</summary>
    public void SetFile(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest), path, System.Text.Encoding.UTF8);
        if (_trees.TryGetValue(path, out var old)) _compilation = _compilation.ReplaceSyntaxTree(old, tree);
        else _compilation = _compilation.AddSyntaxTrees(tree);
        _trees[path] = tree;
    }

    public void RemoveFile(string path)
    {
        if (!_trees.Remove(path, out var old)) return;
        _compilation = _compilation.RemoveSyntaxTrees(old);
    }

    public IReadOnlyList<LunetDiagnostic> GetDiagnostics(string path)
    {
        if (!_trees.TryGetValue(path, out var tree)) return [];
        var model = _compilation.GetSemanticModel(tree);
        return model.GetDiagnostics()
            .Where(d => d.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
            .Select(DiagnosticConverter.Convert)
            .OrderBy(d => d.Line).ThenBy(d => d.Column)
            .ToList();
    }

    public IReadOnlyList<CompletionItem> GetCompletions(string path, int position, int maxItems = 60)
    {
        if (!_trees.TryGetValue(path, out var tree)) return [];
        var text = tree.GetText();
        position = Math.Clamp(position, 0, text.Length);

        // Prefixo: identificador imediatamente antes do cursor.
        var start = position;
        while (start > 0 && IsIdentifierChar(text[start - 1])) start--;
        var prefix = text.ToString(new Microsoft.CodeAnalysis.Text.TextSpan(start, position - start));

        // Comentários e strings não sugerem nada.
        var root = tree.GetRoot();
        var trivia = root.FindTrivia(Math.Max(0, position - 1));
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && position > trivia.SpanStart && position <= trivia.Span.End) return [];
        var token = root.FindToken(Math.Max(0, position - 1));
        if (token.IsKind(SyntaxKind.StringLiteralToken) && position > token.SpanStart && position < token.Span.End) return [];

        var model = _compilation.GetSemanticModel(tree);
        IEnumerable<ISymbol> symbols;
        var isMember = false;

        var before = start > 0 ? root.FindToken(start - 1) : default;
        if (start > 0 && before.IsKind(SyntaxKind.DotToken) && before.Parent is MemberAccessExpressionSyntax access)
        {
            isMember = true;
            symbols = MemberSymbols(model, access.Expression, position);
        }
        else if (start > 0 && before.IsKind(SyntaxKind.DotToken) && before.Parent is QualifiedNameSyntax qualified)
        {
            isMember = true;
            symbols = MemberSymbols(model, qualified.Left, position);
        }
        else
        {
            symbols = model.LookupSymbols(position);
        }

        var groups = new Dictionary<(string, CompletionKind), (ISymbol Symbol, int Count)>();
        foreach (var symbol in symbols)
        {
            if (!IsUsable(symbol) || !model.IsAccessible(position, symbol)) continue;
            if (prefix.Length > 0 && !symbol.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var key = (symbol.Name, KindOf(symbol));
            groups[key] = groups.TryGetValue(key, out var g) ? (g.Symbol, g.Count + 1) : (symbol, 1);
        }

        var items = groups.Select(g => new CompletionItem(g.Key.Item1, g.Key.Item2,
                g.Value.Symbol.ToMinimalDisplayString(model, position), g.Key.Item1, start, position - start, g.Value.Count))
            .ToList();
        if (!isMember)
            items.AddRange(Keywords.Where(k => prefix.Length == 0 || k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => new CompletionItem(k, CompletionKind.Keyword, "palavra-chave", k, start, position - start, 1)));

        return items
            .OrderBy(i => i.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(i => i.Kind == CompletionKind.Keyword ? 1 : 0)
            .ThenBy(i => i.Label, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .ToList();
    }

    public HoverInfo? GetHover(string path, int position)
    {
        if (!_trees.TryGetValue(path, out var tree)) return null;
        var token = tree.GetRoot().FindToken(Math.Clamp(position, 0, Math.Max(0, tree.Length - 1)));
        if (!token.IsKind(SyntaxKind.IdentifierToken) && !SyntaxFacts.IsPredefinedType(token.Kind())) return null;
        var model = _compilation.GetSemanticModel(tree);
        var node = token.Parent;
        if (node is null) return null;
        var symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
        if (symbol is null) return null;
        var signature = symbol.ToMinimalDisplayString(model, token.SpanStart);
        return new HoverInfo(signature, symbol.Kind.ToString(), token.SpanStart, token.Span.Length, symbol.OriginalDefinition.GetDocumentationCommentId());
    }

    /// <summary>Local da definição no código do projeto; nulo para símbolos do framework/BCL.</summary>
    public DefinitionLocation? GetDefinition(string path, int position)
    {
        if (!_trees.TryGetValue(path, out var tree)) return null;
        var token = tree.GetRoot().FindToken(Math.Clamp(position, 0, Math.Max(0, tree.Length - 1)));
        if (token.Parent is null) return null;
        var model = _compilation.GetSemanticModel(tree);
        var symbol = model.GetSymbolInfo(token.Parent).Symbol ?? model.GetDeclaredSymbol(token.Parent);
        var location = symbol?.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null) return null;
        var span = location.GetLineSpan();
        return new DefinitionLocation(span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
    }

    /// <summary>Ocorrências do símbolo sob o cursor em todos os arquivos (busca de referências).</summary>
    public IReadOnlyList<DefinitionLocation> FindReferences(string path, int position)
    {
        if (!_trees.TryGetValue(path, out var tree)) return [];
        var token = tree.GetRoot().FindToken(Math.Clamp(position, 0, Math.Max(0, tree.Length - 1)));
        if (token.Parent is null || !token.IsKind(SyntaxKind.IdentifierToken)) return [];
        var model = _compilation.GetSemanticModel(tree);
        var target = (model.GetSymbolInfo(token.Parent).Symbol ?? model.GetDeclaredSymbol(token.Parent))?.OriginalDefinition;
        if (target is null) return [];

        var result = new List<DefinitionLocation>();
        foreach (var other in _trees.Values)
        {
            var otherModel = _compilation.GetSemanticModel(other);
            foreach (var id in other.GetRoot().DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken) && t.ValueText == target.Name))
            {
                if (id.Parent is null) continue;
                var s = (otherModel.GetSymbolInfo(id.Parent).Symbol ?? otherModel.GetDeclaredSymbol(id.Parent))?.OriginalDefinition;
                if (!SymbolEqualityComparer.Default.Equals(s, target)) continue;
                var span = id.GetLocation().GetLineSpan();
                result.Add(new DefinitionLocation(span.Path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1));
            }
        }
        return result;
    }

    private static IEnumerable<ISymbol> MemberSymbols(SemanticModel model, ExpressionSyntax expression, int position)
    {
        var info = model.GetSymbolInfo(expression);
        var symbol = info.Symbol;
        if (symbol is INamespaceOrTypeSymbol container && symbol is not IParameterSymbol)
        {
            // Acesso estático (Tipo.) ou a namespace (System.).
            return model.LookupSymbols(position, container).Where(s => s.IsStatic || s is INamespaceOrTypeSymbol);
        }
        var type = model.GetTypeInfo(expression).Type;
        return type is null ? [] : model.LookupSymbols(position, type, includeReducedExtensionMethods: true).Where(s => !s.IsStatic && s is not ITypeSymbol);
    }

    private static bool IsUsable(ISymbol s) =>
        s.CanBeReferencedByName && !s.IsImplicitlyDeclared && s is not IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.UserDefinedOperator or MethodKind.Conversion or MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove };

    private static CompletionKind KindOf(ISymbol s) => s switch
    {
        INamespaceSymbol => CompletionKind.Namespace,
        ITypeSymbol { TypeKind: TypeKind.Interface } => CompletionKind.Interface,
        ITypeSymbol { TypeKind: TypeKind.Enum } => CompletionKind.Enum,
        ITypeSymbol { TypeKind: TypeKind.Struct } => CompletionKind.Struct,
        ITypeSymbol => CompletionKind.Class,
        IMethodSymbol => CompletionKind.Method,
        IPropertySymbol => CompletionKind.Property,
        IFieldSymbol => CompletionKind.Field,
        IEventSymbol => CompletionKind.Event,
        ILocalSymbol => CompletionKind.Local,
        IParameterSymbol => CompletionKind.Parameter,
        _ => CompletionKind.Other,
    };

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
