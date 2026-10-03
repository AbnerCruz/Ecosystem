# Instalar, atualizar e abrir pelo Hub — P4-3

Autoridades: ADR-0018 / DEC-0030-A. Mapeamento candidato local e explícito em
`apps/hub/config/android-products.json`; não substitui a identidade de build do
Product. Pacote do Lunet vem de seu csproj; fingerprint é conferido por seu pipeline.
**DEC-0032 está pendente:** a política A exige aprovar esses valores específicos.
Sem o registro canônico da alternativa A exata, o fluxo permanece bloqueado.
O Urbe não tem certificado confirmado neste mapeamento e usa seu canal independente.

## Fluxo e limites

Selecione APK, baixe e confira, depois toque em Instalar/Atualizar. Uma confirmação
mostra pacote, release, versão instalada e condição de desenvolvimento. Se a fonte
não estiver permitida, o Hub oferece abrir as configurações Android. Voltar não
retoma instalação: toque novamente, repetindo as conferências. Negar bloqueia.

Antes de preparar e imediatamente antes do commit, o Hub consulta aprovação e
canal atuais: não usa aprovação do cache nem inventa permissão offline. Reconfere
bytes em cópia privada e nos bytes copiados à sessão; analisa pacote/versionCode
/signatário atual e consulta o app instalado. Assinantes múltiplos/rotação ainda
não aprovada, assinante incompatível, versão igual/anterior, metadados ausentes,
permissão revogada, erro de rede/disco e bytes alterados bloqueiam ou abandonam.

PackageInstaller.Session exige consentimento Android; REQUEST_INSTALL_PACKAGES
é restrita à UI. `package.install` e `package.open` são concessões separadas,
sem capability pública ou ferramenta de agente. Não há QUERY_ALL_PACKAGES, root,
Device Owner, desinstalação, exportação de APK, acesso a vault/projeto ou comandos
arbitrários. Visibilidade só para `io.lunet.studio`; essa visibilidade não é confiança.

Resultado só é confirmado com callback da sessão/operação correta e consulta real
do pacote/versionCode/certificado instalado. Duplicatas ou callbacks antigos não
confirmam uma operação diferente. Ao retornar, o Hub reconcilia a própria sessão;
se o resultado desapareceu, consulta a versão sem alegar sucesso. Cancelar abandona
somente a sessão própria, não remove apps/dados. Não há retomada silenciosa após
processo morto. Preparação é cancelável e limitada a cinco minutos/128 MiB.

Abrir consulta novamente aprovação, pacote/signatário e launcher pelo Android,
somente após toque. Sem aprovação atual, rede, pacote acessível ou launcher, mostra
indisponibilidade. Products continuam funcionando e sendo distribuídos sem o Hub.
Chave pública de desenvolvimento limita substituições acidentais, mas **não prova autoria**.

## Conferência em aparelho (após aprovação específica e integração)

1. Instale o APK Hub candidato (artifact hub-debug-apk do CI do PR; depois release
   de desenvolvimento). Preserve um projeto de teste/backup do Lunet.
2. Antes de DEC-0032-A, confirme bloqueio de instalar/abrir, com download independente
   preservado. Aprovar essa decisão não aprova esta validação DEVICE.
3. Após aprovação, atualize o Hub com rede; selecione Lunet. Consulte a versão real.
   Baixe um APK posterior à instalação atual: versão igual/anterior deve ser recusada.
4. Negue a permissão de fonte. Confirme ausência de instalação/retomada automática;
   conceda por ação explícita nas configurações e volte. Toque de novo para continuar.
5. Cancele na confirmação do Hub e no instalador Android. Nenhum app/dado é removido.
   Aceite outra tentativa; confirme versão/package/signatário pelo sistema e reabra
   o projeto de teste, sem reinstalação destrutiva.
6. Durante a preparação, toque Cancelar instalação. Repita recriando a Activity e
   encerrando o processo antes/depois do envio ao Android: sem sucesso fictício ou
   retomada automática. Volte e confira sessão/versão reais; cancele sessão pendente.
7. Toque Abrir; confirme Lunet. Offline/assinante incompatível/pin pendente bloqueiam
   ações do Hub. O app instalado continua funcionando standalone sem Hub.
8. Urbe sem pin continua sem instalação/abertura via Hub; canal independente acessível.

P4-3 fica em review até evidência em aparelho. P4-4 é gate separado por Product e
caminho realmente disponível; esta implementação não aprova o corte do Urbe.
