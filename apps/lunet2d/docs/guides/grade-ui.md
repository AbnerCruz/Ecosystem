# Grade responsiva para inventários e menus — LUNET-433

`UiGridLayout` organiza itens em linhas/colunas a partir da largura disponível. Uma tela estreita reduz o número de colunas; no landscape as células crescem ou entram mais colunas. A altura de cada célula permanece fixa para favorecer o toque. Use o mesmo retângulo para desenho e entrada, e `GraphicsDevice.SafeArea` como pai.

Exemplo completo sem arquivos extras: crie um projeto em branco e use este `Game.cs`:

```csharp
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class InventoryGridDemo : Game
{
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private readonly UiGridLayout grid = new(80, 52, spacing: 8, padding: 12, maxColumns: 5);
    private readonly RectangleF[] cells = new RectangleF[12];
    private readonly TouchButton[] buttons = new TouchButton[12];
    private static readonly string[] Labels =
    {
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12"
    };

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i] = new TouchButton(default);
    }

    protected override void Update(GameTime time)
    {
        grid.Arrange(GraphicsDevice.SafeArea, cells.Length, cells);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Bounds = cells[i];
            buttons[i].Update(Input);
            if (buttons[i].WasClicked) Log.Info("Item " + Labels[i]);
        }
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(UiTheme.Dark.Background);
        batch.Begin(clip: GraphicsDevice.SafeArea);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].Draw(batch, font, Labels[i], UiTheme.Dark.Button, 2);
        batch.End();
    }
}
```

`new UiGridLayout(minimumCellWidth, cellHeight, spacing, padding, maxColumns)` valida os parâmetros; `maxColumns: 0` remove o teto. `Arrange(area, itemCount, results)` escreve só nas posições de `results` correspondentes a itens e devolve o número de colunas. Calcule `results` uma vez, antes do input e do desenho. Nenhum array é criado por `Arrange` e a geometria anterior não é armazenada. Use `GetContentHeight(area, itemCount)` para ajustar `TouchScrollArea.ContentHeight` em inventários com muitas linhas; subtraia `OffsetY` das posições desenhadas, aplique clipping e configure a captura de toque sem sobreposição.

Para listas extensas (centenas ou milhões de entradas), evite criar botões ou buffers para toda a lista: use `GetVisibleRange(area, itemCount, scroll.OffsetY, out first, out endExclusive)` e percorra apenas `first..endExclusive`. `GetCellBounds(area, itemCount, i)` retorna a célula individual sem organizar as anteriores; subtraia o deslocamento vertical de scroll antes de desenhar ou configurar os Bounds dos botões reutilizáveis. Células visíveis parcialmente estão incluídas. Nos intervalos vazios (como o espaço entre duas linhas), ambos os índices são zero. O cálculo é O(1) e não cria objetos, mesmo com milhões de itens. Em botões reutilizáveis, cancele captura antiga ao reatribuir uma célula diferente; não reutilize o dedo de uma linha anterior.

Na largura menor que uma célula, a grade usa uma única coluna e pode reduzir sua largura até zero. Quando há overflow vertical, a grade não corta os itens nem rola automaticamente: combine com uma área de rolagem. `Padding` horizontal é limitado a metade da largura; padding vertical é mantido no conteúdo. Os limites são validados para não produzir infinito; em overflow ocorre exceção antes de modificar resultados.

## Verificação no aparelho

1. Execute o exemplo offline no Preview; confirme doze botões numerados, sem sobreposição.
2. Alterne retrato/paisagem ou redimensione o Preview; a distribuição deve acompanhar o novo espaço.
3. Toque em vários botões; cada toque confirmado deve criar um único log. Arrastar depois de relayout não pode acionar botão anterior.
4. Experimente `maxColumns: 2`; a grade deve ocupar só duas colunas mesmo em paisagem.
5. Use vinte ou mais itens com `TouchScrollArea`, confirmando cálculo de altura, rolagem e clipping.
6. Reinicie o jogo e execute outro projeto antigo para verificar ausência de regressão.

A compilação e os testes automatizados não substituem a validação visual e de toque no Android.
