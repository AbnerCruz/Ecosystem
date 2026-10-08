# Painéis redimensionáveis com nine-slice

Crie um projeto **Em branco**, substitua Game.cs pelo exemplo inteiro e toque em Run. Funciona offline, sem imagens externas. Arraste na área abaixo do cabeçalho para mudar o tamanho do painel; toque em **Bordas** para alternar escala 1×/2×. As quatro cores dos cantos ajudam a observar que só as faixas e o centro esticam.

```csharp
using System;
using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class PanelDemo : Game
{
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Texture2D texture = null!;
    NineSlice panel = null!;
    readonly RectangleF scaleButton = new(16, 48, 140, 40);
    RectangleF bounds = new(24, 140, 260, 240);
    float borderScale = 1;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        var pixels = new byte[24 * 24 * 4];
        for (int y = 0; y < 24; y++)
        for (int x = 0; x < 24; x++)
        {
            var color = new Color(45, 65, 100);
            if (x < 6 || x >= 18 || y < 6 || y >= 18) color = new Color(90, 130, 180);
            if (x < 6 && y < 6) color = Color.Red;
            if (x >= 18 && y < 6) color = Color.Yellow;
            if (x < 6 && y >= 18) color = Color.Green;
            if (x >= 18 && y >= 18) color = Color.White;
            int i = (y * 24 + x) * 4;
            pixels[i] = color.R; pixels[i + 1] = color.G;
            pixels[i + 2] = color.B; pixels[i + 3] = color.A;
        }
        texture = Texture2D.FromPixels(GraphicsDevice, 24, 24, pixels, TextureFilter.Point);
        panel = new NineSlice(texture, new RectangleF(0, 0, 24, 24), 6, 6, 6, 6);
    }

    protected override void Update(GameTime time)
    {
        if (!Input.TryGetPointer(out var pointer)) return;
        if (Input.Pointer.WasPressed && scaleButton.Contains(pointer)) borderScale = borderScale == 1 ? 2 : 1;
        if (pointer.Y >= bounds.Y)
            bounds = new RectangleF(24, 140, Math.Clamp(pointer.X - 24, 4, 312), Math.Clamp(pointer.Y - 140, 4, 440));
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(18, 22, 35));
        batch.Begin(sampler: SamplerState.PointClamp);
        batch.Draw(panel, scaleButton, Color.White);
        batch.DrawString(font, "Bordas " + borderScale + "x", new Vector2(28, 60), Color.White, 2);
        batch.Draw(panel, bounds, Color.White, borderScale);
        batch.DrawString(font, "Nine-slice", new Vector2(16, 16), Color.White, 2);
        batch.DrawString(font, "Arraste para redimensionar", new Vector2(16, 104), Color.White, 2);
        batch.End();
    }

    protected override void UnloadContent() => texture.Dispose();
}
```

## Uso e limites

`new NineSlice(texture, source, left, top, right, bottom)` define bordas em pixels da imagem. Source pode ser uma região dentro de um atlas. As bordas são finitas, não negativas e deixam um centro positivo em ambos os eixos. O jogo mantém e libera a textura; NineSlice é imutável e compartilhável.

`batch.Draw(panel, destination, color, borderScale: 1)` exige Begin/End. Os cantos mantêm o tamanho das bordas; o topo/base esticam horizontalmente, as laterais verticalmente e o centro nos dois eixos. A escala das bordas é positiva e finita, independente do tamanho do painel. Se não couberem, as bordas comprimem proporcionalmente em cada eixo e o centro nesse eixo desaparece. Destino zero não desenha; tamanho negativo, valores não finitos e textura liberada são rejeitados antes de enviar partes.

São até nove quads, agrupados normalmente por textura. O desenho usa câmera, clipping, blend, sampler e shader do lote, sem alocação gerenciada por chamada. O HUD deste exemplo concatena texto e aloca; a garantia é da API. Para atlas com filtro Linear, deixe padding/extrusão nas imagens para evitar vazamento nas bordas; Point preserva os pixels do exemplo.

Não inclui repetição de imagem, rotação individual, layout, âncoras, padding, interação ou temas. O jogo define destino e hit testing em coordenadas virtuais (ou converte o toque pela Camera2D para UI no mundo).

## Roteiro Android intermediário

1. Execute o exemplo no Preview rápido, offline. Arraste: os quatro cantos coloridos mantêm tamanho enquanto o centro cresce.
2. Toque em Bordas: os cantos passam de 6 para 12 unidades virtuais; voltar restaura 6.
3. Arraste perto de (28, 144): destino 4×4, cantos comprimidos sem sobrepor/inverter; arraste para aumentar novamente.
4. Pare e rode no Preview isolado. Repita toques e redimensionamento; nenhuma falha de runtime. Volte ao editor e rode um projeto anterior.

Registre passou/falhou/não testado, versão do APK e modo do Preview. Testes portáteis não substituem GL, aparência ou toque no aparelho.
