# Leia e corrija um erro no editor

## Erro de compilação

No primeiro tutorial, remova o ponto e vírgula de `GraphicsDevice.Clear(Color.Black);`. Toque em **Rodar**. Em **Erros**, procure `CS1002` (esperado ponto e vírgula). Toque no cartão para abrir o arquivo na linha indicada. Restaure o ponto e vírgula, rode de novo e confira o Preview.

Se houver vários avisos, comece pelo primeiro erro. Um caractere ausente pode gerar mensagens nas linhas seguintes. Se aparecer `CS0103`, confira a grafia, o escopo da variável e os `using`. Para `CS0246`, confirme o nome do tipo e o namespace na Referência de API.

## Erro durante o jogo

Se a compilação passou, mas o jogo falhou ao iniciar ou durante a partida, abra **Console**. Procure o erro que ocorreu primeiro. Para falha de textura, confira o caminho relativo em `Content` e se o arquivo realmente é PNG.

[Gere e instale o APK do jogo](guide:instalar-apk)
