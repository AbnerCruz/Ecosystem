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

## Identidade de vários jogos e orientação (2026-10-27)

Validação no aparelho: dois jogos diferentes foram exportados e executados, mas o segundo só instalava após excluir o primeiro; o jogo horizontal abriu vertical. A inspeção encontrou dois defeitos reais: o ID Android era gerado apenas por normalização do nome (com colisões possíveis) e a Activity standalone tinha `ScreenOrientation.Portrait` fixo, ignorando `orientation` em `lunet.json`.

**Novo projeto:** cada criação recebe um `GameId` próprio e `PackageId` com sufixo derivado do GUID, mesmo quando dois nomes normalizam para o mesmo texto. O formulário Novo projeto permite Vertical (9:16) ou Horizontal (16:9), gravado em `orientation` e dimensões virtuais no mesmo manifesto.

**Importação/restauração:** um ZIP importado em uma biblioteca sem conflito preserva `GameId` e `PackageId`, para permitir atualizar o mesmo jogo. Importar um projeto que colide com outra identidade instalada na biblioteca cria uma cópia independente com IDs próprios e mantém o original intacto. Projetos legados que já estão na biblioteca com ID duplicado recebem aviso e ação explícita **Separar e gerar APK**; nenhum projeto é alterado silenciosamente.

**Orientação do jogo existente:** no editor, Arquivo → Orientação do jogo → Vertical/Horizontal. A escolha persiste em `lunet.json`; o builder altera o atributo numérico `android:screenOrientation` no manifesto binário AXML antes de assinar. O app exportado é iniciado pelo Android na orientação pedida, sem forçar `setRequestedOrientation` durante `onCreate`. Em telas grandes/versões novas, restrições do Android podem ser ignoradas; jogos devem continuar adaptáveis.

**Gates:** testes portáteis de IDs de nomes com colisão, cópias ZIP, restauração e edição AXML tipada; CI gera dois APKs independentes (portrait/landscape), valida com `aapt dump xmltree`, `apksigner`, `zipalign`, instala ambos no mesmo Android 13 emulado sem desinstalar nenhum. Verificação DEVICE: exportar dois projetos diferentes com o Lunet atualizado, instalar ambos, abrir, comprovar orientação; recompilar um e atualizar somente ele mantendo o outro intacto. A confirmação em aparelho físico permanece separada.

Fontes: https://developer.android.com/guide/topics/manifest/manifest-element ; https://developer.android.com/guide/topics/manifest/activity-element ; https://developer.android.com/guide/practices/device-compatibility-mode .

### Causa adicional comprovada no emulador — conflito MonoRuntimeProvider

A tentativa de instalar dois APKs de jogos independentes no mesmo Android 13 retornou exatamente:
`INSTALL_FAILED_CONFLICTING_PROVIDER: ... io.lunet.game.smoke.mono.MonoRuntimeProvider.__mono_init__ ... already used by io.lunet.game.offline.portrait`.

A identidade do aplicativo (`package`) já era diferente; faltava reescrever a autoridade do `mono.MonoRuntimeProvider`, inserida pelo .NET Android para a inicialização do runtime. O gerador agora modifica também a string `io.lunet.game.smoke.mono.MonoRuntimeProvider.__mono_init__` para `<PackageId>.mono.MonoRuntimeProvider.__mono_init__` no manifesto AXML, antes de assinar. O CI compara as duas autoridades com `aapt dump xmltree` e exige que ambos os APKs instalem lado a lado. Não remover o provider: ele é necessário ao startup .NET.

Referências: https://github.com/dotnet/android/wiki/Blueprint ; https://developer.android.com/guide/topics/manifest/provider-element .
