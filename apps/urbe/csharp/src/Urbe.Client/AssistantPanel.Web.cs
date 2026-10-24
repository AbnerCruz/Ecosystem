using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>Native network controls. All secrets remain in the existing private AssistantStore.</summary>
public sealed partial class AssistantPanel
{
    private void ShowWebSettings()
    {
        var config = _cfg.Web;
        var body = OpenView("Pesquisa na internet", () => ShowSettings());
        body.Children.Add(Wrap("Fontes públicas para seus agentes", 19, UrbeTheme.Text, FontWeight.Bold));
        body.Children.Add(Hint("O Urbe mantém o vault local e funciona offline. A pesquisa externa só é usada após sua autorização nesta tela."));

        var enabled = new CheckBox { Content = "Permitir acesso à internet para pesquisas", IsChecked = config.Enabled,
            FontSize = 14 };
        enabled.IsCheckedChanged += (_, _) =>
        {
            config.Enabled = enabled.IsChecked == true;
            SaveConfig();
        };
        body.Children.Add(Card(new StackPanel { Spacing = 7, Children =
        {
            enabled,
            Hint("O conteúdo enviado ao serviço externo pode incluir os termos das pesquisas. Não envie dados pessoais ou do vault sem sua autorização."),
            Hint("A rede nunca é habilitada apenas por escolher o modo de leitura ou escrita.")
        } }));

        var provider = Label("Brave Search API", 15, UrbeTheme.Text, FontWeight.SemiBold);
        var key = new TextBox { Text = config.ApiKey, PasswordChar = '•', PlaceholderText = "Cole sua chave Brave Search",
            HorizontalAlignment = HorizontalAlignment.Stretch };
        Avalonia.Automation.AutomationProperties.SetName(key, "Chave da API de pesquisa web");
        key.LostFocus += (_, _) => { config.ApiKey = (key.Text ?? "").Trim(); SaveConfig(); };
        body.Children.Add(Section("Provedor de busca", provider,
            Hint("Brave: resultados estruturados, idioma, país, domínio e período. A leitura das páginas é independente do mecanismo de busca."),
            key,
            Hint("A chave fica apenas nos dados privados do Assistente neste aparelho, no mesmo config.json dos demais provedores."),
            Hint("Preço: consulte a tabela atual do Brave. O Urbe não assume valor zero quando não conhece o custo."),
            TextButton("Página oficial do Brave", () =>
            {
                _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri("https://brave.com/search/api/"));
            })));

        var grants = new StackPanel { Spacing = 6 };
        foreach (var agent in AgentNames.All(_cfg))
        {
            var toggle = new CheckBox
            {
                Content = AgentNames.CallName(agent, _cfg) + " pode usar pesquisa pública",
                IsChecked = config.AgentAccess.GetValueOrDefault(agent.Id),
                FontSize = 13
            };
            Avalonia.Automation.AutomationProperties.SetName(toggle, "Permitir internet: " + agent.Id);
            var id = agent.Id;
            toggle.IsCheckedChanged += (_, _) =>
            {
                config.AgentAccess[id] = toggle.IsChecked == true;
                SaveConfig();
            };
            grants.Children.Add(toggle);
        }
        body.Children.Add(Section("Permissões por agente",
            Hint("Permissões de rede separadas do acesso às notas. O Pesquisador delegado só navega se também estiver autorizado aqui."),
            grants));

        var limits = new StackPanel { Spacing = 8 };
        void AddNumber(string caption, int value, int min, int max, Action<int> save)
        {
            var input = new TextBox { Text = value.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 92,
                HorizontalAlignment = HorizontalAlignment.Left };
            input.LostFocus += (_, _) =>
            {
                var parsed = int.TryParse(input.Text, out var n) ? n : value;
                parsed = Math.Clamp(parsed, min, max);
                input.Text = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                save(parsed); SaveConfig();
            };
            limits.Children.Add(new StackPanel { Spacing = 3, Children = { Label(caption, 13, UrbeTheme.Text2), input } });
        }
        AddNumber("Pesquisas por execução", config.MaxSearchCalls, 1, 100, v => config.MaxSearchCalls = v);
        AddNumber("Leituras de páginas por execução", config.MaxFetchCalls, 1, 100, v => config.MaxFetchCalls = v);
        AddNumber("Tempo limite por requisição (s)", config.TimeoutSeconds, 3, 60, v => config.TimeoutSeconds = v);
        body.Children.Add(Section("Controle de custos e recursos", limits,
            Hint("Limites são proteções configuráveis para APIs e downloads, não limites de planejamento do agente. O modelo continuará mostrando tokens e custo quando o provedor informar."),
            Hint("Não há tentativa automática de ignorar quotas HTTP 429. Ações podem ser canceladas pelo botão Parar no chat.")));

        body.Children.Add(Section("Segurança e limitações",
            Hint("Acesso restrito a HTTP/HTTPS públicos. Endereços locais, redes privadas e redirecionamentos para esses destinos são bloqueados."),
            Hint("Páginas HTML e texto são lidos sem executar scripts. PDFs e páginas dependentes de JavaScript requerem suporte específico."),
            Hint("Textos externos são dados não confiáveis. Nunca têm autoridade para comandar o agente, solicitar segredos ou alterar permissões.")));
    }
}
