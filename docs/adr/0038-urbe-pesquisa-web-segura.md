# ADR-0038 — Pesquisa pública controlada no Assistente do Urbe

## Status

Proposto — para revisão arquitetural. A missão de 24/10/2026 autoriza implementar pesquisa real no Urbe, mas não dispensa CI, governança ou gates de segurança/Android.

## Contexto

O Urbe possui Agent Runtime único, transporte de modelos e ferramentas de vault/artifact, mas não pesquisa web pública. A autorização de leitura local não pode implicitamente dar acesso à rede ou transmitir conteúdo do vault. O navegador de conteúdo interno do Urbe não é um sandbox de investigação web.

## Problema

Pesquisar, abrir fontes e produzir artefatos referenciados com custo e segurança controlados, sem reconstruir o runtime do agente nem duplicar credenciais, e com operações transparentes na UI nativa.

## Opções

1. **Ferramentas com gateway de rede request-scoped** (escolhida): `AssistantWebTools` no loop atual, `AssistantWebSession` para serviços externos, Brave implementado atrás de interface de provedor, controles no `AssistantStore`.
2. Scraping de resultados Google/Bing por HTML: rejeitada (fragilidade, termos, estrutura instável).
3. Usar modelo com web embutida como único backend: rejeitada (perde transparência, controle de fontes, fornecedor único).
4. Incluir um browser completo/WebView com execução JavaScript: rejeitada para a primeira fatia (superfície de ataque e custos desproporcionais).
5. Extrair o gateway para plataforma comum sem segundo consumidor comprovado: rejeitada por NN-022.

## Decisão

A primeira fatia usa **Brave Search** com contrato `IAssistantSearchProvider`; `web_fetch` é independente do mecanismo. `AiWebConfig` é persistido exclusivamente na configuração privada `config.json` do Assistente; `Enabled=false` e os grants `AgentAccess` são falsos por padrão, inclusive para agentes prontos e personalizados. O usuário pode ativá-los individualmente. O Pesquisador delegado não herda consentimento externo só porque o chamador pode pesquisar; recebe `network` apenas quando ambos os contextos permitem o acesso necessário.

Os resultados registram `S1`, `S2` etc. com URL original/final, domínio, data alegada pelo provedor e acesso. A extração identifica status de resultado de busca versus página realmente lida. O agente usa links Markdown reais nos produtos construídos com as ferramentas existentes. Proíbe-se inventar preços e fontes; `unknown` é diferente de `free`.

## Segurança

- Bloquear esquemas fora HTTP/HTTPS, URL com credenciais, IP reservado/privado, DNS local, hosts sem TLD, redirecionamentos para redes privadas. Validar todos os endereços DNS e piná-los no `SocketsHttpHandler.ConnectCallback`; conexões não usam proxy nem cookies.
- GET sem autenticação, no máximo cinco redirecionamentos; timeout e bytes máximos, no máximo chamadas configuradas; cancelamento do usuário propagado à sessão.
- Rejeitar na saída padrões evidentes de credenciais e trechos verbatim extensos do vault; nenhuma política textual é prova total contra exfiltração indireta. Revisar novamente se houver fluxo autenticado ou automação de páginas no futuro.
- Nenhum script ou instrução externa tem autoridade sobre prompts, tools, credenciais e vault. Paginações e extrações não convertem snippets em fatos confirmados.
- Conteúdo HTML simples e plain text; PDFs externos e JS-rendered páginas comunicam limitação. Expansão requer parser/biblioteca especializada e teste de novas superfícies de ataque.

## Consequências

A implementação adiciona tráfego HTTP e regras de permissão de rede específicas do Urbe. A configuração, gestão dos tokens e proveniência passam a ser verificáveis por contrato e testes. O custo e a cobertura de fontes dependem do provedor real. Novos formatos ou autenticações exigem reavaliação do risco.

## Riscos e custos

Brave requer credencial/cota conforme contrato comercial. Não há validação com chave real no CI público; usar handler HTTP/DNS injetáveis. O extrator HTML é conservador; cobertura de sites heterogêneos não é equivalente à de um navegador completo. O limite de requisições protege custo e abuso sem impor número de etapas do agente. Persistência de chaves permanece no armazenamento privado existente, que deve receber revisão dedicada de threat model em versões futuras.

Mudanças na área de permissões de rede exigem classificação adequada pelo integrador. Esta ADR proposta não aprova automaticamente instalação/release nem substitui a validação Android do NN-017.

## Alternativas rejeitadas

Scraping de buscadores, navegação JavaScript completa e duplicação do Agent Runtime não satisfazem o boundary de segurança e os requisitos de preservação do Urbe. Nenhuma ferramenta terá acesso autenticado ao site sem novo escopo.

## Referências

MANIFEST NN-001, NN-005, NN-008, NN-010, NN-011, NN-017, NN-020, NN-022; ADR-0015, ADR-0032, ADR-0033, ADR-0037; apps/urbe/docs/csharp/ROADMAP.md; apps/urbe/docs/csharp/RESEARCH-WEB-AUDIT-2026-10-24.md; https://api-dashboard.search.brave.com/app/documentation/web-search; https://learn.microsoft.com/en-us/lifecycle/announcements/bing-search-api-retirement.
