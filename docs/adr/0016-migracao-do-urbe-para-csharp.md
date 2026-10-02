# ADR-0016 — Migração do Urbe para C#

## Status

Aceito — alternativa **B** escolhida pelo proprietário em DEC-0024 (portal, 2026-10-02; [registro](../governance/responses/DEC-0024.md), Issue #45). Consequências aplicadas por este ADR, pelo programa [`apps/urbe/docs/csharp/`](../../apps/urbe/docs/csharp/README.md) e pelo ADR do produto (Urbe ADR-0010). O texto abaixo preserva o contexto e as opções como estavam quando a decisão foi pedida.

## Contexto

**FATO (registrado conforme NN-009):** o proprietário pediu, em conversa com o agente `claude-code` em 2026-10-02, depois de a sessão avançar RM-F2-01/02 do Urbe: «Na verdade eu quero que você migre para c#». O pedido não diz o motivo, o alcance (tudo ou partes), a plataforma-alvo nem o que fazer com os canais de distribuição atuais.

**FATO (código, 2026-10-02):** o Urbe é JavaScript sem build (ADR-0001 do Urbe): ~13 mil linhas em `src/**/*.js` mais `src/app.js` (~5,3 mil linhas, em decomposição pela Urbe 2.0), 73 módulos em `src/modules.json`, 56 arquivos de teste em Node, E2E em Chromium, e três superfícies: Web/PWA (Urbe Web, GitHub Pages), Electron (Windows) e Android (Capacitor). Os dados do usuário são um vault de arquivos (`.urbe/*`, Markdown), com invariantes de compatibilidade e proteção forward (Urbe REQ-007, REQ-035, REQ-038; ADR-0004).

**FATO (normas):** o MANIFEST diz que C# é o padrão para **nova** infraestrutura (NN-005, §«C# padrão»), que isso **não** autoriza reescrita geral do Urbe, e que migração de partes só ocorre com benefício demonstrável, estratégia incremental, testes e preservação de comportamento e dados; qualquer reescrita estrutural exige ADR, roadmap específico, testes de compatibilidade e aprovação explícita. ADD-0002 §17 e ADR-0009 listam «reescrita do Urbe em C#» entre os itens proibidos sem ADR próprio. NN-013: migrar e refatorar são tarefas separadas. NN-014: o Urbe mantém versão e release próprios. O Hub e o Lunet2D já usam C#/.NET (DEC-0022-A para o Hub).

**INFERÊNCIA:** o pedido é uma mudança de direção do produto, não detalhe de implementação; portanto é decisão crítica do proprietário (ADD-0012; `communication.md` §9.1).

**FATO — superfícies atuais e o que cada uma implica (2026-10-02):**

| Superfície | Tecnologia hoje | Canal de distribuição (`docs/distribution/current.profile.json`) | Implicação de uma migração |
|---|---|---|---|
| Web/PWA | JavaScript no navegador, service worker, IndexedDB/OPFS | GitHub Pages («Urbe Web») | Precisa continuar funcionando offline e sem build (Urbe ADR-0001); C# no navegador exige WebAssembly |
| Windows | Electron (`native/desktop/`), instalador e atualização por releases | GitHub Releases | Trocar o host não pode quebrar a atualização das instalações existentes |
| Android | Capacitor (`native/android/`), APK | GitHub Releases | Idem; assinatura e identidade do app não mudam sem decisão própria |

**FATO — dados do usuário:** o vault é uma pasta de arquivos (Markdown e `.urbe/*`) lida e escrita pelas três superfícies; vale ler tudo que a 1.x escreveu, escrever com proteção forward e backup restaurável antes de migração de formato (Urbe ADR-0004, REQ-007/035/038). `src/persistence/**` e `DATA-CATALOG.md` são zona crítica na política de integração.

**RISCOS** (de qualquer migração; a intensidade varia por opção): (1) perder ou corromper dados do usuário; (2) regressão de comportamento sem paridade provada; (3) quebrar instalações e o Urbe Web existentes; (4) dois stacks mantidos em paralelo durante a transição; (5) custo e prazo altos frente ao ganho, se o motivo não for claro (NN-020); (6) plugins JavaScript full-trust (Urbe ADR-0002) sem equivalente em C#.

## Problema

Qual é o alcance e a estratégia da migração do Urbe para C#, de modo a respeitar os dados do usuário, os três canais de distribuição e a independência do Urbe (NN-002, NN-003, NN-023), sem quebrar o produto durante a transição?

## Opções

1. **A — Incremental pelo núcleo.** O que é lógica pura (modelos, projeção do mundo, índice de conhecimento, migração/backup de vault, math) passa a existir em C# (biblioteca .NET dentro de `apps/urbe`), servida à UI JavaScript existente (WebAssembly no Web/PWA; processo ou host no Electron/Android), com testes de paridade contra o comportamento atual e fixtures de vault. A UI e a casca continuam em JS enquanto a paridade não for provada. Sem big-bang; o Urbe Web nunca sai do ar.
2. **B — Reescrita completa em C#.** Novo cliente em C# (por exemplo Blazor WebAssembly para Web/PWA, host próprio para Windows e Android), com paridade total antes de trocar o produto. Um único código; maior custo e risco; exige reconstruir UI, editor, mundo, páginas, IA e plugins (hoje plugins são JS full-trust, Urbe ADR-0002).
3. **C — Por superfície.** Android (e eventualmente Windows) nativos em C#, como o Hub (DEC-0022-A), mantendo o Web/PWA em JS. Dois códigos para o mesmo produto até a convergência.
4. **D — Manter JavaScript** (posição atual do MANIFEST) e usar C# só para infraestrutura nova (Hub, serviços, ferramentas), sem migrar o Urbe agora.

## Decisão

**DECISÃO (proprietário, DEC-0024-B):** reescrita completa do Urbe em C# — um novo cliente, trocando o produto **só quando houver paridade total** com o atual. A pilha de exemplo da alternativa (Blazor WebAssembly para Web/PWA e hosts para Windows/Android) era ilustrativa: **a pilha de UI e de hosts NÃO foi decidida** e exige ADR e decisão própria antes de qualquer código de produto (UC-5).

O que a decisão **autoriza** (NN-005: «reescrita estrutural exige ADR, roadmap específico, testes de compatibilidade e aprovação explícita» — ADR e aprovação: este ADR e DEC-0024; roadmap: [`apps/urbe/docs/csharp/ROADMAP.md`](../../apps/urbe/docs/csharp/ROADMAP.md); testes de compatibilidade: UC-2/UC-3):

- planejar e executar o programa de migração em fases com gates, **sem big-bang**: o Urbe em JavaScript continua sendo o produto distribuído (Web/PWA, Electron, Capacitor; canais de DEC-0021-C) até o corte;
- construir o cliente em C# dentro do componente `urbe` (mesmo produto, mesma identidade — NN-019), com versão e release próprios (NN-014).

O que a decisão **não** autoriza, e continua exigindo decisão específica:

- a **pilha de UI/hosts** por superfície (UC-5), a **estratégia de transição** das instalações e dos canais (UC-6) e o **modelo de plugins** em C# (hoje JS full-trust, Urbe ADR-0002);
- qualquer mudança no **formato dos dados do usuário**: o vault continua o mesmo (Urbe ADR-0004; REQ-007/035/038) e a paridade é provada contra o código atual;
- o **corte** (trocar o produto distribuído): é decisão crítica do proprietário (UC-31, gate G-C5);
- código C# de Urbe em zona crítica sem estender antes a política de integração (UC-7): dados, segurança e distribuição do código novo precisam estar em [`integration-policy.json`](../governance/integration-policy.json).

## Consequências

- **Programa:** roadmap próprio do produto em `apps/urbe/docs/csharp/` (itens `UC-n`, fases M0–M5, gates G-C0–G-C5). Começa pela **M0 (fundamentos da paridade)**, que não escolhe pilha nem escreve código de produto: matriz de paridade, suíte de aceite independente de linguagem, contrato do vault, inventário de dependências, proposta de pilha e de transição, e a extensão da política de integração.
- **Urbe em JavaScript:** segue como produto e recebe correções. O programa de refatoração do Urbe 2.0 passa a disputar esforço com a migração; o que fazer com ele é a **DEC-0025** (pendente, não bloqueante). Enquanto estiver pendente, nenhum item estrutural novo do 2.0 é iniciado (padrão conservador); correções e o que alimenta a paridade (fixtures, goldens, contratos de dados) continuam.
- **Documentos corrigidos:** `ARCHITECTURE.md` §2 (o Urbe deixa de «permanecer em JavaScript, sem reescrita»), `docs/architecture/refactoring.md` §4 e `candidates.md` §6 (a reescrita do Urbe em C# deixa de ser proibida: é o programa deste ADR), ADR-0009 (complemento) e o parágrafo «Em paralelo» do `ROADMAP.md`. `ecosystem.json` **não muda**: `urbe.language` descreve o produto distribuído hoje (`javascript`) e só muda no corte (UC-32).
- **Invariantes:** o novo Urbe não depende do Hub (NN-003/NN-023); Urbe e Lunet2D continuam independentes (NN-002); validação humana por superfície antes do corte (NN-017); extração para o Ecosystem só por Extraction Review (NN-022).
- **Riscos assumidos pela escolha de B:** custo e prazo altos; dois stacks mantidos até o corte; plugins JS sem equivalente imediato; atualização das instalações existentes. Mitigação: gates, suíte de paridade e corte decidido pelo proprietário.

## Alternativas rejeitadas

- **A — Incremental pelo núcleo:** não escolhida pelo proprietário (a recomendação do agente era A; a decisão é do proprietário, não a do agente). Ideias dela que continuam valendo como **método dentro de B**: provar cada parte contra fixtures e golden antes de ligá-la (UC-9 a UC-16).
- **C — Por superfície (Android/Windows nativos, Web em JS):** não escolhida.
- **D — Manter JavaScript e usar C# só em infraestrutura nova:** não escolhida (era a posição anterior do MANIFEST/ARCHITECTURE; ver «Documentos corrigidos»).

## Referências

MANIFEST §0.1 NN-005, NN-011, NN-013, NN-014, NN-019, NN-022 e a seção «C# padrão»; ADD-0002 §17; ADD-0012; ADR-0009; ADR-0013 (DEC-0022-A); DEC-0021-C; Urbe ADR-0001, ADR-0002, ADR-0004, ADR-0010, REQ-007/035/038; `docs/governance/integration-policy.json`; DEC-0024-B; DEC-0025; [`apps/urbe/docs/csharp/`](../../apps/urbe/docs/csharp/README.md).
