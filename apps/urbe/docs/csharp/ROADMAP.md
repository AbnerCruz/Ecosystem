# ROADMAP — Urbe em C# (UC-n)

> **Autoridade:** abaixo do Ecosystem ADR-0016 e do Urbe ADR-0010; escopo e IDs dos itens. IDs `UC-n` são estáveis (NN-019): nunca reutilizar. Estado vivo das tarefas em andamento = Issues (`state:<estado>`); evidência = handoffs em `docs/governance/handoffs/` (raiz do repositório).
> **Estados:** `[ ]` aberto · `[~]` verificado automaticamente, aguardando validação humana/revisão · `[x]` concluído com evidência. Um item nunca é `[x]` enquanto depender de validação humana (NN-017).
> **Estado de um gate:** linha `*Estado do gate:* **aprovado**`, `**aguardando**` ou `**não iniciado**` logo depois de cada `**Gate:**`.
> **Nenhuma fase escolhe a pilha de UI ou de hosts antes de UC-5**, e nenhum código C# de produto entra antes de UC-7 e do gate G-C0.

## Próxima tarefa

**M2 — Domínio do conhecimento e do mundo.** M1 foi concluído com leitura/escrita do vault, migração/backup/identidade/GC e export/import ZIP no Core C#. G-C1 aprovado. Próxima tarefa: UC-13 — projeção do mundo e bairros, com IDs estáveis.

---

## M0 — Fundamentos de paridade (sem pilha, sem código de produto)

Objetivo: saber exatamente o que é «paridade», ter como provar, e decidir com evidência a pilha e a transição.

- [x] UC-1 — **Matriz de paridade:** cada REQ `IMPLEMENTAR` do Urbe (101 hoje) e cada comportamento observável do 1.x/2.0 (tutorial, E2E, contratos) vira uma linha verificável e independente de linguagem, com a fonte (`caminho:linha` ou teste) e a superfície onde vale. Lacunas ficam explícitas. Saída: `docs/csharp/PARITY.md`. Concluído com CI e integração do PR #142; Issue #138 e handoff `HO-20261003-urbe-csharp-parity`.
- [x] UC-2 — **Suíte de aceite independente de linguagem:** corpus final M0 com 297 casos portáveis, oráculo congelado, protocolo de execução, 14/14 E2E funcionais representados e `surface-protocol.json` separado para lifecycle/permissões/instalação reais, deliberadamente `not-executed` até UC-23/24/25/29. PR #174 integrado automaticamente em `2e088e7`; `urbe-checks` e consistency verdes no estado combinado. Issue #139 encerrada; handoff `HO-20261004-urbe-uc2-concluido`.
- [x] UC-3 — **Contrato do vault:** `DATA-CATALOG.md` + Urbe ADR-0004 como contrato único legível pelos dois clientes (versões, proteção forward, backup restaurável, identidade, GC), com manifesto das fixtures. Projeção verificável em `VAULT-CONTRACT.md` e `acceptance/vault-manifest.json`; Issue #146, handoff `HO-20261003-urbe-m0-contracts`. Integrado pelo PR #150, commit 4f89efe.
- [x] UC-4 — **Inventário de dependências sem equivalente direto:** KaTeX, JSZip, PDF.js, providers de IA, plugins JS full-trust, File System Access/OPFS/IndexedDB, Electron, Capacitor. Para cada uma: uso real, opções em C#/WASM/nativo, risco. Inventário em `DEPENDENCIES.md`; Issue #147, mesmo handoff; nenhuma opção foi escolhida. Integrado pelo PR #150, commit 4f89efe.
- [x] UC-5 — **Pilha de UI e hosts decidida:** DEC-0035-A; ADR-0025 Aceito. Blazor WebAssembly PWA no Web + .NET MAUI Blazor Hybrid em Windows/Android, com Razor Class Library compartilhada. Issue #182.
- [x] UC-6 — **Transição decidida:** DEC-0036-C; ADR-0026 Aceito. Reinstalação deliberada com backup/export/import obrigatório; cliente JS preservado como recuperação até o corte autorizado em UC-31/G-C5. Issue #183.
- [x] UC-7 — **Política de integração para o código novo:** estender `docs/governance/integration-policy.json` às zonas críticas do código C# do Urbe (dados do usuário, segurança, distribuição) **antes** de qualquer PR de código C#. PR crítico (controle do sistema). Concluído: PR #151 autorizado e integrado, commit 584fe73; Issue #148 e handoff `HO-20261003-urbe-csharp-policy`.

**Gate G-C0:** UC-1 a UC-7 concluídos e a pilha (UC-5) e a transição (UC-6) decididas pelo proprietário.
*Estado do gate:* **aprovado**

## M1 — Núcleo de dados (biblioteca .NET, sem UI)

Objetivo: o C# lê e escreve o vault exatamente como o JS.

- [x] UC-8 — Esqueleto C# e CI integrado pelo PR #196 em 13fc285. Core/RCL/Web, MAUI Hybrid Windows/Android, testes e smoke publicado; CI combinado 37206895634 verde e autorização canônica do proprietário. Issue #192 encerrada; handoff `HO-20261004-urbe-uc8-closeout`.
- [x] UC-9 — Vault: leitura de todos os formatos 1.x/2.x com proteção forward, provada contra as fixtures. PR #203 integrado em `8aa72c2`; head final com `urbe-checks` 37214179413 e consistency 37214179366 verdes. Issue #199 encerrada `state:done`.
- [x] UC-10 — Vault: escrita, backup restaurável, migração idempotente, identidade e GC, provados contra as fixtures. PR #209 autorizado pelo proprietário e integrado em `590c9328`; 43/43 testes C# e estado combinado Web/Android/Windows/E2E/checks/consistency verdes. Issue #208 encerrada.
- [x] UC-11 — Exportar/importar ZIP e manifesto. PR #213 integrado manualmente pelo proprietário em `4b572d4f`; head final posteriormente confirmado com 63/63 testes C#, Web/Android/Windows/E2E/checks e consistency verdes. Issue #212 encerrada.

**Gate G-C1 (crítico: dados do usuário):** leitura e escrita idênticas às do JS em todas as fixtures; autorização do proprietário no PR.
*Estado do gate:* **aprovado**

## M2 — Domínio do conhecimento e do mundo

- [x] UC-12 — Documentos, artefatos e índice de conhecimento. PR #218 integrado automaticamente em `79c5be01`; 114/114 testes C#, Web/Android/Windows/E2E/checks e consistency verdes. Issue #216 encerrada.
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
