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


### 20261008-codex-bitmap-fonts — Codex → próximos agentes — review
- LUNET-408 / Issue #327, fontes da Fase 4 (§7/§23); base main `d25bcebf35ec248f474a7332dc3756e60817a01d`, branch `agent/lunet-bitmap-fonts-20261008`. Pedido explícito Lunet conforme AGENTS §1.1/PLAT-001; não é substituto de progresso global.
- SpriteFont.FromBitmap + BitmapGlyph: mapas Unicode/recortes de atlas copiados, avanços proporcionais, offset, fallback, CR/LF/CRLF e espaços invisíveis. Reutiliza DrawString/Measure/SpriteBatch e permite Content.LoadTexture; não cria formato persistente, importador, backend ou modelo de projeto.
- Build .NET 10 sem warnings/errors; suíte padrão 413/413, 13 casos novos incluindo UV/layout, Unicode inválido/suplementar, zero bytes, câmera/clip/state/capacity flush e guia compilado/executado com toque físico 2×. Fonte embutida e contratos anteriores preservados.
- Roteiro `docs/guides/fontes-bitmap.md` pendente no aparelho; TrueType/kerning/shaping/bidi/wrap e item completo de fontes permanecem abertos. Handoff `HO-20261008-lunet-bitmap-fonts`; próxima ação: checks/PR/CI/integrador/release.
- Estado anterior verificado: #316 integrado, APK 1000141 real; roadmap 404/405 reconciliado sem fechar DEVICE. #317 modelos críticos verdes pendentes e #324 nine-slice não integrados; nenhum novo APK público inferido de artefato CI.
