# Colisão simples

As consultas vivem em `Lunet`, usam `Vector2`, funcionam offline e não exigem outro aplicativo, Studio ou um mundo de física. Use coordenadas finitas, tamanhos e raios não negativos. SAT recebe polígonos convexos com vértices em ordem; não decomponha uma forma côncava implicitamente.

## Consultas

- `RectangleF.Intersects`: sobreposição AABB com área positiva; bordas encostadas não contam.
- `Circle.Intersects`: círculo/círculo e círculo/retângulo; tangência conta.
- `RectangleF.Contains`: direita e inferior excluídas, preservando a seleção de UI existente.
- `Circle.Contains`: borda incluída.
- `Ray2D.Intersects`: primeiro acerto à frente, distância em unidades de mundo; origem dentro da forma retorna zero. Para alcance limitado, compare a distância retornada com o alcance.
- `Geometry.ClosestPoint(point, rect)`: ponto mais próximo do retângulo fechado.
- `Geometry.Distance`: ponto/retângulo, ponto/círculo, círculo/círculo, círculo/retângulo e retângulo/retângulo. Mede a distância entre formas preenchidas: contato ou contenção retorna zero.
- `Geometry.Intersection(a, b, out rect)`: região AABB comum com área positiva; retorna falso e `default` sem área.
- `Geometry.SatOverlap(a, b, out push)`: sobreposição convexa, com translação mínima para levar A até o contato com B. Contenção funciona nos dois sentidos. Contato e formas sem área retornam falso. Vértices repetidos adjacentes são tolerados.
- `Geometry.DistanceToSegment` e `SegmentsIntersect`: consultas de segmento existentes; segmentos paralelos/colineares retornam falso na interseção.

As consultas não alocam memória. Reutilize os buffers de vértices no jogo. A camada é geométrica: movimento muito rápido exige consulta ao longo do percurso ou subpassos; ela não resolve gravidade, velocidade, impulso ou tunneling automaticamente.

## Testar no Preview

Crie um projeto **Em branco** separado e substitua o conteúdo do arquivo da classe de jogo pelo código abaixo. A única classe de jogo é `CollisionDemo`; o carregador a descobre sem alterar outros arquivos. Abra Run sem internet.

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class CollisionDemo : Game
{
    readonly Vector2[] obstacle = { new(100, 180), new(260, 180), new(260, 340), new(100, 340) };
    readonly Vector2[] player = new Vector2[4];
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Vector2 position = new(140, 220);
    Vector2 desired = new(140, 220);
    Vector2 push;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
    }

    protected override void Update(GameTime time)
    {
        if (Input.TryGetPointer(out var pointer)) desired = pointer;
        position = desired;
        player[0] = position;
        player[1] = position + new Vector2(32, 0);
        player[2] = position + new Vector2(32, 32);
        player[3] = position + new Vector2(0, 32);
        if (Geometry.SatOverlap(player, obstacle, out push)) position += push;
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        batch.Begin();
        batch.Rect(new RectangleF(100, 180, 160, 160), Color.Yellow, 3);
        batch.Rect(new RectangleF(desired.X, desired.Y, 32, 32), Color.Red, 2);
        batch.FillRect(new RectangleF(position.X, position.Y, 32, 32), Color.Green);
        batch.Line(desired, position, Color.White, 2);
        batch.DrawString(font, $"Separacao: {push.Length():F1}", new Vector2(10, 20), Color.White);
        batch.End();
    }
}
```

1. Ao iniciar, o quadrado vermelho está inteiramente dentro do obstáculo amarelo. O verde aparece fora, encostado na esquerda; separação inicial **72,0** unidades.
2. Toque/arraste dentro do obstáculo. O vermelho segue o ponto desejado; o verde é separado até a borda mais próxima. A linha branca representa o empurrão.
3. Toque fora. Verde e vermelho coincidem e a separação fica zero.
4. Pare e execute novamente; repita em Preview rápido e isolado. Código e projetos anteriores devem continuar acessíveis. Nenhum teste deste roteiro exige pareamento com outro aplicativo.

Este é um roteiro intermediário da LUNET-402, não o gate completo da Fase 4. O build testado e o resultado no aparelho devem ser registrados separadamente dos testes automáticos.
