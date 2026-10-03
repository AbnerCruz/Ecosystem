# ADR-0019 — Builds e releases dos Products no Ecosystem

## Status

Aceito — DEC-0031, decisão explícita do proprietário em ADD-0015. Implementação
em revisão; não implica que todos os canais já tenham sido migrados.

## Contexto

O código já é canônico em apps/<id>. A distribuição de Lunet/Urbe depende de
espelhos, enquanto o Hub já publica no Ecosystem. O proprietário pediu eliminar
essa sincronização intermediária. Urbe tem chave Android privada e atualizadores
que usam /releases/latest da origem; Lunet usa chave de desenvolvimento pública.

## Problema

Produzir e entregar cada Product sem sincronizar código para outro repositório,
sem regressão de assinatura/versionCode nem release de produto errado.

## Opções

1. Manter espelhos de código e seus pipelines.
2. Pipelines diretos e seletivos no Ecosystem, migração gradual de consumidores.
3. Construir primeiro uma plataforma de distribuição própria.

## Decisão

Opção 2, autorizada em ADD-0015. Tags `<component-id>-v<version>`; nunca usar
`releases/latest` global como catálogo de todos os Products. Lunet publica
pré-lançamentos com `--latest=false`, mesma chave/certificado e versionCode
`1000000 + run_number` (acima das builds da origem; contador do workflow próprio).
Seu pipeline constrói APK no PR e publica apenas main/dispatch. O integrador
redispara esse workflow para mudanças em apps/lunet2d e no workflow de release,
pois push de GITHUB_TOKEN não dispara outro workflow.

Urbe usa urbe-v<package.version>, testes e builds Windows/Android diretos. Somente
a tag aprovada publica (REQ-006/066); main não publica release do Urbe. Sem a
mesma chave privada configurada no Ecosystem, o release Android falha fechado.
PRs só constroem artefato de teste, sem receber secrets de produção. O pipeline
está preparado; migração do canal primário do Urbe requer versão-ponte, assinatura
conferida e teste de updater em item P4-9. Até lá o perfil current mantém os canais
reais do Urbe; os antigos não são desligados nem redirecionados automaticamente.

Hub continua com pipeline existente. Ecosystem AI é biblioteca/Product sem shell
distribuível nesta fase; não inventar APK. Novo Product declara seu build e canal.

O checker seleciona workflows reutilizáveis ou com nome <id>-ci/checks para o
gate combinado; um workflow de publicação com o mesmo filtro não substitui
o CI. A descoberta anterior dependia da ordem de enumeração dos arquivos.

## Consequências

Remove espera da sincronização para o Lunet; mantém versões independentes e
checksum/manifest. Urbe pode construir sem espelho, mas não pode prometer atualização
automática dos apps antigos ainda. P4-9 registra os pré-requisitos. Não arquivar
repos antigos, não excluir releases/Pages. Depois de integrar e publicar o Lunet,
desativar apenas o workflow de sincronização do Lunet; rollback por reativação,
com cuidado para não publicar APK antigo com versionCode menor que o instalado.

## Alternativas rejeitadas

Sincronização como requisito permanente foi rejeitada pelo proprietário. Backend
novo antes de consumidores adicionaria complexidade (NN-020/022). Trocar a chave
ou apontar atualizadores para releases/latest global quebraria compatibilidade.

## Referências

ADD-0015; DEC-0031; MANIFEST NN-001/011/014/016/017/018/023; ADR-0015;
docs/distribution/direct-releases.md; REQ-006/019/066/081 do Urbe.

Rastreabilidade da tarefa: **P4-10**, Issue #120, handoff
`HO-20261003-releases-diretas`. O ID P4-8 foi usado por engano na
implementação/PR #123 e na resposta #126; esses registros históricos
permanecem preservados. P4-8 identifica Patch Notes (Issue #119).
Reconciliação: P4-11, Issue #128. P4-9 depende de P4-10.
