# ADR-0049 — Autonomia por escopo e releases automáticas

## Status

Aceito — decisão explícita do proprietário, DEC-0063 / ADD-0034 (15/11/2026).

## Contexto

A exigência da label `integrar` bloqueia PRs críticos mesmo com execução pronta e CI verde. O Urbe publica apenas quando seu csproj muda. Hub/Lunet/Ecosystem AI acrescentam estágio de desenvolvimento e número do workflow fora da autoridade de versão; outros Products não possuem publicação automática. O GITHUB_TOKEN não dispara on:push após integração, exigindo dispatch explícito. Permissão máxima no Urbe não equivale a permissão sobre o Ecosystem.

## Decisão

Manter integrador serial e testes do estado combinado. A política confiável libera mudanças de produto verdes automaticamente, conservando classificação de risco para observabilidade. Governança e alterações transversais de autoridade continuam reservadas ao proprietário, por review vinculada ao head. Decisões de direção não concedidas aguardam apenas no slice dependente.

O glue de Git/GitHub/JSON já reside em `.github/integrator` como exceção local a C#; `lifecycle.py` estende esse glue, sem criar runtime compartilhado. Configuração de distribuição em `docs/distribution/automatic-releases.json` aponta para versões canônicas existentes. Preparar incremento e intenção de release antes do commit combinado e antes dos checks. Registrar cada intenção por produto/versão, permitindo reconciliar falhas sem depender da memória do job. O dispatcher encontra o commit de introdução da intenção pelo histórico first-parent e passa `source_sha` para todos os jobs do workflow de publicação. Um retry não incrementa versão.

Preservar os canais dev já aprovados, transportando para VERSION os últimos números publicados de Lunet, Hub e Ecosystem AI. Alpha do Atlas e beta do Urbe são preservados; versões estáveis não recebem sufixo inventado. A escolha do estágio nunca vem do tipo de mudança ou opinião do agente.

Usar os pipelines existentes para APKs/desktop. Atlas distribui HTML offline construído e testado. Math Authoring/Tabletop RPG distribuem bibliotecas NuGet empacotadas e testadas enquanto não têm cliente distribuível; não alegar APK inexistente.

## Consequências

- NN-001/014: versão por produto e uma autoridade; política de distribuição não duplica o número.
- NN-008/009/011: concessão e mudança normativa registradas, head/main/combinado rastreáveis.
- NN-016: configuração da main, candidato sem segredos nos checks; nenhuma autoelevação por handoff.
- NN-017/018: verde integra/publica, mas não inventa validação no aparelho nem fecha requisito DEVICE.
- NN-020/022: cola local, sem novo componente compartilhado.
- Máxima é concessão de processo por componente, não isolamento criptográfico entre agentes usando a mesma conta GitHub. O proprietário pode restringir/revogar a concessão por nova decisão; o agente não pode fazê-lo sozinho.
- Alterações legítimas de governança que mudam checks antigos precisam de migração explícita; não usar a nova política do PR para se autoaprovar.

## Pesquisa e confronto

Auditoria: integrate.yml, integrate.sh, IntegrationQueue/ChangeReview em Check.cs, simulate.sh e workflows de release. Reaproveitamos testes de corridas/commit exato; removemos o acoplamento entre risco de produto e confirmação repetitiva.

GitHub documenta a supressão de eventos pelo GITHUB_TOKEN e a exceção workflow_dispatch: https://docs.github.com/en/actions/concepts/security/github_token. Código da documentação: https://github.com/github/docs/blob/main/content/actions/reference/workflows-and-actions/events-that-trigger-workflows.md.

Discussão da comunidade #27349 relata que auto-merge nativo pode não esperar checks não obrigatórios: https://github.com/orgs/community/discussions/27349. É relato, não prevalência medida. Adaptação: exigir explicitamente o resultado de cada CI de Product selecionado; não tratar job ausente/skipped como sucesso para componente tocado. Rejeitado substituir o integrador por uma simples habilitação de auto-merge sem preservar o estado combinado e os checks.

## Problema

Aprovação repetida por risco de arquivo impede tarefas já autorizadas; releases precisam corresponder a cada mudança real sem promover maturidade.

## Opções

1. Manter label para toda mudança crítica.
2. Liberar tudo, inclusive autoalteração de governança.
3. Automatizar por escopo com autoridade reservada ao proprietário (escolhida).

## Alternativas rejeitadas

Manter label perpetua o gargalo; liberar governança a agentes permite autoelevação; criar um segundo runtime de permissões amplia complexidade sem isolamento real de credenciais.
