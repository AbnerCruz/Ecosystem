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
