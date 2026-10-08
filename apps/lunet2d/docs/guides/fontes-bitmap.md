# Fontes bitmap personalizadas

Crie um projeto **Em branco**, substitua Game.cs pelo exemplo inteiro e toque em Run. O exemplo cria uma fonte de placar de sete segmentos, offline e sem imagens externas. Toque em **Pontos** para aumentar o número e em **Escala** para alternar 2×/3×. O dígito 1 é mais estreito que os outros.

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class BitmapFontDemo : Game
{
    SpriteBatch batch = null!;
    SpriteFont caption = null!;
    SpriteFont digits = null!;
    Texture2D texture = null!;
    readonly RectangleF pointsButton = new(16, 48, 144, 44);
    readonly RectangleF scaleButton = new(184, 48, 144, 44);
    int score = 1234;
    float scale = 3;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        caption = SpriteFont.CreateDefault(GraphicsDevice);
        var pixels = new byte[80 * 12 * 4];
        int[] masks = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
        for (int digit = 0; digit < 10; digit++)
        for (int y = 0; y < 12; y++)
        for (int x = 0; x < 8; x++)
        {
            bool on = (y < 2 && x >= 1 && x < 7 && (masks[digit] & 1) != 0)
                || (x >= 6 && y >= 1 && y < 6 && (masks[digit] & 2) != 0)
                || (x >= 6 && y >= 6 && y < 11 && (masks[digit] & 4) != 0)
                || (y >= 10 && x >= 1 && x < 7 && (masks[digit] & 8) != 0)
                || (x < 2 && y >= 6 && y < 11 && (masks[digit] & 16) != 0)
                || (x < 2 && y >= 1 && y < 6 && (masks[digit] & 32) != 0)
                || (y >= 5 && y < 7 && x >= 1 && x < 7 && (masks[digit] & 64) != 0);
            if (!on) continue;
            int i = (y * 80 + digit * 8 + x) * 4;
            pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = 255;
        }
        texture = Texture2D.FromPixels(GraphicsDevice, 80, 12, pixels, TextureFilter.Point);
        var glyphs = new Dictionary<int, BitmapGlyph>();
        for (int digit = 0; digit < 10; digit++)
            glyphs['0' + digit] = digit == 1
                ? new BitmapGlyph(new RectangleF(14, 0, 2, 12), 4)
                : new BitmapGlyph(new RectangleF(digit * 8, 0, 8, 12), 10);
        glyphs[' '] = new BitmapGlyph(default, 5);
        digits = SpriteFont.FromBitmap(texture, glyphs, lineHeight: 16, fallbackCodePoint: '0');
    }

    protected override void Update(GameTime time)
    {
        if (!Input.Pointer.WasPressed || !Input.TryGetPointer(out var pointer)) return;
        if (pointsButton.Contains(pointer)) score = (score + 1) % 10000;
        if (scaleButton.Contains(pointer)) scale = scale == 3 ? 2 : 3;
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(18, 22, 35));
        string text = score.ToString("D4") + "\n0123456789";
        Vector2 position = new(24, 140);
        batch.Begin(sampler: SamplerState.PointClamp);
        batch.FillRect(pointsButton, new Color(45, 65, 100));
        batch.FillRect(scaleButton, new Color(45, 65, 100));
        batch.DrawString(caption, "Pontos", new Vector2(28, 62), Color.White, 2);
        batch.DrawString(caption, "Escala", new Vector2(196, 62), Color.White, 2);
        batch.DrawString(caption, "Fonte bitmap personalizada", new Vector2(16, 16), Color.White, 2);
        batch.DrawString(digits, text, position, Color.Yellow, scale);
        Vector2 size = digits.Measure(text, scale);
        batch.Rect(new RectangleF(position.X, position.Y, size.X, size.Y), Color.Green);
        batch.End();
    }

    protected override void UnloadContent() => texture.Dispose();
}
```

## Usar sua própria imagem

Carregue uma imagem normalmente com `Content.LoadTexture("Fonts/minha-fonte.png", TextureFilter.Point)` e forneça o mapa para `SpriteFont.FromBitmap`. Os números das regiões são pixels dentro da imagem/atlas; o mapa e as métricas são C# inspecionável, sem importador ou formato privado. Uma letra pode ocupar 5 pixels de imagem e avançar 7, deixando espaço para a próxima. `Offset` move a imagem em relação ao cursor da linha e aceita valores negativos.

O mapa usa códigos Unicode inteiros: `'A'` para A, `0x1F600` para um símbolo fora do BMP. DrawString percorre escalares Unicode, portanto um par UTF-16 válido usa um glifo. UTF-16 inválido vira U+FFFD, sujeito ao mesmo fallback. O fallback deve existir no mapa; neste placar, qualquer símbolo ausente vira 0. Na fonte comum, forneça '?' e use o fallback padrão. `\n`, `\r` e `\r\n` quebram linha e não são glifos mapeáveis.

`BitmapGlyph(source, advance, offset)` aceita avanço finito não negativo; zero permite marcas sobrepostas, com deslocamento escolhido pelo jogo. Source precisa caber na textura e ter dimensões positivas ou 0×0 para espaço invisível. FromBitmap copia o mapa e não assume posse da textura. Texturas carregadas via Content são liberadas pelo ContentManager; as criadas com FromPixels são liberadas pelo jogo.

`Measure` informa o maior **avanço de linha** e a quantidade de linhas × LineHeight, incluindo espaçamento final. Não mede o contorno da tinta: Offset pode ultrapassar essas dimensões. Texto vazio tem uma linha com largura zero. Medição que excede float lança OverflowException; desenho omite glifos cujas coordenadas/dimensões não cabem em float. Escala personalizada deve ser positiva/finita e posição finita. A fonte embutida conserva medidas, fallback e comportamento anteriores.

DrawString e Measure da fonte personalizada não alocam por chamada; criação do mapa/fonte aloca uma vez. O texto do HUD deste exemplo é formatado a cada frame e aloca. Câmera, clip, blend, sampler e shader vêm do lote. Para atlas com filtro Linear, use padding/extrusão na imagem para evitar bleeding.

Não há TrueType, kerning, shaping tipográfico, ligaturas, bidi, quebra automática de linha nem rasterização. Isso é fonte bitmap de jogo, não um editor de texto tipográfico. Esses recursos exigem entregas próprias; o item completo de fontes continua aberto.

## Roteiro Android intermediário

1. Execute offline no Preview rápido. Observe o placar amarelo, dez dígitos e o retângulo verde de layout; 1 deve ser mais estreito.
2. Toque em Pontos: o placar aumenta uma vez por toque. Toque em Escala: texto e retângulo alternam juntos entre 2×/3×, com pixels nítidos.
3. Pare e repita no Preview isolado. Volte ao editor e rode um projeto anterior com a fonte embutida; nenhuma regressão ou falha de runtime.

Registre passou/falhou/não testado, versão do APK e modo do Preview. GL, legibilidade e toque no aparelho permanecem separados dos testes portáteis.
