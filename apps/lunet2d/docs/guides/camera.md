# Câmera 2D

`Camera2D` vive em `Lunet.Graphics`. É uma API opcional do Framework para deslocar a vista, aproximar/afastar e girar o mundo, sem mover os objetos nem modificar colisões. Funciona offline.

## Coordenadas e desenho

- `Position`: ponto do mundo que fica no centro da vista. O padrão é a origem; para uma câmera neutra use `Position = GraphicsDevice.ViewSize / 2`.
- `Zoom`: escala positiva e finita, com inverso representável; 2 duplica o tamanho aparente e 0,5 mostra mais mundo.
- `Rotation`: radianos da câmera; o mundo gira no sentido inverso.
- `GetViewMatrix(viewSize)`: matriz mundo → vista virtual.
- `GetWorldViewBounds(viewSize)`: AABB conservadora da câmera em coordenadas do mundo para `TileMap.Draw`; inclui os quatro cantos com zoom e rotação. Não aloca, usa tamanho virtual/render target e rejeita limites não representáveis.
- `WorldToScreen` e `ScreenToWorld`: conversões entre mundo e vista virtual. **Não recebem pixels físicos**. O toque de `Input` já está convertido; para um ponto físico use primeiro `GraphicsDevice.SurfaceToVirtual`.
- `batch.Begin(camera)` captura a câmera e o tamanho da vista no início do lote. Mudanças na câmera durante o lote valem no próximo Begin. Texture flush e limite de quads preservam a captura.
- Sprites, texto e DebugDraw recebem a mesma transformação. Colisões continuam em coordenadas do mundo.
- `clip` continua sendo recorte na vista virtual, independente do zoom/rotação.
- Para HUD fixo, termine o lote do mundo e use `batch.Begin()` sem câmera.
- Em render targets, a câmera usa `GraphicsDevice.ViewSize`, que contém o tamanho do alvo. Encerre o lote antes de trocar o alvo.

Use câmera e conversão com o mesmo tamanho de vista. A projeção, o letterbox, a área segura e o shader continuam sendo responsabilidade do dispositivo gráfico. As conversões e o desenho com câmera não alocam memória. Para pixel art, zoom inteiro e posição alinhada a pixels ajudam a evitar tremulação; a câmera não arredonda coordenadas automaticamente.

## Testar no Preview

**Teste rápido no celular:** crie um projeto com o modelo **Laboratório**, execute e toque duas vezes no cabeçalho para abrir **Página 3/3**. Toque/arraste na grade; use **+ Zoom**, **- Zoom** e **Girar 45 graus**. A grade muda, o marcador acompanha o toque e os controles ficam fixos. Toque novamente no cabeçalho para retornar à página de dispositivos. Projetos Laboratório já existentes preservam seu código; a nova página aparece ao criar um novo projeto.

Para inspecionar uma demonstração menor ou adaptar ao seu jogo, use o exemplo abaixo:

Crie um projeto **Em branco** separado e substitua a classe do jogo pelo código abaixo. Run deve funcionar sem internet. A classe pública `CameraDemo` é descoberta pelo carregador.

```csharp
using System;
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Input;

public sealed class CameraDemo : Game
{
    readonly Camera2D camera = new() { Position = new(500, 400) };
    Vector2 player = new(500, 400);
    SpriteBatch batch = null!;
    SpriteFont font = null!;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
    }

    protected override void Update(GameTime time)
    {
        foreach (var gesture in Input.Gestures)
        {
            if (gesture.Type != GestureType.Tap || gesture.Position.Y >= 60) continue;
            if (gesture.Position.X < 90) camera.Zoom = Math.Clamp(camera.Zoom * 2, 0.25f, 4);
            else if (gesture.Position.X < 180) camera.Zoom = Math.Clamp(camera.Zoom / 2, 0.25f, 4);
            else camera.Rotation += MathF.PI / 4;
        }
        if (Input.TryGetPointer(out var touch) && touch.Y >= 60)
            player = camera.ScreenToWorld(touch, GraphicsDevice.ViewSize);
        camera.Position = Vector2.Lerp(camera.Position, player, Math.Clamp(time.DeltaSeconds * 5, 0, 1));
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        batch.Begin(camera);
        for (int i = 0; i <= 2000; i += 100)
        {
            batch.Line(new(i, 0), new(i, 2000), Color.CornflowerBlue);
            batch.Line(new(0, i), new(2000, i), Color.CornflowerBlue);
        }
        batch.Circle(player, 12, Color.Yellow, 3);
        batch.End();

        batch.Begin();
        batch.FillRect(new RectangleF(0, 0, 360, 60), Color.Black);
        batch.Rect(new RectangleF(5, 5, 80, 45), Color.White);
        batch.Rect(new RectangleF(95, 5, 80, 45), Color.White);
        batch.Rect(new RectangleF(185, 5, 170, 45), Color.White);
        batch.DrawString(font, "+ Zoom", new(12, 20), Color.White);
        batch.DrawString(font, "- Zoom", new(102, 20), Color.White);
        batch.DrawString(font, "Girar 45 graus", new(192, 20), Color.White);
        batch.End();
    }
}
```

1. A grade azul e o círculo amarelo aparecem; o círculo inicia no centro da vista. Os três controles ficam fixos no topo.
2. Toque/arraste abaixo dos controles: o círculo segue o toque no mundo e a câmera acompanha suavemente, deslocando a grade. Solte: o círculo volta gradualmente ao centro.
3. Toque em **+ Zoom** e **- Zoom**. Grade e círculo mudam de escala; controles permanecem fixos.
4. Toque em **Girar 45 graus** e repita o movimento. O círculo continua acompanhando o ponto tocado, com a grade inclinada.
5. Pare/execute novamente; confira Preview rápido e isolado. Abra também um projeto anterior sem câmera e confira se continua desenhando no mesmo lugar.

Roteiro intermediário LUNET-403: resultado no aparelho e build usado devem ser registrados separadamente dos testes automáticos. Não fecha o gate da Fase 4.
