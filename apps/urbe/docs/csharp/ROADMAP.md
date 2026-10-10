# ROADMAP — Urbe em C# (UC-n)

> **Autoridade:** abaixo do Ecosystem ADR-0016 e do Urbe ADR-0010; escopo e IDs dos itens. IDs `UC-n` são estáveis (NN-019): nunca reutilizar. Estado vivo das tarefas em andamento = Issues (`state:<estado>`); evidência = handoffs em `docs/governance/handoffs/` (raiz do repositório).
> **Estados:** `[ ]` aberto · `[~]` verificado automaticamente, aguardando validação humana/revisão · `[x]` concluído com evidência. Um item nunca é `[x]` enquanto depender de validação humana (NN-017).
> **Estado de um gate:** linha `*Estado do gate:* **aprovado**`, `**aguardando**` ou `**não iniciado**` logo depois de cada `**Gate:**`.
> **Nenhuma fase escolhe a pilha de UI ou de hosts antes de UC-5**, e nenhum código C# de produto entra antes de UC-7 e do gate G-C0.

## Identidade obrigatória do produto — jogo/simulador de mundo aberto

**Ordem do proprietário, 10/10/2026:** Urbe não é uma grade de arquivos, um catálogo de cartões nem um mapa de fundo do Editor. É um **jogo/simulador 2D top-down de mundo aberto, contínuo, procedural e vivo**. O Markdown é a representação editável de parte do mundo: pasta = bairro/região; nota = casa/construção; `[[wikilinks]]` = ligações/estradas. O mundo existe e pode ser explorado **mesmo sem notas**. A interface do editor não substitui a experiência do jogo.

**Fonte de verdade de comportamento:** `src/world/terrain.js` (geografia/biomas/rio/vegetação e mundo sem bordas), `src/world/pixel-art.js` (pixel art procedural), `src/world/life.js` (ciclo dia/noite, clima, fauna, eventos e efeitos), `src/app.js` (câmera, movimento, moradores, construções, regiões, estradas, gestos e camadas) na versão 1.8.4-beta. Requisitos já documentados: REQ-026, REQ-097 a REQ-099 e REQ-101 a REQ-103. Novos comportamentos 2.0 permanecem itens próprios; preservar os originais antes de ampliar.

**Invariante de arquitetura:** separar **estado e simulação temporal** (mundo, entidades, relógio, decisões, interações e persistência apropriada), **renderização** (camadas visuais procedurais e animações) e **input/câmera** (pan livre por gesto, pinch zoom, toque/seleção, criação, navegação). O motor de simulação deve continuar independentemente de redesenhar frames, com orçamento móvel e testes determinísticos. O renderizador não é a fonte dos dados; o vault não vira armazém de pixels do terreno. Implementação C# Android sem voltar a executar JS como motor do jogo.

**Não aceitar como produto pronto** uma grade 8×8, uma grade dinamicamente expandida, botões de deslocar câmera, chunks PNG em data URI ou testes de igualdade RGBA **isoladamente**. São componentes transitórios úteis, mas não comprovam mundo aberto jogável, vida ou simulação. Enquanto não houver gameplay de mundo, indicar explicitamente *protótipo técnico de terreno/editor*, não *beta de simulador*. Preservar código válido de terreno/vault, sem começar do zero nem redesenhar a arte.

**Aceite mínimo da UC-19, antes de chamar a Cidade de jogável:** exploração contínua por gesto/pinch sem bordas artificiais; geografia e arte procedural originais com camadas/culling corretos; árvores, água, construções e estradas integradas à cena; moradores e fauna existentes, animados e atualizados pelo tempo; ciclo de dia/noite, clima e eventos conforme o legado; bairros, casas, ligações, criação e seleção funcionando no mundo, com Markdown e persistência real; foco Android horizontal e FPS/toques testados no aparelho. Testes de seed/geografia, continuidade de chunks, ticks e trajetórias determinísticas, interação de mundo e comparação visual são indispensáveis. G-C3/G-C4/DEVICE continuam aguardando validação humana.

## Contrato de continuidade — comando "continue o Urbe" (proprietário, 10/10/2026)

Este bloco é a **ordem operacional única** para qualquer agente que receba "continue o Urbe" ou equivalente sem tarefa mais específica. Não cria novo roadmap nem substitui MANIFEST/ADRs/SPEC; **escolhe a próxima tarefa do ROADMAP UC-n** conforme decisões já aprovadas. O comando do proprietário deve produzir implementação e testes, não apenas relatórios, documentação, planejamento ou uma nova proposta estética.

**Inicialização obrigatória em toda sessão (sem voltar a perguntar decisões tomadas):**

1. Ler o `AGENTS.md` raiz, `apps/urbe/AGENTS.md`, este ROADMAP, `UC19-VISUAL-ACCEPTANCE.md`, `UC19-PROCEDURAL-CITY.md`, DEC-0042 e as regras de integração. Verificar `main`, Issues, PRs abertos/fechados recentemente, CI e handoffs **ao vivo** antes de escolher trabalho; relatos antigos em PR não prevalecem sobre o estado atual do Git.
2. **Continuar o item já em curso**, corrigindo CI, dependências ou conflitos reais antes de abrir PR concorrente para o mesmo arquivo/objetivo. Se uma branch divergiu ou ficou obsoleta, preservar as implementações comprovadamente úteis e os testes; fechar/substituir apenas o PR incompatível com referência explícita à sucessão. Nunca misturar o SVG experimental (#385), o tabuleiro provisório e o renderer definitivo como três direções de produto.
3. Selecionar o menor incremento executável que desbloqueia a experiência **Android C# utilizável**. Ordem de desempate:
   - **Integridade:** abertura, edição, persistência física SAF, autosave, reabertura e segurança de `.md`/`.urbe`; corrigir regressão real de dados antes da UI. A base SAF do PR #397 foi integrada, mas teste físico continua obrigatório.
   - **UC-19 — JOGO PRIMEIRO:** integrar o motor **temporal de simulação** com a câmera e a renderização do **mundo top-down contínuo original 1.8.4-beta**. Portar comportamento de `src/world/life.js` e moradores/fauna de `src/app.js` em C# além de `terrain.js` + `pixel-art.js`: árvores, construções, estradas, bairros, ciclo dia/noite, clima, eventos, NPCs, animais e animações. Exploração livre por toque/arrasto/pinch, não por botões nem grade de lotes. PRs #421, #425 e o compositor/viewport do PR #428 são **infraestrutura**, não um simulador completo nem aceite UC-19.
   - **UC-18:** concluir Editor Visual/Fonte e Explorer sobre vault real, preservando bytes/round-trip e seleção/caret; corrigir PRs empilhados/confusos (notadamente #287/#333/#337) antes de duplicar UI.
   - **UC-19/UC-18:** restaurar composição visual completa da 1.8.4-beta (HUD, menus, ícones, cores, tipografia, layout e gestos), apenas substituindo handlers pelo C#; sem alternativas gráficas.
   - **UC-25/UC-27 e G-C3/G-C4:** distribuir APK Android *opt-in* com link direto e testar no aparelho físico, screenshots comparáveis, toque, FPS, armazenamento e reabertura. Só depois avançar UC-20/21/22, UC-28 e corte UC-31 pelo roteiro e gates.
4. **Sem bifurcar decisões:** Android primeiro; Windows depois; nenhum novo Web/PWA público. O Web de teste existente pode continuar como *harness*, não como Product a distribuir. Vault novo com cópia manual de Markdown é permitido por DEC-0042, sem migração histórica obrigatória; dados **atuais** continuam protegidos.
5. **Arte = algoritmos em C# executados no dispositivo.** PNG `data:` produzido em memória pode transportar pixels RGBA ao WebView; **PNG exportado/empacotado não é a fonte da arte**. JavaScript legado só serve como oráculo de testes; não recriar o runtime JS no APK. Para mudança visual, provar equivalência com imagens/casos congelados da 1.8.4-beta; diferença não verificada permanece aberta, nunca "resolvida" por aproximação.
6. Uma entrega = código + teste relevante + checagens aplicáveis + PR pequeno + evidência/handoff + próxima ação exata. `npm run check`, testes .NET/Android, E2E e `consistency` quando aplicáveis; respeitar integrador automático e autorização específica de mudanças críticas. **CI verde não aprova automaticamente qualidade visual nem DEVICE** (NN-017). Sem publish/corte silencioso.
7. Na resposta ao proprietário, informar objetivamente: **implementado / integrado / testado / bloqueado / próximo incremento**. Se ferramenta, autorização ou teste físico faltar, dizer exatamente o quê; continuar em outras tarefas seguras sem transformar bloqueio localizado em paralisação geral.

**Regra contra regressão de processo:** não iniciar nova reescrita, framework, redesign, nova versão web, sistema de assets estáticos ou feature lateral para contornar um teste quebrado, um conflito de branch ou a paridade da Cidade. Os requisitos do Urbe original continuam fonte de produto, independentemente de quantos protótipos C# já existirem.

## Direção de plataforma — proprietário, 09/10/2026

**Urbe C# será entregue para Android e desktop/Windows, sem versão Web/PWA.** Prioridade atual: desenvolvimento e releases Android; Windows vem depois. UC-23 permanece no documento apenas como ID histórico, sem nova entrega. O preview Web antigo não é alvo de release do produto C#. A retirada formal do host Web das ADRs consolidadas exige reconciliação crítica; esta direção já foi expressamente aprovada pelo proprietário.

## Próxima tarefa

**Ordem do proprietário DEC-0042 (2026-10-09) — beta utilizável prioritário, sem retrocompatibilidade histórica obrigatória.** Paridade **de funcionalidades** com o cliente JS é mantida como objetivo do produto C# final. O proprietário é o único usuário da beta, fará backup manual e copiará poucos arquivos `.md` a um vault novo; **migração de estado/configurações de versões antigas, IndexedDB, lixeira/histórico/layout e atualização in-place JS→C# NÃO são bloqueadores** do beta nem do corte. Isto modifica a obrigação anterior de DEC-0036-C/ADR-0026 para o cliente C#; ver ADD-0019/DEC-0042. Não apagar código pronto de compatibilidade; apenas evitar nova infraestrutura legada sem benefício. Manter leitura/gravação segura dos arquivos reais e persistência do estado **novo**. O cliente JS publicado permanece intacto até validação do beta opt-in e posterior corte aprovado.

**Caminho crítico imediato:** a fundação de vault real SAF e APK beta opt-in já entrou na `main` pelo PR #397, e o raster procedural inicial já entrou pelos PRs #421/#425. **Verificar o estado vivo dessas entregas, sem refazê-las.** Em seguida: corrigir pendências de CI/PR; fechar UC-18 e UC-19 com dados físicos e mundo *contínuo* visualmente idêntico ao legado; provar toque, armazenamento, desempenho e composição na máquina Android; prosseguir UC-20/21/22 e a paridade funcional. O beta próprio pode sair **antes** de G-C5, porém não será apresentado como beta de jogo/simulador sem os requisitos jogáveis mínimos da UC-19 e validação física.

**M3 — Interface e extensões.** UC-17 está integrada. UC-18 e UC-19 são as frentes ativas; escolher o próximo incremento conforme o contrato de continuidade acima, reconciliando PRs em andamento. Validação humana de UI/toque continua reservada ao G-C3.

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

- [x] UC-17 — Shell e navegação. PR #280 integrado automaticamente em `9143b239`: shell compartilhado Web/MAUI, navegação Início/Explorer/Editor/Cidade/Mais, base path `/` e `/preview/`, online/offline e regressão Web/Android/Windows/E2E/checks/consistency verdes. Issue #279 encerrada; validação humana de layout/toque continua G-C3.
- [ ] UC-18 — Editor (visual e fonte) e Explorer. Issue #281 em andamento; sessão compartilhada, Explorer real, edição Fonte, render Visual, relações e autocomplete C# de `[[` com criação de nota ausente (REQ-027/REQ-109) implementados em fatias. CI e validação humana de toque/layout ainda necessários para encerrar o item.
- [ ] UC-19 — **Jogo/simulador de mundo aberto: motor, mundo, desenho, vida e toque.** **ORDEM DO PROPRIETÁRIO (10/10/2026): preservar a experiência completa e o visual EXATO da 1.8.4-beta, sem redesenho.** Fazer o terreno contínuo existir, ser explorável e permanecer vivo: camadas/rios/árvores/biomas; gestos de câmera e zoom; construções/pastas, regiões, estradas por links, posicionamento e edição; **simulação de moradores, animais, clima, ciclo dia/noite e eventos** com tick de jogo independente do desenho. Preservar arte, paleta, sprites, HUD, ícones, tipografia, gestos e as mecânicas originais em C# Android/Windows. Testes de pixels/chunks isolados e um overlay de lotes (mesmo ilimitado) **não concluem UC-19**. A versão 1.8.4-beta e REQ-026/097/098/099/101/102/103 são oráculos; entregar jogo funcional, simulação em execução, prova de toque/FPS e validação humana, além de LEGACY-1 a LEGACY-4 em `docs/csharp/UC19-VISUAL-ACCEPTANCE.md`.
- [ ] UC-20 — Personalização, tema e **plugins** (decisão do modelo de confiança em C#).
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
- [ ] UC-29 — Validação humana por superfície (Android real, Windows instalação/atualização, toque, layout).
- [ ] UC-30 — Plano e ensaio de corte e de volta (rollback).
- [ ] UC-31 — **Corte:** o produto distribuído passa a ser o cliente C# (decisão crítica do proprietário; muda canais e atualizações).
- [ ] UC-32 — Encerramento do JS: arquivar o código antigo, `ecosystem.json` (`language`), documentação.

**Gate G-C5 (crítico, proprietário):** paridade **de funcionalidades** provada e validada por humano, com escrita de arquivos reais e estado C# atual íntegros; **não exige retrocompatibilidade/migração de estado histórico** (DEC-0042); autoriza o corte.
*Estado do gate:* **não iniciado**
