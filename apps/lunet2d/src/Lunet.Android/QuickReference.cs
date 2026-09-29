namespace Lunet.Android;

/// <summary>Referência rápida offline da API (a documentação completa gerada virá na Fase 3).</summary>
internal static class QuickReference
{
    public const string Text = """
        Lunet — referência rápida

        public sealed class MeuJogo : Game
        {
            protected override void Initialize()    // ajuste Configuration (resolução virtual, updates/s)
            protected override void LoadContent()   // crie SpriteBatch e Texture2D
            protected override void Update(GameTime time)  // passo fixo (60/s); time.DeltaSeconds
            protected override void Draw(GameTime time)    // desenho
        }

        Game
          Configuration.VirtualWidth/VirtualHeight   resolução virtual (padrão 360×640)
          Configuration.UpdatesPerSecond             padrão 60
          GraphicsDevice.Clear(Color)
          Input.TryGetPointer(out Vector2)           posição do primeiro toque, em coordenadas virtuais
          Input.Gestures                             Tap, LongPress, Drag (Delta), Swipe (Velocity)
          Input.Touches                              todos os toques (Id, Phase, Position)
          Log.Info / Warning / Error(string)         aparece no Console

        Texture2D
          Texture2D.CreateSolid(device, w, h, cor)
          Texture2D.CreateCircle(device, diâmetro, cor)
          Texture2D.FromPixels(device, w, h, bytes RGBA, TextureFilter.Point/Linear)

        Content (menu ⋯ → Importar imagem PNG copia para Content/Textures)
          Content.LoadTexture("Textures/hero.png")   PNG 8/16 bits, sem entrelaçamento; cacheado
          Content.LoadSound("Audio/jump.wav").Play(volume, pan, pitch, loop)   efeitos curtos
          Content.ReadText("Data/level.json")

        Texto
          var font = SpriteFont.CreateDefault(GraphicsDevice);   fonte 5×7 com acentos do português
          batch.DrawString(font, "Pontos: 10", posição, cor, escala)
          font.Measure(texto, escala)                              tamanho, para centralizar

        SpriteSheet
          new SpriteSheet(textura, larguraQuadro, alturaQuadro); sheet.Frame(i)
          batch.Draw(textura, posição, sheet.Frame(i), cor)

        Gráficos avançados
          batch.Begin(BlendState.Additive, SamplerState.PointClamp, shader, clip: retângulo)
          new RenderTarget2D(GraphicsDevice, w, h); GraphicsDevice.SetRenderTarget(rt/null); rt.Texture
          Shader.FromFragmentSource(GraphicsDevice, "uniform float uX; void main(){ outColor = texture(uTex,vUv)*vColor; }")
            (disponíveis no shader: vUv, vColor, uTex, outColor); shader.SetFloat/SetVector2/SetColor
          GraphicsDevice.PixelPerfect / Density / SafeArea / Viewport;  batch.Line/Rect/FillRect/Circle (DebugDraw)

        Áudio avançado
          Audio.Master / Sfx / MusicBus (.Volume, .Muted); Audio.GetBus("Voz")
          Audio.PlayMusic(Content.LoadMusic("Audio/tema.ogg"), loop, fadeInSeg); Audio.StopMusic(fadeOutSeg)
          instância.FadeTo(volume, seg, pararNoFim)

        Controle, giroscópio e vibração
          Input.IsButtonDown/IsButtonPressed(GamepadButtons.A); Input.Gamepad.LeftStick/RightStick/LeftTrigger
          Input.Gyroscope (rad/s)      Haptics.Vibrate(ms, intensidade)

        Salvamento
          Save.Save("chave", objeto);  Save.Load("chave", valorPadrão);  Save.Exists / Delete

        Teclado e sensores
          Input.IsKeyDown(Keys.Space) / IsKeyPressed(...)          teclado físico
          Input.Accelerometer                                      Vector3 em m/s²

        Serviços e tempo
          Services.Get<T>() / Add / TryGet          serviços do jogo
          Dispatcher.Post(ação)                     roda na thread do jogo (seguro de qualquer thread)
          Timers.After(s, ação) / Every(s, ação)    devolve TimerHandle.Cancel()
          new ObjectPool<T>(criar, reiniciar)       Get() / Return(item)

        Gestos extras: GestureType.DoubleTap, Pinch (Scale), Rotate (Rotation)
        VirtualStick(centro, raio).Update(Input) → Direction (0–1) · VirtualButton(círculo).IsDown/WasPressed

        Geometria: Transform2D, Ray2D.Intersects(Circle/RectangleF), Geometry.SatOverlap/PolygonContains/DistanceToSegment

        Utilidades
          MathEx.Lerp / Remap / SmoothStep / MoveToward / AngleDifference
          new RandomSource(semente).NextFloat/NextInt/NextDirection   determinístico
          new Circle(centro, raio).Contains/Intersects; RectangleF.Intersects

        SpriteBatch
          Begin()
          Draw(textura, posição, cor)
          Draw(textura, destino, origem?, cor, rotação, centro)
          End()

        Cores: Color.White, Black, Red, Green, Blue, Yellow, CornflowerBlue, new Color(r,g,b,a)
        Matemática: System.Numerics (Vector2, Matrix3x2...) e RectangleF
        """;
}
