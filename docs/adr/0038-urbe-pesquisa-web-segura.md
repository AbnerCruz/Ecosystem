# ADR-0038 — Pesquisa pública controlada no Assistente do Urbe

## Status

Proposto — revisão crítica em 24/10/2026 após novo direcionamento do proprietário: pesquisa pública deve funcionar como ferramenta padrão, sem chave, seleção de serviço ou grants individuais. A mudança permanece sujeita à revisão de risco, integração governada, CI e validação Android.

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

*Revisão proposta em 24/10/2026, aguardando decisão DEC-0057 e gates críticos.*

**Direção nova do proprietário substitui a primeira decisão de UX**: acesso a pesquisa pública sem cadastro de API, sem seleção obrigatória de provedor e sem permissões por agente. `AiWebConfig.Enabled` passa a `true` em instalações novas, há um único opt-out global de privacidade; um opt-out anteriormente persistido continua respeitado. Consultas públicas enviam apenas termos de busca e visitas enviam a URL solicitada. Nunca inferir autorização para transmitir nota, anexos, segredos ou arquivos privados. `AgentAccess` antigo é aceito ao ler configs mas não controla a pesquisa pública; leitura local e rede continuam capacidades de ferramenta distintas.

Modo padrão: leitor de **RSS público do Bing**, sem chave nem cobrança de API de busca identificada, não uma API oficial contratada. Resultados limitados (aprox. até 10), sem promessa de paginação/filtros/atualização; pode bloquear, mudar ou parar. Falhas retornam erro verificável, nunca referências inventadas. A interface oferece Brave Search API somente como alternativa avançada/opcional já suportada, preservando credenciais anteriores. `web_fetch` continua independente do mecanismo, sobre o gateway seguro já existente.

Mudança crítica de política de rede/credenciais: exige aprovação arquitetural do proprietário para integração e release, não é uma mudança meramente visual. Em atualização de configurações antigas, `enabled=false` é mantido por segurança; usuários já desativados podem reabilitar via preferência única.

### Decisão anterior (substituída quanto à UX)

A primeira fatia usava **Brave Search** com contrato `IAssistantSearchProvider` e configuração privada `config.json`; `Enabled=false` e grants `AgentAccess` eram falsos por padrão, inclusive para agentes prontos e personalizados. O Pesquisador delegado só recebia rede quando ele próprio tinha grant. Esses controles foram revistos pela ordem de simplificação de 24/10; os controles técnicos de SSRF, fontes e segurança continuam.

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

Bing RSS não possui contrato de API de busca geral para o Urbe; ausência de cobrança de chave não significa disponibilidade garantida, uso irrestrito ou ausência de limites. Consultas têm tráfego com terceiros; desativar a pesquisa é uma opção global. Brave requer credencial/cota conforme contrato comercial. Não há validação com chave real no CI público; usar handler HTTP/DNS injetáveis. O extrator HTML é conservador; cobertura de sites heterogêneos não é equivalente à de um navegador completo. O limite de requisições protege custo e abuso sem impor número de etapas do agente. Persistência de chaves permanece no armazenamento privado existente, que deve receber revisão dedicada de threat model em versões futuras.

Mudanças na área de permissões de rede exigem classificação adequada pelo integrador. Esta ADR proposta não aprova automaticamente instalação/release nem substitui a validação Android do NN-017.

## Alternativas rejeitadas

Scraping de buscadores, navegação JavaScript completa e duplicação do Agent Runtime não satisfazem o boundary de segurança e os requisitos de preservação do Urbe. Nenhuma ferramenta terá acesso autenticado ao site sem novo escopo.

## Referências

MANIFEST NN-001, NN-005, NN-008, NN-010, NN-011, NN-017, NN-020, NN-022; ADR-0015, ADR-0032, ADR-0033, ADR-0037; apps/urbe/docs/csharp/ROADMAP.md; apps/urbe/docs/csharp/RESEARCH-WEB-AUDIT-2026-10-24.md; https://api-dashboard.search.brave.com/app/documentation/web-search; https://learn.microsoft.com/en-us/lifecycle/announcements/bing-search-api-retirement.
