using System.Text.Json;
using EcosystemAi.ProjectStore;
using EcosystemAi.Cli;

namespace EcosystemAi.ProjectStore.Tests;

public sealed class LocalProjectStoreTests
{
    private static readonly DateTimeOffset Fixed = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    private static void InTemp(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "ecosystem-ai-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Roundtrip_projeto_sessao_conversa_receipt_e_revisoes()
    {
        InTemp(root =>
        {
            var store = new LocalProjectStore(Path.Combine(root, "catalog"), () => Fixed);
            Assert.Equal(0, store.Read().Revision);
            var project = store.CreateProject("Projeto 1", root);
            var session = store.CreateSession(project.Id, "Sessão inicial");
            store.AppendTurn(project.Id, session.Id, "user", "Leia README.md");
            store.AppendTurn(project.Id, session.Id, "assistant", "Resultado resumido");
            store.AppendRun(project.Id, session.Id, new RunReceipt("run-1", "succeeded", 2, "USD", true,
                "verificação baseada em arquivo", Fixed));

            var fromDisk = new LocalProjectStore(Path.Combine(root, "catalog"), () => Fixed).Read();
            Assert.Equal(5, fromDisk.Revision);
            var p = Assert.Single(fromDisk.Projects);
            Assert.Equal(project.Id, p.Id);
            Assert.Equal("Projeto 1", p.Name);
            var s = Assert.Single(p.Sessions);
            Assert.Equal(session.Id, s.Id);
            Assert.Equal(new[] { "user", "assistant" }, s.Turns.Select(t => t.Role));
            Assert.Equal("Resultado resumido", s.Turns[1].Text);
            Assert.Equal(2, Assert.Single(s.Runs).CostMinor);
            Assert.Equal(Fixed, s.CreatedAt);
        });
    }

    [Fact]
    public void Corrupcao_de_checksum_recusa_modificacao_e_preserva_bytes()
    {
        InTemp(root =>
        {
            var store = new LocalProjectStore(Path.Combine(root, "db"));
            store.CreateProject("Projeto", root);
            var path = Path.Combine(root, "db", "catalog.json");
            var json = File.ReadAllText(path).Replace("Projeto", "Hackeado", StringComparison.Ordinal);
            File.WriteAllText(path, json);
            Assert.Throws<InvalidDataException>(() => store.Read());
            Assert.Throws<InvalidDataException>(() => store.CreateProject("Outro", root));
            Assert.Equal(json, File.ReadAllText(path));
        });
    }

    [Fact]
    public void Versao_futura_falha_fechada_mesmo_que_o_checksum_nao_bata()
    {
        InTemp(root =>
        {
            var store = new LocalProjectStore(Path.Combine(root, "db"));
            store.CreateProject("Projeto", root);
            var path = Path.Combine(root, "db", "catalog.json");
            var json = File.ReadAllText(path).Replace("\"schemaVersion\":1", "\"schemaVersion\":900", StringComparison.Ordinal);
            File.WriteAllText(path, json);
            var error = Assert.Throws<InvalidDataException>(() => store.CreateProject("Novo", root));
            Assert.Contains("Versão", error.Message);
            Assert.Equal(json, File.ReadAllText(path));
        });
    }

    [Fact]
    public void Temp_de_crash_nao_substitui_o_catalogo_valido()
    {
        InTemp(root =>
        {
            var store = new LocalProjectStore(Path.Combine(root, "db"));
            var project = store.CreateProject("Existe", root);
            File.WriteAllText(Path.Combine(root, "db", ".catalog-orphan.tmp"), "{invalid-json");
            var session = store.CreateSession(project.Id, "Retomada");
            Assert.Equal(session.Id, Assert.Single(Assert.Single(new LocalProjectStore(Path.Combine(root, "db")).Read().Projects).Sessions).Id);
            Assert.Equal("{invalid-json", File.ReadAllText(Path.Combine(root, "db", ".catalog-orphan.tmp")));
        });
    }

    [Fact]
    public void Duas_instancias_releem_a_ultima_revisao()
    {
        InTemp(root =>
        {
            var a = new LocalProjectStore(Path.Combine(root, "db"));
            var b = new LocalProjectStore(Path.Combine(root, "db"));
            a.CreateProject("P1", root);
            b.CreateProject("P2", root);
            Assert.Equal(2, a.Read().Revision);
            Assert.Equal(2, b.Read().Projects.Count);
            var lockPath = Path.Combine(root, "db", ".writer.lock");
            using (var writer = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Throws<IOException>(() => a.CreateProject("P3", root));
            Assert.Equal(2, a.Read().Projects.Count);
        });
    }

    [Fact]
    public void Ids_invalidos_receipts_repetidos_e_texto_excessivo_nao_gravam()
    {
        InTemp(root =>
        {
            var db = new LocalProjectStore(Path.Combine(root, "db"));
            var p = db.CreateProject("Projeto", root);
            var s = db.CreateSession(p.Id, "Sessão");
            var before = db.Read().Revision;
            Assert.Throws<ArgumentException>(() => db.AppendTurn(p.Id, s.Id, "system", "malicioso"));
            Assert.Throws<ArgumentException>(() => db.AppendTurn(p.Id, s.Id, "user", new string('x', 17000)));
            Assert.Throws<ArgumentException>(() => db.AppendRun(p.Id, s.Id,
                new RunReceipt("bad", "failed", -5, "USD", true, "inventado", Fixed)));
            Assert.Throws<ArgumentException>(() => db.AppendTurn("../root", s.Id, "user", "escape"));
            Assert.Equal(before, db.Read().Revision);
            var receipt = new RunReceipt("run-x", "blocked", 0, "USD", false, "bloqueado", Fixed);
            db.AppendRun(p.Id, s.Id, receipt);
            Assert.Throws<InvalidOperationException>(() => db.AppendRun(p.Id, s.Id, receipt));
            Assert.Equal(before + 1, db.Read().Revision);
        });
    }

    [Fact]
    public void Arquivo_symlink_de_catalogo_recusado_em_vez_de_escrever_fora()
    {
        if (OperatingSystem.IsWindows()) return; // CI Linux possui suporte a symlink.
        InTemp(root =>
        {
            var store = new LocalProjectStore(Path.Combine(root, "db"));
            var outside = Path.Combine(root, "outside.json");
            File.WriteAllText(outside, "DO-NOT-CHANGE");
            File.CreateSymbolicLink(Path.Combine(root, "db", "catalog.json"), outside);
            Assert.Throws<IOException>(() => store.CreateProject("Projeto", root));
            Assert.Equal("DO-NOT-CHANGE", File.ReadAllText(outside));
        });
    }

    [Fact]
    public void Cli_pode_reabrir_mesmo_projeto_e_sessao_com_resultado_atomico()
    {
        InTemp(root =>
        {
            var dir = Path.Combine(root, "db");
            var first = CliSessionPersistence.Open(dir, root, projectName: "Arquivo", sessionTitle: "Dia 1");
            first.Begin("Como funciona?");
            first.Complete(new RunReceipt("run-1", "succeeded", 2, "USD", true, "teste independente",
                Fixed, CostEstimated: true), "A ferramenta leu o projeto.");
            var reopened = CliSessionPersistence.Open(dir, root, first.ProjectId, first.SessionId);
            reopened.Begin("E agora?");
            reopened.Complete(new RunReceipt("run-2", "blocked", 1, "USD", false, "sem orçamento",
                Fixed), null);
            var session = Assert.Single(Assert.Single(reopened.Read().Projects).Sessions);
            Assert.Equal(new[] { "user", "assistant", "user" }, session.Turns.Select(x => x.Role));
            Assert.Equal(2, session.Runs.Count);
            Assert.True(session.Runs[0].CostEstimated);
            Assert.False(session.Runs[1].Verified);
            Assert.Throws<InvalidOperationException>(() => CliSessionPersistence.Open(dir,
                Path.Combine(root, "db"), first.ProjectId, first.SessionId));
            Assert.Throws<ArgumentException>(() => CliSessionPersistence.Open(dir, root, sessionId: first.SessionId));
        });
    }

    [Fact]
    public void Resposta_ou_receipt_invalidos_nao_persistem_metade_da_revisao()
    {
        InTemp(root =>
        {
            var db = new LocalProjectStore(Path.Combine(root, "db"));
            var p = db.CreateProject("P", root);
            var s = db.CreateSession(p.Id, "S");
            var r = new RunReceipt("run", "succeeded", 2, "USD", true, "verificado", Fixed);
            var before = db.Read().Revision;
            Assert.Throws<ArgumentException>(() => db.CompleteRun(p.Id, s.Id, r, new string('x', 20000)));
            Assert.Throws<ArgumentException>(() => db.CompleteRun(p.Id, s.Id,
                new RunReceipt("run", "failed", -1, "USD", false, null, Fixed), "resposta"));
            Assert.Equal(before, db.Read().Revision);
            db.CompleteRun(p.Id, s.Id, r, "concluído");
            Assert.Throws<InvalidOperationException>(() => db.CompleteRun(p.Id, s.Id, r, "duplicado"));
            var result = Assert.Single(Assert.Single(db.Read().Projects).Sessions);
            Assert.Single(result.Runs);
            Assert.Single(result.Turns);
            Assert.Equal(before + 1, db.Read().Revision);
        });
    }

    [Fact]
    public void Comandos_de_consulta_exibem_historico_sem_endpoint_ou_modelo()
    {
        InTemp(root =>
        {
            var dir = Path.Combine(root, "db");
            Assert.Throws<FileNotFoundException>(() => CliHistoryCommands.List(dir));
            var session = CliSessionPersistence.Open(dir, root, projectName: "Livro");
            session.Begin("primeiro capítulo");
            session.Complete(new RunReceipt("run-1", "succeeded", 4, "USD", true,
                "conferido", Fixed), "texto do capítulo");
            var listing = CliHistoryCommands.List(dir);
            Assert.Contains(listing, line => line.Contains(session.ProjectId, StringComparison.Ordinal));
            Assert.Contains(listing, line => line.Contains(session.SessionId, StringComparison.Ordinal));
            var details = CliHistoryCommands.Show(dir, session.ProjectId, session.SessionId);
            Assert.Contains(details, line => line.Contains("texto do capítulo", StringComparison.Ordinal));
            Assert.Contains(details, line => line.Contains("custo=4", StringComparison.Ordinal));
            Assert.Throws<KeyNotFoundException>(() => CliHistoryCommands.Show(dir, "invalido", session.SessionId));
        });
    }

    [Fact]
    public void Arquivo_de_dados_nunca_contem_credencial_de_provider_ausente_da_entrada()
    {
        InTemp(root =>
        {
            var store = new LocalProjectStore(Path.Combine(root, "db"));
            var p = store.CreateProject("Projeto", root);
            var session = store.CreateSession(p.Id, "Conversa");
            store.AppendTurn(p.Id, session.Id, "user", "resumo público");
            var json = File.ReadAllText(Path.Combine(root, "db", "catalog.json"));
            Assert.DoesNotContain("ECOAI_API_KEY", json);
            Assert.DoesNotContain("sk-", json);
            using var parsed = JsonDocument.Parse(json);
            Assert.Equal(1, parsed.RootElement.GetProperty("body").GetProperty("schemaVersion").GetInt32());
        });
    }
}
