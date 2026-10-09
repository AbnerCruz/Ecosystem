using AgentRuntime;
using EcosystemAi.Cli;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class R4CliRunJournalTests
{
    private static readonly DateTimeOffset When = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
    private static readonly AgentIdentity Agent = new("cli", "Assistente", "executor");

    [Fact]
    public async Task Journal_reabre_run_terminal_e_consulta_nao_revela_payload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "r4-cli-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var journal = new LocalRunEventLog(directory);
            await journal.AppendAsync(new RuntimeEvent("r4-audit", 1, When,
                EventKind.RunCreated, Agent, "task", "ecosystem/product/project"), CancellationToken.None);
            await journal.AppendAsync(new RuntimeEvent("r4-audit", 2, When,
                EventKind.RunFailed, Agent, "task", "ecosystem/product/project",
                Result: "segredo-em-result", Payload: "segredo-em-payload"), CancellationToken.None);

            var summary = await CliRunJournalCommands.ShowAsync(directory, "r4-audit");
            Assert.Contains(summary, line => line.Contains("Estado: Failed", StringComparison.Ordinal));
            Assert.Contains(summary, line => line.Contains("#2: RunFailed", StringComparison.Ordinal));
            Assert.DoesNotContain("segredo-em-result", string.Join("\n", summary), StringComparison.Ordinal);
            Assert.DoesNotContain("segredo-em-payload", string.Join("\n", summary), StringComparison.Ordinal);
            Assert.Equal(0, await EcosystemAiCli.RunAsync(
                ["--show-run", "--journal", directory, "--run-id", "r4-audit"]));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Consulta_inexistente_nao_cria_diretorio_ou_journal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "r4-cli-missing-" + Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await CliRunJournalCommands.ShowAsync(directory, "unknown"));
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void Diario_dentro_do_sandbox_e_recusado_inclusive_com_caminho_normalizado()
    {
        var project = Path.Combine(Path.GetTempPath(), "r4-cli-sandbox");
        Assert.Throws<ArgumentException>(() => CliRunJournalCommands.RequireOutsideWorkspace(project, project));
        Assert.Throws<ArgumentException>(() => CliRunJournalCommands.RequireOutsideWorkspace(
            Path.Combine(project, "a", "..", "auditoria"), project));
        CliRunJournalCommands.RequireOutsideWorkspace(
            Path.Combine(Path.GetTempPath(), "r4-cli-audit-outside"), project);
    }

    [Fact]
    public async Task Consulta_de_run_recusa_flags_de_execucao_e_entrada_malformada()
    {
        Assert.Equal(2, await EcosystemAiCli.RunAsync(
            ["--show-run", "--journal", "/pasta", "--run-id", "r", "--allow-create"]));
        Assert.Equal(2, await EcosystemAiCli.RunAsync(["--run-id", "r"]));
    }
}
