using Hub.Core;

namespace Hub.Tests;

public class HubStartupDiagnosticTests
{
    [Fact]
    public void Falha_nunca_expoe_mensagem_token_ou_caminho_privado()
    {
        var exception = new IOException("token=secret-1234 /data/user/0/private-vault");
        var diagnostic = HubStartupDiagnostic.From(exception);
        Assert.Equal("Não foi possível ler os dados locais.", diagnostic.Explanation);
        Assert.Matches("^[0-9A-F]{12}$", diagnostic.Id);
        Assert.DoesNotContain("secret-1234", diagnostic.Explanation + diagnostic.Id);
        Assert.DoesNotContain("/data/user/0", diagnostic.Explanation + diagnostic.Id);
        Assert.Equal(diagnostic, HubStartupDiagnostic.From(exception));
    }

    [Fact]
    public void Causa_interna_mapeia_categoria_sem_expor_excecao()
    {
        var exception = new InvalidOperationException("detalhes privados",
            new InvalidDataException("conteúdo privado da configuração"));
        var diagnostic = HubStartupDiagnostic.From(exception);
        Assert.Equal("O estado local não pôde ser interpretado.", diagnostic.Explanation);
        Assert.DoesNotContain("privado", diagnostic.Explanation + diagnostic.Id);
    }

    [Fact]
    public void Causas_desconhecidas_nao_fingem_que_cache_foi_recuperado()
    {
        var diagnostic = HubStartupDiagnostic.From(new InvalidOperationException("falha aleatória"));
        Assert.Equal("O Hub não conseguiu concluir a inicialização.", diagnostic.Explanation);
        Assert.Throws<ArgumentNullException>(() => HubStartupDiagnostic.From(null!));
    }
}
