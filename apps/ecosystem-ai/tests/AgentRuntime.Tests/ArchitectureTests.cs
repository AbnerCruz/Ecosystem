using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using AgentRuntime.Testing;
using AgentRuntime.Tools.Files;

namespace AgentRuntime.Tests;

/// <summary>
/// T-13 — arquitetura (ARCH): o Core é host-agnostic e model-agnostic. Os testes leem os METADADOS do IL (referências reais de
/// assemblies, tipos e membros), não texto de código: um using esquecido ou um comentário não os engana.
/// </summary>
public class ArchitectureTests
{
    private sealed record Surface(
        IReadOnlySet<string> AssemblyRefs,
        IReadOnlySet<string> TypeRefs,
        IReadOnlySet<string> MemberRefs,
        IReadOnlyList<string> Names,
        IReadOnlyList<string> UserStrings);

    private static Surface Read(Assembly assembly)
    {
        using var pe = new PEReader(File.OpenRead(assembly.Location));
        var md = pe.GetMetadataReader();

        string TypeName(TypeReference t) => md.GetString(t.Namespace) is { Length: > 0 } ns ? $"{ns}.{md.GetString(t.Name)}" : md.GetString(t.Name);

        var assemblyRefs = md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).ToHashSet(StringComparer.Ordinal);
        var typeRefs = md.TypeReferences.Select(h => TypeName(md.GetTypeReference(h))).ToHashSet(StringComparer.Ordinal);
        var memberRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var h in md.MemberReferences)
        {
            var m = md.GetMemberReference(h);
            if (m.Parent.Kind == HandleKind.TypeReference)
                memberRefs.Add($"{TypeName(md.GetTypeReference((TypeReferenceHandle)m.Parent))}::{md.GetString(m.Name)}");
        }

        var names = new List<string>();
        foreach (var h in md.TypeDefinitions)
        {
            var t = md.GetTypeDefinition(h);
            names.Add(md.GetString(t.Namespace));
            names.Add(md.GetString(t.Name));
        }
        names.AddRange(md.MethodDefinitions.Select(h => md.GetString(md.GetMethodDefinition(h).Name)));
        names.AddRange(md.FieldDefinitions.Select(h => md.GetString(md.GetFieldDefinition(h).Name)));
        names.AddRange(assemblyRefs);
        names.AddRange(typeRefs);

        var strings = new List<string>();
        for (var h = MetadataTokens.UserStringHandle(1); !h.IsNil; h = md.GetNextHandle(h))
            strings.Add(md.GetUserString(h));

        return new Surface(assemblyRefs, typeRefs, memberRefs, names, strings);
    }

    private static readonly Assembly Core = typeof(AgentRunner).Assembly;
    private static readonly Assembly TestingAssembly = typeof(FakeClock).Assembly;
    private static readonly Assembly FilesAssembly = typeof(FileSandbox).Assembly;

    // Quem o Core pode conhecer: só a biblioteca-base. Nada de rede, processo, UI, SDK de provedor nem outro Product ou o Control Plane.
    private static readonly string[] CoreAssemblyAllowlist =
    [
        "System.Runtime", "System.Collections", "System.Linq", "System.Text.Json", "System.Threading",
        "System.Threading.Channels", "System.Memory", "System.Collections.Concurrent", "System.Runtime.InteropServices",
        "System.Text.Encoding.Extensions", "System.Threading.Tasks", "System.Runtime.Extensions",
    ];

    // Os nomes de outros Products e do Control Plane são montados por partes: o check de arquitetura do repositório varre este
    // diretório atrás desses nomes, e o teste que os proíbe não pode contê-los por extenso.
    private static string W(params string[] parts) => string.Concat(parts);

    private static readonly string[] BannedWords =
    [
        W("ur", "be"), W("lu", "net"), W("hu", "b"), W("gi", "thub"), "octokit", "avalonia", "maui", W("anthro", "pic"), W("open", "ai"),
        "httpclient", "winui", "blazor",
    ];

    private static readonly string[] BannedTypePrefixes =
    [
        "System.Net.", "System.Diagnostics.Process", "System.IO.File", "System.IO.Directory", "System.IO.FileStream",
        "System.IO.StreamReader", "System.IO.StreamWriter", "System.Threading.Timer", "System.Threading.PeriodicTimer",
        "System.Timers.", "System.Environment", "System.Data.", "System.Windows.", "System.Runtime.Loader.",
    ];

    private static readonly string[] BannedMembers =
    [
        "System.DateTime::get_Now", "System.DateTime::get_UtcNow", "System.DateTime::get_Today",
        "System.DateTimeOffset::get_Now", "System.DateTimeOffset::get_UtcNow",
        "System.Threading.Tasks.Task::Delay", "System.Threading.Thread::Sleep",
    ];

    [Fact]
    public void T13_core_so_referencia_a_biblioteca_base_permitida()
    {
        var surface = Read(Core);
        var unexpected = surface.AssemblyRefs.Except(CoreAssemblyAllowlist).Order().ToList();
        Assert.True(unexpected.Count == 0, $"O Core referencia assemblies fora da allowlist: {string.Join(", ", unexpected)}");
    }

    [Fact]
    public void T13_core_nao_usa_rede_processo_arquivo_timer_nem_relogio_direto()
    {
        var surface = Read(Core);
        var bannedTypes = surface.TypeRefs
            .Where(t => t == "System.Reflection.Assembly" || BannedTypePrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal)))
            .Order().ToList();
        Assert.True(bannedTypes.Count == 0, $"Tipos proibidos no Core: {string.Join(", ", bannedTypes)}");

        var bannedMembers = surface.MemberRefs.Intersect(BannedMembers).Order().ToList();
        Assert.True(bannedMembers.Count == 0, $"Membros proibidos no Core (tempo vem de IClock): {string.Join(", ", bannedMembers)}");
    }

    [Theory]
    [InlineData("Core")]
    [InlineData("Testing")]
    [InlineData("Files")]
    public void T13_nenhum_assembly_menciona_product_hub_ui_github_ou_sdk_de_provedor(string which)
    {
        var assembly = which switch { "Core" => Core, "Testing" => TestingAssembly, _ => FilesAssembly };
        var surface = Read(assembly);
        var hits = surface.Names.Concat(surface.UserStrings)
            .SelectMany(text => BannedWords.Where(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)).Select(w => $"'{w}' em \"{text}\""))
            .Distinct().Order().ToList();
        Assert.True(hits.Count == 0, $"{assembly.GetName().Name} menciona o que não pode conhecer: {string.Join("; ", hits)}");
    }

    [Fact]
    public void T13_testing_e_files_dependem_so_do_core_e_da_biblioteca_base()
    {
        foreach (var assembly in new[] { TestingAssembly, FilesAssembly })
        {
            var refs = Read(assembly).AssemblyRefs.Where(a => !a.StartsWith("System.", StringComparison.Ordinal) && a != "netstandard").Order().ToList();
            Assert.Equal(["AgentRuntime.Core"], refs);
        }
    }

    [Fact]
    public void T13_files_nao_usa_rede_nem_processo()
    {
        var surface = Read(FilesAssembly);
        var banned = surface.TypeRefs.Where(t => t.StartsWith("System.Net.", StringComparison.Ordinal) || t.StartsWith("System.Diagnostics.Process", StringComparison.Ordinal)).ToList();
        Assert.Empty(banned);
    }

    [Fact]
    public void T13_projetos_nao_tem_pacote_nem_dependencia_alem_do_core()
    {
        var root = AppRoot();
        var coreProject = File.ReadAllText(Path.Combine(root, "src", "AgentRuntime.Core", "AgentRuntime.Core.csproj"));
        Assert.DoesNotContain("PackageReference", coreProject);
        Assert.DoesNotContain("ProjectReference", coreProject);

        foreach (var name in new[] { "AgentRuntime.Testing", "AgentRuntime.Tools.Files" })
        {
            var text = File.ReadAllText(Path.Combine(root, "src", name, $"{name}.csproj"));
            Assert.DoesNotContain("PackageReference", text);
            var projectRefs = text.Split('\n').Where(l => l.Contains("ProjectReference", StringComparison.Ordinal)).ToList();
            Assert.Single(projectRefs);
            Assert.Contains("AgentRuntime.Core.csproj", projectRefs[0]);
        }
    }

    [Fact]
    public void T13_o_product_nao_aponta_para_fora_de_si_nos_arquivos_de_projeto()
    {
        var root = AppRoot();
        foreach (var file in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(root, "*.slnx", SearchOption.TopDirectoryOnly))
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("..\\..\\..\\", text); // nada sobe para fora de apps/ecosystem-ai
            Assert.DoesNotContain("../../../", text);
            foreach (var word in BannedWords) Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Core_nao_expoe_tipos_dos_assemblies_de_teste_nem_de_tools()
    {
        var exported = Core.GetExportedTypes();
        Assert.NotEmpty(exported);
        Assert.All(exported, t => Assert.Equal("AgentRuntime", t.Namespace)); // um único namespace público, sem subáreas "shared"
        Assert.DoesNotContain(exported, t => t.Namespace!.Contains("Testing") || t.Namespace.Contains("Tools"));
    }

    private static string AppRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EcosystemAI.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do Product (EcosystemAI.slnx) não encontrada.");
    }
}
