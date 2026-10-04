# ROADMAP — Urbe em C# (UC-n)

> **Autoridade:** abaixo do Ecosystem ADR-0016 e do Urbe ADR-0010; escopo e IDs dos itens. IDs `UC-n` são estáveis (NN-019): nunca reutilizar. Estado vivo das tarefas em andamento = Issues (`state:<estado>`); evidência = handoffs em `docs/governance/handoffs/` (raiz do repositório).
> **Estados:** `[ ]` aberto · `[~]` verificado automaticamente, aguardando validação humana/revisão · `[x]` concluído com evidência. Um item nunca é `[x]` enquanto depender de validação humana (NN-017).
> **Estado de um gate:** linha `*Estado do gate:* **aprovado**`, `**aguardando**` ou `**não iniciado**` logo depois de cada `**Gate:**`.
> **Nenhuma fase escolhe a pilha de UI ou de hosts antes de UC-5**, e nenhum código C# de produto entra antes de UC-7 e do gate G-C0.

## Próxima tarefa

**M0 — Fundamentos de paridade.** UC-1 está concluído (Issue #138, PR #142 integrado). UC-2 começou pelo corpus Markdown/Visual e oráculo congelado (Issue #139); há 12 cenários portáveis de vault, três de restauração, quatro de crash recovery, 13 de storage (nove FSA/native + quatro IDB/browser), um de migração IDB v1 `kv["cidade"]` e 26 de identidade/GC; há 24 casos nativos por superfície; há 19 casos de atualização Android/Windows e sete de UI; faltam outras transcrições UI e lifecycle/permissões reais. UC-3/UC-4 estão concluídos (PR #150 integrado, Issues #146/#147); UC-7 está concluído (PR #151); UC-5 e UC-6 dependem de UC-1 a UC-4.

---

## M0 — Fundamentos de paridade (sem pilha, sem código de produto)

Objetivo: saber exatamente o que é «paridade», ter como provar, e decidir com evidência a pilha e a transição.

- [x] UC-1 — **Matriz de paridade:** cada REQ `IMPLEMENTAR` do Urbe (101 hoje) e cada comportamento observável do 1.x/2.0 (tutorial, E2E, contratos) vira uma linha verificável e independente de linguagem, com a fonte (`caminho:linha` ou teste) e a superfície onde vale. Lacunas ficam explícitas. Saída: `docs/csharp/PARITY.md`. Concluído com CI e integração do PR #142; Issue #138 e handoff `HO-20261003-urbe-csharp-parity`.
- [ ] UC-2 — **Suíte de aceite independente de linguagem:** congelar como oráculo as fixtures de vault (`tests/fixtures/vaults`), os goldens (`markdown-golden.json`, `visual-golden.json`), os contratos (`persistence-adapter`, `native`, `editor-markdown`, `editor-visual`) e os cenários E2E; definir o formato dos casos (dados, não JS) e como o cliente C# os executa. Corpus atual: 259 casos, incluindo 12 cenários de vault, três de restauração, quatro de crash recovery, 13 de storage (nove FSA/native + quatro IDB/browser), um de migração IDB v1 `kv["cidade"]` e 26 de identidade/GC, mais 24 de contrato nativo, 19 de atualização e sete UI; `acceptance/README.md`, `oracle.json`, `cases.json` e runner; restante explícito no README, Issue #139. Não concluído.
- [x] UC-3 — **Contrato do vault:** `DATA-CATALOG.md` + Urbe ADR-0004 como contrato único legível pelos dois clientes (versões, proteção forward, backup restaurável, identidade, GC), com manifesto das fixtures. Projeção verificável em `VAULT-CONTRACT.md` e `acceptance/vault-manifest.json`; Issue #146, handoff `HO-20261003-urbe-m0-contracts`. Integrado pelo PR #150, commit 4f89efe.
- [x] UC-4 — **Inventário de dependências sem equivalente direto:** KaTeX, JSZip, PDF.js, providers de IA, plugins JS full-trust, File System Access/OPFS/IndexedDB, Electron, Capacitor. Para cada uma: uso real, opções em C#/WASM/nativo, risco. Inventário em `DEPENDENCIES.md`; Issue #147, mesmo handoff; nenhuma opção foi escolhida. Integrado pelo PR #150, commit 4f89efe.
- [ ] UC-5 — **Proposta de pilha de UI e de hosts** por superfície: ADR do Ecosystem `Proposto` + decisão pendente no portal, com alternativas reais e consequências. Depende de UC-1 a UC-4.
- [ ] UC-6 — **Estratégia de transição das instalações e dos canais** (APK, instalador Windows com atualização, Urbe Web/PWA; DEC-0021-C): ADR `Proposto` + decisão no portal. O Urbe JS continua publicando até o corte.
- [x] UC-7 — **Política de integração para o código novo:** estender `docs/governance/integration-policy.json` às zonas críticas do código C# do Urbe (dados do usuário, segurança, distribuição) **antes** de qualquer PR de código C#. PR crítico (controle do sistema). Concluído: PR #151 autorizado e integrado, commit 584fe73; Issue #148 e handoff `HO-20261003-urbe-csharp-policy`.

**Gate G-C0:** UC-1 a UC-7 concluídos e a pilha (UC-5) e a transição (UC-6) decididas pelo proprietário.
*Estado do gate:* **não iniciado**

## M1 — Núcleo de dados (biblioteca .NET, sem UI)

Objetivo: o C# lê e escreve o vault exatamente como o JS.

- [ ] UC-8 — Esqueleto do projeto C# do Urbe e CI (build e testes no estado combinado; `urbe-checks` estendido).
- [ ] UC-9 — Vault: leitura de todos os formatos 1.x/2.x com proteção forward, provada contra as fixtures.
- [ ] UC-10 — Vault: escrita, backup restaurável, migração idempotente, identidade e GC, provados contra as fixtures.
- [ ] UC-11 — Exportar/importar ZIP e manifesto.

**Gate G-C1 (crítico: dados do usuário):** leitura e escrita idênticas às do JS em todas as fixtures; autorização do proprietário no PR.
*Estado do gate:* **não iniciado**

## M2 — Domínio do conhecimento e do mundo

- [ ] UC-12 — Documentos, artefatos e índice de conhecimento.
- [ ] UC-13 — Projeção do mundo e bairros, com IDs estáveis.
- [ ] UC-14 — Markdown: renderização e volta do editor Visual, contra os goldens.
- [ ] UC-15 — Páginas e composições.
- [ ] UC-16 — Matemática (decisão da biblioteca em UC-4/UC-5).

**Gate G-C2:** suíte de aceite (UC-2) verde para todo o domínio, sem UI.
*Estado do gate:* **não iniciado**

## M3 — Interface e extensões

- [ ] UC-17 — Shell e navegação.
- [ ] UC-18 — Editor (visual e fonte) e Explorer.
- [ ] UC-19 — Mundo: desenho e toque.
- [ ] UC-20 — Personalização, tema e **plugins** (decisão do modelo de confiança em C#).
- [ ] UC-21 — IA: providers e agente.
- [ ] UC-22 — Tutorial e primeira abertura.

**Gate G-C3:** paridade de comportamento por superfície; **validação humana** de toque, layout e fluxos (NN-017).
*Estado do gate:* **não iniciado**

## M4 — Hosts e distribuição

- [ ] UC-23 — Web/PWA (offline, service worker ou equivalente).
- [ ] UC-24 — Windows (instalação e atualização).
- [ ] UC-25 — Android.
- [ ] UC-26 — Atualização das instalações existentes e compatibilidade de dados entre clientes.
- [ ] UC-27 — Pipeline de release do cliente novo, sem exigir nenhum componente do plano de controle do ecossistema (NN-023).

**Gate G-C4:** build instalável por superfície; **validação humana** de instalação e atualização.
*Estado do gate:* **não iniciado**

## M5 — Paridade total e corte

- [ ] UC-28 — Matriz de paridade (UC-1) 100% verde e suíte de aceite (UC-2) verde.
- [ ] UC-29 — Validação humana por superfície (Android real, Windows instalação/atualização, Web offline/PWA, toque, layout).
- [ ] UC-30 — Plano e ensaio de corte e de volta (rollback).
- [ ] UC-31 — **Corte:** o produto distribuído passa a ser o cliente C# (decisão crítica do proprietário; muda canais e atualizações).
- [ ] UC-32 — Encerramento do JS: arquivar o código antigo, `ecosystem.json` (`language`), documentação.

**Gate G-C5 (crítico, proprietário):** paridade total provada e validada por humano; autoriza o corte.
*Estado do gate:* **não iniciado**
