# Animação de sprites e tweening

As APIs funcionam só com código. Não precisam de Studio, servidor ou scheduler global.

## Teste rápido no celular

Crie um projeto com o modelo **Demo: Animação e tween**, aperte **Run** e teste offline:

1. O personagem alterna quatro quadros, sem mudar de tamanho ou posição sozinho.
2. Toque abaixo dos botões: ele anda até o marcador amarelo desacelerando por 0,6 segundo.
3. Toque em outro destino durante o movimento: ele parte da posição atual, sem saltar.
4. **Pausar** congela quadro e transição. Escolha um novo destino enquanto pausado; só anda após **Retomar**.
5. **Reiniciar** volta ao centro e retoma a animação. Repita no Preview rápido e isolado.
6. Feche/reabra o projeto e rode novamente; verifique também um projeto existente.

Este roteiro é validação intermediária, não fecha o gate da Fase 4. Testes em memória não comprovam GL/toque no aparelho.

## Exemplo completo

O modelo cria seus próprios pixels uma vez no LoadContent. Também pode substituir a textura por Content.LoadTexture e usar SpriteSheet ou regiões de atlas.

```csharp
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Input;

// Spritesheet procedural criada uma vez; todo movimento/animação usa APIs oficiais.
public sealed class AnimationDemo : Game
{
    readonly RectangleF pauseButton = new(8, 44, 164, 44);
    readonly RectangleF restartButton = new(188, 44, 164, 44);
    readonly Tween motion = new(0.6, Ease.OutQuad);
    SpriteBatch batch = null!;
    SpriteFont font = null!;
    Texture2D sheetTexture = null!;
    SpriteAnimator animator = null!;
    Vector2 from = new(180, 320);
    Vector2 target = new(180, 320);
    Vector2 position = new(180, 320);
    bool paused;

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        var pixels = new byte[128 * 32 * 4];
        for (int frame = 0; frame < 4; frame++)
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            bool body = x >= 6 && x < 26 && y >= 6 && y < 26;
            bool eye = y >= 12 && y < (frame == 2 ? 13 : 16) && (x >= 10 && x < 13 || x >= 19 && x < 22);
            bool foot = y >= 26 && y < 30 && (frame % 2 == 0 ? x >= 7 && x < 13 : x >= 19 && x < 25);
            Color color = eye ? Color.Black : body ? new Color(90, 220, 190) : foot ? Color.Yellow : Color.Transparent;
            int i = (y * 128 + frame * 32 + x) * 4;
            pixels[i] = color.R; pixels[i + 1] = color.G; pixels[i + 2] = color.B; pixels[i + 3] = color.A;
        }
        sheetTexture = Texture2D.FromPixels(GraphicsDevice, 128, 32, pixels, TextureFilter.Point);
        var sheet = new SpriteSheet(sheetTexture, 32, 32);
        animator = new SpriteAnimator(SpriteAnimationClip.FromSheet(sheet, new[] { 0, 1, 2, 3 }, 0.125));
    }

    protected override void Update(GameTime time)
    {
        foreach (var gesture in Input.Gestures)
        {
            if (gesture.Type != GestureType.Tap) continue;
            if (pauseButton.Contains(gesture.Position))
            {
                paused = !paused;
                if (paused) { animator.Pause(); motion.Pause(); }
                else { animator.Resume(); motion.Resume(); }
            }
            else if (restartButton.Contains(gesture.Position))
            {
                paused = false;
                animator.Restart();
                from = position = new Vector2(180, 320);
                target = from;
                motion.Restart();
            }
            else if (gesture.Position.Y >= 120)
            {
                // Rebase no valor exibido para não saltar ao trocar destino durante o movimento.
                from = position;
                target = Vector2.Clamp(gesture.Position, new Vector2(32, 152), GraphicsDevice.ViewSize - new Vector2(32, 32));
                motion.Restart();
                if (paused) motion.Pause();
            }
        }
        animator.Update(time.DeltaSeconds);
        motion.Update(time.DeltaSeconds);
        position = motion.Value(from, target);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(18, 22, 40));
        batch.Begin(sampler: SamplerState.PointClamp);
        batch.DrawString(font, "Animacao + tween", new Vector2(8, 12), Color.White, 2);
        batch.FillRect(pauseButton, paused ? Color.Green : new Color(70, 80, 130));
        batch.FillRect(restartButton, new Color(70, 80, 130));
        batch.DrawString(font, paused ? "Retomar" : "Pausar", pauseButton.Position + new Vector2(12, 14), Color.White, 2);
        batch.DrawString(font, "Reiniciar", restartButton.Position + new Vector2(12, 14), Color.White, 2);
        batch.DrawString(font, "Toque abaixo para mover suavemente", new Vector2(8, 100), Color.White, 1.5f);
        batch.Cross(target, 10, Color.Yellow);
        batch.Draw(animator.Clip.Texture, new RectangleF(position.X - 32, position.Y - 32, 64, 64), animator.Source, motion.Value(Color.White, Color.Yellow), 0, new Vector2(16));
        batch.DrawString(font, "Quadro " + animator.FrameIndex + "  progresso " + motion.Progress.ToString("0.00"), new Vector2(8, 600), Color.White, 1.5f);
        batch.End();
    }

    protected override void UnloadContent() => sheetTexture.Dispose();
}
```

## Clips e playback

`SpriteAnimationClip.FromSheet(sheet, indices, frameSeconds, loop)` aceita uma ordem de quadros, inclusive índices repetidos. Para durações ou regiões diferentes, use o construtor com `SpriteAnimationFrame[]`. As regiões devem caber na textura; durações devem ser positivas, finitas e representáveis na soma. O clip copia a sequência e pode ser compartilhado por vários `SpriteAnimator`, cada um com seu relógio.

`Update(deltaSeconds)` recebe o tempo simulado em segundos, não FPS. Zero preserva o estado; delta negativo/NaN/infinito gera erro antes de mudar o estado. Deltas grandes pulam diretamente para o quadro atual em busca binária; não percorrem loops nem disparam eventos de quadros pulados. Em `loop: false`, o fim mantém o último quadro e `IsComplete` fica verdadeiro. `Pause` guarda a posição; `Resume` não reinicia uma animação completa; `Restart` recomeça. `Play(clip)` troca e reinicia somente se o clip mudou; `Play(clip, restart: true)` força reinício. Repetir Play do mesmo clip a cada Update não impede sua evolução.

O animador não libera a textura. O jogo mantém seu ownership e a libera em UnloadContent. Desenhe Texture/Source com o SpriteBatch existente; o pivô/escala/cor continuam definidos pelo jogo.

## Tween

`Tween(durationSeconds, ease)` é um relógio finito que satura no fim. Duração zero começa completa. `Progress` é linear; `Amount` aplica Linear, InQuad, OutQuad, InOutQuad ou SmoothStep. `Value(from, to)` interpola float, Vector2 ou Color sem callbacks. Para retarget sem salto, use o valor atualmente exibido como novo início e chame Restart. Pause/Resume e Restart não criam objetos.

Endpoints numéricos devem ser finitos. Cor interpola RGBA em bytes, com arredondamento; não faz correção gamma nem escolhe blend por você. Update depende do jogo: se não avançar o relógio, não há movimento. Loops, ping-pong, estados, eventos e composição de transições podem ser escritos em C# pelo jogo; esta entrega não adiciona um editor visual ou scheduler global.

Clips e texturas alocam na criação; playback e amostragem de tween não alocam por quadro. O HUD do exemplo formata texto, portanto o exemplo inteiro não é anunciado como zero alocações.
