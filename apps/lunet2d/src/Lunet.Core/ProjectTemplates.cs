namespace Lunet.Core;

/// <summary>Modelos de projeto iniciais.</summary>
public enum ProjectTemplate { Blank, CoinCatcher }

public static class ProjectTemplates
{
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
            int score, best, lives = 3;
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
}
