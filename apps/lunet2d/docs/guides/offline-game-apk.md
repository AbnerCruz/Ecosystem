# APK offline do Lunet

O botão **Arquivo → Gerar APK do jogo** deve produzir o APK diretamente no dispositivo, sem exigir GitHub, token, conta, conexão com a internet ou SDK Android instalado pelo usuário. O código é compilado pelo Roslyn do Lunet; o host Android já é distribuído junto com a IDE.

O projeto conserva o `GameId` e `PackageId` do `lunet.json`. O APK separado contém o código e recursos de `Content/` e `Assets/`. A assinatura individual do jogo fica no Android Keystore do Lunet. Desinstalar a IDE pode remover a chave e impedir atualizar os jogos já instalados sem desinstalá-los; a assinatura ainda é de desenvolvimento.

## Validação necessária

Executar sem internet: gerar e instalar APK do Bosque Lumi, abrir jogo, jogar com toque, verificar áudio e recursos; recompilar uma mudança e atualizar o APK sem perder dados. O CI deve verificar assinatura com `apksigner verify` e `aapt dump badging` do pacote solicitado. Validar e relatar falhas de compilação sem perda de arquivos.
