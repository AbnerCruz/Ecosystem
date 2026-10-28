# LUNET-450 — Testes da jornada de criação ao APK instalado

Issue: https://github.com/AbnerCruz/Ecosystem/issues/834

## Escopo automatizado

O teste `ProjectJourneyTests.CreateEditAssetsPreviewExportAndReopen`, integrado à suíte xUnit do Lunet, exercita as **implementações reais**: `ProjectStore`, `SpriteDocument`, `TileStudioSession`, `WaveAudioDocument`, `GameCompiler` (Roslyn), `GameLoader`, `GameHost`, `ProjectAssetCatalog` e `ExportZip`.

1. Cria o projeto `JourneyPortrait` via `ProjectStore.Create`, preservando manifesto/identidade/orientação.
2. Edita a fonte `.sprite.json`, associa PNG válido RGBA, pinta um tile e gera JSON v1; importa WAV.
3. Modifica o `Game.cs` recém-criado para carregar a textura e o tilemap do projeto, mudar a cor de fundo e escrever um log exclusivo da jornada.
4. Fecha/reabre a pasta, compila as fontes **editadas** com o compilador real, instancia o jogo e executa o Preview headless com backend registrador. Falha diante de erro de compilação, runtime, PNG, tilemap ou ausência do log.
5. Exporta o projeto como ZIP e verifica `lunet.json`, C#, sprite, PNG, tilemap e áudio.
6. Com `LUNET_JOURNEY_OUTPUT` definido, mantém o mesmo projeto editado para o CI.

No job `apk` do workflow `.github/workflows/lunet2d-release.yml`, o projeto produzido pelo teste é recompilado pelo `runtime-template/Lunet.GameCode.csproj`; `tools/offline-apk-check` o envia ao **`OfflineGameApkBuilder.Build` de produção**, com seus recursos exatos. O teste checa os bytes de todos os recursos empacotados, IDs, alinhamento ZIP, assinatura v2 e manifesto. A etapa de emulador Android 13 x86_64 instala, inicia por `am start -W`, confere processo ativo e aguarda o log originado pelo `Game.cs` editado depois de carregar o PNG e o mapa. Também captura `logcat`, resultado do launcher e tela em artifact `lunet-e2e-journey` (quando o CI alcançar essas etapas).

O teste novo foi escrito no commit anterior à extensão do empacotador de CI e ao workflow, preservando a ordem *testes antes do código*.

## O que essa automação NÃO comprova

- Não toca a interface da IDE no emulador nem desenha pixels com gestos reais; edita fontes por meio das mesmas classes de domínio. A exportação pelo botão **Gerar APK do jogo** no dispositivo continua exigindo DEVICE.
- O PNG mínimo de teste é um fixture estático associado à fonte Sprite Studio; não demonstra que o encoder PNG Android está correto. A suíte de Sprite Studio tem sua própria cobertura.
- O APK de CI usa assinatura RSA **efêmera**, jamais o Keystore do usuário. Logo não certifica a continuidade da chave após reinstalar o Lunet.
- O emulador Android 13 não substitui aparelho físico (especialmente ABI ARM64, GPU, toque, permissões, atualização e orientação real). Não fechar F4/F5/F9 nem reportar aprovação humana a partir de CI.
- O script do workflow depende do GitHub Actions e do SDK no **CI**, não introduz rede ou SDK obrigatório no Lunet.

## Como executar os testes

Portable: `cd apps/lunet2d && dotnet test --project tests/Lunet.Tests`.

Em CI, também gerar o projeto: `cd apps/lunet2d && mkdir -p dist/journey && LUNET_JOURNEY_OUTPUT="$PWD/dist/journey" dotnet test --project tests/Lunet.Tests`.

A prova de APK/Android é feita pelo `lunet2d-release.yml` com Android SDK, Android Workload e emulador. Em caso de falha, baixar o artifact `lunet-e2e-journey` para consultar APK, ZIP, log e captura; ausência do artifact pode indicar falha anterior ao upload.

## Roteiro de aceitação no Android físico — pendente

Use uma release nova **quando** a integração e a publicação estiverem comprovadas. Sem desinstalar o Lunet, faça backup do projeto de teste:

1. Sem internet, crie um projeto `JornadaFísica` vertical e abra `Game.cs`. Edite a cor de fundo e salve. Execute o Preview; confirme que mudou e não ocorreu crash.
2. No Sprite Studio, desenhe um sprite de duas cores e salve. Verifique a fonte editável e o PNG nos Assets; referencie-o em `Game.cs` e rode o Preview.
3. No Tile Studio, crie um mapa e tileset no app, pinte e salve; carregue o mapa no código. Em Audio Studio gere/salve WAV no projeto.
4. Ative modo avião. Em Arquivo → Gerar APK do jogo, aguarde a exportação local. Instale e inicie sem abrir a IDE. Confirme sprite, mapa, áudio, toque e a cor editada.
5. Reabra o mesmo projeto na IDE, altere um detalhe, gere APK novamente, instale como atualização e confira que o jogo abre sem exigir desinstalar/perder dados.
6. Crie um segundo jogo em horizontal, exporte, instale **sem apagar** o primeiro e confira que ambos coexistem e a orientação horizontal é respeitada.
7. Reabra os projetos e cheque se códigos e assets permanecem intactos. Em falhas, anote mensagem do instalador, versão da release, Android, tela/log.

Registre **passou/falhou/não testado** por etapa na Issue e no handoff. CI verde nunca equivale a aceite físico.
