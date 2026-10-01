# Gráficos 2D

O desenho passa por `SpriteBatch`: você abre um lote, desenha vários sprites e fecha. Isso agrupa as chamadas e mantém o jogo rápido.

```csharp
batch.Begin();
batch.Draw(textura, new Vector2(100, 100), Color.White);
batch.End();
```

## Peças principais

- `Texture2D`: imagem na memória da GPU.
- `Sprite` e `SpriteSheet`: recortes de uma textura e animações.
- `SpriteFont`: texto.
- `RenderTarget2D`: desenhar em uma textura, para efeitos e pós-processamento.
- `Shader` e `Material`: efeitos customizados.
- `BlendState` e `SamplerState`: mistura de cores e filtro (pixel perfeito usa ponto).

## Depuração

`DebugDraw` desenha formas e linhas por cima do jogo para ver colisões e limites.
