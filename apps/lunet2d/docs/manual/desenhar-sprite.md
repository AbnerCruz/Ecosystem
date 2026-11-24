# Carregue e desenhe um sprite

## Preparar o PNG

Coloque um PNG em `Content/Textures/hero.png` no projeto. A chamada de carregamento usa `Textures/hero.png`, o caminho relativo à pasta `Content`. Confira as maiúsculas, minúsculas e extensão. Substitua `Game.cs` pelo exemplo completo.

```csharp full
using Lunet;
using Lunet.Graphics;
using System.Numerics;

public sealed class MeuSprite : Game
{
    private SpriteBatch _sprites = null!;
    private Texture2D _hero = null!;

    protected override void LoadContent()
    {
        _sprites = new SpriteBatch(GraphicsDevice);
        _hero = Content.LoadTexture("Textures/hero.png", TextureFilter.Point);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        _sprites.Begin();
        _sprites.Draw(_hero, new RectangleF(48, 64, 96, 96), null, Color.White, 0f, Vector2.Zero);
        _sprites.End();
    }
}
```

## Rodar e conferir

Toque em **Rodar**. O sprite aparece num quadrado de 96 unidades virtuais, ligeiramente afastado do canto superior esquerdo. `Point` preserva bordas duras de pixel art. O filtro padrão é `Linear`, que suaviza a amostragem. Carregue o recurso em `LoadContent` e reutilize a textura ao desenhar. Limpe a tela com `Clear` antes de desenhar a cena.

Se aparecer erro de arquivo, abra **Arquivos** e confira caminho e extensão. Se o arquivo existir mas o carregamento falhar, reexporte a imagem como PNG compatível.

[Referência de LoadTexture](id:M:Lunet.Content.ContentManager.LoadTexture(System.String,Lunet.Graphics.TextureFilter))

[Leia e corrija um erro](guide:ler-erros)
