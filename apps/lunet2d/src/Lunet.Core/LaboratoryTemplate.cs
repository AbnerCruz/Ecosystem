namespace Lunet.Core;

/// <summary>Template auto-suficiente de validação Android: uma única entrada de projeto e páginas organizadas por área.</summary>
/// <remarks>Separado dos templates gerais para evitar acumular regras de tela no ProjectTemplates.
/// O projeto criado recebe Game.cs normal, offline e editável; o gerador não escreve em projetos anteriores.</remarks>
internal static class LaboratoryTemplate
{
    public static string Source(string className) => $$"""
        using System.Numerics;
        using Lunet;
        using Lunet.Audio;
        using Lunet.Graphics;
        using Lunet.Input;
        using Lunet.UI;
        using Lunet.Scenes;
        using Lunet.Pathfinding;
        using System.Collections.Generic;

        // Laboratório 2.0: áreas independentes, índice tocável e navegação anterior/próxima.
        // Compatível com os testes das sete áreas originais; novas áreas validam APIs da Fase 4.
        // O código fonte gerado é do projeto e não altera projetos já existentes.
        public sealed class {{className}} : Game
        {
            const int PageCount = 12;
            readonly TouchButton pageControl = new(new RectangleF(8, 8, 274, 30));
            readonly TouchButton menuControl = new(new RectangleF(288, 8, 64, 30));
            readonly TouchButton previousControl = new(new RectangleF(8, 604, 108, 30));
            readonly TouchButton nextControl = new(new RectangleF(244, 604, 108, 30));
            bool menuOpen;
            readonly string[] pageNames =
            {
                "Dispositivos", "Graficos", "Camera", "Sliders", "Botoes", "Cenas",
                "Depuracao", "Animacao e FX", "Tilemap e A*", "Colisoes", "Fontes e Paineis", "Temas de UI"
            };
            readonly string[] pageHints =
            {
                "Audio / sensores", "Shader / render", "Zoom / mundo", "Dois dedos / steps",
                "Clique / layout", "Entidades / estado", "Raios / eixos", "Tween / particulas",
                "Path / camera", "SAT / overlap", "Bitmap / nine-slice", "Paletas / estados"
            };
            readonly RectangleF pixelButton = new(8, 440, 344, 30);
            readonly RectangleF resolutionButton = new(8, 474, 344, 30);
            readonly RectangleF viewportModeButton = new(8, 280, 344, 37);
            readonly RectangleF cameraZoomIn = new(8, 44, 80, 46);
            readonly RectangleF cameraZoomOut = new(96, 44, 80, 46);
            readonly RectangleF cameraRotate = new(184, 44, 168, 46);
            readonly RectangleF cameraClampToggle = new(8, 530, 344, 48);
            bool cameraBounded;
            readonly Camera2D camera = new() { Position = new Vector2(500, 400) };
            Vector2 cameraMarker = new(500, 400);
            bool altResolution;
            readonly TouchSlider radius = new(new RectangleF(24, 170, 312, 48), 16, 72, 36, knobWidth: 24);
            readonly TouchSlider level = new(new RectangleF(24, 274, 312, 48), 0, 100, 50, 10, 24);
            readonly RectangleF sliderToggle = new(24, 352, 312, 44);
            bool sliderDisabled;
            int sliderChanges;
            readonly TouchButton clickButton = new(new RectangleF(24, 174, 312, 52));
            readonly TouchButton enableButton = new(new RectangleF(24, 258, 312, 52));
            readonly TouchButton moveButton = new(new RectangleF(24, 342, 312, 52));
            bool buttonDisabled;
            bool buttonMoved;
            int buttonClicks;
            readonly Scene2D scene = new();
            readonly Entity2D sceneActor = new();
            readonly TouchButton scenePause = new(new RectangleF(24, 352, 312, 44));
            readonly TouchButton sceneHide = new(new RectangleF(24, 408, 312, 44));
            readonly TouchButton sceneAttach = new(new RectangleF(24, 464, 312, 44));
            SceneMotion sceneMotion = null!;
            readonly Vector2[] debugVertices = { new(-44, -32), new(44, -32), new(44, 32), new(-44, 32) };
            Transform2D debugTransform = new(new Vector2(180, 270));
            Vector2 debugTarget = new(300, 310);
            readonly Camera2D debugCamera = new() { Position = new Vector2(180, 270), Zoom = 1.2f };
            readonly TouchButton debugToggle = new(new RectangleF(24, 416, 312, 44));
            readonly TouchButton debugRotate = new(new RectangleF(24, 472, 148, 44));
            readonly TouchButton debugReflect = new(new RectangleF(188, 472, 148, 44));
            readonly TouchButton debugView = new(new RectangleF(24, 528, 312, 44));
            bool debugShown = true, debugUseCamera;
            Ray2D debugRay = new(new Vector2(40, 270), Vector2.UnitX);
            bool debugHit;
            sealed class SceneMotion : Component2D
            {
                public float Phase;
                public override void Update(GameTime time)
                {
                    Phase += time.DeltaSeconds;
                    Entity!.Transform.Position = new Vector2(180 + 100 * System.MathF.Sin(Phase * 2), 240);
                }
            }
            sealed class ScenePainter : Component2D
            {
                readonly Texture2D texture;
                public ScenePainter(Texture2D texture) { this.texture = texture; }
                public override void Draw(SpriteBatch batch, GameTime time)
                {
                    var position = Entity!.Transform.Position;
                    batch.Draw(texture, new RectangleF(position.X - 24, position.Y - 24, 48, 48), null, Color.Yellow, 0, Vector2.Zero);
                }
            }
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

            // Módulo 8: animação, tweening e partículas com efeitos em pool fixo.
            readonly Tween fxTween = new(0.65, Ease.OutQuad);
            SpriteAnimator fxAnimator = null!;
            ParticleEmitter fxParticles = null!;
            Vector2 fxFrom = new(180, 340), fxTarget = new(180, 340), fxPosition = new(180, 340);
            bool fxPaused;
            readonly RectangleF fxPauseButton = new(16, 72, 152, 44);
            readonly RectangleF fxBurstButton = new(192, 72, 152, 44);

            // Módulo 9: tilemap, obstáculos, pathfinding e viewport real da câmera.
            readonly TileMap tileMap = TileMap.Parse("{\"version\":1,\"texture\":\"Textures/lab.png\",\"width\":6,\"height\":6,\"tileWidth\":48,\"tileHeight\":48,\"layers\":[{\"name\":\"ground\",\"tiles\":[1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]},{\"name\":\"walls\",\"collision\":true,\"tiles\":[0,0,0,0,0,0,0,0,0,2,0,0,0,0,0,2,0,0,0,0,0,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0]}]}");
            readonly GridPathfinder tileGrid = new(6, 6);
            readonly GridPoint[] tilePath = new GridPoint[36];
            readonly Vector2 tileOrigin = new(28, 158);
            readonly Camera2D tileCamera = new() { Position = new Vector2(180, 320) };
            PathResult tileRoute;
            Texture2D tileTexture = null!;
            readonly RectangleF tileZoomButton = new(16, 68, 152, 40);
            readonly RectangleF tileRotateButton = new(192, 68, 152, 40);
            bool tileCameraRotated;

            // Módulo 10: colisão SAT, área desejada e corpo resolvido.
            readonly Vector2[] collisionWall =
            {
                new(102, 216), new(258, 216), new(258, 378), new(102, 378)
            };
            readonly Vector2[] collisionBody = new Vector2[4];
            Vector2 collisionDesired = new(140, 268);
            Vector2 collisionResolved = new(140, 268);
            Vector2 collisionPush;

            // Módulo 11: nine-slice redimensionável e fonte bitmap customizada.
            NineSlice skin = null!;
            Texture2D panelTexture = null!;
            SpriteFont digits = null!;
            Texture2D digitAtlas = null!;
            readonly RectangleF fontScaleButton = new(16, 88, 152, 42);
            readonly RectangleF skinScaleButton = new(192, 88, 152, 42);
            float bitmapScale = 2, skinScale = 1;
            int score = 1234;

            // Módulo 12: aplicação de paletas locais nos estados reais de UI.
            readonly TouchButton themeDemoButton = new(new RectangleF(34, 254, 292, 54));
            readonly TouchSlider themeDemoSlider = new(new RectangleF(34, 356, 292, 48), 0, 100, 50, 10, 24);
            readonly RectangleF themePickDark = new(16, 96, 100, 56);
            readonly RectangleF themePickLight = new(130, 96, 100, 56);
            readonly RectangleF themePickContrast = new(244, 96, 100, 56);
            int paletteIndex;
            int themeClicks;
            // Usa os temas oficiais do Framework, sem duplicar paletas no template.
            readonly UiTheme[] themePresets =
            {
                UiTheme.Dark, UiTheme.Light, UiTheme.HighContrast
            };

            protected override void LoadContent()
            {
                batch = new SpriteBatch(GraphicsDevice);
                font = SpriteFont.CreateDefault(GraphicsDevice);
                pixel = GraphicsDevice.WhiteTexture;
                dot = Texture2D.CreateCircle(GraphicsDevice, 64, Color.White);
                sceneActor.Transform.Position = new Vector2(180, 240);
                sceneMotion = new SceneMotion(); sceneActor.Add(sceneMotion);
                sceneActor.Add(new ScenePainter(dot)); scene.Add(sceneActor);
                rainbow = MakeRainbow(64);
                checker = MakeChecker(8);
                beep = Content.LoadSound("Audio/beep.wav");
                theme = Content.LoadMusic("Audio/loop.wav");
                stick = new VirtualStick(new Vector2(80, 560), 50);
                fire = new VirtualButton(new Circle(new Vector2(290, 560), 36));
                minimap = new RenderTarget2D(GraphicsDevice, 96, 96);
                InitializeAdvancedDemos();
                gray = Shader.FromFragmentSource(GraphicsDevice,
                    "uniform float uAmount; void main() { vec4 c = texture(uTex, vUv) * vColor; float g = dot(c.rgb, vec3(0.3, 0.59, 0.11)); outColor = vec4(mix(c.rgb, vec3(g), uAmount), c.a); }");
            }

            protected override void OnPause()
            {
                radius.Cancel(); level.Cancel(); pageControl.Cancel(); menuControl.Cancel();
                previousControl.Cancel(); nextControl.Cancel();
                clickButton.Cancel(); enableButton.Cancel(); moveButton.Cancel();
                scenePause.Cancel(); sceneHide.Cancel(); sceneAttach.Cancel();
                debugToggle.Cancel(); debugRotate.Cancel(); debugReflect.Cancel(); debugView.Cancel();
                themeDemoButton.Cancel(); themeDemoSlider.Cancel();
            }

            protected override void UnloadContent()
            {
                minimap.Dispose();
                gray.Dispose();
                tileTexture.Dispose();
                digitAtlas.Dispose();
                panelTexture.Dispose();
            }


            void InitializeAdvancedDemos()
            {
                // Quatro frames horizontais do atlas gerado, sem asset externo.
                fxAnimator = new SpriteAnimator(SpriteAnimationClip.FromSheet(
                    new SpriteSheet(rainbow, 16, 64), new[] { 0, 1, 2, 3 }, 0.125));
                fxParticles = new ParticleEmitter(dot,
                    new ParticleSettings(lifetimeSeconds: 1.4, minSpeed: 45, maxSpeed: 140,
                        gravity: new Vector2(0, 85), startSize: 12, endSize: 2,
                        startColor: Color.Yellow, endColor: Color.Red.WithAlpha(0)),
                    capacity: 192, seed: 42) { Position = fxPosition, EmissionRate = 50 };
                fxParticles.Burst(20, fxPosition);

                var pixels = new byte[96 * 48 * 4];
                for (int y = 0; y < 48; y++)
                for (int x = 0; x < 96; x++)
                {
                    int offset = (y * 96 + x) * 4;
                    bool wall = x >= 48;
                    byte green = (byte)(((x / 12 + y / 12) % 2 == 0) ? 110 : 135);
                    pixels[offset] = wall ? (byte)179 : (byte)55;
                    pixels[offset + 1] = wall ? (byte)68 : green;
                    pixels[offset + 2] = wall ? (byte)62 : (byte)100;
                    pixels[offset + 3] = 255;
                }
                tileTexture = Texture2D.FromPixels(GraphicsDevice, 96, 48, pixels, TextureFilter.Point);
                tileMap.CopyCollisionTo(tileGrid);
                tileRoute = tileGrid.FindPath(new GridPoint(0, 0), new GridPoint(5, 5), tilePath);

                // Skin procedural de 9 recortes, em vez de reutilizar o checker de teste gráfico.
                var skinPixels = new byte[24 * 24 * 4];
                for (int y = 0; y < 24; y++)
                for (int x = 0; x < 24; x++)
                {
                    bool frame = x < 6 || x >= 18 || y < 6 || y >= 18;
                    bool corner = (x < 6 || x >= 18) && (y < 6 || y >= 18);
                    var color = corner ? new Color(95, 154, 215)
                        : frame ? new Color(55, 104, 165) : new Color(24, 42, 67);
                    int o = (y * 24 + x) * 4;
                    skinPixels[o] = color.R; skinPixels[o + 1] = color.G;
                    skinPixels[o + 2] = color.B; skinPixels[o + 3] = color.A;
                }
                panelTexture = Texture2D.FromPixels(GraphicsDevice, 24, 24, skinPixels, TextureFilter.Point);
                skin = new NineSlice(panelTexture, new RectangleF(0, 0, 24, 24), 6, 6, 6, 6);
                var glyphPixels = new byte[80 * 12 * 4];
                int[] mask = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };
                for (int digit = 0; digit < 10; digit++)
                for (int y = 0; y < 12; y++)
                for (int x = 0; x < 8; x++)
                {
                    bool on = (y < 2 && x >= 1 && x < 7 && (mask[digit] & 1) != 0)
                        || (x >= 6 && y >= 1 && y < 6 && (mask[digit] & 2) != 0)
                        || (x >= 6 && y >= 6 && y < 11 && (mask[digit] & 4) != 0)
                        || (y >= 10 && x >= 1 && x < 7 && (mask[digit] & 8) != 0)
                        || (x < 2 && y >= 6 && y < 11 && (mask[digit] & 16) != 0)
                        || (x < 2 && y >= 1 && y < 6 && (mask[digit] & 32) != 0)
                        || (y >= 5 && y < 7 && x >= 1 && x < 7 && (mask[digit] & 64) != 0);
                    if (!on) continue;
                    int offset = (y * 80 + digit * 8 + x) * 4;
                    glyphPixels[offset] = glyphPixels[offset + 1] = glyphPixels[offset + 2] = glyphPixels[offset + 3] = 255;
                }
                digitAtlas = Texture2D.FromPixels(GraphicsDevice, 80, 12, glyphPixels, TextureFilter.Point);
                var glyphs = new Dictionary<int, BitmapGlyph>();
                for (int d = 0; d < 10; d++)
                    glyphs['0' + d] = d == 1
                        ? new BitmapGlyph(new RectangleF(14, 0, 2, 12), 4)
                        : new BitmapGlyph(new RectangleF(d * 8, 0, 8, 12), 10);
                glyphs[' '] = new BitmapGlyph(default, 5);
                digits = SpriteFont.FromBitmap(digitAtlas, glyphs, 16, '0');
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


            void SetPage(int value)
            {
                page = (value + PageCount) % PageCount;
                pageControl.Cancel();
                previousControl.Cancel();
                nextControl.Cancel();
                radius.Cancel(); level.Cancel();
                clickButton.Cancel(); enableButton.Cancel(); moveButton.Cancel();
                scenePause.Cancel(); sceneHide.Cancel(); sceneAttach.Cancel();
                debugToggle.Cancel(); debugRotate.Cancel(); debugReflect.Cancel(); debugView.Cancel();
                themeDemoButton.Cancel(); themeDemoSlider.Cancel();
            }

            bool UpdateNavigation()
            {
                menuControl.Update(Input);
                if (menuControl.WasClicked)
                {
                    menuOpen = !menuOpen;
                    if (menuOpen) OnPause();
                }
                if (menuOpen)
                {
                    pageControl.IsEnabled = false;
                    previousControl.IsEnabled = false;
                    nextControl.IsEnabled = false;
                    foreach (var touch in Input.Touches)
                    {
                        if (touch.Phase != TouchPhase.Pressed) continue;
                        var point = touch.Position;
                        if (point.Y < 92 || point.Y >= 566) continue;
                        int row = (int)((point.Y - 92) / 77);
                        int column = point.X >= 182 ? 1 : 0;
                        int target = row * 2 + column;
                        var bounds = new RectangleF(column == 0 ? 8 : 184, 92 + row * 77, 168, 68);
                        if (target < PageCount && bounds.Contains(point))
                        {
                            SetPage(target);
                            menuOpen = false;
                            break;
                        }
                    }
                    return true;
                }
                pageControl.IsEnabled = true;
                previousControl.IsEnabled = true;
                nextControl.IsEnabled = true;
                pageControl.Update(Input);
                previousControl.Update(Input);
                nextControl.Update(Input);
                if (pageControl.WasClicked || nextControl.WasClicked) SetPage(page + 1);
                if (previousControl.WasClicked) SetPage(page - 1);
                return false;
            }

            protected override void Update(GameTime time)
            {
                clock += time.DeltaSeconds;
                if (UpdateNavigation()) return;
                foreach (var gesture in Input.Gestures)
                {
                    lastGesture = gesture.Type.ToString();
                    switch (gesture.Type)
                    {
                        case GestureType.Tap:
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
                                else if (cameraClampToggle.Contains(gesture.Position)) cameraBounded = !cameraBounded;
                                break;
                            }
                            if (page == 1 && viewportModeButton.Contains(gesture.Position))
                            {
                                GraphicsDevice.ViewportScaling = GraphicsDevice.ViewportScaling == ViewportScalingMode.Fit
                                    ? ViewportScalingMode.Fill : ViewportScalingMode.Fit;
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
                clickButton.IsEnabled = page == 4 && !buttonDisabled;
                enableButton.IsEnabled = page == 4; moveButton.IsEnabled = page == 4;
                SetClickButtonBounds();
                clickButton.Update(Input); enableButton.Update(Input); moveButton.Update(Input);
                scenePause.IsEnabled = sceneHide.IsEnabled = sceneAttach.IsEnabled = page == 5;
                scenePause.Update(Input); sceneHide.Update(Input); sceneAttach.Update(Input);
                debugToggle.IsEnabled = debugRotate.IsEnabled = debugReflect.IsEnabled = debugView.IsEnabled = page == 6;
                debugToggle.Update(Input); debugRotate.Update(Input); debugReflect.Update(Input); debugView.Update(Input);
                themeDemoButton.IsEnabled = page == 11;
                themeDemoSlider.IsEnabled = page == 11;
                themeDemoButton.Update(Input);
                themeDemoSlider.Update(Input);
                if (page >= 7) { UpdateAdvancedPage(time); return; }
                if (page == 6)
                {
                    if (debugToggle.WasClicked) debugShown = !debugShown;
                    if (debugRotate.WasClicked) debugTransform.Rotation += System.MathF.PI / 8;
                    if (debugReflect.WasClicked) debugTransform.Scale.X = -debugTransform.Scale.X;
                    if (debugView.WasClicked) debugUseCamera = !debugUseCamera;
                    if (Input.TryGetPointer(out var point) && point.Y >= 160 && point.Y < 400)
                        debugTarget = debugUseCamera ? debugCamera.ScreenToWorld(point, GraphicsDevice.ViewSize) : point;
                    var direction = debugTarget - new Vector2(40, 270);
                    if (direction.LengthSquared() > 0.0001f) debugRay = new Ray2D(new Vector2(40, 270), direction);
                    debugHit = debugRay.Intersects(new Circle(debugTransform.Position, 36), out _);
                    return;
                }
                if (page == 5)
                {
                    if (scenePause.WasClicked) sceneMotion.IsEnabled = !sceneMotion.IsEnabled;
                    if (sceneHide.WasClicked) sceneActor.IsVisible = !sceneActor.IsVisible;
                    if (sceneAttach.WasClicked)
                    {
                        if (sceneActor.Scene is null) scene.Add(sceneActor); else scene.Remove(sceneActor);
                    }
                    scene.Update(time); return;
                }
                if (page == 4)
                {
                    if (clickButton.WasClicked) buttonClicks++;
                    if (enableButton.WasClicked) { buttonDisabled = !buttonDisabled; clickButton.IsEnabled = !buttonDisabled; }
                    if (moveButton.WasClicked) { buttonMoved = !buttonMoved; clickButton.Cancel(); SetClickButtonBounds(); }
                    return;
                }
                if (page == 3)
                {
                    if (radius.WasChanged || level.WasChanged) sliderChanges++;
                    return;
                }

                if (page == 2)
                {
                    if (Input.TryGetPointer(out var touch) && touch.Y >= 120 && !cameraClampToggle.Contains(touch))
                        cameraMarker = camera.ScreenToWorld(touch, GraphicsDevice.ViewSize);
                    camera.Follow(cameraMarker, 5, time.DeltaSeconds);
                    if (cameraBounded)
                        camera.ClampToWorld(new RectangleF(0, 0, 2000, 2000), GraphicsDevice.ViewSize);
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

                GraphicsDevice.Clear(new Color(13, 19, 31));
                if (menuOpen) { DrawIndex(); return; }
                DrawHeader();
                if (page == 0) DrawDevices();
                else if (page == 1) DrawGraphics();
                else if (page == 2) DrawCamera();
                else if (page == 3) DrawSliders();
                else if (page == 4) DrawButtons();
                else if (page == 5) DrawScene(time);
                else if (page == 6) DrawDebug();
                else DrawAdvancedPage();
                DrawFooter();
            }


            Vector2 TileCenter(GridPoint cell) => tileMap.CellBounds(cell, tileOrigin).Center;

            void UpdateAdvancedPage(GameTime time)
            {
                if (page == 7)
                {
                    foreach (var gesture in Input.Gestures)
                    {
                        if (gesture.Type != GestureType.Tap) continue;
                        if (fxPauseButton.Contains(gesture.Position))
                        {
                            fxPaused = !fxPaused;
                            if (fxPaused) { fxTween.Pause(); fxAnimator.Pause(); }
                            else { fxTween.Resume(); fxAnimator.Resume(); }
                        }
                        else if (fxBurstButton.Contains(gesture.Position)) fxParticles.Burst(50, fxPosition);
                        else if (gesture.Position.Y >= 150 && gesture.Position.Y < 560)
                        {
                            fxFrom = fxPosition;
                            fxTarget = gesture.Position;
                            fxTween.Restart();
                            if (fxPaused) fxTween.Pause();
                            fxParticles.Burst(24, fxTarget);
                        }
                    }
                    if (!fxPaused)
                    {
                        fxTween.Update(time.DeltaSeconds);
                        fxAnimator.Update(time.DeltaSeconds);
                        fxPosition = fxTween.Value(fxFrom, fxTarget);
                        fxParticles.Position = fxPosition;
                        fxParticles.Update(time.DeltaSeconds);
                    }
                }
                else if (page == 8)
                {
                    foreach (var gesture in Input.Gestures)
                    {
                        if (gesture.Type != GestureType.Tap) continue;
                        if (tileZoomButton.Contains(gesture.Position))
                            tileCamera.Zoom = tileCamera.Zoom >= 2 ? 1 : tileCamera.Zoom * 2;
                        else if (tileRotateButton.Contains(gesture.Position))
                        {
                            tileCameraRotated = !tileCameraRotated;
                            tileCamera.Rotation = tileCameraRotated ? System.MathF.PI / 4 : 0;
                        }
                        else if (gesture.Position.Y >= 150 && gesture.Position.Y < 480)
                        {
                            var world = tileCamera.ScreenToWorld(gesture.Position, GraphicsDevice.ViewSize);
                            var cell = tileMap.WorldToCell(world, tileOrigin);
                            if (!tileMap.IsBlocked(cell))
                                tileRoute = tileGrid.FindPath(new GridPoint(0, 0), cell, tilePath);
                        }
                    }
                }
                else if (page == 9)
                {
                    if (Input.TryGetPointer(out var pointer) && pointer.Y >= 145 && pointer.Y < 520)
                        collisionDesired = pointer;
                    collisionBody[0] = collisionDesired;
                    collisionBody[1] = collisionDesired + new Vector2(32, 0);
                    collisionBody[2] = collisionDesired + new Vector2(32, 32);
                    collisionBody[3] = collisionDesired + new Vector2(0, 32);
                    collisionResolved = collisionDesired;
                    collisionPush = Vector2.Zero;
                    if (Geometry.SatOverlap(collisionBody, collisionWall, out collisionPush))
                        collisionResolved += collisionPush;
                }
                else if (page == 10)
                {
                    foreach (var gesture in Input.Gestures)
                    {
                        if (gesture.Type != GestureType.Tap) continue;
                        if (fontScaleButton.Contains(gesture.Position)) { score = (score + 1) % 10000; bitmapScale = bitmapScale == 2 ? 3 : 2; }
                        if (skinScaleButton.Contains(gesture.Position)) skinScale = skinScale == 1 ? 2 : 1;
                    }
                }
                else if (page == 11)
                {
                    foreach (var gesture in Input.Gestures)
                    {
                        if (gesture.Type != GestureType.Tap) continue;
                        if (themePickDark.Contains(gesture.Position)) paletteIndex = 0;
                        else if (themePickLight.Contains(gesture.Position)) paletteIndex = 1;
                        else if (themePickContrast.Contains(gesture.Position)) paletteIndex = 2;
                    }
                    if (themeDemoButton.WasClicked) themeClicks++;
                }
            }

            void DrawAdvancedPage()
            {
                if (page == 7) DrawAnimationParticles();
                else if (page == 8) DrawTilemap();
                else if (page == 9) DrawCollision();
                else if (page == 10) DrawFontsAndPanels();
                else DrawThemes();
            }

            void DrawAnimationParticles()
            {
                batch.Begin(BlendState.Additive, clip: new RectangleF(0, 145, GraphicsDevice.ViewSize.X, 422));
                fxParticles.Draw(batch);
                batch.End();
                batch.Begin(sampler: SamplerState.PointClamp);
                Title("Animacao e particulas", "Tween + spritesheet + pool fixo");
                DrawAction(fxPauseButton, fxPaused ? "RETOMAR" : "PAUSAR");
                DrawAction(fxBurstButton, "EXPLOSAO");
                batch.Cross(fxTarget, 11, Color.Yellow, 2);
                batch.Draw(fxAnimator.Clip.Texture,
                    new RectangleF(fxPosition.X - 24, fxPosition.Y - 40, 48, 80),
                    fxAnimator.Source, Color.White, 0, Vector2.Zero);
                batch.DrawString(font, $"Quadro {fxAnimator.FrameIndex + 1}  FX: {fxParticles.Count}/192",
                    new Vector2(24, 542), Color.White, 1.5f);
                batch.DrawString(font, "Toque no campo para mover e emitir.", new Vector2(24, 574), Color.White, 1.35f);
                batch.End();
            }

            void DrawTilemap()
            {
                batch.Begin(tileCamera, clip: new RectangleF(0, 150, GraphicsDevice.ViewSize.X, 340));
                tileMap.Draw(batch, tileTexture, tileCamera.GetWorldViewBounds(GraphicsDevice.ViewSize), tileOrigin, Color.White);
                if (tileRoute.Status == PathStatus.Found)
                    for (int i = 1; i < tileRoute.Length; i++)
                        batch.Line(TileCenter(tilePath[i - 1]), TileCenter(tilePath[i]), Color.Yellow, 4);
                batch.Circle(TileCenter(new GridPoint(0, 0)), 8, Color.Yellow, 2);
                batch.End();
                batch.Begin();
                Title("Tilemap e busca A*", "Mapa em camadas, parede e camera");
                DrawAction(tileZoomButton, tileCamera.Zoom == 1 ? "ZOOM 2X" : "ZOOM 1X");
                DrawAction(tileRotateButton, tileCameraRotated ? "ROTACAO 45" : "ROTACAO 0");
                batch.DrawString(font, "Toque em um tile livre: calcule a rota.", new Vector2(24, 506), Color.White, 1.45f);
                batch.DrawString(font, "Tiles vermelhos sao paredes. A* desvia.", new Vector2(24, 538), Color.Yellow, 1.35f);
                batch.End();
            }

            void DrawCollision()
            {
                batch.Begin();
                Title("Colisao geometrica", "SAT: tentativa, separacao e contato");
                batch.FillRect(new RectangleF(102, 216, 156, 162), new Color(44, 54, 79));
                batch.Rect(new RectangleF(102, 216, 156, 162), Color.Yellow, 3);
                batch.Rect(new RectangleF(collisionDesired.X, collisionDesired.Y, 32, 32), Color.Red, 2);
                batch.FillRect(new RectangleF(collisionResolved.X, collisionResolved.Y, 32, 32), Color.Green);
                batch.Line(collisionDesired + new Vector2(16), collisionResolved + new Vector2(16), Color.White, 2);
                batch.DrawString(font, $"Separacao: {collisionPush.Length():0.0}", new Vector2(24, 452), Color.Yellow, 2);
                batch.DrawString(font, "Arraste o vermelho dentro do obstaculo.", new Vector2(24, 500), Color.White, 1.4f);
                batch.DrawString(font, "O corpo verde e reposicionado pelo SAT.", new Vector2(24, 530), Color.White, 1.4f);
                batch.End();
            }

            void DrawFontsAndPanels()
            {
                batch.Begin(sampler: SamplerState.PointClamp);
                Title("Fontes e paineis", "Bitmap personalizada + nine-slice");
                DrawAction(fontScaleButton, "PONTOS / FONTE");
                DrawAction(skinScaleButton, "BORDAS 1X/2X");
                batch.Draw(skin, new RectangleF(18, 166, 324, 292), Color.White, skinScale);
                batch.DrawString(font, "FONTE BITMAP", new Vector2(38, 190), Color.Yellow, 2);
                batch.DrawString(digits, score.ToString("D4") + " 0123456789", new Vector2(38, 254), Color.White, bitmapScale);
                batch.DrawString(font, $"Tamanho: {bitmapScale:0}x  /  Bordas: {skinScale:0}x", new Vector2(26, 490), Color.White, 1.5f);
                batch.DrawString(font, "Cantos preservados ao mudar a borda.", new Vector2(26, 532), Color.White, 1.4f);
                batch.End();
            }

            void DrawThemes()
            {
                var selectedTheme = themePresets[paletteIndex];
                var panel = selectedTheme.Panel;
                var textColor = selectedTheme.Text;
                batch.Begin();
                Title("Temas de interface", "Cores locais, toque e estados");
                DrawAction(themePickDark, "ESCURO");
                DrawAction(themePickLight, "CLARO");
                DrawAction(themePickContrast, "CONTRASTE");
                batch.FillRect(new RectangleF(20, 188, 320, 312), panel);
                batch.DrawString(font, "PALETA ATIVA", new Vector2(38, 214), textColor, 2);
                themeDemoButton.Draw(batch, font, "CLIQUE AQUI", selectedTheme.Button, 2);
                themeDemoSlider.Draw(batch, selectedTheme.Slider);
                batch.DrawString(font, $"Cliques {themeClicks}  Nivel {themeDemoSlider.Value:0}", new Vector2(38, 438), textColor, 1.5f);
                batch.DrawString(font, "Os controles continuam funcionais.", new Vector2(22, 530), Color.White, 1.4f);
                batch.End();
            }

            void Title(string name, string detail)
            {
                batch.DrawString(font, name, new Vector2(20, 48), Color.White, 2);
                batch.DrawString(font, detail, new Vector2(20, 122), new Color(165, 184, 205), 1.25f);
            }

            void DrawAction(RectangleF area, string label)
            {
                batch.FillRect(area, new Color(45, 91, 142));
                batch.Rect(area, new Color(91, 154, 220));
                batch.DrawString(font, label, area.Position + new Vector2(8, 14), Color.White, 1.3f);
            }

            void DrawHeader()
            {
                batch.Begin();
                batch.FillRect(new RectangleF(0, 0, GraphicsDevice.VirtualWidth, 42), new Color(24, 40, 64));
                batch.FillRect(new RectangleF(0, 40, GraphicsDevice.VirtualWidth, 2), new Color(62, 153, 201));
                batch.FillRect(pageControl.Bounds, pageControl.IsPressed ? new Color(51, 95, 146) : new Color(31, 63, 105));
                batch.DrawString(font, "LUNET LAB", new Vector2(14, 15), Color.White, 1.55f);
                batch.DrawString(font, $"{page + 1:00}/{PageCount:00}  {pageNames[page]}",
                    new Vector2(105, 16), new Color(180, 218, 244), 1.03f);
                menuControl.Draw(batch, font, menuOpen ? "FECHAR" : "INDICE",
                    TouchButtonStyle.Default, 1.05f);
                batch.End();
            }

            void DrawFooter()
            {
                batch.Begin();
                batch.FillRect(new RectangleF(0, 600, GraphicsDevice.VirtualWidth, 40), new Color(24, 40, 64));
                previousControl.Draw(batch, font, "ANTERIOR", TouchButtonStyle.Default, 1.25f);
                nextControl.Draw(batch, font, "PROXIMO", TouchButtonStyle.Default, 1.25f);
                batch.DrawString(font, "INDICE", new Vector2(153, 614), Color.White, 1.3f);
                batch.End();
            }

            void DrawIndex()
            {
                GraphicsDevice.Clear(new Color(13, 19, 31));
                batch.Begin();
                batch.FillRect(new RectangleF(0, 0, GraphicsDevice.VirtualWidth, 56), new Color(24, 40, 64));
                batch.DrawString(font, "LUNET  /  LABORATORIO", new Vector2(12, 16), Color.White, 2);
                menuControl.Draw(batch, font, "FECHAR", TouchButtonStyle.Default, 1);
                batch.DrawString(font, "ESCOLHA UM TESTE - SEM EDITAR CODIGO", new Vector2(12, 66), Color.Yellow, 1.4f);
                for (int i = 0; i < PageCount; i++)
                {
                    int col = i % 2, row = i / 2;
                    var area = new RectangleF(col == 0 ? 8 : 184, 92 + row * 77, 168, 68);
                    batch.FillRect(area, i == page ? new Color(42, 104, 143) : new Color(31, 47, 71));
                    batch.Rect(area, i == page ? Color.Yellow : new Color(63, 83, 111), 1);
                    batch.DrawString(font, $"{i + 1:00}  {pageNames[i]}", area.Position + new Vector2(8, 14), Color.White, 1.15f);
                    batch.DrawString(font, pageHints[i], area.Position + new Vector2(8, 42), new Color(160, 190, 214), 1.02f);
                }
                batch.DrawString(font, "RUN / OFFLINE  -  12 TESTES NUM SO PROJETO", new Vector2(12, 574), Color.Green, 1.2f);
                batch.End();
            }

            void DrawDebug()
            {
                if (debugUseCamera) batch.Begin(camera: debugCamera); else batch.Begin();
                batch.Draw(dot, new RectangleF(144, 234, 72, 72), null, new Color(50, 90, 140), 0, Vector2.Zero);
                if (debugShown)
                {
                    batch.Grid(new RectangleF(24, 160, 312, 224), new Vector2(28, 28), new Color(70, 75, 95));
                    batch.Polygon(debugVertices, Color.Yellow, thickness: 2, transform: debugTransform.ToMatrix());
                    batch.Axes(debugTransform, 64, Color.Red, Color.Green, 3);
                    batch.Ray(debugRay, 280, debugHit ? Color.Green : Color.Red, 2);
                    batch.Cross(debugTarget, 8, Color.White, 2);
                }
                batch.End();
                batch.Begin();
                batch.DrawString(font, "Depuracao visual", new Vector2(24, 64), Color.White, 2);
                batch.DrawString(font, "Toque na area para apontar o raio.", new Vector2(24, 104), Color.White, 1.3f);
                batch.DrawString(font, debugHit ? "Raio atinge o circulo: SIM" : "Raio atinge o circulo: NAO", new Vector2(24, 128), debugHit ? Color.Green : Color.Red, 1.4f);
                debugToggle.Draw(batch, font, debugShown ? "Ocultar debug" : "Mostrar debug", TouchButtonStyle.Default, 2);
                debugRotate.Draw(batch, font, "Girar", TouchButtonStyle.Default, 2);
                debugReflect.Draw(batch, font, "Espelhar X", TouchButtonStyle.Default, 1.5f);
                debugView.Draw(batch, font, debugUseCamera ? "Camera: ligada" : "Camera: desligada", TouchButtonStyle.Default, 2);
                batch.End();
            }

            void DrawScene(GameTime time)
            {
                batch.Begin();
                batch.DrawString(font, "Cena e componentes", new Vector2(24, 64), Color.White, 2);
                batch.DrawString(font, "Movimento e desenho separados.", new Vector2(24, 100), Color.White, 1.4f);
                batch.DrawString(font, "Pausar nao oculta. Ocultar nao pausa.", new Vector2(24, 124), Color.White, 1.3f);
                batch.Rect(new RectangleF(40, 190, 280, 100), Color.White);
                scene.Draw(batch, time);
                scenePause.Draw(batch, font, sceneMotion.IsEnabled ? "Pausar movimento" : "Retomar movimento", TouchButtonStyle.Default, 2);
                sceneHide.Draw(batch, font, sceneActor.IsVisible ? "Ocultar entidade" : "Mostrar entidade", TouchButtonStyle.Default, 2);
                sceneAttach.Draw(batch, font, sceneActor.Scene is null ? "Reanexar entidade" : "Remover da cena", TouchButtonStyle.Default, 2);
                batch.DrawString(font, $"Entidades na cena: {scene.Count}", new Vector2(24, 548), Color.Yellow, 1.5f);
                batch.End();
            }

            void SetClickButtonBounds()
            {
                clickButton.Bounds = LayoutRect.Fixed(new Vector2(0.5f, 0), new Vector2(312, 52),
                    new Vector2(0.5f, 0), new Vector2(0, buttonMoved ? 438 : 174))
                    .GetBounds(new RectangleF(0, 0, GraphicsDevice.VirtualWidth, GraphicsDevice.VirtualHeight));
            }

            void DrawButtons()
            {
                batch.Begin();
                batch.DrawString(font, "Botoes de UI", new Vector2(24, 64), Color.White, 2);
                batch.DrawString(font, "Pressione e solte dentro para contar.", new Vector2(24, 100), Color.White, 1.4f);
                batch.DrawString(font, "Arraste para fora. Mova o botao.", new Vector2(24, 124), Color.White, 1.4f);
                clickButton.Draw(batch, font, "Somar clique", TouchButtonStyle.Default, 2);
                enableButton.Draw(batch, font, buttonDisabled ? "Habilitar" : "Desabilitar", TouchButtonStyle.Default, 2);
                moveButton.Draw(batch, font, buttonMoved ? "Mover para cima" : "Mover para baixo", TouchButtonStyle.Default, 2);
                batch.DrawString(font, $"Cliques: {buttonClicks}", new Vector2(24, 540), Color.Yellow, 2);
                batch.DrawString(font, "Pausa e troca de pagina cancelam.", new Vector2(24, 584), Color.White, 1.4f);
                batch.End();
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
                batch.FillRect(cameraClampToggle, cameraBounded ? new Color(37, 117, 83) : new Color(52, 64, 110));
                batch.DrawString(font, cameraBounded ? "LIMITE DE MUNDO: ON" : "LIMITE DE MUNDO: OFF",
                    cameraClampToggle.Position + new Vector2(12, 16), Color.White, 1.55f);
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

                // Ajuste ao tamanho do celular: Fit pode ter barras, Fill cobre tudo com recorte proporcional.
                batch.Begin();
                bool filled = GraphicsDevice.ViewportScaling == ViewportScalingMode.Fill;
                batch.FillRect(viewportModeButton, filled ? new Color(39, 119, 86) : new Color(56, 70, 108));
                batch.DrawString(font, filled ? "TELA: FILL (sem borda; recorta laterais)"
                    : "TELA: FIT (tudo visivel; pode ter bordas)",
                    viewportModeButton.Position + new Vector2(10, 12), Color.White, 1.3f);
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
