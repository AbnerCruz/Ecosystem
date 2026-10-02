# ADR-0016 — Migração do Urbe para C#

## Status

Proposto — aguardando DEC-0024 (portal). Nenhuma implementação autorizada até a decisão (NN-005, NN-011).

## Contexto

**FATO (registrado conforme NN-009):** o proprietário pediu, em conversa com o agente `claude-code` em 2026-10-02, depois de a sessão avançar RM-F2-01/02 do Urbe: «Na verdade eu quero que você migre para c#». O pedido não diz o motivo, o alcance (tudo ou partes), a plataforma-alvo nem o que fazer com os canais de distribuição atuais.

**FATO (código, 2026-10-02):** o Urbe é JavaScript sem build (ADR-0001 do Urbe): ~13 mil linhas em `src/**/*.js` mais `src/app.js` (~5,3 mil linhas, em decomposição pela Urbe 2.0), 73 módulos em `src/modules.json`, 56 arquivos de teste em Node, E2E em Chromium, e três superfícies: Web/PWA (Urbe Web, GitHub Pages), Electron (Windows) e Android (Capacitor). Os dados do usuário são um vault de arquivos (`.urbe/*`, Markdown), com invariantes de compatibilidade e proteção forward (Urbe REQ-007, REQ-035, REQ-038; ADR-0004).

**FATO (normas):** o MANIFEST diz que C# é o padrão para **nova** infraestrutura (NN-005, §«C# padrão»), que isso **não** autoriza reescrita geral do Urbe, e que migração de partes só ocorre com benefício demonstrável, estratégia incremental, testes e preservação de comportamento e dados; qualquer reescrita estrutural exige ADR, roadmap específico, testes de compatibilidade e aprovação explícita. ADD-0002 §17 e ADR-0009 listam «reescrita do Urbe em C#» entre os itens proibidos sem ADR próprio. NN-013: migrar e refatorar são tarefas separadas. NN-014: o Urbe mantém versão e release próprios. O Hub e o Lunet2D já usam C#/.NET (DEC-0022-A para o Hub).

**INFERÊNCIA:** o pedido é uma mudança de direção do produto, não detalhe de implementação; portanto é decisão crítica do proprietário (ADD-0012; `communication.md` §9.1).

## Problema

Qual é o alcance e a estratégia da migração do Urbe para C#, de modo a respeitar os dados do usuário, os três canais de distribuição e a independência do Urbe (NN-002, NN-003, NN-023), sem quebrar o produto durante a transição?

## Opções

1. **A — Incremental pelo núcleo.** O que é lógica pura (modelos, projeção do mundo, índice de conhecimento, migração/backup de vault, math) passa a existir em C# (biblioteca .NET dentro de `apps/urbe`), servida à UI JavaScript existente (WebAssembly no Web/PWA; processo ou host no Electron/Android), com testes de paridade contra o comportamento atual e fixtures de vault. A UI e a casca continuam em JS enquanto a paridade não for provada. Sem big-bang; o Urbe Web nunca sai do ar.
2. **B — Reescrita completa em C#.** Novo cliente em C# (por exemplo Blazor WebAssembly para Web/PWA, host próprio para Windows e Android), com paridade total antes de trocar o produto. Um único código; maior custo e risco; exige reconstruir UI, editor, mundo, páginas, IA e plugins (hoje plugins são JS full-trust, Urbe ADR-0002).
3. **C — Por superfície.** Android (e eventualmente Windows) nativos em C#, como o Hub (DEC-0022-A), mantendo o Web/PWA em JS. Dois códigos para o mesmo produto até a convergência.
4. **D — Manter JavaScript** (posição atual do MANIFEST) e usar C# só para infraestrutura nova (Hub, serviços, ferramentas), sem migrar o Urbe agora.

## Decisão

**Pendente** (DEC-0024). **PROPOSTA do agente, não decisão:** A, em fatias pequenas e cada uma com ADR do produto, porque é a única que cumpre NN-005 sem interromper o Urbe Web nem arriscar dados; B só se o objetivo for abandonar o stack JS e o proprietário aceitar o custo e o tempo de paridade; C só se o motivo for Android/Windows nativos. A primeira fatia candidata (a decidir depois da escolha): migração/backup de vault em C#, por ser pura, testável com as fixtures existentes e **sem alterar o formato** — mas `src/persistence/**` é zona crítica (`integration-policy.json`, «dados do usuário»), então essa fatia exige a sua autorização no PR.

## Consequências

- Enquanto pendente: o Urbe continua sendo refatorado dentro do stack atual (RM-F2-*), sem tocar `src/persistence/**`; nada é escrito em C# para o Urbe.
- Se A/B/C for escolhida: criar roadmap específico (ROADMAP do Ecosystem e/ou do Urbe), ADR do produto por fatia, testes de paridade e de compatibilidade de dados, validação humana em aparelho/navegador por superfície (NN-017) e plano para atualização de instalações existentes (APK, instalador Windows, PWA). As decisões de distribuição de DEC-0021-C não mudam por este ADR.
- A direção da Urbe 2.0 (ADR-0001 do Urbe: sem build) e o escopo «fora da 2.0: reescrita geral» (SPEC §13) precisariam ser reavaliados pelo processo do próprio Urbe, citados como «Urbe ADR-0001» (referência qualificada).
- O formato dos dados do usuário permanece intocado por qualquer opção, salvo decisão específica depois.

## Alternativas rejeitadas

Ainda nenhuma: o proprietário não escolheu. Não foi considerada a opção de começar a reescrever antes da decisão, por contrariar NN-005 e NN-011.

## Referências

MANIFEST §0.1 NN-005, NN-011, NN-013, NN-014, NN-022 e a seção «C# padrão»; ADD-0002 §17; ADD-0012; ADR-0009; ADR-0013 (DEC-0022-A); DEC-0021-C; Urbe ADR-0001, ADR-0002, ADR-0004, REQ-007/035/038; `docs/governance/integration-policy.json`; DEC-0024.
