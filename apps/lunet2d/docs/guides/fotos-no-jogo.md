# Fotos da galeria dentro do jogo

O jogo pode pedir que a pessoa escolha uma foto do aparelho (por exemplo, para virar a imagem de um quebra-cabeça). O Lunet abre o seletor de arquivos do próprio Android: o jogo não recebe acesso à galeria inteira nem o endereço do arquivo, só os pixels da foto escolhida.

- A foto chega já em pé (a rotação gravada pela câmera é aplicada) e com no máximo 1024 pixels no lado maior.
- O seletor não trava o jogo: chame `Pick()` e confira `TryReceive` a cada `Update`. Cancelar ou uma falha também chegam por `TryReceive` (com `Rgba` nulo).
- Funciona no Preview rápido e no APK do jogo. No Preview isolado e em hosts sem tela (`lunet-server`) o serviço não existe; por isso, sempre teste com `Services.TryGet`.

```csharp
using System.Numerics;
using Lunet;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Input;

public sealed class FotoGame : Game
{
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Texture2D? photo;
    string status = "Toque para escolher uma foto";

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
    }

    protected override void Update(GameTime time)
    {
        if (!Services.TryGet<IGameImagePicker>(out var picker) || !picker.CanPick)
        {
            status = "Este host não oferece fotos";
            return;
        }
        if (Input.Pointer.WasPressed) picker.Pick();
        if (picker.TryReceive(out var result))
        {
            if (result.Rgba is null) { status = result.Error ?? "Escolha cancelada"; return; }
            photo?.Dispose();
            photo = Texture2D.FromPixels(GraphicsDevice, result.Width, result.Height, result.Rgba);
            status = $"Foto {result.Width}x{result.Height}";
        }
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(18, 22, 40));
        batch.Begin();
        if (photo is not null) batch.Draw(photo, new Vector2(0, 60), Color.White);
        batch.DrawString(font, status, new Vector2(8, 12), Color.White, 2);
        batch.End();
    }

    protected override void UnloadContent() => photo?.Dispose();
}
```

## Validação no aparelho (DEVICE)

1. Importe um projeto que use o seletor (ex.: FotoPuzzle), rode no **Preview rápido** e escolha uma foto tirada com a câmera em pé (retrato): ela deve aparecer em pé, não deitada.
2. Repita com uma foto em paisagem, um print e uma imagem grande (mais de 4000 px): todas aparecem, sem travar o jogo.
3. Abra o seletor e volte sem escolher: o jogo continua e recebe "cancelado".
4. Gere o APK do jogo, instale e repita o passo 1 no APK.
