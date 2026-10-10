# Inventário responsivo e tocável — LUNET-433

`UiGridLayout` organiza células com cálculos sem alocação por quadro; `TouchGridView` usa essa grade com `TouchScrollArea` para rolagem, seleção por dedo, recorte e desenho virtualizado. O jogo continua sendo dono dos dados e das texturas, e funciona offline em C#.

## Game.cs completo para testar no Preview

```csharp
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class InventoryGridDemo : Game
{
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private readonly TouchGridView inventory = new(
        new RectangleF(0, 0, 320, 450), 30,
        new UiGridLayout(80, 56, spacing: 8, padding: 12, maxColumns: 5));
    private static readonly string[] Labels =
    {
        "1","2","3","4","5","6","7","8","9","10",
        "11","12","13","14","15","16","17","18","19","20",
        "21","22","23","24","25","26","27","28","29","30"
    };

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
    }

    protected override void Update(GameTime time)
    {
        inventory.Bounds = GraphicsDevice.SafeArea;
        inventory.Update(Input, (float)time.DeltaSeconds);
        if (inventory.ActivatedIndex >= 0)
            Log.Info("Item " + Labels[inventory.ActivatedIndex]);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(UiTheme.Dark.Background);
        inventory.Draw(batch, font, Labels, UiTheme.Dark.Button, Color.Blue, 2);
    }
}
```

O toque somente ativa um item se a soltura ocorrer na mesma célula, sem arrastar. `SelectedIndex` é a seleção atual, `ActivatedIndex` é um pulso de um único Update e `WasSelectionChanged` distingue mudança de seleção de uma segunda ativação. `ScrollToItem(index, center)` mostra uma célula sem depender de arraste. Alterar `Bounds` recalcula as colunas e cancela o dedo anterior.

Para inventários muito grandes, não crie controles por item: use `UiGridLayout.GetVisibleRange(area, count, offsetY, out first, out end)`, `GetCellBounds` e `HitTest` para desenhar e atingir apenas células visíveis; `TouchGridView.GetVisibleRange` e `GetItemBounds` oferecem o mesmo sobre a rolagem atual. A função `TouchGridView.Draw` é um atalho para rótulos de listas moderadas. Ela não impede desenho próprio com sprites e texturas do jogo. `maxColumns: 0` significa sem limite de colunas; telas estreitas reduzem a largura de uma célula quando necessário.

## Validação Android

1. Abra o exemplo no Preview: devem aparecer 30 células e, quando houver overflow, uma barra de rolagem.
2. Toque e solte no item 2, depois sobre o espaço entre dois itens; somente o toque sobre o item seleciona.
3. Arraste verticalmente e solte; não pode haver clique acidental.
4. Experimente dois dedos e um toque cancelado; nenhum dedo deve roubar uma captura anterior.
5. Troque retrato/paisagem, teste novamente a seleção e confira a distribuição das colunas.
6. Abra um projeto antigo e confirme que seus dados, controles e desempenho continuam corretos.

CI e APK compilado não substituem a validação real em aparelho.
