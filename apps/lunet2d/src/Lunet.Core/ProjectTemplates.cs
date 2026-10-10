namespace Lunet.Core;

/// <summary>Modelos de projeto iniciais.</summary>
public enum ProjectTemplate
{
    /// <summary>Jogo vazio.</summary>
    Blank,
    /// <summary>Jogo inicial de coletar moedas.</summary>
    CoinCatcher,
    /// <summary>Laboratório das APIs e dispositivos.</summary>
    Lab,
    /// <summary>Demonstração de animação de sprites e tween por toque.</summary>
    Animation,
    /// <summary>Demonstração de partículas com burst e fluxo contínuo.</summary>
    Particles
}

public static class ProjectTemplates
{
    /// <summary>Demonstração code-first de animação, pausa e tween por toque.</summary>
    /// <param name="className">Identificador C# válido para a classe do jogo.</param>
    /// <returns>Código completo do jogo procedural de animação/tween.</returns>
    public static string AnimationSource(string className) => $"""
        using System.Numerics;
        using Lunet;
        using Lunet.Graphics;
        using Lunet.Input;

        // Spritesheet procedural criada uma vez; todo movimento/animação usa APIs oficiais.
        public sealed class {{className}} : Game
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
        """;

    /// <summary>Demonstração de partículas com burst, fluxo contínuo e pool fixo.</summary>
    /// <param name="className">Identificador C# válido para a classe do jogo.</param>
    /// <returns>Código completo do jogo procedural de partículas.</returns>
    public static string ParticlesSource(string className) => $"""
        using System.Numerics;
        using Lunet;
        using Lunet.Graphics;
        using Lunet.Input;

        public sealed class {{className}} : Game
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
        """;

    public static string BlankGameSource(string className) => $$"""
        using System.Numerics;
        using Lunet;
        using Lunet.Graphics;

        // Toque na tela para mover a bola. Edite e aperte Run.
        public sealed class {{className}} : Game
        {
            SpriteBatch batch = null!;
            Texture2D ball = null!;
            Vector2 position;

            protected override void LoadContent()
            {
                batch = new SpriteBatch(GraphicsDevice);
                ball = Texture2D.CreateCircle(GraphicsDevice, 48, Color.Yellow);
                position = new Vector2(GraphicsDevice.VirtualWidth, GraphicsDevice.VirtualHeight) / 2;
                Log.Info("Jogo iniciado. Toque na tela.");
            }

            protected override void Update(GameTime time)
            {
                if (Input.TryGetPointer(out var pointer))
                    position = Vector2.Lerp(position, pointer, 0.25f);
            }

            protected override void Draw(GameTime time)
            {
                GraphicsDevice.Clear(Color.CornflowerBlue);
                batch.Begin();
                batch.Draw(ball, position - new Vector2(ball.Width, ball.Height) / 2, Color.White);
                batch.End();
            }
        }
        """.Replace("\r\n", "\n") + "\n";

    public static string CoinCatcherSource(string className) => $$"""
        using System.Collections.Generic;
        using System.Numerics;
        using Lunet;
        using Lunet.Audio;
        using Lunet.Graphics;
        using Lunet.Input;

        // Demo: toque nas moedas antes que caiam.
        // Mostra: texto, gestos (Tap), som, salvamento (recorde), aleatório com semente e colisão com círculo.
        public sealed class {{className}} : Game
        {
            sealed class Coin { public Vector2 Position; public float Speed; }
            sealed class Progress { public int Best; }

            const float CoinRadius = 20f;

            readonly RandomSource random = new(2024);
            readonly List<Coin> coins = new();
            SpriteBatch batch = null!;
            SpriteFont font = null!;
            Texture2D coinTexture = null!;
            SoundEffect beep = null!;
            [Inspect, Tooltip("Pontos desta partida")] int score;
            [Inspect, Tooltip("Recorde salvo")] int best;
            [Inspect, Range(0, 5), Tooltip("Vidas restantes")] int lives = 3;
            float spawnTimer;

            protected override void LoadContent()
            {
                batch = new SpriteBatch(GraphicsDevice);
                font = SpriteFont.CreateDefault(GraphicsDevice);
                coinTexture = Texture2D.CreateCircle(GraphicsDevice, (int)(CoinRadius * 2), Color.Yellow);
                beep = Content.LoadSound("Audio/beep.wav");
                best = Save.Load("progress", new Progress()).Best;
                Log.Info($"Recorde salvo: {best}");
            }

            protected override void Update(GameTime time)
            {
                bool tapped = false;
                foreach (var gesture in Input.Gestures)
                {
                    if (gesture.Type != GestureType.Tap) continue;
                    tapped = true;
                    if (lives > 0) TryCollect(gesture.Position);
                }

                if (lives <= 0)
                {
                    if (tapped) Restart();
                    return;
                }

                spawnTimer -= time.DeltaSeconds;
                if (spawnTimer <= 0)
                {
                    coins.Add(new Coin
                    {
                        Position = new Vector2(random.NextFloat(CoinRadius, GraphicsDevice.VirtualWidth - CoinRadius), -CoinRadius),
                        Speed = 90 + System.Math.Min(score * 4, 220),
                    });
                    spawnTimer = System.Math.Max(0.35f, 1f - score * 0.02f);
                }

                for (int i = coins.Count - 1; i >= 0; i--)
                {
                    coins[i].Position.Y += coins[i].Speed * time.DeltaSeconds;
                    if (coins[i].Position.Y > GraphicsDevice.VirtualHeight + CoinRadius)
                    {
                        coins.RemoveAt(i);
                        lives--;
                    }
                }

                if (lives <= 0 && score > best)
                {
                    best = score;
                    Save.Save("progress", new Progress { Best = best });
                    Log.Info($"Novo recorde: {best}");
                }
            }

            void TryCollect(Vector2 point)
            {
                for (int i = coins.Count - 1; i >= 0; i--)
                {
                    if (!new Circle(coins[i].Position, CoinRadius + 8).Contains(point)) continue;
                    coins.RemoveAt(i);
                    score++;
                    beep.Play(1f, 0f, 1f + score * 0.01f, false);
                    return;
                }
            }

            void Restart()
            {
                coins.Clear();
                score = 0;
                lives = 3;
                spawnTimer = 0;
            }

            protected override void Draw(GameTime time)
            {
                GraphicsDevice.Clear(new Color(20, 24, 48));
                batch.Begin();
                foreach (var coin in coins)
                    batch.Draw(coinTexture, coin.Position - new Vector2(CoinRadius, CoinRadius), Color.White);

                batch.DrawString(font, $"Pontos: {score}", new Vector2(8, 8), Color.White, 2);
                batch.DrawString(font, $"Vidas: {lives}", new Vector2(8, 32), Color.Red, 2);
                batch.DrawString(font, $"Recorde: {best}", new Vector2(8, 56), Color.Green, 2);

                if (lives <= 0)
                {
                    string text = "Fim de jogo!\nToque para jogar de novo";
                    var size = font.Measure(text, 2);
                    var center = new Vector2(GraphicsDevice.VirtualWidth, GraphicsDevice.VirtualHeight) / 2;
                    batch.DrawString(font, text, center - size / 2, Color.White, 2);
                }
                batch.End();
            }
        }
        """.Replace("\r\n", "\n") + "\n";

    /// <summary>Laboratório: painel de testes de aparelho em sete páginas (dispositivos, gráficos, câmera, sliders, botões, cenas e debug).</summary>
    public static string LabSource(string className) => LaboratoryTemplate.Source(className);

}
