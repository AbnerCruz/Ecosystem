# APK offline do Lunet

O botão **Arquivo → Gerar APK do jogo** deve produzir o APK diretamente no dispositivo, sem exigir GitHub, token, conta, conexão com a internet ou SDK Android instalado pelo usuário. O código é compilado pelo Roslyn do Lunet; o host Android já é distribuído junto com a IDE.

O projeto conserva o `GameId` e `PackageId` do `lunet.json`. O APK separado contém o código e recursos de `Content/` e `Assets/`. A assinatura individual do jogo fica no Android Keystore do Lunet. Desinstalar a IDE pode remover a chave e impedir atualizar os jogos já instalados sem desinstalá-los; a assinatura ainda é de desenvolvimento.

## Validação necessária

Executar sem internet: gerar e instalar APK do Bosque Lumi, abrir jogo, jogar com toque, verificar áudio e recursos; recompilar uma mudança e atualizar o APK sem perder dados. O CI deve verificar assinatura com `apksigner verify` e `aapt dump badging` do pacote solicitado. Validar e relatar falhas de compilação sem perda de arquivos.

## Defeito de instalação Android 11+ — correção 2026-10-27

Relato real: o APK exportado pelo Lunet não instala, mesmo após a geração e a assinatura passarem no CI. A análise do `OfflineGameApkBuilder.WriteStoredEntry` localizou ausência de alinhamento dos dados no ZIP. O Android rejeita `resources.arsc` quando armazenado comprimido ou desalinhado em APKs com `targetSdkVersion >= 30` (erro possível: `INSTALL_PARSE_FAILED_RESOURCES_ARSC_COMPRESSED`, código -124).

A correção grava cada entrada ZIP nova como STORED e acrescenta um campo ZIP extra local válido para alinhar os dados em 4 bytes **antes** da assinatura APK v2. Não aplicar `zipalign` depois de assinar: isso invalidaria a assinatura.

**Testes antes do código:** `OfflineApkZipAlignmentTests` exercita 8 offsets por entrada, incluindo `resources.arsc` e `Game.dll`. O CI roda `zipalign -c -v 4`, `apksigner verify` e `aapt dump badging` no mesmo APK exportado pelo empacotador. Um smoke adicional instala esse APK com `adb install -r` num emulador Android 13 x86_64; o release da IDE preserva seu template ARM64. Esses testes não substituem o DEVICE físico.

**Prova em aparelho:** atualizar o Lunet sem desinstalar e sem apagar projetos; exportar Bosque Lumi sem internet; conferir se o Android instala o novo APK; abrir e testar toque, sprites e áudio; exportar novamente com mesmo `GameId`/`PackageId` e atualizar o jogo instalado. Havendo falha, registrar mensagem completa do instalador e resultado de `adb install -r arquivo.apk` quando disponível. Caso seja `INSTALL_FAILED_UPDATE_INCOMPATIBLE`, verificar se existe uma instalação anterior do **jogo** com certificado diferente; não apagar dados automaticamente nem trocar a chave.

Fontes: https://developer.android.com/about/versions/11/behavior-changes-11 ; https://developer.android.com/tools/zipalign ; https://android.googlesource.com/platform/frameworks/base.git/+/425ec7a92099817e0791dc26afca0f005efa8763/services/core/java/com/android/server/pm/pkg/parsing/ParsingPackageUtils.java

## Artefato Standalone Smoke do CI

O `lunet-standalone-smoke-game` é um APK de diagnóstico gerado pelo workflow `lunet2d-game-apk-smoke`, **não** o APK de um projeto exportado pela IDE. A assinatura desse artefato é debug e efêmera a cada execução do CI. Historicamente todas as execuções usavam o mesmo `ApplicationId` (`io.lunet.game.smoke`), o que podia causar `INSTALL_FAILED_UPDATE_INCOMPATIBLE` ao instalar uma execução sobre outra já instalada. A partir da correção, cada execução do smoke recebe identificador próprio `io.lunet.game.smoke.run<GitHub run_id>`. Isso evita o conflito sem distribuir a chave de assinatura do Lunet nos jogos e sem requerer desinstalar nada do usuário. Instalações antigas podem ser removidas manualmente, se desejado.

O teste de assinatura/alinhamento de cada artefato é executado também nesse workflow. Para uma atualização persistente do seu **próprio jogo**, mantenha o mesmo projeto (`GameId`/`PackageId`) e preserve o Keystore do Lunet; a identidade exclusiva por execução se aplica somente ao projeto de teste descartável.
