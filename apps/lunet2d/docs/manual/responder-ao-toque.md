# Faça seu primeiro jogo responder ao toque

Comece com o projeto em branco do tutorial anterior. Substitua `Game.cs` pelo exemplo completo e toque em **Rodar**.

```csharp full
using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class ToqueNaTela : Game
{
    private SpriteBatch _sprites = null!;

    protected override void LoadContent() => _sprites = new SpriteBatch(GraphicsDevice);
    private Vector2 _position = new(180, 320);
    private bool _changed;

    protected override void Update(GameTime time)
    {
        var pointer = Input.Pointer;
        if (!pointer.WasPressed) return;
        _position = pointer.Position;
        _changed = !_changed;
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(_changed ? Color.CornflowerBlue : Color.Black);
        _sprites.Begin();
        _sprites.FillRect(new RectangleF(_position.X - 12, _position.Y - 12, 24, 24), Color.Yellow);
        _sprites.End();
    }
}
```

## Início do toque

Antes do primeiro toque, a tela está preta. Toque no meio da área do jogo. O quadrado vai até o ponto tocado e o fundo fica azul. Manter o dedo parado não alterna a cor. Solte e toque outra vez para voltar ao preto. `Input.Pointer.WasPressed` vale apenas no primeiro passo de atualização após o início do toque. A posição usa as coordenadas virtuais do jogo, com origem no canto superior esquerdo da área jogável.

Se nada mudar, toque na área do jogo e confira se a lógica usa `Update`. Se houver mensagem vermelha em **Erros**, abra o diagnóstico e corrija a linha antes de rodar novamente.

## Mais de um dedo

Este tutorial acompanha o primeiro toque ativo. Em um controle de arrasto ou de dois dedos, acompanhe a identidade de cada toque e libere a captura quando o gesto terminar ou for cancelado.

[Joysticks com dedos separados](guide:entrada#joysticks-de-movimento-e-mira-com-dedos-separados)

[Tempo e laço do jogo](guide:tempo-do-jogo)
