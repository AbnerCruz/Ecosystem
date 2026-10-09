# UI rolável por toque — LUNET-427

O framework oferece `Lunet.UI.TouchScrollArea` para listas verticais de inventário, missões e configurações. O estado da lista fica no jogo; nenhum arquivo de projeto é alterado. Use coordenadas virtuais, com `Update(Input, (float)time.DeltaSeconds)` em cada passo fixo.

Cole no `Game.cs` de um projeto Em branco:

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class ScrollDemo : Game
{
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private TouchScrollArea scroll = null!;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        scroll = new TouchScrollArea(new RectangleF(16, 80, 328, 420), 1000);
    }

    protected override void Update(GameTime time)
    {
        scroll.Update(Input, (float)time.DeltaSeconds);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(17, 20, 30));
        batch.Begin();
        batch.DrawString(font, "ARRASTE PARA ROLAR", new Vector2(16, 32), Color.White, 2);
        batch.End();

        // Begin usa a mesma área do controle para recorte no OpenGL ES.
        batch.Begin(clip: scroll.Bounds);
        for (int i = 0; i < 20; i++)
        {
            float y = scroll.Bounds.Y + 12 + i * 50 - scroll.OffsetY;
            batch.FillRect(new RectangleF(24, y, 290, 42), new Color(40, 55, 85));
            batch.DrawString(font, "MISSAO " + i, new Vector2(38, y + 15), Color.White);
        }
        batch.End();

        // Indicador de posição visual; sem textura nova, recorte ou toque próprio.
        var thumb = scroll.GetThumbBounds();
        if (thumb.Width > 0)
        {
            batch.Begin();
            batch.FillRect(thumb, new Color(230, 238, 248));
            batch.End();
        }
    }
}
```

O primeiro toque dentro de Bounds captura o dedo, mas deslocamentos menores que 6 unidades não rolam. Arrastar move a lista e soltar aplica inércia amortecida até o limite. `ScrollTo(0)` retorna ao topo; redimensionar Bounds ou alterar ContentHeight limita OffsetY. `Cancel()` ou `IsEnabled = false` param captura e inércia sem apagar o deslocamento.

O controle **não** implementa árvore de UI, roteamento automático de toque ou scroll horizontal. Ao combinar botões dentro da área, atualize primeiro a área; não confirme clique enquanto `IsDragging` for true nem no quadro com `WasDragged`. Os toques não são consumidos automaticamente; o jogo define a prioridade. Sem clipping com `SpriteBatch.Begin(clip: scroll.Bounds)`, o conteúdo pode sair dos limites visuais.

## Teste no Android

1. Execute em modo offline o exemplo no Preview rápido e arraste do rodapé para o topo; a lista deve avançar e nunca ultrapassar a última missão.
2. Solte em movimento: a lista continua por um instante e desacelera. Toque brevemente: não deve rolar.
3. Tente dois dedos; o dedo capturado continua controlando o painel até soltar ou cancelar.
4. Repita no Preview isolado, pause e reinicie. Confira o recorte da lista e a posição no aparelho.
5. Valide a API em projetos anteriores: abrir/editar/Run e controles existentes sem regressão.

Registre passou/falhou/não testado e a versão do APK. CI não valida inércia percebida, latência ou precisão real do toque.

A suíte agora compila este exemplo pelo compilador C# embutido e o executa no GameHost com toque sintético, além de verificar fases inválidas (não permite movimento nem inércia). Isso não substitui a validação visual do recorte e da ergonomia no Android.
