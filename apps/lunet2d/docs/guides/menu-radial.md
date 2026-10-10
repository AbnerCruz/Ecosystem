# Menu radial de acoes mobile — LUNET-434

Roda offline no framework C# do Lunet. A coroa permite escolher ferramentas, armas e ordens sem abrir outro painel. O primeiro setor aponta para cima, seguido dos demais no sentido horario. A zona central e morta: arraste para um setor e solte para confirmar.

## Game.cs completo

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.UI;

public sealed class RadialDemo : Game
{
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private readonly TouchRadialMenu wheel = new(new Vector2(160, 240), 30, 100, 4);
    private static readonly string[] labels = ["ARMA", "KIT", "ORDEM", "MAPA"];

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
    }

    protected override void Update(GameTime time)
    {
        wheel.Update(Input);
        if (wheel.ActivatedIndex >= 0)
            Log.Info("Acao: " + labels[wheel.ActivatedIndex]);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        batch.Begin();
        wheel.Draw(batch, font, labels, Color.White, Color.Yellow, Color.White, 1.5f);
        batch.End();
    }
}
```

Use SetGeometry ao mudar de orientacao ou area segura; reaplicar a mesma geometria nao cancela gestos. Chame Cancel ao pausar ou fechar o menu e IsEnabled=false para desabilitar. ActivatedIndex retorna -1 nos passos sem confirmacao. Draw apenas desenha os setores; o jogo controla SpriteBatch.Begin/End e a lista de comandos. Outros controles nao tem seus toques consumidos: o jogo deve arbitrar areas sobrepostas.

## Roteiro Android

1. Toque o centro e arraste para cima: ARMA aparece apenas quando soltar.
2. Arraste para a direita: KIT aparece. Soltar no centro ou fora do anel nao executa nada.
3. Use dois dedos: o segundo nao pode mudar a acao controlada pelo primeiro.
4. Redimensione/oriente a tela e chame SetGeometry: gestos anteriores nao podem executar.
5. Execute um projeto antigo para confirmar que controles e dados continuam intactos.

CI e APK nao substituem a verificacao em aparelho.
