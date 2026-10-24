using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// Web research works out of the box. Only the opt-out and optional advanced provider are user-facing.
/// The source of truth remains AssistantStore; agent-specific grants from old configs are ignored.
/// </summary>
public sealed partial class AssistantPanel
{
    private void ShowWebSettings()
    {
        var config = _cfg.Web;
        var body = OpenView("Pesquisa na internet", () => ShowSettings());
        body.Children.Add(Wrap("Peça para pesquisar. O Urbe faz o resto.", 18,
            UrbeTheme.Text, FontWeight.SemiBold));
        body.Children.Add(Hint("Qualquer agente pode procurar informações públicas, abrir fontes e citar os links. " +
            "Não é preciso cadastrar chave, escolher provedor ou dar permissões por agente."));

        var enabled = new CheckBox
        {
            Content = "Permitir pesquisas públicas na internet",
            IsChecked = config.Enabled,
            FontSize = 14
        };
        Avalonia.Automation.AutomationProperties.SetName(enabled, "Permitir pesquisas públicas na internet");
        enabled.IsCheckedChanged += (_, _) =>
        {
            config.Enabled = enabled.IsChecked == true;
            SaveConfig();
        };
        body.Children.Add(enabled);
        body.Children.Add(Hint(config.Enabled
            ? "Pesquisa disponível para os agentes. O uso ocorre quando uma tarefa pede informações externas."
            : "Pesquisa desativada para todos os agentes. As notas locais continuam acessíveis."));

        body.Children.Add(Section("Privacidade e segurança",
            Hint("A busca envia as palavras pesquisadas ao buscador; abrir uma fonte envia a solicitação ao site visitado. " +
                "O Urbe não transmite automaticamente arquivos ou textos do vault."),
            Hint("Links e páginas são dados não confiáveis: endereços privados, redirecionamentos perigosos " +
                "e padrões de segredos são bloqueados. Instruções encontradas em páginas não autorizam ações."),
            Hint("A pesquisa padrão não cobra uma API de busca, mas é um serviço público sem garantia de disponibilidade. " +
                "O provedor do modelo de IA ainda pode cobrar tokens.")));

        body.Children.Add(TextButton("Opções avançadas (opcional)", ShowWebAdvancedSettings));
    }

    private void ShowWebAdvancedSettings()
    {
        var config = _cfg.Web;
        var body = OpenView("Pesquisa · opções avançadas", ShowWebSettings);
        body.Children.Add(Hint("O modo recomendado dispensa chave e usa resultados públicos RSS. " +
            "Ele pode ficar indisponível ou retornar poucos resultados. Quem precisa de um serviço de busca " +
            "contratado pode usar a API Brave opcionalmente."));

        var free = new CheckBox { Content = "Pesquisa automática sem chave (recomendado)",
            IsChecked = config.Provider != "brave", FontSize = 14 };
        free.IsCheckedChanged += (_, _) =>
        {
            config.Provider = free.IsChecked == true ? "bing-rss" : "brave";
            SaveConfig();
        };
        body.Children.Add(free);

        var key = new TextBox
        {
            Text = config.ApiKey, PasswordChar = '•',
            PlaceholderText = "Chave da API Brave, somente se optar por ela",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Avalonia.Automation.AutomationProperties.SetName(key, "Chave opcional da API Brave");
        key.LostFocus += (_, _) => { config.ApiKey = (key.Text ?? "").Trim(); SaveConfig(); };
        body.Children.Add(Section("Serviço opcional com chave",
            Hint("Desmarque a pesquisa gratuita acima para utilizar Brave Search. " +
                "Sem chave, o Brave não funciona. A cobrança segue as condições desse serviço."),
            key,
            TextButton("Documentação oficial do Brave", () =>
            {
                _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri("https://brave.com/search/api/"));
            })));

        body.Children.Add(Section("Proteções técnicas",
            Hint("O Urbe aplica tempo limite, cota operacional por tarefa, tamanho máximo por download " +
                "e cancelamento. Esses limites evitam travamentos ou excesso de requisições; " +
                "não limitam artificialmente o raciocínio dos agentes."),
            Hint("O feed RSS gratuito não garante paginação, data de publicação, cobertura ou filtros de período. " +
                "O agente deve verificar o conteúdo original das páginas e declarar incertezas.")));
    }
}
