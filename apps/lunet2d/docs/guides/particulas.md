# Partículas

Pool fixo de quads para efeitos code-first, sem Studio obrigatório.

## Teste no celular

Crie um projeto **Em branco** separado, substitua a classe do jogo pelo exemplo completo abaixo, aperte **Run** e teste offline:

1. As faíscas saem do centro, caem, diminuem e desaparecem após 1,5 segundo.
2. Toque abaixo do cabeçalho: uma explosão nasce naquele ponto. **Burst** emite na origem atual.
3. **Fluxo** liga emissão contínua. Arraste a origem: partículas antigas continuam sua trajetória.
4. **Parar** encerra novos nascimentos; as antigas continuam até desaparecer.
5. **Limpar** remove as antigas. Com fluxo ativo, novas partículas voltam a aparecer.
6. Aperte Burst repetidamente: contador nunca passa de 256. Pare, deixe esvaziar e emita de novo.
7. Repita em Preview rápido/isolado, reabra o projeto e rode um projeto antigo.

Roteiro intermediário da Fase 4; aparência/performance/toque real exigem aparelho. Teste em memória não comprova esses pontos.

## Exemplo completo

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Input;

public sealed class ParticleDemo : Game
{
    readonly RectangleF burstButton = new(8, 44, 108, 44);
    readonly RectangleF flowButton = new(126, 44, 108, 44);
    readonly RectangleF clearButton = new(244, 44, 108, 44);
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Texture2D dot = null!;
    ParticleEmitter particles = null!;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        dot = Texture2D.CreateCircle(GraphicsDevice, 16, Color.White);
        var effect = new ParticleSettings(lifetimeSeconds: 1.5, minSpeed: 40, maxSpeed: 160,
            gravity: new Vector2(0, 100), startSize: 12, endSize: 2,
            startColor: Color.Yellow, endColor: Color.Red.WithAlpha(0));
        particles = new ParticleEmitter(dot, effect, capacity: 256, seed: 42)
        {
            Position = new Vector2(180, 320),
            EmissionRate = 80
        };
        particles.Burst(60, particles.Position);
    }

    protected override void Update(GameTime time)
    {
        foreach (var gesture in Input.Gestures)
        {
            if (gesture.Type != GestureType.Tap) continue;
            if (burstButton.Contains(gesture.Position)) particles.Burst(60, particles.Position);
            else if (flowButton.Contains(gesture.Position))
            {
                if (particles.IsEmitting) particles.Stop(); else particles.Start();
            }
            else if (clearButton.Contains(gesture.Position)) particles.Clear();
            else if (gesture.Position.Y >= 120) particles.Burst(40, gesture.Position);
        }
        if (Input.TryGetPointer(out var pointer) && pointer.Y >= 120) particles.Position = pointer;
        particles.Update(time.DeltaSeconds);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(18, 22, 40));
        batch.Begin(BlendState.Additive, clip: new RectangleF(0, 120, GraphicsDevice.ViewSize.X, GraphicsDevice.ViewSize.Y - 120));
        particles.Draw(batch);
        batch.End();
        batch.Begin();
        batch.DrawString(font, "Particulas: burst + fluxo", new Vector2(8, 12), Color.White, 2);
        batch.FillRect(burstButton, new Color(70, 80, 130));
        batch.FillRect(flowButton, particles.IsEmitting ? Color.Green : new Color(70, 80, 130));
        batch.FillRect(clearButton, new Color(70, 80, 130));
        batch.DrawString(font, "Burst", burstButton.Position + new Vector2(14, 14), Color.White, 2);
        batch.DrawString(font, particles.IsEmitting ? "Parar" : "Fluxo", flowButton.Position + new Vector2(14, 14), Color.White, 2);
        batch.DrawString(font, "Limpar", clearButton.Position + new Vector2(14, 14), Color.White, 2);
        batch.DrawString(font, "Toque/arraste abaixo: origem do efeito", new Vector2(8, 100), Color.White, 1.5f);
        batch.Cross(particles.Position, 10, Color.Yellow);
        batch.DrawString(font, "Vivas: " + particles.Count + "/" + particles.Capacity, new Vector2(8, 600), Color.White, 2);
        batch.End();
    }

    protected override void UnloadContent() => dot.Dispose();
}
```

## Uso e limites

`ParticleSettings` é imutável: vida, intervalo de velocidade radial, gravidade, tamanho/cor inicial/final. `ParticleEmitter(texture, settings, capacity, seed)` aloca seu pool uma vez. O jogo mantém a textura e a libera em UnloadContent; pode compartilhar textura/configuração entre emissores. A mesma semente com a mesma sequência de operações/passos reproduz velocidades; não há promessa de determinismo global entre taxas de atualização diferentes.

`Burst(count, position)` retorna o número realmente emitido. Pool cheio descarta excedentes e não guarda fila. `Start/Stop` controlam emissão contínua em `EmissionRate` partículas/segundo; novas partículas nascem após um intervalo inteiro. Taxa zero desliga nascimentos. Mudar taxa, Stop ou Clear zera a fração acumulada. Clear remove partículas sem mudar taxa/estado/semente.

Update aceita segundos finitos não negativos, sem alocação. Expira antigas, calcula somente nascimentos ainda vivos no fim do passo e aceita os mais recentes que cabem; não percorre todos os ciclos perdidos nem faz catch-up ilimitado. A origem contínua é a Position no Update; não interpola o caminho arrastado. Nascimentos que não couberem são descartados. Como o pool é finito, passos diferentes sob saturação podem reter conjuntos diferentes; isso é limite explícito, não simulação física.

Draw usa um lote já aberto. Câmera, clip, sampler e blend vêm do SpriteBatch. A textura inteira vira um quad quadrado, tamanho em unidades do jogo, origem no centro, cor RGBA linear pela vida; não há correção gamma. Movimento é balístico analítico, com gravidade constante. Partículas fora do intervalo numérico float são omitidas do desenho até expirar. Não há culling de tela, depth sorting ou colisão; a remoção pode reordenar quads, portanto alpha transparente sobreposto pode mudar a composição. O exemplo usa Additive e HUD separado.

Update/Burst/Draw têm trabalho limitado à capacidade e não alocam no caminho quente. HUD formatado e criação de texturas/configuração/pool podem alocar. O exemplo inteiro não é anunciado como zero bytes.
