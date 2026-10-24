# Diagnóstico — pesquisa pública no Assistente nativo (24/10/2026)

## Autoridade e inventário

Fontes consultadas antes das mudanças: MANIFEST.md (inclusive NN), AGENTS.md e apps/urbe/AGENTS.md; ecosystem.json; docs/adr/0032-urbe-ui-nativa-sem-webview.md; docs/adr/0033-unificacao-incremental-da-ia.md e 0037-urbe-assistente-execucao-continuada.md; roadmap C# do Urbe; implementação integral de AssistantAgent.cs, AssistantTools.cs, AssistantProviders.cs, AssistantStore.cs, AssistantPanel.cs, AssistantPanel.Views.cs, AssistantQuestionTools.cs e AssistantPluginTools.cs, além dos testes oracle e headless pertinentes. PR #726: runtime adaptativo/segundo plano integrado a main antes desta branch; PR #732: UI de atividade/configurações pendente à data da criação da branch; PR #725: exibição de código HTML separada. Essas PRs não devem ser reimplementadas nem ter ownership apropriado por esta mudança.

### Capacidades existentes verificadas

- Um único loop de agente `AssistantAgent.RunAsync`, `AiTool` (schema JSON, access, label, preview, run), `IAssistantHost`, execução de ferramentas em sequência ou leituras paralelas, feedback por `AiAgentEvent` e aprovação de mutações. `delegate_research` roda `pesquisador` no mesmo motor.
- `AssistantTools` mantém ferramentas locais para buscar/ler/criar/editar notas e atualizar plano, `AssistantPageTools` gera páginas/livros, `AssistantImageTools` salva mídia; nenhuma ferramenta pública `web_search` ou `web_fetch` existente na main auditada.
- `AssistantProviders` contém transporte multimodelo e `AiProviderConfig`; é transporte de modelos, não buscador independente.
- `AssistantStore` mantém `config.json` e conversas na área privada do aplicativo. Credenciais dos modelos já são armazenadas ali; o vault não é armazenamento de segredo. O arquivo de configuração permanece fonte única para credencial e permissão de busca.
- `AssistantPanel` (Avalonia nativo) gera histórico de ferramentas, cards, aprovação/diff, plano, streaming, indicadores de custo e botão Parar. A PR #732 já implementa uma linha compacta de atividade com expansão detalhada, sem outro runtime.
- O Android nativo mantém o foco; funções do vault continuam offline. Não se deve reabrir a UI WebView/Blazor.

### Lacunas

- Sem transportador de buscas públicas, leitor seguro de URLs públicas, consentimento de rede independente da leitura do vault, prova da origem das afirmações ou referências persistidas em conversas.
- Faltavam políticas de SSRF/redirect/DNS/limites de bytes e cancelamento voltadas para sites não confiáveis.
- Custos de pesquisa não podem ser inferidos dos valores de tokens do modelo.

## Seleção de provedor

- **Brave Search API (primeira implementação):** endpoint oficial JSON `GET https://api.search.brave.com/res/v1/web/search`, header `X-Subscription-Token`, `q`, `search_lang`, `country`, `freshness`, `count` e `offset`; índice independente. Fonte: https://api-dashboard.search.brave.com/app/documentation/web-search
- **Tavily:** alternativa posterior para investigação orientada a modelos; não há credencial, contrato ou teste de integração no repositório neste incremento.
- **Bing Search clássico:** API descontinuada em 11/08/2025; não é uma base válida para nova integração. Fonte: https://learn.microsoft.com/en-us/lifecycle/announcements/bing-search-api-retirement
- Preço real em data de uso: **não verificado no app**. Configuração mostra valor desconhecido e link oficial; jamais zero presumido.

## Estratégia

`AiWebConfig` serializa na mesma configuração privada de `AssistantStore`, desativado por padrão, com grants por agente e limites ajustáveis. Ferramentas entram na lista do agente somente com grant global + individual. Não equiparar `read` a `network`; as regras existentes de read-only continuam e um segundo gate exclusivo da rede controla exposição de `web_search` e `web_fetch`. O delegado só herda pesquisa externa quando o próprio Pesquisador tem grant.

`AssistantWebSession` é proprietário apenas da sessão de rede: request-scoped, cancelável, sem cookies/proxy, com DNS validado, conexão TCP ancorada em IP público, redirecionamentos manuais, limites de bytes/tempo/chamadas e provenance `AiWebSource`. HTML é convertido em texto sem scripts. PDF/SPA JS são limitações explícitas, não resultados fabricados. URLs, datas e trechos do provedor são identificados e os snippets não substituem a leitura. Citações via Markdown padrão (URL real) acompanham notas e páginas; o Assistente reutiliza as ferramentas de escrita que já existem.

A validação local de credenciais reais não é presumida. Os testes determinísticos usam HTTP e DNS simulados, o CI compila o Android e confirma as suítes; validação física NN-017 permanece a cargo da instalação/dispositivo.
