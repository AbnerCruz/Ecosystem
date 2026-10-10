# Layout responsivo mobile — LUNET-431

`UiStackLayout` organiza botões, painéis e outros controles em pilhas verticais ou horizontais, com margens, espaços e itens flexíveis. Combine com `LayoutRect` para posicionar o painel dentro de outro e com `GraphicsDevice.SafeArea` para respeitar os recuos do celular. Nenhum controle é copiado, recriado ou gerenciado pelo layout; o jogo só atualiza a propriedade `Bounds`.

Exemplo completo em `Game.cs`, sem assets extras:

```csharp
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class ResponsiveMenu : Game
{
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private TouchButton button = null!;
    private readonly UiStackItem[] items =
    {
        UiStackItem.Fixed(52),
        UiStackItem.Flex(),
        UiStackItem.Fixed(64)
    };
    private readonly RectangleF[] rows = new RectangleF[3];
    private readonly UiStackLayout stack = UiStackLayout.Vertical(spacing: 12, padding: 12);

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        button = new TouchButton(new RectangleF(0, 0, 1, 1));
    }

    protected override void Update(GameTime time)
    {
        stack.Arrange(GraphicsDevice.SafeArea, items, rows);
        button.Bounds = rows[0];
        button.Update(Input);
        if (button.WasClicked) Log.Info("Menu confirmado");
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(UiTheme.Dark.Background);
        batch.Begin();
        batch.FillRect(rows[1], UiTheme.Dark.Panel);
        batch.FillRect(rows[2], UiTheme.Dark.Panel);
        button.Draw(batch, font, "JOGAR", UiTheme.Dark.Button, 2);
        batch.End();
    }
}
```

O cálculo usa somente spans fornecidos pelo jogo, sem alocar memória ou criar um novo array a cada quadro. `Fixed(52)` reserva 52 unidades do eixo principal; `Flex(2)` recebe o dobro da parcela livre de `Flex(1)`. A pilha horizontal distribui da esquerda para a direita. `UiStackAlignment.Center`, `Start` e `End` alinhariam controles de largura/altura transversal explícita; `Stretch` preenche a faixa disponível. `UiStackItem.Fixed(50, 140)` define tamanho principal 50 e transversal 140. Espaço transversal zero significa ocupar toda a largura/altura interna.

Se itens fixos ultrapassarem o espaço, não são encolhidos automaticamente: os flexíveis ficam com tamanho zero e os últimos itens podem sair da área. Para listas compridas, combine com `TouchScrollArea` ou `TouchListView` e clipping. Para menus, use `GraphicsDevice.SafeArea`, atualize os limites antes de chamar `Update(Input)` e use `Cancel()` ao ocultar o menu. O layout não gerencia toques de controles sobrepostos.

## Teste no Android

1. Execute o exemplo no Preview em paisagem; confirme botão JOGAR, faixa central que ocupa o espaço restante, margens e painel inferior.
2. Troque a orientação ou dimensão do Preview; os três retângulos devem se rearranjar sem deixar o botão fora da área segura.
3. Toque no botão após redimensionar e confirme um único clique.
4. Teste `Horizontal` em um painel superior e `Flex(2)` / `Flex(1)` para conferir distribuição de espaço.
5. Abra um jogo existente que usa `LayoutRect` diretamente para confirmar que a API anterior permanece intacta.

O CI garante consistência matemática e execução no GameHost. A ergonomia e a aparência no dispositivo só podem ser aprovadas depois do teste real.
