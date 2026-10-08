
using AgentRuntime;
using System.Security.Cryptography;
using System.Text;
using EcosystemAi.RunJournal;

namespace AgentRuntime.Tests;

public sealed class DurableRunJournalTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly AgentIdentity Agent = new("journal-agent", "Agent", "tester");

    private static RuntimeEvent Start(string id, long sequence = 1) =>
        new(id, sequence, At, EventKind.RunCreated, Agent, "task", "ecosystem/product/project");

    private static RuntimeEvent Failure(string id, long sequence = 2) =>
        new(id, sequence, At, EventKind.RunFailed, Agent, "task", "ecosystem/product/project",
            Result: "erro verificável");

    private static void InTemp(Action<string> action)
    {
        var dir = Path.Combine(Path.GetTempPath(), "eco-run-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try { action(dir); } finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task Run_efetivo_reabre_e_reconstroi_estado_do_core()
    {
        var path = Path.Combine(Path.GetTempPath(), "eco-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var a = new LocalRunEventLog(path);
            await a.AppendAsync(Start("run-A"), CancellationToken.None);
            await a.AppendAsync(Failure("run-A"), CancellationToken.None);
            var b = new LocalRunEventLog(path);
            var events = await b.ReadRunAsync("run-A", CancellationToken.None);
            Assert.Equal(2, events.Count);
            Assert.Equal(RunStatus.Failed, RunState.Replay(events).Status);
            Assert.Empty(await b.ReadRunAsync("other-run", CancellationToken.None));
            await b.AppendAsync(Start("run-B"), CancellationToken.None);
            Assert.Single(await a.ReadRunAsync("run-B", CancellationToken.None));
            Assert.Equal(2, Directory.GetFiles(path, "*.json").Length);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public async Task Sequencia_incorreta_e_evento_apos_terminal_nunca_publicam_estado()
    {
        var root = Path.Combine(Path.GetTempPath(), "eco-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new LocalRunEventLog(root);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await db.AppendAsync(
                Failure("r"), CancellationToken.None));
            Assert.Empty(await db.ReadRunAsync("r", CancellationToken.None));
            await db.AppendAsync(Start("r"), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await db.AppendAsync(
                Failure("r", 99), CancellationToken.None));
            await db.AppendAsync(Failure("r"), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await db.AppendAsync(
                Failure("r", 3), CancellationToken.None));
            Assert.Equal(2, (await db.ReadRunAsync("r", CancellationToken.None)).Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Checksum_alterado_nao_pode_ser_regravado_ou_lido()
    {
        var root = Path.Combine(Path.GetTempPath(), "eco-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new LocalRunEventLog(root);
            await db.AppendAsync(Start("r"), CancellationToken.None);
            var path = Assert.Single(Directory.GetFiles(root, "*.json"));
            var tampered = File.ReadAllText(path).Replace("journal-agent", "fake-agent-xx", StringComparison.Ordinal);
            File.WriteAllText(path, tampered);
            await Assert.ThrowsAsync<InvalidDataException>(async () => await db.ReadRunAsync("r", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await db.AppendAsync(Failure("r"), CancellationToken.None));
            Assert.Equal(tampered, File.ReadAllText(path));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Versao_futura_e_temp_abandonado_nao_promovidos_automaticamente()
    {
        var root = Path.Combine(Path.GetTempPath(), "eco-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new LocalRunEventLog(root);
            await db.AppendAsync(Start("r"), CancellationToken.None);
            File.WriteAllText(Path.Combine(root, ".run-orphan.tmp"), "{broken");
            await db.AppendAsync(Failure("r"), CancellationToken.None);
            Assert.Equal(RunStatus.Failed, RunState.Replay(await db.ReadRunAsync("r", CancellationToken.None)).Status);
            var path = Assert.Single(Directory.GetFiles(root, "*.json"));
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"schemaVersion\":1", "\"schemaVersion\":900", StringComparison.Ordinal));
            var ex = await Assert.ThrowsAsync<InvalidDataException>(async () => await db.ReadRunAsync("r", CancellationToken.None));
            Assert.Contains("Versão", ex.Message);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Multi_instancias_lock_e_symlink_recusados()
    {
        var root = Path.Combine(Path.GetTempPath(), "eco-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var a = new LocalRunEventLog(root);
            var b = new LocalRunEventLog(root);
            await a.AppendAsync(Start("one"), CancellationToken.None);
            await b.AppendAsync(Start("two"), CancellationToken.None);
            var lockPath = Path.Combine(root, ".writer.lock");
            using (var held = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await Assert.ThrowsAsync<IOException>(async () => await a.AppendAsync(Failure("one"), CancellationToken.None));
            await a.AppendAsync(Failure("one"), CancellationToken.None);
            Assert.Equal(2, (await b.ReadRunAsync("one", CancellationToken.None)).Count);

            if (!OperatingSystem.IsWindows())
            {
                var outside = Path.Combine(Path.GetTempPath(), "eco-journal-outside-" + Guid.NewGuid().ToString("N"));
                try
                {
                    File.WriteAllText(outside, "DO-NOT-TOUCH");
                    var other = new LocalRunEventLog(Path.Combine(root, "linked"));
                    // O journal canônico é identificado por SHA-256 do RunId e não por um caminho fornecido.
                    var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("one"))).ToLowerInvariant() + ".json";
                    var src = Path.Combine(root, fileName);
                    var payload = File.ReadAllText(src);
                    var victim = Path.Combine(root, "linked", Path.GetFileName(src));
                    File.CreateSymbolicLink(victim, outside);
                    await Assert.ThrowsAsync<IOException>(async () => await other.ReadRunAsync("one", CancellationToken.None));
                    Assert.Equal("DO-NOT-TOUCH", File.ReadAllText(outside));
                    Assert.NotEmpty(payload);
                }
                finally { if (File.Exists(outside)) File.Delete(outside); }
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
