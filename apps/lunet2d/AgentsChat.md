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
