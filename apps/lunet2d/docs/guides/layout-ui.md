# Layout de UI por âncoras e margens

Crie um projeto **Em branco**, substitua Game.cs pelo exemplo inteiro e toque em Run. Toque em **Alternar área** para mudar o tamanho do painel e em **Somar** para testar seu botão. O cabeçalho acompanha a largura, o conteúdo respeita margens e o botão fica preso ao canto inferior direito. O quadrado permanece centralizado no conteúdo.

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class LayoutDemo : Game
{
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    readonly RectangleF resizeButton = new(16, 48, 240, 44);
    readonly LayoutRect headerLayout = new(Vector2.Zero, new Vector2(1, 0), Vector2.Zero, new Vector2(0, 48));
    readonly LayoutRect contentLayout = LayoutRect.Stretch(12, 60, 12, 68);
    readonly LayoutRect actionLayout = LayoutRect.Fixed(Vector2.One, new Vector2(120, 44), Vector2.One, new Vector2(-12, -12));
    readonly LayoutRect markerLayout = LayoutRect.Fixed(new Vector2(0.5f), new Vector2(48), new Vector2(0.5f));
    RectangleF panelBounds;
    RectangleF headerBounds;
    RectangleF contentBounds;
    RectangleF actionBounds;
    RectangleF markerBounds;
    bool narrow;
    int clicks;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        Arrange();
    }

    void Arrange()
    {
        panelBounds = narrow ? new RectangleF(16, 120, 180, 360) : new RectangleF(16, 120, 328, 480);
        headerBounds = headerLayout.GetBounds(panelBounds);
        contentBounds = contentLayout.GetBounds(panelBounds);
        actionBounds = actionLayout.GetBounds(panelBounds);
        markerBounds = markerLayout.GetBounds(contentBounds);
    }

    protected override void Update(GameTime time)
    {
        if (!Input.Pointer.WasPressed || !Input.TryGetPointer(out var pointer)) return;
        if (resizeButton.Contains(pointer)) { narrow = !narrow; Arrange(); return; }
        if (actionBounds.Contains(pointer)) clicks++;
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(18, 22, 35));
        batch.Begin();
        batch.DrawString(font, "Layout por ancoras", new Vector2(16, 16), Color.White, 2);
        batch.FillRect(resizeButton, new Color(65, 85, 130));
        batch.DrawString(font, "Alternar area", resizeButton.Position + new Vector2(12, 14), Color.White, 2);
        batch.FillRect(panelBounds, new Color(30, 42, 65));
        batch.FillRect(headerBounds, new Color(50, 70, 105));
        batch.DrawString(font, "Painel", headerBounds.Position + new Vector2(12, 14), Color.White, 2);
        batch.Rect(contentBounds, Color.Green);
        batch.FillRect(markerBounds, Color.Yellow);
        batch.FillRect(actionBounds, new Color(65, 85, 130));
        batch.DrawString(font, "Somar", actionBounds.Position + new Vector2(20, 12), Color.White, 2);
        batch.DrawString(font, "Cliques: " + clicks, new Vector2(16, 612), Color.White, 2);
        batch.End();
    }
}
```

## Usar no jogo

`new LayoutRect(anchorMin, anchorMax, offsetMin, offsetMax)` define os limites relativos ao pai. Âncoras ficam entre 0 e 1: 0 é topo/esquerda, 0.5 centro, 1 base/direita. Offset está em unidades do mesmo espaço de coordenadas do pai, normalmente pixels virtuais. Cada limite é `posição do pai + tamanho do pai × âncora + offset`. AnchorMax precisa ser maior ou igual a AnchorMin por eixo.

`LayoutRect.Fixed(anchor, size, pivot, offset)` prende um ponto do retângulo à âncora. O pivot também vai de 0 a 1. Para botão no canto inferior direito, use anchor e pivot iguais a Vector2.One, com offset negativo de margem. Pivot padrão é o canto superior esquerdo. `LayoutRect.Stretch(left, top, right, bottom)` ocupa o pai com margens internas finitas não negativas.

Passe `GetBounds(parent)` para desenhar e use o **mesmo resultado** no `RectangleF.Contains` do toque. Para aninhar, passe os limites resolvidos do painel como pai do conteúdo. O layout não guarda árvore nem retângulos antigos: após mudar o pai, recalcule os resultados antes de desenhar e receber input. `Fixed` também pode ser recalculado por frame sem alocação.

Para HUD completo, o pai pode ser `new RectangleF(0, 0, GraphicsDevice.ViewSize.X, GraphicsDevice.ViewSize.Y)`; para respeitar recortes da tela, use `GraphicsDevice.SafeArea` na superfície principal. A resolução virtual do Lunet é fixa e usa letterbox: girar o aparelho não muda ViewSize por si só. Este exemplo altera explicitamente uma área interna para tornar o redimensionamento observável. Em render target, escolha o pai no espaço do alvo; em UI no mundo, converta o toque com Camera2D antes do Contains.

Pai zero é permitido. Se margens/offsets inverterem os limites, o tamanho colapsa a zero no limite mínimo; não vira negativo e não recebe toque por Contains. Layout não prende coordenadas dentro do pai e não recorta; offsets podem colocar conteúdo para fora. Entradas não finitas, âncoras fora de 0–1 e tamanhos/margens negativos são rejeitados. Resultados fora da faixa float lançam OverflowException.

Os layouts são structs imutáveis, sem alocação no cálculo. O HUD do exemplo concatena texto por quadro e aloca; a garantia de zero bytes é da API de layout. Não há flex/grid/stack automático, texto com wrap, eventos, navegação de foco, controles completos ou temas; esses recursos permanecem no item de UI.

## Roteiro Android intermediário

1. Execute offline no Preview rápido. Toque em Somar: o contador aumenta uma vez por toque.
2. Toque em Alternar área: painel/cabeçalho/conteúdo encolhem, botão acompanha o canto e quadrado permanece centralizado.
3. Toque onde o botão ficava antes: não deve somar. Toque no botão deslocado: deve somar. Alterne de volta e repita.
4. Pare, execute no Preview isolado e repita. Volte ao editor e rode um projeto anterior.

Registre passou/falhou/não testado, versão do APK e modo do Preview. Aparência/GL, toque e lifecycle no aparelho não são inferidos dos testes portáteis.
