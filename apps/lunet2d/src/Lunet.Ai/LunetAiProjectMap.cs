using System.Text;
using System.Text.Json;
using AgentRuntime;
using Lunet.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Ai;

/// <summary>One declared symbol of the game code, from the syntax only (fast, no compilation).</summary>
public sealed record LunetAiSymbol(string File, int Line, string Kind, string Name, string Signature, string? Container);

/// <summary>
/// The map of the project sent with every request (LUNET-498): files grouped by folder and the classes, fields,
/// properties and methods of every C# file. With it the model starts already knowing where things are and which
/// variables and classes exist, instead of spending model round-trips listing and reading files (the main cause of slowness).
/// </summary>
public sealed class LunetAiProjectMap
{
    public const int DefaultBudget = 9_000;
    private readonly LunetProject _project;
    private readonly Dictionary<string, IndexedFile> _index = new(StringComparer.Ordinal);
    private readonly object _indexLock = new();

    private sealed record IndexedFile(string Hash, int Lines, IReadOnlyList<LunetAiSymbol> Symbols);

    public LunetAiProjectMap(LunetProject project) => _project = project ?? throw new ArgumentNullException(nameof(project));

    /// <summary>Every symbol of the C# files of the game (Tests/ included, so tests can be explained too).</summary>
    public IReadOnlyList<LunetAiSymbol> Symbols(string? onlyFile = null) => Symbols(onlyFile, CancellationToken.None);

    internal IReadOnlyList<LunetAiSymbol> Symbols(string? onlyFile, CancellationToken cancellationToken) =>
        Index(new LunetAiProjectFiles(_project).List(), onlyFile, cancellationToken)
            .SelectMany(file => file.Symbols).ToArray();

    private IReadOnlyList<IndexedFile> Index(IReadOnlyList<string> paths, string? onlyFile, CancellationToken cancellationToken)
    {
        var files = paths.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.Ordinal);
        var indexed = new List<IndexedFile>();
        lock (_indexLock)
        {
            foreach (var removed in _index.Keys.Where(path => !files.Contains(path)).ToArray())
                _index.Remove(removed);
            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!files.Contains(path) || (onlyFile is not null && !string.Equals(path, onlyFile, StringComparison.Ordinal))) continue;
                string text;
                try { text = _project.ReadText(path); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException)
                { _index.Remove(path); continue; }
                if (text.Length > 400_000) { _index.Remove(path); continue; }

                // SAF and disk can both be edited outside Lunet. Compare content rather than relying on timestamps.
                var hash = LunetAiProjectFiles.Hash(text);
                if (!_index.TryGetValue(path, out var file) || file.Hash != hash)
                    _index[path] = file = new(hash, text.Count(c => c == '\n') + 1, Parse(path, text, cancellationToken));
                indexed.Add(file);
            }
        }
        return indexed;
    }

    internal IReadOnlyList<string> ArchitectureWarnings(CancellationToken cancellationToken)
    {
        var files = Index(new LunetAiProjectFiles(_project).List(), null, cancellationToken)
            .Where(file => file.Symbols.Count > 0).ToArray();
        return LunetAiArchitecture.Review(files.SelectMany(file => file.Symbols).ToArray(),
            files.ToDictionary(file => file.Symbols[0].File, file => file.Lines, StringComparer.Ordinal));
    }

    public static IReadOnlyList<LunetAiSymbol> Parse(string file, string source) => Parse(file, source, CancellationToken.None);

    private static IReadOnlyList<LunetAiSymbol> Parse(string file, string source, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);
        var result = new List<LunetAiSymbol>();
        var text = tree.GetText();
        int LineOf(SyntaxNode n) => text.Lines.GetLineFromPosition(n.SpanStart).LineNumber + 1;
        void Visit(SyntaxNode node, string? container)
        {
            foreach (var child in node.ChildNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (child)
                {
                    case BaseNamespaceDeclarationSyntax ns:
                        Visit(ns, container);
                        break;
                    case BaseTypeDeclarationSyntax type:
                    {
                        var kind = type switch
                        {
                            InterfaceDeclarationSyntax => "interface",
                            StructDeclarationSyntax => "struct",
                            EnumDeclarationSyntax => "enum",
                            RecordDeclarationSyntax => "record",
                            _ => "class",
                        };
                        var bases = type.BaseList is null ? "" : " : " + string.Join(", ", type.BaseList.Types.Select(t => t.ToString()));
                        var name = type.Identifier.Text;
                        result.Add(new(file, LineOf(type), kind, name, Modifiers(type.Modifiers) + kind + " " + name + bases, container));
                        if (type is EnumDeclarationSyntax e)
                            result.Add(new(file, LineOf(e), "values", name, string.Join(", ", e.Members.Select(m => m.Identifier.Text)), name));
                        else Visit(type, name);
                        break;
                    }
                    case MethodDeclarationSyntax m:
                        result.Add(new(file, LineOf(m), "method", m.Identifier.Text,
                            Modifiers(m.Modifiers) + m.ReturnType + " " + m.Identifier.Text + m.TypeParameterList + Compact(m.ParameterList.ToString()), container));
                        break;
                    case ConstructorDeclarationSyntax c:
                        result.Add(new(file, LineOf(c), "constructor", c.Identifier.Text,
                            Modifiers(c.Modifiers) + c.Identifier.Text + Compact(c.ParameterList.ToString()), container));
                        break;
                    case PropertyDeclarationSyntax p:
                        result.Add(new(file, LineOf(p), "property", p.Identifier.Text,
                            Modifiers(p.Modifiers) + p.Type + " " + p.Identifier.Text, container));
                        break;
                    case FieldDeclarationSyntax f:
                        foreach (var v in f.Declaration.Variables)
                            result.Add(new(file, LineOf(f), "field", v.Identifier.Text,
                                Modifiers(f.Modifiers) + f.Declaration.Type + " " + v.Identifier.Text, container));
                        break;
                    case EventFieldDeclarationSyntax ev:
                        foreach (var v in ev.Declaration.Variables)
                            result.Add(new(file, LineOf(ev), "event", v.Identifier.Text,
                                Modifiers(ev.Modifiers) + "event " + ev.Declaration.Type + " " + v.Identifier.Text, container));
                        break;
                    case DelegateDeclarationSyntax d:
                        result.Add(new(file, LineOf(d), "delegate", d.Identifier.Text,
                            "delegate " + d.ReturnType + " " + d.Identifier.Text + Compact(d.ParameterList.ToString()), container));
                        break;
                }
            }
        }
        Visit(tree.GetRoot(cancellationToken), null);
        return result;
    }

    private static string Modifiers(SyntaxTokenList modifiers)
    {
        var kept = modifiers.Where(m => !m.IsKind(SyntaxKind.PartialKeyword)).Select(m => m.Text).ToArray();
        return kept.Length == 0 ? "" : string.Join(' ', kept) + " ";
    }

    private static string Compact(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static readonly string[] MediaExtensions = [".png", ".wav", ".ogg", ".mp3", ".ttf", ".otf"];

    private static bool IsMedia(string path) =>
        (path.StartsWith("Content/", StringComparison.Ordinal) || path.StartsWith("Assets/", StringComparison.Ordinal))
        && MediaExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Binary content the code can load (textures, sounds, fonts), relative to the project root.</summary>
    public IReadOnlyList<string> MediaFiles() =>
        new LunetAiProjectFiles(_project).List(IsMedia).Where(IsMedia).Take(400).ToArray();

    /// <summary>Compact text map: tree of files and, for each C# file, its types and members. Cut to <paramref name="budget"/> characters.</summary>
    public string Describe(int budget = DefaultBudget) => Describe(CancellationToken.None, budget);

    internal string Describe(CancellationToken cancellationToken, int budget = DefaultBudget)
    {
        var paths = new LunetAiProjectFiles(_project).List(IsMedia);
        var files = paths.Where(path => !IsMedia(path)).ToArray();
        var builder = new StringBuilder();
        builder.Append("Arquivos (").Append(files.Length).AppendLine("):");
        foreach (var group in files.GroupBy(f => f.Contains('/') ? f[..f.LastIndexOf('/')] : ".").OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var names = group.Select(f => f[(f.LastIndexOf('/') + 1)..]).ToArray();
            builder.Append("- ").Append(group.Key == "." ? "(raiz)" : group.Key + "/").Append(": ")
                .AppendLine(string.Join(", ", names.Take(40)) + (names.Length > 40 ? $" …(+{names.Length - 40})" : ""));
        }
        var media = paths.Where(IsMedia).Take(400).ToArray();
        if (media.Length > 0)
            builder.Append("Imagens e sons (").Append(media.Length).Append("): ")
                .AppendLine(string.Join(", ", media.Take(60)) + (media.Length > 60 ? $" …(+{media.Length - 60})" : ""));
        var indexed = Index(files, null, cancellationToken).Where(file => file.Symbols.Count > 0).ToArray();
        if (indexed.Length > 0)
        {
            builder.AppendLine("Código (tipos e membros; linha entre colchetes):");
            foreach (var file in indexed)
            {
                builder.Append(file.Symbols[0].File).Append(" (").Append(file.Lines).AppendLine(" linhas)");
                foreach (var symbol in file.Symbols)
                {
                    var indent = symbol.Container is null ? "  " : "    ";
                    builder.Append(indent).Append('[').Append(symbol.Line).Append("] ").AppendLine(symbol.Signature);
                }
            }
        }
        var map = builder.ToString();
        return map.Length <= budget ? map : map[..budget] + "\n…(mapa truncado; use lunet.code.outline com o caminho do arquivo)";
    }
}

/// <summary>Architecture review of the game code (LUNET-498): flags the "everything in one file" shape.</summary>
public static class LunetAiArchitecture
{
    public const int LargeFileLines = 350;

    public static IReadOnlyList<string> Review(IReadOnlyList<LunetAiSymbol> symbols, IReadOnlyDictionary<string, int> lineCounts)
    {
        var warnings = new List<string>();
        foreach (var file in symbols.GroupBy(s => s.File))
        {
            var types = file.Where(s => s.Kind is "class" or "struct" or "record" or "interface" or "enum").ToArray();
            var topLevel = types.Count(t => t.Container is null);
            if (topLevel > 2)
                warnings.Add($"{file.Key} declara {topLevel} tipos ({string.Join(", ", types.Where(t => t.Container is null).Select(t => t.Name))}). " +
                             "Considere separar responsabilidades se isso melhorar este projeto; não é um erro de compilação.");
            if (lineCounts.TryGetValue(file.Key, out var lines) && lines > LargeFileLines)
                warnings.Add($"{file.Key} tem {lines} linhas. Avalie se separar responsabilidades ajudaria; o tamanho sozinho não exige refatoração.");
        }
        return warnings;
    }

    public const string Guidance =
        "ARQUITETURA (recomendações para código novo, subordinadas à autoria do usuário). Preserve a organização existente e a escala do jogo; quando útil, separe responsabilidades: " +
        "Prefira um tipo público por arquivo e nome correspondente quando isso ajudar. Game.cs pode orquestrar (cria cenas/sistemas, chama Update/Draw); " +
        "não divida projetos pequenos por obrigação. Pastas por responsabilidade, seguindo as que o projeto já tiver; quando forem úteis, considere: " +
        "Scenes/ (telas e fases), Entities/ (jogador, inimigos, itens), Components/ (comportamentos reutilizáveis), Systems/ (física, spawn, pontuação, colisão), " +
        "UI/ (HUD, menus), Data/ (constantes, configurações, níveis), Util/ (extensões e ajudantes). Todos os .cs fora de Tests/ são compilados juntos, " +
        "então classes em pastas diferentes se enxergam (use o mesmo namespace do projeto, opcionalmente com sufixo da pasta, e os using necessários). " +
        "Antes de criar algo, reutilize as classes, campos e métodos que já existem (veja o mapa do projeto): chame-os pelos nomes reais, não duplique. " +
        "Convenções C#: PascalCase em tipos, métodos e propriedades; _camelCase em campos privados; constantes e números mágicos em Data/; " +
        "campos privados com propriedades quando necessário; dependências pelo construtor quando fizer sentido. Arquivos longos podem merecer revisão, mas tamanho não obriga divisão. " +
        "Não reorganize arquivos ou renomeie símbolos existentes por preferência; sugira refatoração separadamente, salvo pedido explícito ou necessidade técnica demonstrada.";
}

/// <summary><c>lunet.code.outline</c>: classes, fields, properties and methods with lines, for one file or the whole project.</summary>
public sealed class LunetAiOutlineTool : ITool
{
    public const string Name = "lunet.code.outline";
    private readonly LunetAiProjectMap _map;
    public ToolDescriptor Descriptor { get; }

    public LunetAiOutlineTool(LunetProject project) : this(project, new LunetAiProjectMap(project)) { }

    internal LunetAiOutlineTool(LunetProject project, LunetAiProjectMap map)
    {
        _map = map;
        Descriptor = new ToolDescriptor(Name, "1.0.0",
            "Lista os tipos e membros (campos, propriedades, métodos com parâmetros) do código do jogo, com a linha de cada um. " +
            "Sem 'path', devolve o projeto inteiro. Use para saber quais classes e variáveis existem antes de criar ou chamar algo.",
            """{"type":"object","properties":{"path":{"type":"string","description":"Arquivo .cs (opcional)"},"query":{"type":"string","description":"Filtra por nome (opcional)"}},"additionalProperties":false}""",
            new HashSet<string> { Permissions.AgentAct, Permissions.FsRead }, RiskClass.Read,
            ContextPath.Create(new ContextStep(ContextLevel.Ecosystem, "ecosystem"), new ContextStep(ContextLevel.Product, "lunet2d"),
                new ContextStep(ContextLevel.Project, project.Manifest.GameId)), true);
    }

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string Arg(string key) => invocation.Arguments.ValueKind == JsonValueKind.Object &&
                                  invocation.Arguments.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        var path = Arg("path");
        var query = Arg("query");
        var symbols = _map.Symbols(path.Length > 0 ? path : null, cancellationToken)
            .Where(s => query.Length == 0 || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (s.Container?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(400)
            .Select(s => new { file = s.File, line = s.Line, kind = s.Kind, signature = s.Signature, inType = s.Container })
            .ToArray();
        if (symbols.Length == 0)
            return ValueTask.FromResult(ToolOutcome.Ok(path.Length > 0 ? "Nenhum símbolo em " + path : "Nenhum símbolo encontrado."));
        return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(symbols)));
    }
}
