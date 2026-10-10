# Fontes TrueType no Lunet — LUNET-430

O Lunet converte arquivos **.ttf** para um atlas de textura RGBA usando C# portátil. O jogo desenha com `SpriteBatch.DrawString` e mede com `SpriteFont.Measure`, sem consultar serviços de rede nem carregar fontes do sistema. Coloque sua fonte licenciada em `Content/Fonts/ui.ttf`. A fonte e a textura são preparadas uma única vez quando carregadas pelo `ContentManager`.

Cole em um projeto em branco com o arquivo `Content/Fonts/ui.ttf` disponível:

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class TrueTypeDemo : Game
{
    private SpriteBatch batch = null!;
    private TrueTypeFont font = null!;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = Content.LoadTrueTypeFont("Fonts/ui.ttf", 28);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(16, 22, 35));
        batch.Begin();
        batch.DrawString(font.Font, "MISSÕES E AÇÕES", new Vector2(24, 70), Color.White);
        batch.DrawString(font.Font, "Olá, mundo! 123", new Vector2(24, 128), Color.Yellow);
        batch.End();
    }
}
```

O resultado de `LoadTrueTypeFont` é cacheado por arquivo, tamanho, conjunto de caracteres e tamanho do atlas. Ele pertence ao `ContentManager`, que libera a textura ao encerrar o jogo. Para fontes geradas de bytes obtidos de qualquer outra origem, use `using var baked = TrueTypeFont.Bake(device, ttfBytes, 28)` e controle o `Dispose` você mesmo.

O conjunto padrão contém ASCII, acentuação do português e sinais comuns; para economizar memória ou incluir outro alfabeto, use `Content.LoadTrueTypeFont("Fonts/ui.ttf", 28, "ABCabc012éç漢字?", 1024)`. Todo escalar sem glifo na fonte (ou fora do conjunto pré-rasterizado) usa `?`. O conjunto é limitado a 1024 caracteres, o arquivo a 8 MiB, a altura a 4–192 px e o atlas a 128–2048 px. Se não couberem todos os glifos, o carregamento falha explicitamente; reduza o conjunto ou aumente o atlas antes de executar o jogo.

Atualmente o suporte não cobre kerning entre letras, formação de ligaduras, shaping árabe/complexo, texto bidirecional, fontes variáveis, glifos coloridos nem CFF/OTF. Estes itens exigem pipeline tipográfico posterior. `SpriteFont.CreateDefault` e `SpriteFont.FromBitmap` não mudaram. Nenhum projeto existente precisa ser migrado.

## Teste no Android

1. Acrescente um arquivo .ttf ao Content do projeto, execute o exemplo e confirme a fonte, acentos e números no Preview.
2. Compare "iiii" com "WWWW": a largura deve ser diferente; nenhuma letra deve aparecer como bloco ilegível.
3. Troque o tamanho de 28 para 18 e 44 e confirme que o texto permanece estável.
4. Pause, retome, saia e abra outro projeto: sem texturas antigas ou crashes.
5. Teste uma fonte pequena e atlas de 128 para conferir erro legível quando não couber.
6. Execute um jogo antigo que usa fonte bitmap e confirme ausência de regressão.

Registre versão do APK, sucesso/falha e evidências visuais. CI verde prova compilação/testes portáteis, mas não legibilidade ou fluxo de importação no aparelho.
