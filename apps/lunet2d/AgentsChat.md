# Comunicação entre agentes

Registro assíncrono sobre o trabalho produzido no Lunet2D. Leia `AGENTS.md` antes de participar. Acrescente mensagens com IDs únicos e cite o ID respondido. O autor registra somente seu próprio trabalho; contexto recuperado do Git é identificado como tal.

## Formato

### ID — data e fuso — autor → destinatário — estado
- Responde a: ID anterior, quando houver.
- Item do ROADMAP / objetivo:
- Base: branch e commit examinados.
- Trabalho / arquivos:
- Verificação: comandos, resultados e links de CI/PR, separando executado de pendente.
- Bloqueios / perguntas:
- Próximo passo / responsável sugerido:

Estados sugeridos: assumido, em andamento, pronto para revisão, bloqueado, entregue.
Não guarde tokens, senhas ou dados privados. Não marque validação de aparelho sem evidência do usuário. Preserve entradas anteriores e corrija informações em uma nova mensagem.

## Mensagens

### 20260929-codex-01 — 2026-09-29 (America/Sao_Paulo) — Codex → próximos agentes — em andamento
- Item: continuidade entre agentes (§24/§30/§36) e Git progressivo da Fase 3 (§22).
- Contexto recuperado do Git, não mensagem do Claude: `main` em `898c628` incorporou Inspector/classificação/layouts pelo PR #19. A branch `ccr-8e262421-r4rdz0` contém trabalho posterior, `b934056`, no PR #20: Git em C# puro e painel Android. Não reiniciar essa implementação.
- Evidência: https://github.com/AbnerCruz/Lunet2D/pull/20 . CI anterior https://github.com/AbnerCruz/Lunet2D/actions/runs/36593986362 : testes aprovados, APK falhou com CS0104 (`Path` ambíguo) e CS0029 (`_` era parâmetro IProgress, não descarte).
- Trabalho: instruções migradas para `AGENTS.md`; este canal criado; rotina, README e teste de existência atualizados. Corrigidos os dois erros de build em `GitPanel.InitRepository`. Removidas marcações de concluído dos itens que explicitamente aguardam validação em aparelho; isso não remove implementação.
- Verificação local: `bash tools/roadmap-status.sh --pending` e `git diff --check`. Ambiente desta sessão sem .NET SDK; testes .NET e build Android serão verificados no CI atualizado. Não houve teste em aparelho nesta sessão.
- Limites herdados do Git: ADR 0005 documenta ausência de SSH/LFS/submódulos/rebase/shallow clone e mesclagem por arquivo. O token está em arquivo privado, sem Keystore; proteção de credencial exige revisão antes da versão estável. Não declarar esse subsistema auditado por completo apenas com o build corrigido.
- Próximo passo: aguardar CI do PR #20 corrigido, integrar somente com testes e APK verdes, confirmar release; validar Git e os recursos recentes da IDE no aparelho. Fase 3 continua aberta; não avançar à 4 sem gate e auditoria.

### 20260929-codex-02 — 2026-09-29 (America/Sao_Paulo) — Codex → próximos agentes — pronto para revisão
- Responde a: 20260929-codex-01.
- Correção publicada em `ed218311c2ab47d256a72a142e6268ad7839969c`, no mesmo PR #20.
- CI confirmado: 265 testes passaram (zero falhas); APK compilado, assinatura conferida e artifact enviado. Evidência: https://github.com/AbnerCruz/Lunet2D/actions/runs/36599346137 .
- Continuidade e migração de instruções concluídas. Este registro final altera somente documentação; conferir o CI do novo commit antes de integrar.
- Validação em aparelho continua pendente: criar projeto, abrir Ferramentas → Git, iniciar repositório, fazer commit, editar e inspecionar diff/histórico, trocar ramo e reabrir o projeto; depois testar push/pull em repositório de teste. Confirmar preservação do código ao executar/parar Preview.
- Próximo responsável: agente que retomar deve consultar PR #20 e Releases para confirmar o resultado da integração, concluir as pendências da Fase 3 e registrar a versão e o resultado relatado pelo usuário. Não há aprovação de aparelho nesta mensagem.

### 20260929-codex-03 — 2026-09-29 (America/Sao_Paulo) — Codex → próximos agentes — pronto para revisão
- Responde a: 20260929-codex-02.
- Item: entrega transversal de validação de fase (§24, §28, §36), solicitada pelo usuário.
- Base: `main` em `c80ea9d` (PR #20 integrado; release `v0.0.1-dev.84`).
- Trabalho: diretriz de fechamento com roteiro detalhado, resposta por funcionalidade e aprovação explícita em `AGENTS.md`, `docs/DEVELOPMENT.md`, `docs/SPEC.md`, `docs/audits/TEMPLATE.md` e `ROADMAP.md`. Fase 3 permanece em andamento até sua própria validação e auditoria.
- Verificação: revisão de consistência documental e `git diff --check`; CI e release desta alteração ainda pendentes neste registro.
- Próximo passo: ao terminar a Fase 3, gerar release candidata e entregar ao usuário instruções concretas para testar suas funcionalidades, registrar cada resultado na auditoria, corrigir falhas e aguardar aprovação antes de avançar à Fase 4.

### 20260929-codex-04 — 2026-09-29 (America/Sao_Paulo) — Codex → próximos agentes — em andamento
- Responde a: 20260929-codex-03.
- Item: Fase 3, autosave com journal e recuperação (§22).
- Base: `main` em `ea875c3`, após PR #21 e release `v0.0.1-dev.87`.
- Trabalho: histórico de cinco versões por arquivo em `.lunet/autosave`, escolha de versão ao recuperar, acesso pelo menu Ferramentas e guia offline. O salvamento normal limpa as versões temporárias. Fase 3 continua 🟡.
- Verificação: testes de rotação, reinício e recuperação anterior adicionados; CI e APK ainda pendentes neste registro. Validação no aparelho pendente.
- Próximo passo: validar CI, publicar release e testar no celular; seguir demais itens abertos da Fase 3 antes do roteiro de fechamento.

### 20260930-claude-01 — 2026-09-30 — Claude → próximos agentes — em andamento
- Responde a: 20260929-codex-04.
- Item: Fase 3 — "Cada API com … exemplos, remarks, relacionadas e versão" (§15) e ajustes do ROADMAP.
- Base: `main` em `445ae33` (PR #22). Retomei após o Codex; li `AGENTS.md`, `AgentsChat.md`, ROADMAP e PRs (nenhum aberto). 267 testes passavam na `main`.
- Trabalho: exemplo em todo tipo público não-enum (compilado por teste), `<param>`/`<returns>` em toda API pública (exigidos por teste de cobertura), JSON da API regenerado. Painéis Assets/Profiler/Agent movidos para as fases 5/4/8 com motivo. Fase 3 segue 🟡.
- Verificação: 269 testes locais passando; CI e APK pendentes neste registro. Nada validado em aparelho.
- Adendo (mesma sessão): Preview isolado implementado (`PreviewHost` extraído de `MainActivity`, `IsolatedPreviewActivity` em processo `:preview`, opção em Configurações, ADR 0006). Mexe no código do Preview já validado; conferir regressão do Preview rápido no aparelho.
- Próximo passo: Fast Preview × Isolated Preview; documentos grandes (medição em aparelho pendente); auditoria da Fase 3 e roteiro detalhado ao usuário.

### 20260930-claude-02 — 2026-09-30 — Claude → próximos agentes — em andamento
- Responde a: 20260930-claude-01.
- Item: Fase 3 — validação. O usuário pediu que eu executasse o roteiro sozinho e devolvesse o diagnóstico.
- Trabalho: sem aparelho nem emulador no ambiente, criei `Fase3RoteiroTests` (17 testes) que executam sobre um projeto real do modelo Coletor os passos do roteiro que não dependem de tela. Achado: o Inspector ficava vazio no modelo Coletor (campos privados); corrigido com `[Inspect]`.
- Limite: isto NÃO substitui a validação em aparelho. Fica sem cobertura automática: desenho de dobrar/multi-cursor/minimapa, diálogos, rotação, GL/Preview (rápido e isolado), teclado físico, push/pull no GitHub real. Nenhum item `[ ]` foi marcado `[x]`.
- Verificação: 286 testes locais passando; CI pendente neste registro.


### 20261003-codex-import-zip — Codex → próximos agentes — review
- Task: LUNET-301, Issue Ecosystem #111. Base: 3d9e05cf72fc67b299a3f55a8fd0181e6d6356c8. Branch: codex/lunet-import-zip.
- Pedido: importar projetos ZIP no Lunet; implementação local no Core, botão na tela de projetos e seletor Android. Conflitos criam cópia numerada sem alterar o original.
- Arquivos: ProjectStore, ProjectZipImporter, MainActivity, testes ProjectZipImportTests; SPEC, guia importar-projeto, ROADMAP e CHANGELOG.
- Verificação: roadmap-status --pending e diff --check sem erro; dotnet test e consistency não puderam executar (SDK ausente). CI do PR pendente; nenhuma aprovação de aparelho inventada.
- Handoff: docs/governance/handoffs/HO-20261003-lunet-import-zip.json. Fase 3 continua aberta.
- Próximo passo: acompanhar CI e integrador, confirmar sincronização/release e validar roteiro docs/guides/importar-projeto.md no aparelho.


### 20261003-codex-code-first — Codex → próximos agentes — review
- Task LUNET-302, Issue #149; base `c024c0876b09721e7868c4b14c97ee71e4088746`; branch `codex/lunet-code-first-safe-fixes`.
- Direção do proprietário persistida em ADR 0007 e SPEC: um Product, Framework/IDE/Studio, código como autoridade, Studio opcional. Nenhum csproj vazio ou mudança de namespace.
- Correção real Fase 3: remoção de usings não apaga classe inline; ordenação preserva global/comment/CRLF/EOF e recusa código misto/condicionais. Tests RefactoringTests e ArchitectureTests.
- Documentos: README raiz/local, SPEC, ROADMAP, CHANGELOG, ADR, auditoria e roteiro C6.
- Build portátil verde; testes/checks/CI finais no handoff `HO-20261003-lunet-code-first-safe-fixes`. Sem validação humana inventada.
- #121 passou; #111 permanece review; reconciliador de governança separado, não repetir o teste ZIP.
- Gate da Fase 3 aberto; próximo: CI/integração pelo integrador, APK direto e C6/J1/roteiro integral no aparelho.

### 20261004-chatgpt-f3-candidate — ChatGPT → próximos agentes — review
- Escopo LUNET-303/Issue #200: entrega do candidato 1000008 para roteiro integral da Fase 3, sem avançar fases ou implementar IPC.
- PRs #116/#152 merged e aprovações #121/#155 verificadas; LUNET-301/302 reconciliadas para done preservando evidência humana.
- Candidato inclui ambas as implementações por ancestralidade Git; APK/digest conferidos em metadados da release.
- Objeto: docs/audits/fase-3-candidata.md; roteiro A–J e J1 ainda aguardam o proprietário. A validação integral passa a aparecer como crítica no portal, distinta de C6 e do gate P4-4.
- Testes/checks da alteração e integração serão registrados no handoff; próximo passo: resultados por código, analisar J1, corrigir/retestar antes de concluir Fase 3.


### 20261004-chatgpt-f3-closeout — 2026-10-04 — ChatGPT → próximos agentes — entregue
- Responde a: 20261004-chatgpt-f3-candidate.
- Item: LUNET-303 / fechamento da Fase 3 e liberação do Lunet como candidato a Product Shell do Ecosystem.
- Base: `main` em `ed8bfe36c4d076ea20a2c9229805f83c27f10ff6`; validação humana integral aprovada pelo proprietário na Issue #204.
- Trabalho: ROADMAP e auditoria da Fase 3 reconciliados; handoff LUNET-303 passa a `done`. Os tempos brutos de J1 não foram persistidos no formulário, então nenhuma métrica foi inventada e `PieceTable` permanece deliberadamente sem integração automática.
- Verificação: evidência humana canônica #204; CI da mudança documental segue pelo PR Ecosystem #205. Nenhum código/runtime do Lunet foi alterado neste closeout.
- Limite: a aprovação da Fase 3 não escolhe IPC, não concede grants e não integra Host API no Lunet. A decisão transversal está em DEC-0037/ADR-0027.
- Próximo passo local do Lunet: Fase 4 está liberada pelo roadmap. Trabalho de Ecosystem P5-4 segue separado e não deve ser confundido com a Fase 4 do produto.


### 20261004-chatgpt-p5-4-binder-candidate — 2026-10-04 — ChatGPT → próximos agentes — pronto para validação
- Item: LUNET-401 / Ecosystem P5-4, primeiro caller Android da Host API por Binder.
- Base técnica: PR Ecosystem #210, head `b001eafda4baa16868d626eff402a2f7f57f6918`; CI testou merge-ref `c69f909cffccc1369ee611e883fb8dbae0b8050b` sobre `main` `1d69d5e055238d602283abe1c508743f3378020c`.
- Trabalho: caller descobre provider por action do contrato, valida package/signatário, pareia por código humano e chave EC no Android Keystore, verifica challenge ECDSA, abre sessão Host-owned, executa `text.inspect@1.0.0` e mantém modo observável de 60 s para testar process death. Não há referência Lunet → código/nome de provider concreto.
- Verificação automatizada: Lunet 321/321; APK arm64 verde no run 37221326943, artifact 11310540558; APK SHA-256 `17e9913e569890f254c9074a2960e4002238c886c81690d1ff1eceed600c3422`. Provider Android e consistency também verdes no mesmo candidato.
- Limite: isto NÃO é validação em aparelho. Pairing visual, Keystore real, Binder entre APKs/processos, LinkToDeath e funcionamento standalone ainda exigem o roteiro `docs/validation/P5-4-binder-ipc.md`.
- Próximo passo: proprietário executa o gate DEVICE; PR #210 permanece draft/critical e não deve ser integrado antes da aprovação canônica.


### 20261008-codex-collision — Codex → próximos agentes — review
- Item: LUNET-402, Issue #288, Fase 4 (§7); base `f724b3c`, branch `agent/lunet-continue-20261008`.
- Geometry ganhou distância/closest point/interseção AABB; SAT corrige contenção e rejeita formas sem área. APIs anteriores e convenções de borda preservadas.
- 11 casos de teste, incluindo compilação/execução do guia offline no runtime real; documentação XML/API regenerada. Build portátil sem warnings/errors; suíte padrão 337/337 e 22 checks. A falha concorrente de alocação foi reproduzida na base e corrigida isolando as medições por coleção, sem relaxar zero bytes.
- Roteiro Android intermediário em `docs/guides/colisao.md`; sem validação humana inventada e sem fechar a Fase 4. Evidência final/PR: `HO-20261008-lunet-collision`.


### 20261008-codex-camera — Codex → próximos agentes — review
- Item LUNET-403, Issue #293; branch `agent/lunet-camera-20261008`, base `c0c7be02`; main `9c8e799` juntada por fast-forward antes da entrega.
- Camera2D opcional e overload de SpriteBatch: mundo, zoom, rotação, conversão de toque e HUD fixo; nenhum backend alterado e APIs anteriores preservadas.
- Build portátil sem warnings/errors; suíte padrão 351/351, incluindo 14 casos novos. Guia offline compilado/executado com toque físico 2×. Evidência final/PR no handoff `HO-20261008-lunet-camera`.
- PR #291 integrado e APK 1000120 publicado; handoff anterior recebe evidência automatizada sem inventar teste do aparelho. Fase 4 permanece aberta; câmera aguarda roteiro Android intermediário.

- Complemento LUNET-403: nova página 3 do modelo Laboratório testa câmera sem copiar código. Suíte padrão rerodada 351/351, com regressão das páginas antigas e controles de zoom/giro/toque.


### 20261008-codex-pathfinding — Codex → próximos agentes — review
- LUNET-406 / Issue #311; base main `0dc9471293d1d17657c54b91c812e7be58f1b717`, branch `agent/lunet-pathfinding-20261008`. Entrega independente de #302/#307, sem alterar templates, ProjectStore, persistência ou formato do futuro Tile Studio.
- GridPoint/PathStatus/PathResult/GridPathfinder: A* com custos positivos inclusive <1, obstáculos, diagonais sem cortar cantos, heap indexado e Span de saída; falha/saída pequena não escrevem rota parcial. API XML/JSON e guia offline completo.
- Build .NET 10 sem warnings/errors; suíte padrão 363/363 (12 casos novos). Oracle Dijkstra independente em 200 grades ponderadas; zero bytes por busca, repetição/edição/cantos/buffers/validação. Guia compilado e executado com toque físico 2× no GameHost.
- Roteiro de aparelho em `docs/guides/pathfinding.md` continua pendente; sem fechar item/gate da Fase 4. Busca síncrona O(células) para limpeza + heap de expansão, 32 bytes/célula de buffers; não é thread-safe, não faz movimento nem coordenação de agentes. Integração/release ficam com integrador.
- Estado observado dos trabalhos anteriores: #302 testes combinados verdes, classificado crítico pela política da main por tocar ProjectStore.cs; aguarda label integrar do proprietário. #307 CI/consistency/APK verdes, empilhado sobre #302, ainda não integrado. Não simular autorização nem alterar política para liberar entrega.


### 20261008-codex-advanced-api — Codex → próximos agentes — review
- LUNET-404 / Issue #298 e LUNET-405 / Issue #306: entrega de APIs/guias independente dos modelos de criação protegidos. Branch `agent/lunet-advanced-api-20261008`, base main `040dd8d` (A* já publicado no APK 1000133).
- SpriteAnimationClip/Animator/Tween e ParticleSettings/Emitter preservam a implementação testada; exemplos dos guias usam projeto Em branco + edição normal de Game.cs. ProjectStore.cs, ProjectTemplates.cs e MainActivity.cs têm blobs idênticos aos da main; nenhum modelo novo nem efeito de persistência foi incluído nesta fatia.
- Build .NET 10 sem warnings/errors, suíte padrão 400/400. Testes dos guias agora criam projeto Em branco, escrevem o código pelo fluxo existente e compilam/executam no GameHost com toque físico 2×; medições de zero bytes e regressões de A*/câmera incluídas. Testes de criação dos novos modelos permanecem nos PRs protegidos originais, não foram removidos daqueles PRs.
- Reconciliação dos PRs #302/#307 publicada separadamente, com 386/400 testes e 22 checks, sem simular autorização. A inclusão dos modelos continua crítica pela zona ProjectStore e depende do proprietário; isso não impede a entrega destas APIs sem efeito protegido.
- Closeout de colisão/câmera conserva as aprovações canônicas #303/#304. A* recebeu evidência de integração/release; DEVICE A*/animação/partículas continua pendente, Fase 4 aberta. Próximo: integrar esta fatia pelo integrador, confirmar APK direto e entregar roteiro dos guias.


### 20261008-codex-touch-buttons — Codex → próximos agentes — review
- LUNET-410 / Issue #331, incremento de UI da Fase 4 (§7/§23). Base main `29fbc892197882ec8de180ba90cd90f0d344e7d8`, branch `agent/lunet-touch-buttons-20261008`; escopo explícito Lunet, reuso product-specific.
- TouchButton acompanha o ID que começou dentro: clique somente ao soltar dentro dos limites atuais. Fora mantém captura sem visual pressionado; retorno restaura visual. Cancelled/ausência/posição inválida, disable e Cancel não clicam. Histórico fixo de dedos impede rearmar Pressed retido entre passos.
- TouchButtonStyle imutável e Draw com fundo/texto centralizado, cores normal/pressionado/desabilitado, SpriteBatch/SpriteFont existentes. Sem alocação Update/Draw após recursos aquecidos; sem consumo/arbitragem de input, foco, árvore, tema global ou persistência. Controles sobrepostos exigem escolha explícita do jogo.
- Build .NET 10 sem warnings/errors; runner padrão 413/413 (base main 400 + 13 novos). Guia completo compilado/executado no GameHost com toque físico 2×, clique/arraste/disable/paleta/pausa/retomada e desenho. Não altera VirtualButton, InputState, ProjectStore ou backend.
- #324/#328/#330 continuam independentes, com CI verde e aguardando integrador; última release pública confirmada 1000141. Não declarar APIs novas integradas por artifact de PR. Handoff `HO-20261008-lunet-touch-buttons`; guia `docs/guides/botoes-ui.md`, DEVICE pendente e Fase 4 aberta.

### 20261008-codex-ui-layout — Codex → próximos agentes — review
- LUNET-409 / Issue #329, UI da Fase 4 (§7/§23); base main `29fbc892197882ec8de180ba90cd90f0d344e7d8`, branch `agent/lunet-ui-layout-20261008`. Pedido explícito Lunet; sem substituir prioridade global (AGENTS §1.1).
- LayoutRect/Fixed/Stretch: âncoras normalizadas, pivôs, offsets/margens e composição aninhada; GetBounds entrega o mesmo RectangleF para desenho/Contains, sem alocação. Pais pequenos colapsam limites invertidos; resultados não representáveis são rejeitados. Sem persistência, árvore de UI, backend, modelos ou ProjectStore.
- Build .NET 10 sem warnings/errors, suíte padrão 411/411 (main 400 + 11 novos). Guia compilado/executado no GameHost com toque físico 2×: botão se reposiciona, desenho coincide e toque na posição antiga não conta. Casos de extremos/validação/zero bytes incluídos.
- Bloqueio global P4-9 resolvido por #326/main 29fbc89. Reconciliações feitas pelo outro agente preservadas: #324 c902b02, #328 7622f53, #317 9b77faf. Ainda não integrados; APK público confirmado continua 1000141. Não inventar green combinado novo nem aplicar integrar.
- Roteiro `docs/guides/layout-ui.md` no aparelho continua pendente. UI completa/temas e Fase 4 abertos; handoff `HO-20261008-lunet-ui-layout`, próximo: checks/PR/CI/integrador/release.
### 20261008-codex-bitmap-fonts — Codex → próximos agentes — review
- LUNET-408 / Issue #327, fontes da Fase 4 (§7/§23); base main `d25bcebf35ec248f474a7332dc3756e60817a01d`, branch `agent/lunet-bitmap-fonts-20261008`. Pedido explícito Lunet conforme AGENTS §1.1/PLAT-001; não é substituto de progresso global.
- SpriteFont.FromBitmap + BitmapGlyph: mapas Unicode/recortes de atlas copiados, avanços proporcionais, offset, fallback, CR/LF/CRLF e espaços invisíveis. Reutiliza DrawString/Measure/SpriteBatch e permite Content.LoadTexture; não cria formato persistente, importador, backend ou modelo de projeto.
- Build .NET 10 sem warnings/errors; suíte padrão 413/413, 13 casos novos incluindo UV/layout, Unicode inválido/suplementar, zero bytes, câmera/clip/state/capacity flush e guia compilado/executado com toque físico 2×. Fonte embutida e contratos anteriores preservados.
- Roteiro `docs/guides/fontes-bitmap.md` pendente no aparelho; TrueType/kerning/shaping/bidi/wrap e item completo de fontes permanecem abertos. Handoff `HO-20261008-lunet-bitmap-fonts`; próxima ação: checks/PR/CI/integrador/release.
- Estado anterior verificado: #316 integrado, APK 1000141 real; roadmap 404/405 reconciliado sem fechar DEVICE. #317 modelos críticos verdes pendentes e #324 nine-slice não integrados; nenhum novo APK público inferido de artefato CI.
### 20261008-codex-nine-slice — Codex → próximos agentes — review
- LUNET-407 / Issue #319, UI da Fase 4 (§7/§23); base main `91aebbc012490a598ec18fd53503d90df1a421cd`, branch `agent/lunet-nine-slice-20261008`. APIs de animação/tween/partículas já integradas pelo #316 e publicadas no APK 1000141; modelos opcionais #317 críticos verdes aguardam autorização real do proprietário.
- NineSlice imutável e SpriteBatch.Draw(panel, ...): região de atlas, bordas assimétricas/escaláveis, compressão proporcional em destinos pequenos, até nove quads e zero alocação por desenho. Reutiliza câmera/clip/state/batching, sem tocar ProjectStore, criação de projetos, persistência ou backend.
- Build .NET 10 sem warnings/errors; suíte padrão 410/410, 10 casos novos incluindo cobertura/UV, sem sobreposição, zero/extremos/validação, câmera/capacity flush/state, zero bytes e guia compilado/executado com toque físico 2×.
- Layout/âncoras/temas e UI Studio continuam pendentes. DEVICE/GL/aparência/Preview rápido e isolado aguardam roteiro `docs/guides/nine-slice.md`; sem encerrar item/gate. Handoff `HO-20261008-lunet-nine-slice`; próxima ação: CI/integrador e confirmação da release.


### 20261008-codex-reconcile-332-nine-slice — Codex → próximos agentes — review
- PR #332 reconciliado semanticamente com main `f3392f2a3e111de7f7bcd36fc78073a84bf375e7` (NineSlice integrado pelo #324, release pública 1000150). Entradas de código e de histórico de ambos os lados preservadas; duplicação do changelog de APIs corrigida, ROADMAP 404/405/407 alinhado às integrações reais sem fechar DEVICE.

### 20261008-codex-reconcile-330-nine-slice — Codex → próximos agentes — review
- PR #330 reconciliado semanticamente com main `f3392f2a3e111de7f7bcd36fc78073a84bf375e7` (NineSlice integrado pelo #324, release pública 1000150). Entradas de código e de histórico de ambos os lados preservadas; duplicação do changelog de APIs corrigida, ROADMAP 404/405/407 alinhado às integrações reais sem fechar DEVICE.
### 20261008-codex-reconcile-328-nine-slice — Codex → próximos agentes — review
- PR #328 reconciliado semanticamente com main `f3392f2a3e111de7f7bcd36fc78073a84bf375e7` (NineSlice integrado pelo #324, release pública 1000150). Entradas de código e de histórico de ambos os lados preservadas; duplicação do changelog de APIs corrigida, ROADMAP 404/405/407 alinhado às integrações reais sem fechar DEVICE.
- Código específico do PR preservado; testes completos e checks serão registrados no handoff de reconciliação. Sem alterar gates, contratos ou política de integração; nenhuma label integrar aplicada.
- Preferência explícita do proprietário nesta sessão: demonstrações para testar no celular devem ser executáveis pelo Laboratório ou por um template, prontas para Run; não entregar copiar código de guia como roteiro principal. Não reformar documentação agora; usar esse formato nas próximas demonstrações. Isso não é aprovação de DEVICE nem autorização do PR crítico #317.


### 20261008-codex-reconcile-332-bitmap — Codex → próximos agentes — review
- PR #332 combinado com main `2e62f554fdd13525e9489f07386862ee39e5d298`: fontes bitmap #328 publicadas no APK 1000155; código específico e registros anteriores preservados, changelog sem duplicação, API JSON regenerada.
- Build sem warnings/errors; suíte padrão 436/436, zero falhas/erros/skips; delta source/tests limitado aos três arquivos bitmap da main, byte idênticos. Consistency no handoff; DEVICE e gate da Fase 4 não aprovados. Classificação anterior mantida; sem merge manual/label integrar.

### 20261008-codex-reconcile-330-bitmap — Codex → próximos agentes — review
- PR #330 combinado com main `2e62f554fdd13525e9489f07386862ee39e5d298`: fontes bitmap #328 publicadas no APK 1000155; código específico e registros anteriores preservados, changelog sem duplicação, API JSON regenerada.
- Build sem warnings/errors; suíte padrão 434/434, zero falhas/erros/skips; delta source/tests limitado aos três arquivos bitmap da main, byte idênticos. Consistency no handoff; DEVICE e gate da Fase 4 não aprovados. Classificação anterior mantida; sem merge manual/label integrar.

### 20261008-codex-touch-slider — Codex → próximos agentes — review
- LUNET-411 / Issue #335; branch `agent/lunet-touch-slider-20261008`, base main `2e62f554fdd13525e9489f07386862ee39e5d298` (fontes bitmap #328 integradas).
- TouchSlider/TouchSliderStyle: faixa contínua ou passos, captura por ID, arraste fora da barra, Released final, cancelamento/disable, bounds mutáveis e desenho sem alocação. Reutiliza InputState/SpriteBatch; sem backend ou persistência novos.
- Demonstração na página 4 do modelo Laboratório existente: raio, intensidade, passos de 10 e disable. Dois dedos podem controlar barras independentes; troca de página e OnPause cancelam captura. Novos projetos prontos para Run; projetos existentes preservados.
- Preferência explícita do proprietário: exemplos executáveis no Laboratório ou template. Não reformar os guias anteriores agora. Guia novo contém roteiro da página pronta, sem exigir cópia de código.
- Build sem warnings/errors; suíte padrão combinada 438/438 (423 da main + 15 novos casos), incluindo execução real do Laboratório com toque físico 2×, zero bytes do controle, extremos/validação e regressões das páginas anteriores. Consistency: 22 checks; snapshot ROADMAP × Issues conferido remotamente.
- DEVICE e release ainda pendentes. Slider horizontal sem foco/teclado/layout/tema global; não conclui UI nem gate da Fase 4. Integração exclusivamente automática, sem merge manual/label integrar.


### 20261008-codex-reconcile-332-slider — Codex → próximos agentes — review
- PR #332 combinado com main `b614d94c39a1ec7ad133718d7af184eef7670003` após integração real de #336. Página 4 do Laboratório e slider preservados; código específico anterior mantido, API JSON regenerada.
- Build sem warnings/errors; suíte padrão 451/451, zero falhas/erros/skips. Delta source/tests só traz slider e Laboratório da main; ProjectStore/demos próprios mantidos. Consistency no handoff; DEVICE/gate pendentes, classificação anterior e política de integração preservadas.

### 20261008-codex-reconcile-330-slider — Codex → próximos agentes — review
- PR #330 combinado com main `b614d94c39a1ec7ad133718d7af184eef7670003` após integração real de #336. Página 4 do Laboratório e slider preservados; código específico anterior mantido, API JSON regenerada.
- Build sem warnings/errors; suíte padrão 449/449, zero falhas/erros/skips. Delta source/tests só traz slider e Laboratório da main; ProjectStore/demos próprios mantidos. Consistency no handoff; DEVICE/gate pendentes, classificação anterior e política de integração preservadas.


### 20261008-codex-buttons-laboratory — Codex → próximos agentes — review
- LUNET-410 / Issue #331 / PR #332, reconciliado com main `4966481372efcdb2dedd7816806621d48ba51eab` (LayoutRect #330 e APK 1000164). APIs próprias de TouchButton/Style preservadas.
- Quinta página no Laboratório existente: clique confirmado na soltura, feedback, drag-out/retorno, disable/habilitar, reposicionamento por LayoutRect, contador e pausa. Não altera projetos existentes nem ProjectStore; próximos exemplos continuam prontos para Run.
- Cabeçalho usa TouchButton para aceitar segundo dedo enquanto outro controla uma barra/botão; navegação cancela controles escondidos. Reconhecedor de gestos não alterado. OnPause cancela também navegação. Teste antigo passou a enviar Pressed no início dos taps, como o Android, mantendo todas as verificações de áudio/sensores/gráficos/câmera.
- Build sem warnings/errors, suíte padrão completa 463/463 (main 449 + 13 casos de botões + 1 novo fluxo do Laboratório). Novo caso executa o projeto gerado, em 720×1280: clique/arraste/disable/pausa/relocação/redimensionamento/segundo dedo no cabeçalho; testes anteriores de slider e páginas preservados.
- Guia novo laboratorio-botoes é roteiro da página pronta; guia antigo não reformado. Integração/release e DEVICE pendentes, sem fechar UI/Fase 4 ou inferir autorização crítica de #317. Handoff HO-20261008-lunet-buttons-laboratory.


### 20261008-codex-scene2d — Codex → próximos agentes — review
- LUNET-412 / Issue #338; branch agent/lunet-scene2d-20261008, base main 18debffa8c9d7858091078aae6d70e36efa2c2c8. Scene2D/Entity2D/Component2D opcionais no Framework, sem ECS obrigatório, formato persistente, novo backend ou dependência externa.
- Ownership exclusivo, inserção determinística, transform identidade, flags Update/Draw independentes, consulta Get<T>; remoção preserva objetos/componentes/recursos. Alterações estruturais e reentrada da mesma cena são rejeitadas durante percurso; flags são imediatas, exceções propagam e finally libera bloqueio. Componentes com Equals customizado são removidos por identidade de referência.
- Sexta página executável do Laboratório; criar novo projeto e Run. Componentes de movimento/desenho separados, pausa/visibilidade/desanexar/reattach e cancelamento no OnPause. Nenhuma escrita em projetos existentes/ProjectStore. Ajustados testes de navegação para seis páginas mantendo verificações anteriores.
- Build .NET 10 sem warnings/errors; runner xUnit padrão 475/475 (main 463 + 12 novos casos), API JSON regenerada, exemplos XML compilados; runtime da demonstração executado com toque físico 2×, zero bytes de percursos/lookup medidos. Consistency registrada no handoff.
- DEVICE pendente em docs/guides/laboratorio-cenas.md. Fase 4 continua aberta, sem avanço de fase. CI/integração/release separados; integrador automático, sem label integrar ou merge manual.


### 20261008-codex-debug-shapes — Codex → próximos agentes — review
- LUNET-413 / Issue #340, Fase 4 §7/§23; branch agent/lunet-debug-shapes-20261008, base remota 62baa859c210b15a1a54c47ea20260d79391abc5. Snapshot local inicial tem árvore 6a09181 idêntica à main; publicação será parented na main real.
- Helpers aditivos DebugDraw.Polygon/Ray/Axes/Grid: validação de toda geometria antes de escrever, limites de 4096 segmentos, transform/câmera/clip/batch existentes e zero bytes após aquecimento. Não altera Line/Circle/Rect/Cross nem cria backend, collider, persistência ou Tool transversal.
- Sétima página executável do Laboratório: contorno transformado, raio com hit de Circle pela API existente, grade, eixos, girar/refletir/câmera/toggle e cancelamento na pausa. Projetos anteriores/ProjectStore preservados; navegação testada mantendo todas as verificações antigas.
- Build sem warnings/errors, 485/485 testes (main 475 + 10 novos casos). Geometria registrada pelo backend, falhas sem desenho parcial, extremos/limites, ordem/reflexão/matriz, batching/câmera/clipping e zero bytes; projeto gerado compilado/executado com toque físico 2×. API JSON e XML cobertura verdes.
- Proprietário adiou o teste no aparelho em 2026-10-08 e autorizou continuar desenvolvimento independente. Isso não é aprovação DEVICE, nem fecha Fase 4, nem autoriza pular seu gate. LUNET-412 já integrado/publicado no APK 1000169; validação de ambos permanece pendente.
- Handoff HO-20261008-lunet-debug-shapes; guia laboratorio-debug.md. Próximo: CI/integrador/release, continuar pendências independentes da Fase 4 sem pedir teste agora.

### 20261008-chatgpt-tilemaps — 2026-10-08 (America/Sao_Paulo) — ChatGPT → próximos agentes — em revisão
- Item: LUNET-415, Issue #355, Fase 4 (SPEC §7, §13, §23).
- Base: main 97eefdeac179884478cb790339e3234e707a4915; branch agent/lunet-tilemaps-20261008.
- Trabalho: TileMap JSON local, camada collision/visible, viewport culling, conversão do A*, ContentManager.LoadTileMap, testes de casos inválidos e demonstração compilável do guia; ROADMAP/CHANGELOG e documentação offline.
- Verificação: SDK .NET 10 e Android indisponíveis nesta sessão; CI e DEVICE pendentes. Não declarar merge, release ou aprovação.
- Isolamento: sem mudar ProjectStore, Android, outros produtos nem a trilha de Profiler #343 ou modelos PR #317.
- Handoff: HO-20261008-lunet-tilemaps. Próximo: CI, correções, integrador automático, roteiro de toque no aparelho.

### 20261008-chatgpt-lunet-camera-culling — ChatGPT → próximos agentes — review
- LUNET-416 / Issue #363, Fase 4. Branch `agent/lunet-camera-culling-20261008`, base main `61309b902472c99410133e04c02360a4b32dd725`, após TileMap integrado no PR #358.
- `Camera2D.GetWorldViewBounds`: AABB conservadora contendo os quatro cantos da vista mesmo com zoom/rotação; arredondamento para fora e validação de extremos, sem alocar.
- Testes: geometria dos cantos, validação de limites, renderização com culling do TileMap e zero bytes após warmup. Guias e catálogo de API atualizados.
- Escopo local, sem ProjectStore, migração, Android ou Host. CI/consistency/APK e DEVICE ainda por verificar; gate Fase 4 permanece aberto.
- Reuse assessment product-specific, NN-002/008/017/018/022. Handoff HO-20261008-lunet-camera-culling.

### 20261008-chatgpt-lunet-ui-themes — ChatGPT → próximos agentes — review
- LUNET-417 / Issue #365, Fase 4. Branch `agent/lunet-ui-themes-20261008` baseada em main `61309b902472c99410133e04c02360a4b32dd725`; coordenação: PR #364 de câmera em paralelo, pode exigir reconciliação de histórico/doc.
- `UiTheme` imutável: Dark, Light, HighContrast, estilos TouchButton/TouchSlider e cores de fundo/painel/texto em um único valor. Sem global mutable state, efeitos colaterais ou resources novos.
- Testes de opções opacas/estados pressionados-desabilitados, personalização, desenho real dos controles no backend e zero bytes após warmup; guia offline e catálogo API atualizados.
- Sem modificação dos controles existentes, ProjectStore, Android, formato salvo, Host ou Tools; CI/merge/release/DEVICE pendentes. Fase 4 permanece aberta.
- Reuse assessment: product-specific; NN-002/008/017/018/022. Handoff HO-20261008-lunet-ui-themes.

### 20261008-chatgpt-lunet-ui-themes-reconcile — ChatGPT → próximos agentes — review
- PR #366 conciliado com main `5f51edac5a4a0651c585f2a3b939fe9a070e68cd` após merge do PR #364 (LUNET-416). As APIs e testes de viewport da Camera2D foram preservados.
- Árvore mesclada com dois pais Git (branch de temas e main), sem reescrever histórico, incluindo alteração local de UI, docs API regeneradas na combinação e notas/handoffs.
- CI combinada, integração e DEVICE seguem independentes e pendentes; política do integrador preservada. Sem alteração de ProjectStore ou Host.

### 20261008-chatgpt-lunet-laboratory-v2 — ChatGPT → próximos agentes — review
- LUNET-418 / Issue #367 / PR #368, Fase 4, branch `agent/lunet-laboratory-v2-20261008`, base main `8a71937e9f1fda7ba986c596d8037f23f4e6ff7d`.
- Extração do Laboratório para `Lunet.Core/LaboratoryTemplate.cs`, `ProjectTemplates.LabSource` permanece com assinatura estável. `ProjectStore.cs` permanece byte-intacto; projetos anteriores não são regravados.
- 12 módulos: dispositivos, gráficos, câmera, sliders, botões, cenas, depuração, animação/partículas, tilemaps/pathfinding, colisão SAT, bitmap font/nine-slice e paletas de UI. Índice em grid, anterior/próximo e Run offline. Assets de exemplo procedurais.
- Os testes das sete áreas antigas foram preservados e adaptados ao total de 12; demais testes end-to-end, CI, APK e DEVICE precisam ser verificados. Não inferir aprovação no aparelho.
- Reuse assessment product-specific, NN-002/008/017/018/022; sem alteração em formato, ProjectStore, distribuição, build/signing, Tool ou Host.

### 20261008-chatgpt-lunet-laboratory-v2-ui-merge — ChatGPT → próximos agentes — review
- PR #368 reconciliado com main bb7f6992a53c753f4ad488f72d4a44e0ecc08f21 após integração de UiTheme pelo PR #366; histórico preservado usando commit com dois pais.
- Demo Temas do Laboratório usa diretamente UiTheme.Dark/Light/HighContrast em vez de paletas locais duplicadas. Mantém todas as demais demos, testes regressivos e documentos da main. CI combinado e release Android devem ser reconfirmados; DEVICE permanece pendente.
- Sem alteração dos arquivos de projeto existentes ou do ProjectStore, sem novas dependências e sem mudança nas decisões da Fase 4.

### 20261008-chatgpt-lunet-live-profiler — ChatGPT → próximos agentes — review
- LUNET-419 / Issue #369 / Fase 4. Branch `agent/lunet-live-profiler-20261008`; base main `c00ae11dfdbdcd9ba671a9704673203fbd24b2a2`, após release Laboratório 2.0 dev.1000193.
- Incremento real no Preview Android: botão `Perf`, painel de janela móvel com FPS/cadência, custo CPU de `GameHost.Tick` (Update+Draw juntos), draw calls, triângulos, alocações da thread GL, heap gerenciado e GC 0/1/2. Instrumentação opt-in no GlesBackend/PreviewRenderer, sem alteração no Framework, GameHost, assinatura das APIs ou ProjectStore.
- Modelo portátil em `Lunet.Runtime.Profiling.PreviewFrameStatistics`, com testes de janela/validação/ausência de alocação; UI atualizada a cada 300 ms quando visível, desligando coleta quando ocultada. Reiniciar/loss de contexto zera a janela; pause/Step continuam funcionando.
- Separação de trabalho: LUNET-414 Issue #343 ainda propõe profiler framework completo, com medições Update/Draw, audio etc, e não possui branch ativa; não confundir implementação parcial com conclusão da Fase 4.
- Verificação: CI Lunet Android, consistency, APK e DEVICE a verificar após abrir PR. Reuse assessment product-specific, NN-002/008/017/018/022; sem novo backend, telemetry remota, API pública do Framework, dados persistidos ou dependência externa.

### 20261008-owner-lunet-laboratory-validation — proprietário → registro por ChatGPT — approved
- O proprietário informou na conversa em 2026-10-08: "Eu fiz os testes, tá tudo aprovado". Confirmação humana referente ao Laboratório 2.0 com os doze módulos do APK `lunet2d-v0.0.1-dev.1000193`.
- Evidência persistida na Issue #367, comentário 6071609183; issue encerrada como concluída. PR #368 integrado na main c00ae11dfdbdcd9ba671a9704673203fbd24b2a2, release pública confirmada. CI integrado 521/521 Lunet.Tests, 9/9 inspeção de texto.
- Aprovação de DEVICE específica do Laboratório e seus testes funcionais. O proprietário não informou modelo do aparelho ou medições numéricas; nenhuma foi inferida. Fase 4 ainda aberta para física completa, TrueType, Tile Studio e Profiler.
- O Profiler LUNET-419 (Issue #369 / PR #370) ainda não foi incluído no APK testado, portanto permanece com DEVICE pendente.

### 20261008-chatgpt-lunet-cpu-phases — ChatGPT → próximos agentes — review
- LUNET-420 / Issue #371, incremento de #343 (Profiler básico, Fase 4), branch `agent/lunet-cpu-phase-profiling-20261008`, base main `9edfec3a586414244a3feeeb3874180f7c0c179a`, após Profiler ao vivo PR #370 release dev.1000196.
- GameHost: ProfileFrameTimings desativado por padrão; LastUpdateCpuMilliseconds, LastDrawCpuMilliseconds, LastUpdateSteps; captura por Stopwatch monotônico somente quando ativada, inclusive Step e pausa. Granularidade Update engloba Timers/Audio no RunUpdate mas não Dispatcher; Draw engloba a chamada CPU sem GPU.
- PreviewRenderer habilita/consulta as medições sem mexer no código do jogo. Histórico do Profiler preserva compatibilidade da sobrecarga Record antiga e separa PhasedSamples para não apresentar zeros falsos; overlay exibe duração média das duas fases e passos médios, com amostras válidas.
- Testes xUnit para fases, rolamento, validação, alocação e host; snapshot de docs API atualizado. Sem alteração em ProjectStore, formato persistido, backend ou Tools. CI Android, consistency e DEVICE ainda por verificar. Não fecha Fase 4 nem reivindica audio underruns/GPU time.

### 20261008-chatgpt-lunet-camera-follow — ChatGPT → próximos agentes — review
- LUNET-421 / Issue #373 / Fase 4, branch `agent/lunet-camera-follow-bounds-20261008` baseada na main `9edfec3a586414244a3feeeb3874180f7c0c179a`. PR #372 CPU Profiler em integração concorrente; reconciliar docs API e ROADMAP com duas histórias quando entrar.
- `Camera2D.Follow` faz amortecimento exponencial por passo sem GC; `ClampToWorld` limita ao mundo levando zoom/rotação em consideração e centraliza a câmera quando o cenário for menor do que a extensão da vista. Entradas não finitas são rejeitadas.
- No Laboratório novo, página Câmera preserva zoom/giro/arrastar existentes, substitui suavização manual pela API oficial e acrescenta botão de LIMITES desligado por padrão. Projetos anteriores não são regravados; ProjectStore intacto.
- Testes matemáticos de frame rate, ângulos, limites, validação, no alloc e demonstração de toque. API snapshot, guia e roteiro offline atualizados. Sem backend, persistência, permissões ou Tools novos.
- Reuse assessment product-specific, NN-002/008/017/018/022; CI/consistency/APK/DEVICE ainda por verificar. Não fecha Fase 4.

### 20261009-chatgpt-camera-follow-reconcile — ChatGPT → próximos agentes — review
- PR câmera #374 reconciliado via merge Git com main d27c9effd0998fbf5e76126cdec32dfc302e486a, após PR #372 de CPU phases integrado. A API JSON preserva os quatro membros públicos de GameHost e acrescenta os dois métodos da Camera2D.
- Os testes, guias e mão de obra de ambas tarefas foram preservados sem reescrever histórico; CI do novo head e DEVICE ainda devem passar. Fase 4 permanece aberta.

### 20261009-chatgpt-lunet-viewport-fit-fill — ChatGPT → próximos agentes — review
- LUNET-422 / Issue #375, Fase 4. Branch `agent/lunet-viewport-fit-fill-20261009`, base main `d5eae81b7d832157397414c9de9efedafefeccb8` após releases 1000200 e 1000202; PR #372/#374 já integrados.
- `ViewportScalingMode.Fit` mantém comportamento legado. `Fill` amplia viewport proporcionalmente sem distorcer, podendo recortar bordas. `GameConfiguration.ViewportScaling` opt-in na inicialização e `GraphicsDevice.ViewportScaling` editável em runtime. Com PixelPerfect, Fill usa escala inteira ceil quando >=1 para não voltar a criar barras.
- Mapeamento de toques, clipping GL, SafeArea e resize recalculados no mesmo caminho de projeção original; sem alterar entrada de toque ou conteúdo do Game.cs persistido. Painel Gráficos do Laboratório 2.0 demonstra Fit/Fill diretamente sem edição de código.
- Testes de aspect portrait/landscape, crop, toque round-trip, PixelPerfect, rejeição de valores inválidos, scissor/render target e caminho sem alocação. Catálogo de API offline/guia/regressões. CI/DEVICE ainda pendentes; não declarar concluída a Fase 4.
- Reuse assessment product-specific, NN-002/008/017/018/022, paths de ProjectStore, Host Android e ferramentas compartilhadas intactos.

### 20261009-chatgpt-lunet-423 — 2026-10-09 (America/Sao_Paulo) — ChatGPT → próximos agentes — em revisão
- Item: LUNET-423, Issue #377, Fase 4 §7/§23, ergonomia de joysticks virtuais e botões com dois dedos.
- Base: `main` em `61a9682fda8ce6bb36e932085cc22ca6f7c45a13`; branch `agent/lunet-virtual-controls-20261009`. PR #317 de ProjectStore continua isolado.
- Trabalho: ajustes opt-in no `VirtualStick`, captura `TouchId`/exclusão, atualização do `VirtualButton`, testes de precisão/multitouch e alocação, guia `entrada.md`, changelog, roadmap e handoff.
- Verificação: .NET 10 SDK não disponível no ambiente da sessão; testes, consistency e APK Android deverão ser conferidos pelo CI do PR. Não declarar dispositivo aprovado.
- Riscos/escopo: um único ID reservado por Update; não mexe em ProjectStore, MainActivity, configuração persistida nem código dos jogos. Defaults legados preservados.
- Próximo passo: verificar e corrigir CI, integrar pela política do repositório; confirmar APK e solicitar roteiro DEVICE no celular.

### 20261009-chatgpt-lunet-touch-scroll — ChatGPT → próximos agentes — review
- LUNET-427 / Issue #393 / Fase 4 §7/23. Branch `agent/lunet-ui-touch-scroll-20261009` baseada na main atual consultada ao iniciar.
- `TouchScrollArea`: controle vertical sem árvore ou roteamento implícito, ID de dedo, limiar para preservar toque rápido, inércia por delta, saturação de limites, Cancel/disable e posição independente da pintura.
- Integração com SpriteBatch.Begin(clip: Bounds) demonstrada em guia executável; arbitragem com botões filhos permanece responsabilidade do jogo via IsDragging/WasDragged.
- Testes sintéticos para tap, drag, release, multitouch, cancel, resize, bounds e nenhuma alocação gerenciada por Update. CI, docs API, APK e DEVICE pendentes; não declarar fase concluída.
- ProjectStore, arquivos dos projetos, distribuição, outros apps e repo espelho não alterados. Reuse product-specific, NN-002/008/017/018/022.
