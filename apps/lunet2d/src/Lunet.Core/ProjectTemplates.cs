namespace Lunet.Core;

/// <summary>Modelos de projeto iniciais.</summary>
public enum ProjectTemplate { Blank, CoinCatcher, Lab }

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

    /// <summary>Laboratório: painel de testes de aparelho (áudio, sensores, controle, gestos, controles virtuais).</summary>
    public static string LabSource(string className) => $$"""
        using System.Numerics;
        using Lunet;
        using Lunet.Audio;
        using Lunet.Graphics;
        using Lunet.Input;

        // Laboratório: cada bloco testa um recurso no aparelho. Toque nos botões; incline o aparelho;
        // conecte um controle; faça pinça e giro com dois dedos.
        public sealed class {{className}} : Game
        {
            readonly (string Label, RectangleF Area)[] buttons =
            {
                ("Música liga/desliga (fade)", new RectangleF(8, 8, 344, 34)),
                ("Beep + vibrar", new RectangleF(8, 48, 344, 34)),
                ("Volume música +", new RectangleF(8, 88, 168, 34)),
                ("Volume música -", new RectangleF(184, 88, 168, 34)),
            };

            SpriteBatch batch = null!;
            SpriteFont font = null!;
            Texture2D pixel = null!;
            Texture2D dot = null!;
            SoundEffect beep = null!;
            Music theme = null!;
            VirtualStick stick = null!;
            VirtualButton fire = null!;
            Vector2 tilt = new(180, 300);
            Vector2 padBall = new(90, 330);
            Vector2 stickBall = new(80, 560);
            float boxSize = 60;
            float boxAngle;
            string lastGesture = "-";
            int fires;

            protected override void LoadContent()
            {
                batch = new SpriteBatch(GraphicsDevice);
                font = SpriteFont.CreateDefault(GraphicsDevice);
                pixel = Texture2D.CreateSolid(GraphicsDevice, 1, 1, Color.White);
                dot = Texture2D.CreateCircle(GraphicsDevice, 32, Color.White);
                beep = Content.LoadSound("Audio/beep.wav");
                theme = Content.LoadMusic("Audio/loop.wav");
                stick = new VirtualStick(new Vector2(80, 560), 50);
                fire = new VirtualButton(new Circle(new Vector2(290, 560), 36));
            }

            protected override void Update(GameTime time)
            {
                foreach (var gesture in Input.Gestures)
                {
                    lastGesture = gesture.Type.ToString();
                    switch (gesture.Type)
                    {
                        case GestureType.Tap:
                            for (int i = 0; i < buttons.Length; i++)
                                if (buttons[i].Area.Contains(gesture.Position)) Press(i);
                            break;
                        case GestureType.Pinch:
                            boxSize = System.Math.Clamp(boxSize * gesture.Scale, 20, 160);
                            break;
                        case GestureType.Rotate:
                            boxAngle += gesture.Rotation;
                            break;
                    }
                }

                // Acelerômetro: inclinar move a bola azul.
                var a = Input.Accelerometer;
                tilt += new Vector2(-a.X, a.Y) * 2f;
                tilt = Vector2.Clamp(tilt, new Vector2(10, 140), new Vector2(350, 300));

                // Controle: stick esquerdo move a bola verde; A vibra.
                var pad = Input.Gamepad;
                padBall += pad.LeftStick * 3f;
                padBall = Vector2.Clamp(padBall, new Vector2(10, 310), new Vector2(350, 420));
                if (Input.IsButtonPressed(GamepadButtons.A)) { beep.Play(); Haptics.Vibrate(30); }

                stick.Update(Input);
                fire.Update(Input);
                stickBall += stick.Direction * 3f;
                stickBall = Vector2.Clamp(stickBall, new Vector2(10, 440), new Vector2(350, 630));
                if (fire.WasPressed) { fires++; beep.Play(1f, 0f, 1.5f, false); Haptics.Vibrate(15, 0.4f); }
            }

            void Press(int index)
            {
                switch (index)
                {
                    case 0:
                        if (Audio.CurrentMusic is null) Audio.PlayMusic(theme, loop: true, fadeInSeconds: 1.5f);
                        else Audio.StopMusic(fadeOutSeconds: 1.5f);
                        break;
                    case 1:
                        beep.Play();
                        Haptics.Vibrate(60);
                        break;
                    case 2:
                        Audio.MusicBus.Volume += 0.2f;
                        break;
                    case 3:
                        Audio.MusicBus.Volume -= 0.2f;
                        break;
                }
            }

            protected override void Draw(GameTime time)
            {
                GraphicsDevice.Clear(new Color(18, 22, 40));
                batch.Begin();
                foreach (var (label, area) in buttons)
                {
                    batch.Draw(pixel, area, null, new Color(52, 64, 110), 0f, Vector2.Zero);
                    batch.DrawString(font, label, area.Position + new Vector2(6, 8), Color.White, 2);
                }

                var pos = new Vector2(8, 130);
                batch.DrawString(font, $"Música: {(Audio.CurrentMusic is null ? "parada" : "tocando")}  vol {Audio.MusicBus.Volume:0.0}", pos, Color.Yellow, 1.5f);
                var g = Input.Gyroscope;
                var acc = Input.Accelerometer;
                batch.DrawString(font, $"Acel {acc.X:0.0} {acc.Y:0.0} {acc.Z:0.0}  Giro {g.X:0.0} {g.Y:0.0} {g.Z:0.0}", pos + new Vector2(0, 16), Color.White, 1.5f);
                batch.DrawString(font, $"Último gesto: {lastGesture}   Toques: {Input.TouchCount}", pos + new Vector2(0, 32), Color.White, 1.5f);
                batch.DrawString(font, Input.Gamepad.IsConnected ? $"Controle: {Input.Gamepad.Buttons}" : "Controle: desconectado", pos + new Vector2(0, 176), Color.Green, 1.5f);

                batch.Draw(dot, tilt - new Vector2(16, 16), Color.CornflowerBlue);
                batch.Draw(dot, padBall - new Vector2(16, 16), Color.Green);
                batch.Draw(pixel, new RectangleF(250, 200, boxSize, boxSize), null, Color.Yellow, boxAngle, new Vector2(0.5f, 0.5f));

                batch.DrawString(font, "Joystick e botão de tela", new Vector2(8, 430), Color.White, 1.5f);
                batch.Draw(dot, new RectangleF(stick.Center.X - 50, stick.Center.Y - 50, 100, 100), null, new Color(255, 255, 255, 40), 0, Vector2.Zero);
                batch.Draw(dot, stickBall - new Vector2(16, 16), Color.Red);
                batch.Draw(dot, stick.Knob - new Vector2(16, 16), new Color(255, 255, 255, 160));
                batch.Draw(dot, new Vector2(290 - 36, 560 - 36), fire.IsDown ? Color.Yellow : new Color(120, 120, 120));
                batch.DrawString(font, $"x{fires}", new Vector2(278, 552), Color.Black, 2);
                batch.End();
            }
        }
        """.Replace("\r\n", "\n") + "\n";
}
