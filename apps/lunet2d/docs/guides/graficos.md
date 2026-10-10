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

## Preencher a tela: Fit vs Fill

O Lunet possui dois modos de escala **sem deformar os sprites**:

- **Fit (padrão):** desenha toda a resolução virtual, com barras pretas quando o formato do celular é diferente do projeto. Ideal para garantir que nenhum botão ou cenário fique fora da tela.
- **Fill:** aumenta o viewport proporcionalmente para cobrir a superfície inteira. **Não aparecem barras do letterbox**, mas partes do cenário podem ficar fora da tela (recorte nas laterais, topo ou base).

Para um projeto novo, configure na inicialização:

```csharp
// Na classe do seu Game, dentro de Initialize():
Configuration.ViewportScaling = ViewportScalingMode.Fill;
```

Durante uma partida, também pode alternar a configuração com `GraphicsDevice.ViewportScaling = ViewportScalingMode.Fit` ou `ViewportScalingMode.Fill`. Não modifica os arquivos salvos nem a resolução virtual do jogo.

Ao usar **Fill**, posicionar botões de HUD próximos às bordas virtuais pode deixá-los fora da área visível; use `GraphicsDevice.SafeArea` para layout e ancoragem. `SurfaceToVirtual` e `VirtualToSurface` continuam transformando toques corretamente. `PixelPerfect` escolhe escala inteira que não volta a criar barras quando Fill está ativo e a escala cabe em ao menos 1×.

**Teste sem editar código:** no Laboratório 2.0, vá até **Gráficos** (página 2/12) e toque em **TELA: FIT/FILL**. Teste com aparelho em proporções diferentes, zoom, recorte e pixel-perfect. O padrão dos projetos antigos permanece Fit; o ajuste é explícito e reversível. A barra de controles do Preview pertence à IDE e é distinta das barras de letterbox do jogo.
