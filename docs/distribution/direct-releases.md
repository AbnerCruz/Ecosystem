# Releases diretas dos Products

Decisão: ADD-0015 / DEC-0031 / ADR-0019. Código em apps/<id>; builds nos workflows
da raiz. Nenhum app depende do Hub. Releases têm tags prefixadas por produto.

| Product | Pipeline | Publicação | Assinatura / estado |
|---|---|---|---|
| Lunet2D | lunet2d-release.yml | main ou dispatch, development | Mesma chave pública de desenvolvimento e pin de certificado; não é canal estável |
| Urbe | urbe-build.yml / urbe-release.yml | tag urbe-v<package.version> | Mesma chave privada obrigatória para publicação Android; pipeline preparado |
| Hub | hub-release.yml | política existente | Chave dev do Hub |
| Ecosystem AI | ecosystem-ai-ci.yml | sem artefato instalável nesta fase | Não criar release fictícia |

## Lunet

Depois da integração, executar `gh workflow run lunet2d-release.yml --repo
AbnerCruz/Ecosystem --ref main`. O integrador dispara automaticamente para mudanças
do Product/workflow; documentos do Lunet também geram desenvolvimento, como antes.
Conferir os três jobs: test → apk → release. Downloads são standalone nas Releases
do Ecosystem, tag lunet2d-vX.Y.Z-dev.N; APK, SHA256SUMS e manifest do Product.
O versionCode e o sufixo dev passam para 1000000 + contador do workflow. A origem
estava abaixo desse piso; preserva io.lunet.studio e certificado SHA256 48:07:F9:08:
0B:F5:53:5D:20:73:44:76:E3:E3:BC:9F:E6:44:6C:70:E3:84:77:14:86:E6:AC:33:3E:DD:92:07.
Não mudar o piso/renomear o workflow sem verificar monotonicidade de versionCode.

Somente depois de publicar e conferir o primeiro APK direto, desativar
sync-from-ecosystem.yml na origem Lunet2D. O histórico permanece. Rollback:
reativar o espelho para diagnóstico; não distribuir APK com versionCode menor.

## Urbe: build pronto, corte condicionado

O Pages do Ecosystem também monta o Urbe Web diretamente de apps/urbe, em
`https://abnercruz.github.io/Ecosystem/urbe/`, junto do portal. O URL/PWA
anterior permanece intacto: este deploy não transfere dados ou redireciona
PWAs antigos; conferir abertura, offline e armazenamento em aparelho.


`urbe-app.yml` constrói Windows e Android em PRs, sem publicar nem receber secrets.
Para publicar, manter gates REQ-006/066: versão única e CHANGELOG, tag
urbe-v<package.version> e workflow urbe-release.yml. Nunca tag genérica v* no monorepo.
O `latest` do repo é global: as releases diretas usam make_latest:false e precisam
de consumidor que filtre o Product. Os leitores antigos não são alterados aqui.

P4-9 usa `1.8.3-beta` como versão-ponte: o canal legado a entrega primeiro; Windows passa a embutir `AbnerCruz/Ecosystem` com `tagNamePrefix=urbe-v`, e Android filtra a lista de releases do Ecosystem por tag/asset exatos. O `latest` global permanece proibido.

Antes do primeiro release/corte (P4-9):

1. Configurar no Ecosystem os secrets ANDROID_KEYSTORE_BASE64 e
   ANDROID_KEYSTORE_PASSWORD com a MESMA chave do Urbe. GitHub não permite ler
   valores de secrets existentes; o agente não os recupera nem imprime.
2. Preparar versão-ponte do Urbe com feed por Product para Windows e Android,
   testando filtro de tags, falhas, ausência de release e download correto.
3. Entregar a ponte no canal antigo, ou documentar instalação manual deliberada.
   Sem ponte, apps antigos continuam consultando AbnerCruz/Urbe.
4. Conferir certificado/package/versionCode dos APKs; validar atualização com
   vault real de teste e backup, sem reinstalação nem perda de dados.
5. Mudar o perfil current só com canal real publicado e validado; então desligar
   a sincronização do Urbe. Pages/PWA antiga permanece como recuperação. Migrar
   URL/escopo do service worker em item próprio, com preservação de dados.

A chave e o canal de atualização são bloqueios reais; pipeline existente não é
prova de publicação nem atualização automática.

## Novo Product

Declare identidade e versão no manifest, comando de teste, workflow de build
seletivo, política de publicação e assinatura. Registre canal no Distribution
Profile e consumidores. Use prefixo <id>-v e assets/checksums próprios. Se sua
release deve sair após merge automático, acrescente dispatch seletivo ao integrador
com teste; pipelines por tag não recebem dispatch de publicação em main.

## Conferência no Android

Baixar APK direto do Lunet, conferir versão e assinatura, atualizar por cima do
Lunet existente sem desinstalar; reabrir um projeto, editar, executar e importar ZIP.
Esta validação humana está pendente e não é substituída pelo CI.

O perfil current declara o pipeline/canal direto do Lunet habilitado por este PR;
sem release publicada, portal e Hub mostram indisponibilidade. Isso não afirma
que um APK já foi publicado ou validado. Urbe permanece no canal antigo até P4-9.

Rastreabilidade da tarefa: **P4-10**, Issue #120, handoff
`HO-20261003-releases-diretas`. O ID P4-8 foi usado por engano na
implementação/PR #123 e na resposta #126; esses registros históricos
permanecem preservados. P4-8 identifica Patch Notes (Issue #119).
Reconciliação: P4-11, Issue #128. P4-9 depende de P4-10.
