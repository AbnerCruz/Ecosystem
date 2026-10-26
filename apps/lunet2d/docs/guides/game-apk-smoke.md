# F9 — experimento de APK autônomo do jogo (#787)

O APK da IDE `Lunet.Android` não é o APK do jogo. Este experimento cria
um segundo projeto Android independente, com `Game.dll`, `Content/`,
`Assets/` e o renderizador real do Lunet, mas **sem editor, Roslyn ou Git**.

- Código de jogo de prova: `samples/apk-smoke/Game.cs`. O projeto `runtime-template/Lunet.GameCode.csproj` compila os fontes `.cs` de qualquer pasta de projeto como `CompiledGame.dll` (excluindo Tests/ e caches).
- Empacotador de teste: `runtime-template/Lunet.Game.Android.csproj`.
- Conteúdo: `runtime-template/PackagedContentSource.cs`, resolve `Content/`
  e permite `Content.LoadTexture("Assets/arquivo.png")` por fallback restrito.
- Host Android independente: `runtime-template/StandaloneGameActivity.cs`.
- CI: `lunet2d-game-apk-smoke` gera artifact `lunet-standalone-smoke-game` com APK e hash.

## Roteiro de validação

1. Confirmar no CI que compilação C#, build Android ARM64, assinatura
   de debug e presença de `game/Game.dll` e conteúdo no APK passaram.
2. Instalar o APK do artifact **sem instalar/abrir a IDE**.
3. Abrir o jogo: deve aparecer fundo azul e círculo amarelo.
4. Tocar/arrastar para mover o círculo, voltar, retomar o app.
5. Conferir que `io.lunet.game.smoke` não substitui `io.lunet.studio`.

## Ainda não entregue

O workflow usa um jogo fixo como prova, mas o compilador e o template aceitam `-p:GameSourceRoot=/caminho/do/projeto`, além de `-p:ApplicationId=io.exemplo.meujogo` no build Android, com ferramentas .NET/Android SDK instaladas. **Ainda não está conectado à UI da IDE.** Não implementa
ainda o botão "Gerar APK" de um projeto no aparelho, assinatura persistente
por jogo, empacotamento de ZIP enviado pelo usuário, compilação local offline,
AAB ou liberação do APK como release. Os passos posteriores dependem da prova
real do template no CI e DEVICE; não declarar o gate F9 concluído.

## Build de projeto por pasta

Com .NET 10 e workload Android disponíveis no computador/runner, compile os fontes da pasta do jogo com `dotnet build runtime-template/Lunet.GameCode.csproj -p:GameSourceRoot=/caminho/jogo`. Em seguida compile `runtime-template/Lunet.Game.Android.csproj` com o mesmo GameSourceRoot, `ApplicationId` exclusivo e `AndroidSdkDirectory` quando necessário. A assinatura debug atual é experimental e não substitui chave própria estável de um jogo publicado.
