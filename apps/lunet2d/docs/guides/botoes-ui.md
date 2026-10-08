# Botões de UI por toque

Crie um projeto **Em branco**, substitua Game.cs pelo código inteiro e toque em Run. **Somar** muda de cor enquanto é pressionado e só soma quando o mesmo dedo é solto dentro do botão. Arraste para fora e solte: não soma. Voltar para dentro antes de soltar confirma o clique.

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class ButtonDemo : Game
{
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    readonly TouchButton action = new(new RectangleF(24, 160, 312, 72));
    readonly TouchButton toggle = new(new RectangleF(24, 256, 312, 72));
    readonly TouchButton palette = new(new RectangleF(24, 352, 312, 72));
    readonly TouchButtonStyle amber = new(new Color(120, 65, 20), new Color(210, 130, 25), new Color(55, 50, 45), Color.White, new Color(145, 135, 120));
    bool warm;
    int clicks;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
    }

    protected override void Update(GameTime time)
    {
        action.Update(Input);
        toggle.Update(Input);
        palette.Update(Input);
        if (action.WasClicked) clicks++;
        if (toggle.WasClicked) action.IsEnabled = !action.IsEnabled;
        if (palette.WasClicked) warm = !warm;
    }

    protected override void OnPause()
    {
        action.Cancel(); toggle.Cancel(); palette.Cancel();
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(18, 22, 35));
        var style = warm ? amber : TouchButtonStyle.Default;
        batch.Begin();
        batch.DrawString(font, "Botoes de UI", new Vector2(24, 32), Color.White, 2);
        batch.DrawString(font, "Cliques: " + clicks, new Vector2(24, 88), Color.White, 2);
        action.Draw(batch, font, action.IsEnabled ? "Somar" : "Desabilitado", style, 2);
        toggle.Draw(batch, font, "Habilitar/desabilitar", style, 2);
        palette.Draw(batch, font, "Trocar cores", style, 2);
        batch.DrawString(font, "Solte dentro para clicar", new Vector2(24, 480), Color.White, 2);
        batch.End();
    }
}
```

## Usar no jogo

Crie um `TouchButton` por controle e atualize uma vez por passo de Update, inclusive quando desabilitado. `WasClicked` é um pulso do Update que recebeu Released dentro de Bounds; o próximo Update o limpa. `IsCaptured` indica que o botão acompanha um dedo, inclusive fora dos limites. `IsPressed` indica que esse dedo está dentro, útil para desenhar feedback. Alterar IsEnabled para false cancela imediatamente sem clique.

O controle só captura uma fase Pressed dentro dos limites; entrar arrastando um dedo que começou fora não ativa. O ID capturado não é trocado por um segundo dedo. Cancelled, desaparecimento do dedo ou posição não finita cancelam sem clique. Fases repetidas de Pressed/Released em passos fixos não geram cliques repetidos. Voltar de fora para dentro restaura o estado pressionado; o clique depende da posição final de soltura.

`Bounds` é um RectangleF finito, de tamanho não negativo, no mesmo espaço do input. Pode ser atualizado antes de Update com o resultado de um layout; os limites atuais também valem para a soltura. Área zero não recebe toque nem desenha. O host converte pixels físicos para coordenadas virtuais. Para UI no mundo, transforme os toques para o mesmo espaço ou mantenha o botão no HUD. Câmera/clip do SpriteBatch afetam o desenho, mas não transformam automaticamente o input.

`Draw` deve estar entre SpriteBatch.Begin/End. Ele reutiliza a fonte do jogo, desenha fundo e texto centralizado, e usa TouchButtonStyle.Default ou uma paleta própria. `default(TouchButtonStyle)` é transparente. Não há ajuste automático de texto, wrap, recorte ou nine-slice; escolha tamanho/escala adequados, ou desenhe você mesmo com os estados do botão. A fonte é propriedade do jogo/Content, não do controle.

Chame `Cancel()` em OnPause, ao ocultar um painel ou trocar de tela. Para controles ocultos, cancele e mantenha Update (com IsEnabled false) se precisar acompanhar os dedos: o histórico impede que reabilitar capture um Pressed repetido do mesmo dedo. Uma instância recém-criada não conhece snapshots anteriores ao seu primeiro Update.

Os controles não consomem input nem resolvem sobreposição/z-order: dois botões sobrepostos podem capturar o mesmo dedo. Mantenha áreas separadas ou escolha explicitamente qual controle atualizar. Os dados vêm dos snapshots existentes do host; um ciclo inteiro de toque substituído entre dois Updates não é reconstruído. Não há foco, teclado/gamepad, árvore de UI ou sistema global de temas nesta entrega. VirtualButton continua apropriado para ação contínua de jogo; TouchButton confirma uma ação de UI na soltura.

Update/Draw não alocam por chamada após criar o botão e os recursos gráficos. O HUD do exemplo concatena texto por quadro e aloca; não é essa a garantia medida.

## Roteiro Android intermediário

1. Execute offline no Preview rápido. Segure Somar: o fundo muda; o contador não muda. Solte dentro: soma exatamente uma vez.
2. Comece em Somar, arraste para fora e solte: não soma. Repita, volte para dentro e solte: soma uma vez. Comece fora e arraste para dentro: não soma.
3. Segure Somar com um dedo e use outro em Trocar cores. O botão mantém o primeiro dedo; trocar a paleta não soma. Soltar o segundo dedo não dispara Somar.
4. Desabilite Somar. Tocar nele não soma; ele usa as cores desabilitadas. Reabilite e confirme um clique novo.
5. Segure Somar e mande o app para segundo plano. Volte: a interação antiga foi cancelada e não soma ao soltar. Um novo toque funciona.
6. Pare e repita no Preview isolado. Rode também um projeto anterior para regressão.

Registre passou/falhou/não testado, versão do APK, modelo/Android e modo de Preview. Os testes portáteis não comprovam aparência GL nem lifecycle no aparelho.
