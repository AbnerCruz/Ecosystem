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

    /// <summary>Laboratório: painel de testes de aparelho em quatro páginas (dispositivos, gráficos, câmera e sliders de UI).</summary>
    public static string LabSource(string className) => $$"""
        using System.Numerics;
        using Lunet;
        using Lunet.Audio;
        using Lunet.Graphics;
        using Lunet.Input;
        using Lunet.UI;

        // Laboratório: cada bloco testa um recurso no aparelho.
        // Página 1: música, som, vibração, sensores, controle, gestos de dois dedos, joystick e botão de tela.
        // Página 2: mistura (blend), shader, recorte, alvo de desenho, amostragem, pixel perfect e área segura.
        // Página 3: câmera, conversão de toque, zoom, rotação e HUD fixo.
        // Página 4: sliders de UI, passos, disable e cancelamento na pausa.
        public sealed class {{className}} : Game
        {
            readonly RectangleF pageButton = new(8, 8, 344, 30);
            readonly RectangleF pixelButton = new(8, 440, 344, 30);
            readonly RectangleF resolutionButton = new(8, 474, 344, 30);
            readonly RectangleF cameraZoomIn = new(8, 44, 80, 46);
            readonly RectangleF cameraZoomOut = new(96, 44, 80, 46);
            readonly RectangleF cameraRotate = new(184, 44, 168, 46);
            readonly Camera2D camera = new() { Position = new Vector2(500, 400) };
            Vector2 cameraMarker = new(500, 400);
            bool altResolution;
            readonly TouchSlider radius = new(new RectangleF(24, 170, 312, 48), 16, 72, 36, knobWidth: 24);
            readonly TouchSlider level = new(new RectangleF(24, 274, 312, 48), 0, 100, 50, 10, 24);
            readonly RectangleF sliderToggle = new(24, 352, 312, 44);
            bool sliderDisabled;
            int sliderChanges;
            readonly (string Label, RectangleF Area)[] buttons =
            {
                ("Música liga/desliga (fade)", new RectangleF(8, 44, 344, 30)),
                ("Beep + vibrar", new RectangleF(8, 80, 344, 30)),
                ("Volume música +", new RectangleF(8, 116, 168, 30)),
                ("Volume música -", new RectangleF(184, 116, 168, 30)),
            };

            SpriteBatch batch = null!;
            SpriteFont font = null!;
            Texture2D pixel = null!;
            Texture2D dot = null!;
            Texture2D rainbow = null!;
            Texture2D checker = null!;
            SoundEffect beep = null!;
            Music theme = null!;
            VirtualStick stick = null!;
            VirtualButton fire = null!;
            RenderTarget2D minimap = null!;
            Shader gray = null!;
            Vector2 tilt = new(180, 250);
            Vector2 padBall = new(90, 350);
            Vector2 stickBall = new(80, 540);
            float boxSize = 60;
            float boxAngle;
            float clock;
            string lastGesture = "-";
            int fires;
            int page;

            protected override void LoadContent()
            {
                batch = new SpriteBatch(GraphicsDevice);
                font = SpriteFont.CreateDefault(GraphicsDevice);
                pixel = GraphicsDevice.WhiteTexture;
                dot = Texture2D.CreateCircle(GraphicsDevice, 64, Color.White);
                rainbow = MakeRainbow(64);
                checker = MakeChecker(8);
                beep = Content.LoadSound("Audio/beep.wav");
                theme = Content.LoadMusic("Audio/loop.wav");
                stick = new VirtualStick(new Vector2(80, 560), 50);
                fire = new VirtualButton(new Circle(new Vector2(290, 560), 36));
                minimap = new RenderTarget2D(GraphicsDevice, 96, 96);
                gray = Shader.FromFragmentSource(GraphicsDevice,
                    "uniform float uAmount; void main() { vec4 c = texture(uTex, vUv) * vColor; float g = dot(c.rgb, vec3(0.3, 0.59, 0.11)); outColor = vec4(mix(c.rgb, vec3(g), uAmount), c.a); }");
            }

            protected override void OnPause() { radius.Cancel(); level.Cancel(); }

            protected override void UnloadContent()
            {
                minimap.Dispose();
                gray.Dispose();
            }

            Texture2D MakeRainbow(int size)
            {
                var pixels = new byte[size * size * 4];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = (y * size + x) * 4;
                    pixels[i] = (byte)(255 * x / size);
                    pixels[i + 1] = (byte)(255 * y / size);
                    pixels[i + 2] = (byte)(255 - 255 * x / size);
                    pixels[i + 3] = 255;
                }
                return Texture2D.FromPixels(GraphicsDevice, size, size, pixels);
            }

            Texture2D MakeChecker(int size)
            {
                var pixels = new byte[size * size * 4];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = (y * size + x) * 4;
                    byte v = (byte)((x + y) % 2 == 0 ? 255 : 40);
                    pixels[i] = pixels[i + 1] = pixels[i + 2] = v;
                    pixels[i + 3] = 255;
                }
                return Texture2D.FromPixels(GraphicsDevice, size, size, pixels);
            }

            protected override void Update(GameTime time)
            {
                clock += time.DeltaSeconds;
                foreach (var gesture in Input.Gestures)
                {
                    lastGesture = gesture.Type.ToString();
                    switch (gesture.Type)
                    {
                        case GestureType.Tap:
                            if (pageButton.Contains(gesture.Position)) { page = (page + 1) % 4; break; }
                            if (page == 3)
                            {
                                if (sliderToggle.Contains(gesture.Position)) sliderDisabled = !sliderDisabled;
                                break;
                            }
                            if (page == 2)
                            {
                                if (cameraZoomIn.Contains(gesture.Position)) camera.Zoom = System.Math.Clamp(camera.Zoom * 2, 0.25f, 4);
                                else if (cameraZoomOut.Contains(gesture.Position)) camera.Zoom = System.Math.Clamp(camera.Zoom / 2, 0.25f, 4);
                                else if (cameraRotate.Contains(gesture.Position)) camera.Rotation += System.MathF.PI / 4;
                                break;
                            }
                            if (page == 1 && pixelButton.Contains(gesture.Position)) { GraphicsDevice.PixelPerfect = !GraphicsDevice.PixelPerfect; break; }
                            if (page == 1 && resolutionButton.Contains(gesture.Position))
                            {
                                altResolution = !altResolution;
                                if (altResolution) GraphicsDevice.SetVirtualResolution(350, 620);
                                else GraphicsDevice.SetVirtualResolution(360, 640);
                                break;
                            }
                            if (page != 0) break;
                            for (int i = 0; i < buttons.Length; i++)
                                if (buttons[i].Area.Contains(gesture.Position)) Press(i);
                            break;
                        case GestureType.DoubleTap:
                            break;
                        case GestureType.Pinch:
                            boxSize = System.Math.Clamp(boxSize * gesture.Scale, 20, 160);
                            break;
                        case GestureType.Rotate:
                            boxAngle += gesture.Rotation;
                            break;
                    }
                }

                radius.IsEnabled = page == 3; level.IsEnabled = page == 3 && !sliderDisabled;
                radius.Update(Input); level.Update(Input);
                if (page == 3)
                {
                    if (radius.WasChanged || level.WasChanged) sliderChanges++;
                    return;
                }

                if (page == 2)
                {
                    if (Input.TryGetPointer(out var touch) && touch.Y >= 120)
                        cameraMarker = camera.ScreenToWorld(touch, GraphicsDevice.ViewSize);
                    camera.Position = Vector2.Lerp(camera.Position, cameraMarker, System.Math.Clamp(time.DeltaSeconds * 5, 0, 1));
                    return;
                }

                var a = Input.Accelerometer;
                tilt += new Vector2(-a.X, a.Y) * 2f;
                tilt = Vector2.Clamp(tilt, new Vector2(10, 200), new Vector2(350, 300));

                var pad = Input.Gamepad;
                padBall += pad.LeftStick * 3f;
                padBall = Vector2.Clamp(padBall, new Vector2(10, 330), new Vector2(350, 400));
                if (Input.IsButtonPressed(GamepadButtons.A)) { beep.Play(); Haptics.Vibrate(30); }

                stick.Update(Input);
                fire.Update(Input);
                stickBall += stick.Direction * 3f;
                stickBall = Vector2.Clamp(stickBall, new Vector2(10, 470), new Vector2(350, 630));
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
                // Alvo de desenho primeiro: um minimapa girando (página 2 mostra o resultado).
                GraphicsDevice.SetRenderTarget(minimap);
                GraphicsDevice.Clear(new Color(30, 60, 30));
                batch.Begin();
                batch.Draw(rainbow, new RectangleF(48, 48, 48, 48), null, Color.White, clock, new Vector2(32, 32));
                batch.Cross(new Vector2(48, 48), 6, Color.White);
                batch.End();
                GraphicsDevice.SetRenderTarget(null);

                GraphicsDevice.Clear(new Color(18, 22, 40));
                batch.Begin();
                batch.FillRect(pageButton, new Color(90, 60, 120));
                batch.DrawString(font, $"Página {page + 1}/4 (toque para trocar)", pageButton.Position + new Vector2(6, 8), Color.White, 2);
                batch.End();
                if (page == 0) DrawDevices();
                else if (page == 1) DrawGraphics();
                else if (page == 2) DrawCamera();
                else DrawSliders();
            }

            void DrawSliders()
            {
                batch.Begin();
                batch.DrawString(font, "Sliders de UI", new Vector2(24, 64), Color.White, 2);
                batch.DrawString(font, "Arraste as barras. Teste dois dedos.", new Vector2(24, 96), Color.White, 1.4f);
                batch.DrawString(font, $"Raio continuo: {radius.Value:0.0}", new Vector2(24, 140), Color.White, 1.5f);
                radius.Draw(batch, TouchSliderStyle.Default);
                batch.DrawString(font, $"Passos de 10: {level.Value:0}", new Vector2(24, 244), Color.White, 1.5f);
                level.Draw(batch, TouchSliderStyle.Default);
                batch.FillRect(sliderToggle, new Color(60, 75, 110));
                batch.DrawString(font, sliderDisabled ? "Habilitar passos" : "Desabilitar passos", sliderToggle.Position + new Vector2(12, 14), Color.White, 1.5f);
                float r = radius.Value;
                batch.Draw(dot, new RectangleF(180 - r, 490 - r, r * 2, r * 2), null, new Color(30, (byte)(80 + level.Value), 200), 0, Vector2.Zero);
                batch.DrawString(font, $"Mudancas por toque: {sliderChanges}", new Vector2(24, 584), Color.White, 1.4f);
                batch.End();
            }

            void DrawCamera()
            {
                // Recorte em coordenadas da vista preserva o HUD no topo.
                batch.Begin(camera, clip: new RectangleF(0, 120, GraphicsDevice.ViewSize.X, GraphicsDevice.ViewSize.Y - 120));
                for (int i = 0; i <= 2000; i += 100)
                {
                    batch.Line(new Vector2(i, 0), new Vector2(i, 2000), Color.CornflowerBlue);
                    batch.Line(new Vector2(0, i), new Vector2(2000, i), Color.CornflowerBlue);
                }
                batch.Circle(cameraMarker, 12, Color.Yellow, 3);
                batch.End();
                batch.Begin();
                batch.FillRect(cameraZoomIn, new Color(52, 64, 110));
                batch.FillRect(cameraZoomOut, new Color(52, 64, 110));
                batch.FillRect(cameraRotate, new Color(52, 64, 110));
                batch.DrawString(font, "+ Zoom", cameraZoomIn.Position + new Vector2(6, 14), Color.White, 1.5f);
                batch.DrawString(font, "- Zoom", cameraZoomOut.Position + new Vector2(6, 14), Color.White, 1.5f);
                batch.DrawString(font, "Girar 45 graus", cameraRotate.Position + new Vector2(6, 14), Color.White, 1.5f);
                batch.DrawString(font, "Toque/arraste na grade. Os controles ficam fixos.", new Vector2(8, 102), Color.White, 1.1f);
                batch.End();
            }

            void DrawDevices()
            {
                batch.Begin();
                foreach (var (label, area) in buttons)
                {
                    batch.FillRect(area, new Color(52, 64, 110));
                    batch.DrawString(font, label, area.Position + new Vector2(6, 8), Color.White, 2);
                }

                var pos = new Vector2(8, 154);
                batch.DrawString(font, $"Música: {(Audio.CurrentMusic is null ? "parada" : "tocando")}  vol {Audio.MusicBus.Volume:0.0}", pos, Color.Yellow, 1.5f);
                var g = Input.Gyroscope;
                var acc = Input.Accelerometer;
                batch.DrawString(font, $"Acel {acc.X:0.0} {acc.Y:0.0} {acc.Z:0.0}  Giro {g.X:0.0} {g.Y:0.0} {g.Z:0.0}", pos + new Vector2(0, 16), Color.White, 1.5f);
                batch.DrawString(font, $"Último gesto: {lastGesture}   Toques: {Input.TouchCount}", pos + new Vector2(0, 32), Color.White, 1.5f);
                batch.DrawString(font, Input.Gamepad.IsConnected ? $"Controle: {Input.Gamepad.Buttons}" : "Controle: desconectado", new Vector2(8, 312), Color.Green, 1.5f);

                batch.Draw(dot, new RectangleF(tilt.X - 16, tilt.Y - 16, 32, 32), null, Color.CornflowerBlue, 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(padBall.X - 16, padBall.Y - 16, 32, 32), null, Color.Green, 0, Vector2.Zero);
                batch.Draw(pixel, new RectangleF(250, 420, boxSize, boxSize), null, Color.Yellow, boxAngle, new Vector2(0.5f, 0.5f));

                batch.DrawString(font, "Joystick e botão de tela", new Vector2(8, 450), Color.White, 1.5f);
                batch.Draw(dot, new RectangleF(stick.Center.X - 50, stick.Center.Y - 50, 100, 100), null, new Color(255, 255, 255, 40), 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(stickBall.X - 16, stickBall.Y - 16, 32, 32), null, Color.Red, 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(stick.Knob.X - 16, stick.Knob.Y - 16, 32, 32), null, new Color(255, 255, 255, 160), 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(290 - 36, 560 - 36, 72, 72), null, fire.IsDown ? Color.Yellow : new Color(120, 120, 120), 0, Vector2.Zero);
                batch.DrawString(font, $"x{fires}", new Vector2(278, 552), Color.Black, 2);
                batch.End();
            }

            void DrawGraphics()
            {
                var white = Color.White;

                // Mistura: aditivo (esquerda) x alfa (direita).
                batch.Begin(BlendState.Additive);
                batch.Draw(dot, new RectangleF(30, 70, 64, 64), null, new Color(255, 0, 0), 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(60, 70, 64, 64), null, new Color(0, 255, 0), 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(45, 100, 64, 64), null, new Color(0, 0, 255), 0, Vector2.Zero);
                batch.End();
                batch.Begin(BlendState.Alpha);
                batch.Draw(dot, new RectangleF(230, 70, 64, 64), null, new Color(255, 0, 0, 160), 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(260, 70, 64, 64), null, new Color(0, 255, 0, 160), 0, Vector2.Zero);
                batch.Draw(dot, new RectangleF(245, 100, 64, 64), null, new Color(0, 0, 255, 160), 0, Vector2.Zero);
                batch.DrawString(font, "Aditivo", new Vector2(50, 170), white, 1.5f);
                batch.DrawString(font, "Alfa", new Vector2(250, 170), white, 1.5f);
                batch.End();

                // Shader: cinza que oscila.
                gray.SetFloat("uAmount", 0.5f + 0.5f * System.MathF.Sin(clock * 2));
                batch.Begin(shader: gray);
                batch.Draw(rainbow, new RectangleF(20, 200, 96, 96), null, white, 0, Vector2.Zero);
                batch.End();

                // Amostragem: ponto x linear.
                batch.Begin(sampler: SamplerState.PointClamp);
                batch.Draw(checker, new RectangleF(140, 200, 64, 64), null, white, 0, Vector2.Zero);
                batch.End();
                batch.Begin(sampler: SamplerState.LinearClamp);
                batch.Draw(checker, new RectangleF(220, 200, 64, 64), null, white, 0, Vector2.Zero);
                batch.End();

                // Recorte: só o que está dentro do retângulo aparece.
                var clip = new RectangleF(20, 330, 150, 90);
                batch.Begin(clip: clip);
                float x = 20 + 75 + 90 * System.MathF.Sin(clock * 1.5f);
                batch.Draw(dot, new RectangleF(x - 40, 340, 80, 80), null, Color.Yellow, 0, Vector2.Zero);
                batch.End();
                batch.Begin();
                batch.Rect(clip, Color.Red, 2);
                batch.DrawString(font, "Recorte", new Vector2(20, 424), white, 1.5f);

                // Alvo de desenho (minimapa).
                batch.Draw(minimap.Texture, new RectangleF(200, 330, 96, 96), null, white, 0, Vector2.Zero);
                batch.Rect(new RectangleF(200, 330, 96, 96), white, 1);
                batch.DrawString(font, "Alvo", new Vector2(200, 430), white, 1.5f);

                // Pixel perfect: botão liga/desliga + grade de linhas de 1 pixel, que mostra a diferença.
                bool pp = GraphicsDevice.PixelPerfect;
                batch.FillRect(pixelButton, pp ? new Color(40, 120, 60) : new Color(120, 50, 50));
                batch.DrawString(font, pp ? "Pixel perfect: LIGADO (toque para desligar)" : "Pixel perfect: DESLIGADO (toque para ligar)", pixelButton.Position + new Vector2(6, 8), Color.White, 1.5f);
                batch.FillRect(resolutionButton, new Color(60, 60, 130));
                batch.DrawString(font, altResolution ? "Resolução virtual: 350x620 (escala fracionada)" : "Resolução virtual: 360x640 (toque para testar 350x620)", resolutionButton.Position + new Vector2(6, 8), Color.White, 1.2f);
                for (int i = 0; i < 40; i += 2) batch.FillRect(new RectangleF(20 + i, 512, 1, 24), Color.White); // linhas verticais de 1 px
                for (int i = 0; i < 24; i += 2) batch.FillRect(new RectangleF(70, 512 + i, 40, 1), Color.White);   // linhas horizontais de 1 px
                batch.DrawString(font, "Se a escala for inteira (ex.: 3,000) não há diferença; use o botão de resolução", new Vector2(8, 542), Color.Yellow, 1f);
                batch.DrawString(font, "acima e alterne o pixel perfect: as linhas ficam uniformes só quando ligado.", new Vector2(8, 554), Color.Yellow, 1f);

                batch.Rect(GraphicsDevice.SafeArea, new Color(255, 0, 255), 2);
                batch.DrawString(font, $"Escala {GraphicsDevice.Scale:0.000}  Densidade {GraphicsDevice.Density:0.0}", new Vector2(8, 570), white, 1.5f);
                var safe = GraphicsDevice.SafeArea;
                batch.DrawString(font, $"Área segura {safe.X:0} {safe.Y:0} {safe.Width:0} {safe.Height:0}", new Vector2(8, 586), Color.Yellow, 1.5f);
                batch.End();
            }
        }
        """.Replace("\r\n", "\n") + "\n";
}
