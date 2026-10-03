using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Lunet.Editor;

/// <summary>Troca de <see cref="Length"/> caracteres a partir de <see cref="Start"/> por <see cref="NewText"/>.</summary>
public readonly record struct TextChange(int Start, int Length, string NewText);

public sealed record FileEdits(string Path, IReadOnlyList<TextChange> Changes);

public sealed record RenameResult(bool Success, string? Error, IReadOnlyList<FileEdits> Edits)
{
    public int Occurrences => Edits.Sum(e => e.Changes.Count);
}

/// <summary>Correção rápida: aplique <see cref="Changes"/> ao arquivo <see cref="Path"/>.</summary>
public sealed record CodeFix(string Title, string Path, IReadOnlyList<TextChange> Changes);

public static class TextChanges
{
    /// <summary>Aplica as mudanças (que não devem se sobrepor) de trás para frente.</summary>
    public static string Apply(string text, IEnumerable<TextChange> changes)
    {
        foreach (var change in changes.OrderByDescending(c => c.Start))
            text = string.Concat(text.AsSpan(0, change.Start), change.NewText, text.AsSpan(change.Start + change.Length));
        return text;
    }
}

public sealed partial class CodeAnalyzer
{
    private Dictionary<string, List<string>>? _typeNamespaces;
    private Dictionary<string, List<string>>? _extensionNamespaces;

    /// <summary>
    /// Renomeia o símbolo do projeto sob o cursor em todos os arquivos. Recusa nomes inválidos, símbolos do framework e
    /// renomeações que criariam novos erros de compilação (conflito de nomes).
    /// </summary>
    public RenameResult Rename(string path, int position, string newName)
    {
        if (!_trees.TryGetValue(path, out var tree)) return Fail("Arquivo desconhecido.");
        if (string.IsNullOrWhiteSpace(newName) || !SyntaxFacts.IsValidIdentifier(newName) || SyntaxFacts.GetKeywordKind(newName) != SyntaxKind.None)
            return Fail($"\"{newName}\" não é um nome válido.");

        var token = tree.GetRoot().FindToken(Math.Clamp(position, 0, Math.Max(0, tree.Length - 1)));
        if (token.Parent is null || !token.IsKind(SyntaxKind.IdentifierToken)) return Fail("Coloque o cursor sobre um nome.");
        var model = _compilation.GetSemanticModel(tree);
        var symbol = model.GetSymbolInfo(token.Parent).Symbol ?? model.GetDeclaredSymbol(token.Parent);
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor } ctor) symbol = ctor.ContainingType;
        if (symbol is null) return Fail("Nenhum símbolo encontrado.");
        symbol = symbol.OriginalDefinition;
        if (symbol.Kind is SymbolKind.Namespace or SymbolKind.Assembly or SymbolKind.NetModule)
            return Fail("Não é possível renomear namespaces por aqui.");
        if (symbol.IsImplicitlyDeclared || symbol.Locations.Length == 0 || symbol.Locations.Any(l => !l.IsInSource))
            return Fail($"\"{symbol.Name}\" vem do framework ou do sistema e não pode ser renomeado.");
        if (symbol.Name == newName) return Fail("O novo nome é igual ao atual.");

        var byFile = new Dictionary<string, List<TextChange>>(StringComparer.Ordinal);
        foreach (var (filePath, other) in _trees)
        {
            var otherModel = _compilation.GetSemanticModel(other);
            foreach (var id in other.GetRoot().DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken) && t.ValueText == symbol.Name))
            {
                if (id.Parent is null) continue;
                var found = otherModel.GetSymbolInfo(id.Parent).Symbol ?? otherModel.GetDeclaredSymbol(id.Parent);
                if (found is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor or MethodKind.Destructor } c) found = c.ContainingType;
                if (!SymbolEqualityComparer.Default.Equals(found?.OriginalDefinition, symbol)) continue;
                if (!byFile.TryGetValue(filePath, out var list)) byFile[filePath] = list = [];
                list.Add(new TextChange(id.SpanStart, id.Span.Length, newName));
            }
        }
        if (byFile.Count == 0) return Fail("Nenhuma ocorrência encontrada.");

        // Confere se não criou erros novos (ex.: nome já usado no mesmo escopo).
        var before = CountErrors(_compilation);
        var trial = _compilation;
        foreach (var (filePath, changes) in byFile)
        {
            var old = _trees[filePath];
            var newText = TextChanges.Apply(old.GetText().ToString(), changes);
            trial = trial.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText(newText, (CSharpParseOptions)old.Options, filePath, System.Text.Encoding.UTF8));
        }
        if (CountErrors(trial) > before) return Fail($"Renomear para \"{newName}\" causaria conflito de nomes.");

        return new RenameResult(true, null, byFile.Select(kv => new FileEdits(kv.Key, kv.Value.OrderBy(c => c.Start).ToList())).ToList());

        static RenameResult Fail(string error) => new(false, error, []);
    }

    private static int CountErrors(CSharpCompilation compilation) =>
        compilation.GetDiagnostics().Count(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

    /// <summary>Correções rápidas para os problemas sob o cursor (usings faltando ou sobrando, ";" faltando, "você quis dizer…").</summary>
    public IReadOnlyList<CodeFix> GetQuickFixes(string path, int position)
    {
        if (!_trees.TryGetValue(path, out var tree)) return [];
        var text = tree.GetText();
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var model = _compilation.GetSemanticModel(tree);
        var fixes = new List<CodeFix>();
        // Problemas da linha do cursor (no celular o cursor raramente cai exatamente sobre o erro).
        var cursorLine = text.Lines.GetLineFromPosition(Math.Clamp(position, 0, text.Length)).LineNumber;
        var diagnostics = model.GetDiagnostics()
            .Where(d => d.Location.IsInSource && (d.Location.SourceSpan.IntersectsWith(new TextSpan(position, 0)) ||
                text.Lines.GetLineFromPosition(d.Location.SourceSpan.Start).LineNumber == cursorLine))
            .ToList();

        foreach (var diagnostic in diagnostics)
        {
            var span = diagnostic.Location.SourceSpan;
            switch (diagnostic.Id)
            {
                case "CS0246" or "CS0103" or "CS0234" or "CS0305" or "CS0119":
                {
                    var name = text.ToString(span).Split('<')[0].Trim();
                    if (name.Length == 0 || !name.All(c => char.IsLetterOrDigit(c) || c == '_')) break;
                    foreach (var ns in NamespacesFor(name, extension: false).Where(ns => !HasUsing(root, ns)).Take(6))
                        fixes.Add(new CodeFix($"using {ns};", path, [AddUsing(root, text, ns)]));
                    fixes.AddRange(DidYouMean(model, tree, span, name, path));
                    break;
                }
                case "CS1061":
                {
                    var name = text.ToString(span).Trim();
                    foreach (var ns in NamespacesFor(name, extension: true).Where(ns => !HasUsing(root, ns)).Take(6))
                        fixes.Add(new CodeFix($"using {ns};", path, [AddUsing(root, text, ns)]));
                    fixes.AddRange(DidYouMean(model, tree, span, name, path));
                    break;
                }
                case "CS8019" or "CS0105" or "CS8933":
                {
                    var directive = root.FindNode(span).AncestorsAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();
                    if (directive is not null) fixes.Add(new CodeFix("Remover using desnecessário", path, [RemoveUsing(directive)]));
                    break;
                }
                case "CS1002" when diagnostic.GetMessage().Contains(';'):
                {
                    var previous = root.FindToken(span.Start).GetPreviousToken();
                    var at = previous == default || previous.Span.End > span.Start ? span.Start : previous.Span.End;
                    fixes.Add(new CodeFix("Inserir \";\"", path, [new TextChange(at, 0, ";")]));
                    break;
                }
            }
        }

        var unused = model.GetDiagnostics().Where(d => d.Id is "CS8019" && d.Location.IsInSource).ToList();
        if (unused.Count > 1)
        {
            var removals = unused.Select(d => root.FindNode(d.Location.SourceSpan).AncestorsAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault())
                .Where(u => u is not null).Select(u => RemoveUsing(u!)).ToList();
            if (removals.Count > 1) fixes.Add(new CodeFix($"Remover todos os {removals.Count} usings desnecessários", path, removals));
        }

        var sorted = SortUsings(root, text);
        if (sorted is not null) fixes.Add(new CodeFix("Ordenar usings", path, [sorted.Value]));

        return fixes.DistinctBy(f => (f.Title, f.Changes.Count > 0 ? f.Changes[0] : default)).ToList();
    }

    private static bool HasUsing(CompilationUnitSyntax root, string ns) =>
        root.Usings.Any(u => u.Name?.ToString() == ns) || root.Members.OfType<BaseNamespaceDeclarationSyntax>().Any(n => n.Usings.Any(u => u.Name?.ToString() == ns));

    private static TextChange AddUsing(CompilationUnitSyntax root, SourceText text, string ns)
    {
        var line = $"using {ns};\n";
        var usings = root.Usings.Where(u => u.Alias is null && u.StaticKeyword == default).ToList();
        if (usings.Count == 0) return new TextChange(0, 0, line + (root.Members.Count > 0 ? "\n" : ""));
        var after = usings.LastOrDefault(u => string.CompareOrdinal(u.Name?.ToString(), ns) < 0 || u.Name?.ToString().StartsWith("System", StringComparison.Ordinal) == ns.StartsWith("System", StringComparison.Ordinal) && string.CompareOrdinal(u.Name?.ToString(), ns) < 0);
        if (after is null) return new TextChange(usings[0].FullSpan.Start, 0, line);
        var lineEnd = text.Lines.GetLineFromPosition(after.Span.End);
        return new TextChange(Math.Min(text.Length, lineEnd.EndIncludingLineBreak), 0, line);
    }

    // Remove only the syntax owned by the directive. A using can share a line with
    // another using, a namespace or game code; comments are not disposable either.
    private static TextChange RemoveUsing(UsingDirectiveSyntax directive) =>
        new(directive.SpanStart, directive.Span.Length, "");

    private static TextChange? SortUsings(CompilationUnitSyntax root, SourceText text)
    {
        var usings = root.Usings.Where(u => u.Alias is null && u.StaticKeyword == default && u.Name is not null).ToList();
        if (usings.Count < 2) return null;
        var first = text.Lines.GetLineFromPosition(usings[0].SpanStart).LineNumber;
        var last = text.Lines.GetLineFromPosition(usings[^1].SpanStart).LineNumber;
        if (last - first + 1 != usings.Count) return null;

        // Move complete, standalone lines instead of reconstructing directives.
        // This preserves global, comments and CRLF. Do not reorder across directives
        // or leading comments whose ownership is ambiguous (e.g. #if / #endif).
        foreach (var directive in usings)
        {
            if (directive.GetLeadingTrivia().Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia)))
                return null;
            var line = text.Lines.GetLineFromPosition(directive.SpanStart);
            if (!string.IsNullOrWhiteSpace(text.ToString(TextSpan.FromBounds(line.Start, directive.SpanStart)))) return null;
            if (directive.Span.End > line.End) return null;
            var tail = text.ToString(TextSpan.FromBounds(directive.Span.End, line.End)).Trim();
            if (tail.Length > 0 && !tail.StartsWith("//", StringComparison.Ordinal)) return null;
        }
        static int Group(string n) => n == "System" || n.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1;
        var ordered = usings.OrderBy(u => u.GlobalKeyword == default ? 1 : 0)
            .ThenBy(u => Group(u.Name!.ToString())).ThenBy(u => u.Name!.ToString(), StringComparer.Ordinal).ToList();
        if (ordered.SequenceEqual(usings)) return null;

        // Keep terminators at their line positions, including EOF without a newline.
        var replacement = new System.Text.StringBuilder();
        for (var i = 0; i < ordered.Count; i++)
        {
            var originalLine = text.Lines.GetLineFromPosition(ordered[i].SpanStart);
            var targetLine = text.Lines[first + i];
            replacement.Append(text.ToString(originalLine.Span));
            replacement.Append(text.ToString(TextSpan.FromBounds(targetLine.End, targetLine.EndIncludingLineBreak)));
        }
        var start = text.Lines[first].Start;
        var end = text.Lines[last].EndIncludingLineBreak;
        return new TextChange(start, end - start, replacement.ToString());
    }

    private IEnumerable<CodeFix> DidYouMean(SemanticModel model, SyntaxTree tree, TextSpan span, string name, string path)
    {
        if (name.Length < 3) yield break;
        IEnumerable<string> candidates;
        var node = tree.GetRoot().FindNode(span);
        if (node.Parent is MemberAccessExpressionSyntax access && access.Name.Span == span)
        {
            var type = model.GetTypeInfo(access.Expression).Type;
            var symbol = model.GetSymbolInfo(access.Expression).Symbol;
            candidates = type is null && symbol is not INamespaceOrTypeSymbol ? []
                : model.LookupSymbols(span.Start, symbol as INamespaceOrTypeSymbol ?? type).Where(IsUsable).Select(s => s.Name);
        }
        else candidates = model.LookupSymbols(span.Start).Where(IsUsable).Select(s => s.Name);

        foreach (var candidate in candidates.Distinct().Where(c => c != name)
                     .Select(c => (Name: c, Distance: Distance(name.ToLowerInvariant(), c.ToLowerInvariant())))
                     .Where(x => x.Distance <= Math.Max(1, name.Length / 3))
                     .OrderBy(x => x.Distance).ThenBy(x => x.Name, StringComparer.Ordinal).Take(3))
            yield return new CodeFix($"Você quis dizer \"{candidate.Name}\"?", path, [new TextChange(span.Start, span.Length, candidate.Name)]);
    }

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    /// <summary>Namespaces (das referências) que declaram um tipo — ou método de extensão — com esse nome.</summary>
    private List<string> NamespacesFor(string name, bool extension)
    {
        if (_typeNamespaces is null)
        {
            _typeNamespaces = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            _extensionNamespaces = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var reference in _references.GetReferences())
            {
                if (_compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                    Index(assembly.GlobalNamespace);
            }
        }
        var map = extension ? _extensionNamespaces! : _typeNamespaces;
        return map.TryGetValue(name, out var list) ? list : [];

        void Index(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
            {
                if (type.DeclaredAccessibility != Accessibility.Public) continue;
                var nsName = ns.ToDisplayString();
                if (nsName.Length == 0) continue;
                var typeName = type.Name;
                if (!_typeNamespaces!.TryGetValue(typeName, out var list)) _typeNamespaces[typeName] = list = [];
                if (!list.Contains(nsName)) list.Add(nsName);
                if (type.IsStatic && type.MightContainExtensionMethods)
                    foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(m => m.IsExtensionMethod && m.DeclaredAccessibility == Accessibility.Public))
                    {
                        if (!_extensionNamespaces!.TryGetValue(method.Name, out var ext)) _extensionNamespaces[method.Name] = ext = [];
                        if (!ext.Contains(nsName)) ext.Add(nsName);
                    }
            }
            foreach (var child in ns.GetNamespaceMembers()) Index(child);
        }
    }
}
