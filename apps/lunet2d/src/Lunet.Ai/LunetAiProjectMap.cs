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

    public LunetAiProjectMap(LunetProject project) => _project = project ?? throw new ArgumentNullException(nameof(project));

    /// <summary>Every symbol of the C# files of the game (Tests/ included, so tests can be explained too).</summary>
    public IReadOnlyList<LunetAiSymbol> Symbols(string? onlyFile = null)
    {
        var files = new LunetAiProjectFiles(_project).List()
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => onlyFile is null || string.Equals(f, onlyFile, StringComparison.Ordinal));
        var symbols = new List<LunetAiSymbol>();
        foreach (var file in files)
        {
            string text;
            try { text = _project.ReadText(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException) { continue; }
            if (text.Length > 400_000) continue;
            symbols.AddRange(Parse(file, text));
        }
        return symbols;
    }

    public static IReadOnlyList<LunetAiSymbol> Parse(string file, string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var result = new List<LunetAiSymbol>();
        var text = tree.GetText();
        int LineOf(SyntaxNode n) => text.Lines.GetLineFromPosition(n.SpanStart).LineNumber + 1;
        void Visit(SyntaxNode node, string? container)
        {
            foreach (var child in node.ChildNodes())
            {
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
        Visit(tree.GetRoot(), null);
        return result;
    }

    private static string Modifiers(SyntaxTokenList modifiers)
    {
        var kept = modifiers.Where(m => !m.IsKind(SyntaxKind.PartialKeyword)).Select(m => m.Text).ToArray();
        return kept.Length == 0 ? "" : string.Join(' ', kept) + " ";
    }

    private static string Compact(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static readonly string[] MediaExtensions = [".png", ".wav", ".ogg", ".mp3", ".ttf", ".otf"];

    /// <summary>Binary content the code can load (textures, sounds, fonts), relative to the project root.</summary>
    public IReadOnlyList<string> MediaFiles()
    {
        var result = new List<string>();
        foreach (var root in new[] { "Content", "Assets" })
        {
            var absolute = Path.Combine(_project.Directory, root);
            if (!Directory.Exists(absolute)) continue;
            foreach (var file in Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories))
            {
                if (!MediaExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
                result.Add(Path.GetRelativePath(_project.Directory, file).Replace('\\', '/'));
                if (result.Count >= 400) break;
            }
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    /// <summary>Compact text map: tree of files and, for each C# file, its types and members. Cut to <paramref name="budget"/> characters.</summary>
    public string Describe(int budget = DefaultBudget)
    {
        var files = new LunetAiProjectFiles(_project).List();
        var builder = new StringBuilder();
        builder.Append("Arquivos (").Append(files.Count).AppendLine("):");
        foreach (var group in files.GroupBy(f => f.Contains('/') ? f[..f.LastIndexOf('/')] : ".").OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var names = group.Select(f => f[(f.LastIndexOf('/') + 1)..]).ToArray();
            builder.Append("- ").Append(group.Key == "." ? "(raiz)" : group.Key + "/").Append(": ")
                .AppendLine(string.Join(", ", names.Take(40)) + (names.Length > 40 ? $" …(+{names.Length - 40})" : ""));
        }
        var media = MediaFiles();
        if (media.Count > 0)
            builder.Append("Imagens e sons (").Append(media.Count).Append("): ")
                .AppendLine(string.Join(", ", media.Take(60)) + (media.Count > 60 ? $" …(+{media.Count - 60})" : ""));
        var symbols = Symbols();
        if (symbols.Count > 0)
        {
            builder.AppendLine("Código (tipos e membros; linha entre colchetes):");
            foreach (var file in symbols.GroupBy(s => s.File))
            {
                int lines = 0;
                try { lines = _project.ReadText(file.Key).Count(c => c == '\n') + 1; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException) { }
                builder.Append(file.Key).Append(" (").Append(lines).AppendLine(" linhas)");
                foreach (var symbol in file)
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
                             "Use um arquivo por classe, em pastas por responsabilidade.");
            if (lineCounts.TryGetValue(file.Key, out var lines) && lines > LargeFileLines)
                warnings.Add($"{file.Key} tem {lines} linhas. Extraia responsabilidades (entidades, sistemas, UI, dados) para arquivos próprios.");
        }
        return warnings;
    }

    public const string Guidance =
        "ARQUITETURA (obrigatório ao criar ou ampliar código). Organize como um projeto profissional, com responsabilidades separadas: " +
        "UM tipo público por arquivo, nome do arquivo = nome da classe. O Game.cs só orquestra (cria cenas/sistemas, chama Update/Draw); " +
        "não acumule lógica nele. Pastas por responsabilidade, seguindo as que o projeto já tiver; quando não houver, use: " +
        "Scenes/ (telas e fases), Entities/ (jogador, inimigos, itens), Components/ (comportamentos reutilizáveis), Systems/ (física, spawn, pontuação, colisão), " +
        "UI/ (HUD, menus), Data/ (constantes, configurações, níveis), Util/ (extensões e ajudantes). Todos os .cs fora de Tests/ são compilados juntos, " +
        "então classes em pastas diferentes se enxergam (use o mesmo namespace do projeto, opcionalmente com sufixo da pasta, e os using necessários). " +
        "Antes de criar algo, reutilize as classes, campos e métodos que já existem (veja o mapa do projeto): chame-os pelos nomes reais, não duplique. " +
        "Convenções C#: PascalCase em tipos, métodos e propriedades; _camelCase em campos privados; constantes e números mágicos em Data/; " +
        "campos privados com propriedades só quando outra classe precisa ler; dependências passadas pelo construtor. Arquivos acima de ~300 linhas devem ser divididos. " +
        "Quando o pedido for pequeno e o projeto ainda for só o Game.cs, pode editar o Game.cs, mas assim que surgir uma entidade ou sistema novo, crie o arquivo dele.";
}

/// <summary><c>lunet.code.outline</c>: classes, fields, properties and methods with lines, for one file or the whole project.</summary>
public sealed class LunetAiOutlineTool : ITool
{
    public const string Name = "lunet.code.outline";
    private readonly LunetAiProjectMap _map;
    public ToolDescriptor Descriptor { get; }

    public LunetAiOutlineTool(LunetProject project)
    {
        _map = new LunetAiProjectMap(project);
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
        var symbols = _map.Symbols(path.Length > 0 ? path : null)
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
