using AgentRuntime.Testing;
using AgentRuntime.Tools.Files;

namespace AgentRuntime.Tests;

/// <summary>T-3 (travessia de caminho) e a Tool de arquivos de ponta a ponta: o agente age no mundo só por capabilities.</summary>
public sealed class FileToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ecosystem-ai-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _outside = Path.Combine(Path.GetTempPath(), "ecosystem-ai-outside-" + Guid.NewGuid().ToString("N"));

    public FileToolTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
        File.WriteAllText(Path.Combine(_outside, "segredo.txt"), "fora");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        if (Directory.Exists(_outside)) Directory.Delete(_outside, recursive: true);
    }

    [Theory]
    [InlineData("../fora.txt")]
    [InlineData("a/../../fora.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData("   ")]
    public void T3_sandbox_recusa_travessia_absoluto_e_vazio(string path)
    {
        var sandbox = new FileSandbox(_root);
        Assert.Null(sandbox.Resolve(path, out var problem));
        Assert.False(string.IsNullOrEmpty(problem));
    }

    [Fact]
    public void T3_sandbox_aceita_caminhos_internos_inclusive_com_pontos_internos()
    {
        var sandbox = new FileSandbox(_root);
        Assert.NotNull(sandbox.Resolve("a/b/c.txt", out _));
        Assert.NotNull(sandbox.Resolve("a/../b.txt", out _)); // continua dentro
    }

    [Fact]
    public void T3_sandbox_recusa_nome_de_irmao_com_mesmo_prefixo()
    {
        var sandbox = new FileSandbox(_root);
        var sibling = Path.GetFileName(_root) + "-irmao";
        Assert.Null(sandbox.Resolve($"../{sibling}/x.txt", out _));
    }

    [Fact]
    public void T3_sandbox_recusa_symlink_que_escapa_da_raiz()
    {
        var link = Path.Combine(_root, "atalho");
        try { Directory.CreateSymbolicLink(link, _outside); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException) { return; } // sem permissão para symlink neste ambiente

        var sandbox = new FileSandbox(_root);
        Assert.Null(sandbox.Resolve("atalho/segredo.txt", out var problem));
        Assert.Contains("link", problem);
    }

    [Fact]
    public async Task Ferramentas_leem_e_gravam_dentro_da_raiz_e_recusam_fora()
    {
        var sandbox = new FileSandbox(_root);
        var write = new FilesWriteTool(sandbox, Rig.ProjectScope);
        var read = new FilesReadTool(sandbox, Rig.ProjectScope);
        var ctx = new ToolContext("r", Rig.Agent, Rig.Context);
        static ToolInvocation Call(string id, string json) => new("c", id, ScriptedModelProvider.Json(json));

        var wrote = await write.InvokeAsync(Call("files.write", """{"path":"notas/a.txt","content":"oi"}"""), ctx, CancellationToken.None);
        Assert.False(wrote.IsError);
        Assert.Equal("notas/a.txt", Assert.Single(wrote.Artifacts).Location);

        var again = await write.InvokeAsync(Call("files.write", """{"path":"notas/a.txt","content":"x"}"""), ctx, CancellationToken.None);
        Assert.True(again.IsError); // sobrescrever é recusado

        Assert.Equal("oi", (await read.InvokeAsync(Call("files.read", """{"path":"notas/a.txt"}"""), ctx, CancellationToken.None)).Content);

        var escape = await read.InvokeAsync(Call("files.read", """{"path":"../x"}"""), ctx, CancellationToken.None);
        Assert.True(escape.IsError);
        Assert.Contains("recusado", escape.Content);

        Assert.True((await read.InvokeAsync(Call("files.read", """{"nome":"x"}"""), ctx, CancellationToken.None)).IsError);
        Assert.True((await read.InvokeAsync(Call("files.read", """{"path":"nao-existe.txt"}"""), ctx, CancellationToken.None)).IsError);
    }

    [Fact]
    public async Task Verificador_olha_o_disco_e_falha_fechado_em_criterio_desconhecido()
    {
        var sandbox = new FileSandbox(_root);
        File.WriteAllText(Path.Combine(_root, "r.txt"), "conteudo importante");
        var verifier = new FileExistsVerifier(sandbox);
        var output = new RunOutput("terminei", []);

        var ok = await verifier.VerifyAsync(new TaskSpec("t", "g", ["exists:r.txt", "contains:r.txt:importante"]), output, CancellationToken.None);
        Assert.True(ok.Passed);
        Assert.False(string.IsNullOrWhiteSpace(ok.Evidence));

        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "g", ["exists:nao.txt"]), output, CancellationToken.None)).Passed);
        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "g", ["contains:r.txt:ausente"]), output, CancellationToken.None)).Passed);
        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "g", ["o código ficou bonito"]), output, CancellationToken.None)).Passed); // não sabe verificar → falha
        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "g", []), output, CancellationToken.None)).Passed);                       // sem critério → falha
        Assert.False((await verifier.VerifyAsync(new TaskSpec("t", "g", ["exists:../fora"]), output, CancellationToken.None)).Passed);        // fora da raiz → falha
    }

    [Fact]
    public async Task Run_completo_o_agente_cria_o_arquivo_e_o_verificador_confirma_no_disco()
    {
        var sandbox = new FileSandbox(_root);
        var rig = new Rig { Verifier = new FileExistsVerifier(sandbox) };
        rig.Host.Register(new FilesReadTool(sandbox, Rig.ProjectScope)).Register(new FilesWriteTool(sandbox, Rig.ProjectScope));
        rig.Provider.ThenToolUse("c1", "files.write", """{"path":"saida.txt","content":"feito"}""").ThenText("criei o arquivo");

        var grant = Rig.Grant(["files.read", "files.write"]);
        var result = await rig.Runner().RunAsync(Rig.Request(grant: grant, acceptance: ["exists:saida.txt", "contains:saida.txt:feito"]));

        Assert.Equal(RunStatus.Succeeded, result.State.Status);
        Assert.True(File.Exists(Path.Combine(_root, "saida.txt")));
        Assert.Equal("file:saida.txt", Assert.Single(result.Artifacts).Id);
    }

    [Fact]
    public async Task Run_completo_o_modelo_diz_que_criou_mas_nao_criou_e_nao_conclui()
    {
        var sandbox = new FileSandbox(_root);
        var rig = new Rig { Verifier = new FileExistsVerifier(sandbox) };
        for (var i = 0; i < 5; i++) rig.Provider.ThenText("criei o arquivo (mentira)");

        var result = await rig.Runner().RunAsync(Rig.Request(acceptance: ["exists:saida.txt"]));

        Assert.NotEqual(RunStatus.Succeeded, result.State.Status);
        Assert.False(File.Exists(Path.Combine(_root, "saida.txt")));
    }

    [Fact]
    public async Task Run_completo_travessia_pedida_pelo_modelo_e_recusada_pela_ferramenta()
    {
        var sandbox = new FileSandbox(_root);
        var rig = new Rig();
        rig.Host.Register(new FilesReadTool(sandbox, Rig.ProjectScope));
        rig.Provider.ThenToolUse("c1", "files.read", """{"path":"../../etc/passwd"}""").ThenText("não consegui");

        await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["files.read"])));

        var result = rig.Events().Single(e => e.Kind == EventKind.ToolResult);
        Assert.Equal("error", result.Result);
        Assert.Contains("recusado", result.Payload);
    }

    [Fact]
    public async Task Remover_arquivo_exige_aprovacao_no_runtime()
    {
        var sandbox = new FileSandbox(_root);
        File.WriteAllText(Path.Combine(_root, "apagar.txt"), "x");
        var rig = new Rig();
        rig.Host.Register(new FilesDeleteTool(sandbox, Rig.ProjectScope));
        rig.Provider.ThenToolUse("c1", "files.delete", """{"path":"apagar.txt"}""");

        var result = await rig.Runner().RunAsync(Rig.Request(grant: Rig.Grant(["files.delete"])));

        Assert.Equal(BlockReason.Escalation, result.State.Block);
        Assert.True(File.Exists(Path.Combine(_root, "apagar.txt"))); // nada foi apagado sem aprovação
    }
}
