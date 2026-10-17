using EcosystemAi.Product;

namespace AgentRuntime.Tests;

public sealed class SessionDraftCacheTests
{
    [Fact]
    public void Projetos_e_conversas_nao_compartilham_rascunho()
    {
        var drafts = new SessionDraftCache();
        drafts.Save("project-a", "session-1", "segredo do projeto A", "palavra A");
        drafts.Save("project-a", "session-2", "outra conversa", null);
        drafts.Save("project-b", "session-1", "projeto B", "palavra B");

        Assert.Equal("segredo do projeto A", drafts.Get("project-a", "session-1").Goal);
        Assert.Equal("palavra A", drafts.Get("project-a", "session-1").RequiredText);
        Assert.Equal("outra conversa", drafts.Get("project-a", "session-2").Goal);
        Assert.Equal("", drafts.Get("project-a", "session-2").RequiredText);
        Assert.Equal("projeto B", drafts.Get("project-b", "session-1").Goal);
        Assert.Equal("palavra B", drafts.Get("project-b", "session-1").RequiredText);
        Assert.Equal(SessionDraft.Empty, drafts.Get("project-b", "session-2"));
    }

    [Fact]
    public void Sucesso_apaga_somente_a_conversa_executada()
    {
        var drafts = new SessionDraftCache();
        drafts.Save("p", "s1", "enviado", "exato");
        drafts.Save("p", "s2", "nao enviado", null);

        drafts.Clear("p", "s1");

        Assert.Equal(SessionDraft.Empty, drafts.Get("p", "s1"));
        Assert.Equal("nao enviado", drafts.Get("p", "s2").Goal);
        drafts.Save("p", "s2", null, null);
        Assert.Equal(SessionDraft.Empty, drafts.Get("p", "s2"));
    }

    [Fact]
    public void Entrada_rejeita_tamanhos_invalidos_sem_apagar_o_ultimo_rascunho()
    {
        var drafts = new SessionDraftCache();
        drafts.Save("p", "s", "preservado", "criterio");
        Assert.Throws<ArgumentException>(() => drafts.Save("p", "s",
            new string('x', ProductExecutionHost.MaxText + 1), ""));
        Assert.Throws<ArgumentException>(() => drafts.Save("p", "s", "",
            new string('x', SessionDraftCache.MaxRequiredText + 1)));
        Assert.Throws<ArgumentException>(() => drafts.Save("", "s", "x", null));
        Assert.Equal("preservado", drafts.Get("p", "s").Goal);
        Assert.Equal("criterio", drafts.Get("p", "s").RequiredText);
    }

    [Fact]
    public void Cache_e_limitado_sem_expor_o_rascunho_mais_recente()
    {
        var drafts = new SessionDraftCache();
        for (int i = 0; i < SessionDraftCache.MaxSessions; i++)
            drafts.Save("p", $"s{i}", $"texto {i}", "");
        drafts.Save("p", "s0", "mais recente", "");
        drafts.Save("p", "extra", "novo", "");
        Assert.Equal(SessionDraft.Empty, drafts.Get("p", "s1"));
        Assert.Equal("mais recente", drafts.Get("p", "s0").Goal);
        Assert.Equal("novo", drafts.Get("p", "extra").Goal);
    }

    [Fact]
    public void Um_novo_cache_nao_retem_rascunhos_do_processo_anterior()
    {
        var first = new SessionDraftCache();
        first.Save("p", "s", "somente na memoria", "");
        var reopened = new SessionDraftCache();
        Assert.Equal(SessionDraft.Empty, reopened.Get("p", "s"));
    }
}
