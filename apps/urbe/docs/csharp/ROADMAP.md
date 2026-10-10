# ROADMAP — Urbe em C# (UC-n)

> **Autoridade:** abaixo do Ecosystem ADR-0016 e do Urbe ADR-0010; escopo e IDs dos itens. IDs `UC-n` são estáveis (NN-019): nunca reutilizar. Estado vivo das tarefas em andamento = Issues (`state:<estado>`); evidência = handoffs em `docs/governance/handoffs/` (raiz do repositório).
> **Estados:** `[ ]` aberto · `[~]` verificado automaticamente, aguardando validação humana/revisão · `[x]` concluído com evidência. Um item nunca é `[x]` enquanto depender de validação humana (NN-017).
> **Estado de um gate:** linha `*Estado do gate:* **aprovado**`, `**aguardando**` ou `**não iniciado**` logo depois de cada `**Gate:**`.
> **Nenhuma fase escolhe a pilha de UI ou de hosts antes de UC-5**, e nenhum código C# de produto entra antes de UC-7 e do gate G-C0.

## Mandato universal de continuidade — TODOS os agentes (proprietário, 10/10/2026)

A diretriz de DEC-0043/ADD-0020/ADR-0032 e a ordem de desenvolvimento abaixo **vinculam todo agente do Urbe**, em qualquer plataforma/agente/sessão. Antes de qualquer implementação, leia `AGENTS.md` (raiz §1.2), `apps/urbe/AGENTS.md` §10 e `PRODUCT-DIRECTION.md`; verifique `main`, PRs e CI. A referência de arte/experiência/funcionalidades é exclusivamente **1.8.4-beta**, e o alvo é **Avalonia nativo + Urbe.Core**, Android primeiro e depois Windows. Não continuar nenhuma UI Blazor, Web/PWA, mapa estático, produto de notas genérico ou redesign.

**Próxima entrega independente e verificável:** prova G-N0 no aparelho e ligação do **vault físico ao novo cliente Avalonia**, reutilizando as operações SAF e `VaultReader`/`VaultWriter` existentes sem dependências com Blazor. Persistir e reler uma nota real é gate de beta. Em paralelo, completar `mapa.json`, a simulação de `life.js`, as ferramentas/overlays e o editor Visual conforme 1.8.4. Não marcar UC-18/19/25/33 concluídas nem publicar beta utilizável até provas reais correspondentes.

**PRs remanescentes de Blazor:** reconciliar escopo e extrair apenas Core/testes antes de qualquer merge; jamais integrar a casca obsoleta. Uma ordem simples para continuar significa **implementar o próximo incremento da lista, com testes/handoff**, e não reiniciar planejamento ou reabrir decisões de UI.

## O Urbe é a 1.8.4-beta, em C# nativo — proprietário, 10/10/2026 (DEC-0043)

**Esta seção prevalece sobre as seções abaixo onde houver conflito.** Fonte: DEC-0043, [ADD-0020](../../../../docs/governance/addenda/ADD-0020-urbe-e-a-1-8-4-nativo-sem-web.md), [ADR-0032](../../../../docs/adr/0032-urbe-ui-nativa-sem-webview.md), [`PRODUCT-DIRECTION.md`](PRODUCT-DIRECTION.md).

- **O Urbe é a 1.8.4-beta.** Ela é a especificação de experiência e visual: a cidade em tela cheia é o aplicativo; barra de ferramentas, busca, minimapa/mapa, HUD, painel da casa, explorador lateral e editor aparecem **por cima** dela. Toda tela é comparada lado a lado com a 1.8.4.
- **Interface nativa, sem WebView/HTML/CSS**, em Android e Windows (Avalonia, condicionada ao gate G-N0 do UC-33). `Urbe.Core` é preservado e continua sendo o único lugar do domínio.
- **Casca Blazor congelada:** `Urbe.UI`, `Urbe.App` e `Urbe.Web` não recebem funcionalidade nova; saem no UC-34. O shell de abas do UC-17 não é o Urbe.
- **Direção seguinte, depois da paridade:** o Urbe como criador e produtor de artefatos sobre o sistema de edição de texto, com o editor Visual no centro (UC-35).
- **Ordem:** UC-33 (G-N0) → UC-19 cidade da 1.8.4 em tela cheia → vault real no cliente nativo → UC-18 painel da casa, explorador e editor sobrepostos → UC-25 beta Android → UC-24 Windows → paridade restante → UC-34 → UC-35.

## Direção de plataforma — proprietário, 09/10/2026

**Urbe C# será entregue para Android e desktop/Windows, sem versão Web/PWA.** Prioridade atual: desenvolvimento e releases Android; Windows vem depois. UC-23 permanece no documento apenas como ID histórico, sem nova entrega. O preview Web antigo não é alvo de release do produto C#. A retirada formal do host Web das ADRs consolidadas exige reconciliação crítica; esta direção já foi expressamente aprovada pelo proprietário.

## Próxima tarefa

**Ordem do proprietário DEC-0042 (2026-10-09) — beta utilizável prioritário, sem retrocompatibilidade histórica obrigatória.** Paridade **de funcionalidades** com o cliente JS é mantida como objetivo do produto C# final. O proprietário é o único usuário da beta, fará backup manual e copiará poucos arquivos `.md` a um vault novo; **migração de estado/configurações de versões antigas, IndexedDB, lixeira/histórico/layout e atualização in-place JS→C# NÃO são bloqueadores** do beta nem do corte. Isto modifica a obrigação anterior de DEC-0036-C/ADR-0026 para o cliente C#; ver ADD-0019/DEC-0042. Não apagar código pronto de compatibilidade; apenas evitar nova infraestrutura legada sem benefício. Manter leitura/gravação segura dos arquivos reais e persistência do estado **novo**. O cliente JS publicado permanece intacto até validação do beta opt-in e posterior corte aprovado.

**Caminho crítico imediato:** (1) Issue #386 — ligar `WorkspaceSession` ao filesystem/vault **real** no Android, abrir/editar/salvar/reabrir sem depender de banco de dados de notas; (2) terminar UC-18 Editor/Explorer e UC-19 Cidade utilizáveis com posições atuais em `.urbe`; (3) disponibilizar **APK beta opt-in separado e link direto**, com testes de dados e validação física; (4) continuar UC-20/21/22 e a paridade funcional até o produto completo. O beta próprio pode sair **antes** de G-C5 sem virar automaticamente o produto distribuído; gates, CI, integração crítica e validação humana não são dispensados.

**M3 — Interface e extensões.** Próxima tarefa: **UC-33** — spike do cliente nativo (G-N0), conforme DEC-0043/ADR-0032. UC-18 e UC-19 passam a ser implementados no cliente nativo; validação humana de UI/toque continua reservada ao G-C3.

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
- [x] UC-13 — Projeção do mundo e bairros, com IDs estáveis. PR #223 integrado automaticamente em `ccab4b91`; 134/134 testes C#, Web/Android/Windows/E2E/checks e consistency verdes. Issue #221 encerrada.
- [x] UC-14 — Markdown: renderização e volta do editor Visual, contra os goldens. PR #232 integrado automaticamente em `1eafe4db`; estado combinado verde (Core/Web/Android/Windows/E2E/checks/consistency), Issue #230 encerrada.
- [x] UC-15 — Páginas e composições concluídas. Fatias integradas nos PRs #250 (`4a7dd74d`), #252 (`eeefea40`), #253, #254 (`b1ce8347`), #269 (`c7bf1ba4`) e #270 (`df668599`): modelo lossless/forward-safe, composições legadas, blocos semânticos, TOC/âncoras/livro, layout livre responsivo, conversão/compactação lossless, temas, shell HTML completo e 13 templates com variantes. O merge final `df668599` passou C# portátil, Web/checks, Android, Windows, E2E e consistency na main (runs 37517474704 / 37517474691). Nenhuma migração física de vault foi antecipada.
- [x] UC-16 — Matemática concluída. PR #258 (`4e6cfe8f`) portou edição/scanner/catálogo/snippets/autocomplete; PR #265 registrou o probe reproduzível; PR crítico #274 integrou, após autorização do proprietário, `CSharpMath`/`CSharpMath.Rendering 1.0.0-pre.2` + backend SVG próprio sem VectSharp/LGPL/JS; PRs #275/#276 adicionaram compatibilidade e diagnósticos estruturados; PR #277 (`680e6db6`) portou macros de string limitadas, sem estado global. A suíte de aceite de domínio cobre composição edição→compatibilidade→macros→renderer, corpus real 30/31, determinismo e geometria SVG segura. UI/paridade visual humana permanece UC-18/G-C3; avisos de terceiros e instaláveis permanecem UC-27/G-C4.

**Gate G-C2:** suíte de aceite (UC-2) verde para todo o domínio, sem UI.
*Estado do gate:* **aprovado** — domínio M2 fechado no Core C#; scanner/catálogo, páginas/composições e matemática possuem provas automatizadas. Validação humana/visual de UI não é inferida e fica no G-C3.

## M3 — Interface e extensões

- [x] UC-17 — Shell e navegação. PR #280 integrado automaticamente em `9143b239`: shell compartilhado Web/MAUI, navegação Início/Explorer/Editor/Cidade/Mais, base path `/` e `/preview/`, online/offline e regressão Web/Android/Windows/E2E/checks/consistency verdes. Issue #279 encerrada; validação humana de layout/toque continua G-C3. **Substituído por DEC-0043/ADR-0032:** o shell de abas Blazor não é a experiência do Urbe; a casca nativa nasce no UC-33 e o shell Blazor sai no UC-34.
- [ ] UC-18 — Editor (visual e fonte) e Explorer. **Superfície: cliente nativo (ADR-0032), como na 1.8.4: painel da casa, explorador lateral e editor sobrepostos à cidade.** A lógica já em `Urbe.Core` é reaproveitada; a UI Blazor desta fatia não é continuada. Issue #281 em andamento; sessão compartilhada, Explorer real, edição Fonte, render Visual, relações e autocomplete C# de `[[` com criação de nota ausente (REQ-027/REQ-109) implementados em fatias. CI e validação humana de toque/layout ainda necessários para encerrar o item.
- [ ] UC-19 — Mundo: desenho e toque. **Superfície: cliente nativo (ADR-0032), cidade em tela cheia desenhada a partir do motor do `Urbe.Core`.** Portado do código da 1.8.4 e provado por oráculo (PR #461): terreno idêntico ao `terrain.js` (biomas, rios, elevação, hidrologia), chão perto/longe (`chunkPixels`/`chunkFarPixels`), bairros/lotes/casas/ruas da primeira abertura (`LegacyCity`, idêntico ao app original com os mesmos ids), desenho de bairros, ruas, pontes, árvores, casas e rótulos, enquadramento `city.fit` e painel da casa. Sprites dos moradores (`villager`) portados e provados byte a byte por oráculo (72 quadros). Falta: vida (`life.js`/`v25*`: rotas e passeio dos moradores e seu desenho, fauna, clima visual, luzes), mapa/minimapa, busca, edição de bairro e casa (mover/copiar/excluir), `.urbe/mapa.json` e pasta real. **ORDEM DO PROPRIETÁRIO (10/10/2026): copiar o visual EXATO da versão 1.8.4-beta, sem qualquer redesenho.** A direção própria de SVGs da beta.7 foi rejeitada. Reutilizar sprites, paleta, textura, terreno, edifícios, ruas, HUD, ícones, tipografia e interações originais; apenas a implementação migra para C# Android/Windows. Exportador de assets originais é a primeira etapa, não a paridade integral. Etapas LEGACY-1 a LEGACY-4 em `docs/csharp/UC19-VISUAL-ACCEPTANCE.md`; G-C3 continua aberto até comparação real aprovada.
- [ ] UC-20 — Personalização, tema e **plugins** (decisão do modelo de confiança em C#).
- [ ] UC-33 — **Cliente nativo sem WebView — spike e gate G-N0** (DEC-0043, ADR-0032; Issue #460). Projetos `Urbe.Client` (Avalonia), `Urbe.Desktop` e `Urbe.Android` sobre `Urbe.Core`: mundo da 1.8.4 desenhado nativamente em tela cheia com pan/pinch, editor de texto nativo com teclado virtual/IME, builds Android e Windows no CI. **Gate G-N0:** os três pontos provados, com validação humana em Android real (mundo e digitação).
- [ ] UC-34 — **Remover a casca Blazor** (`Urbe.UI`, `Urbe.App`, `Urbe.Web`, smoke Web) quando o cliente nativo cobrir o que ela faz, mantendo uma única UI. Depende de UC-25 aprovado no aparelho.
- [ ] UC-35 — **Produção de artefatos** (direção DEC-0043): páginas, livros, composições e documentos produzidos a partir das notas pelo editor Visual, dentro da experiência da cidade. Começa depois da paridade da 1.8.4; cada melhoria vira fatia própria com aprovação do proprietário.
- [ ] UC-21 — IA: providers e agente.
- [ ] UC-22 — Tutorial e primeira abertura.

**Gate G-C3:** paridade de comportamento por superfície; **validação humana** de toque, layout e fluxos (NN-017).
*Estado do gate:* **não iniciado**

## M4 — Hosts e distribuição

- [ ] UC-23 — Web/PWA: **não aplicável** após a direção do proprietário de 09/10/2026; ID preservado somente para rastreabilidade.
- [ ] UC-24 — Windows (instalação e atualização).
- [ ] UC-25 — Android.
- [ ] UC-26 — Transição deliberada dos clientes sem migração histórica obrigatória (DEC-0042): suporte a vault limpo e cópia manual de Markdown; preservar instalação e dados legados sem conversão/sobrescrita automática. Não bloquear beta/corte por estados históricos.
- [ ] UC-27 — Pipeline de release do cliente novo, sem exigir nenhum componente do plano de controle do ecossistema (NN-023).

**Gate G-C4:** build instalável por superfície; **validação humana** de instalação e atualização.
*Estado do gate:* **não iniciado**

## M5 — Paridade total e corte

- [ ] UC-28 — **Paridade funcional** da matriz UC-1 e aceites relevantes UC-2 (editor, Explorer, cidade, ferramentas, IA, páginas, matemática, personalização etc.); testes de migração histórica de estado 1.x/2.x deixam de ser gate para o novo cliente C# (DEC-0042). Arquivos .md e estado novo continuam protegidos.
- [ ] UC-29 — Validação humana por superfície (Android real, Windows instalação/atualização, Web offline/PWA, toque, layout).
- [ ] UC-30 — Plano e ensaio de corte e de volta (rollback).
- [ ] UC-31 — **Corte:** o produto distribuído passa a ser o cliente C# (decisão crítica do proprietário; muda canais e atualizações).
- [ ] UC-32 — Encerramento do JS: arquivar o código antigo, `ecosystem.json` (`language`), documentação.

**Gate G-C5 (crítico, proprietário):** paridade **de funcionalidades** provada e validada por humano, com escrita de arquivos reais e estado C# atual íntegros; **não exige retrocompatibilidade/migração de estado histórico** (DEC-0042); autoriza o corte.
*Estado do gate:* **não iniciado**
