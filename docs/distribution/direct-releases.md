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

## Urbe: corte direto, sem espelho

DEC-0039 substitui a exigência de versão-ponte e de preservar a assinatura legada para o corte do Urbe. O proprietário escolheu deliberadamente **backup/export da cidade + reinstalação** e abandono de `AbnerCruz/Urbe` como canal ativo.

Estado operacional:

1. código canônico: `apps/urbe`;
2. Web/PWA atual: `https://abnercruz.github.io/Ecosystem/urbe/`;
3. releases Android/Windows: GitHub Releases do `AbnerCruz/Ecosystem`, tags `urbe-v<versão>`;
4. updater do app novo: Ecosystem, filtrado por tag/asset do Product; nunca `/releases/latest` global;
5. repositório `AbnerCruz/Urbe`: histórico congelado, sem `sync-from-ecosystem` e sem novas releases.

O primeiro release direto pode ser `1.8.3-beta`; ele não precisa chegar ao canal antigo. No Android, a migração desde a instalação legada é deliberadamente incompatível com atualização in-place: faça backup/export da cidade, desinstale a versão antiga, instale o APK direto e restaure o backup.

### Assinatura Android do canal beta direto

O canal beta usa `apps/urbe/tools/urbe-dev.keystore`, chave **PÚBLICA** e estável, exclusiva do Urbe no Ecosystem. Ela serve para continuidade técnica entre builds beta diretos, não para provar autoria.

- alias: `urbe`;
- senha pública do canal de desenvolvimento: `urbe-dev`;
- certificado SHA-256: `19:D9:49:32:17:4A:E7:90:82:EC:52:4C:BC:42:AD:48:A7:13:4F:AC:46:82:38:7A:50:68:15:EF:E6:5E:6C:F3`;
- hash SHA-256 do keystore: `36a597cd0f276c6118a5bc4572eb6920880c7c80891feffcfde0f795e2ccf97b`.

O CI verifica os dois hashes antes de construir e reconfere o certificado do APK. Como a chave é pública, este canal deve continuar identificado como beta/desenvolvimento; uma futura identidade estável privada exige item/decisão próprios.

### Publicação

`urbe-cutover.yml` agora possui somente o corte direto. O proprietário fornece a versão exata atual; o workflow cria a tag imutável `urbe-v<versão>` na `main` e dispara `urbe-release.yml`. Não há etapa `legacy-bridge`, tag `urbe/v*` nem chamada ao repositório antigo.

Depois da primeira release direta, validar em DEVICE:

- backup/export feito antes de remover a instalação legada;
- APK direto instala após a desinstalação deliberada;
- backup restaura a mesma cidade/vault e permanece editável;
- fechar/reabrir e funcionamento offline do domínio essencial;
- atualização posterior entre **duas builds diretas** mantém a assinatura beta e funciona sem reinstalação;
- Web/PWA abre na URL do Ecosystem.


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

O perfil `current` usa `locationFrom: ecosystem.repository` para os canais GitHub Releases já reais do Lunet e do Hub, derivando a localização de `ecosystem.repository` em `ecosystem.json` sem copiar URL. O mesmo locator será usado pelo canal de releases do Urbe somente depois que uma release `urbe-v*` existir e for validada. Urbe permanece no canal antigo até P4-9.

Rastreabilidade da tarefa: **P4-10**, Issue #120, handoff
`HO-20261003-releases-diretas`. O ID P4-8 foi usado por engano na
implementação/PR #123 e na resposta #126; esses registros históricos
permanecem preservados. P4-8 identifica Patch Notes (Issue #119).
Reconciliação: P4-11, Issue #128. P4-9 depende de P4-10.
