# Jogo de validação conjunta do Framework Advanced

Este jogo é uma prova reproduzível para o gate da Fase 4, não um novo modelo ou um dos jogos oficiais da Fase 11. Combina mapa em camadas, paredes físicas, sensores, câmera, animação, partículas, UI com tema, fonte e áudio. A suíte compila exatamente o código abaixo pelo compilador de jogos e executa vitória, derrota, reinício e liberação de recursos. Isso não substitui o teste no aparelho nem afirma aprovação do gate.

Offline, crie um **novo Coletor de moedas** chamado `GateF4` (ele fornece `Content/Audio/beep.wav`). Substitua a classe principal por este código. Aperte Run. Use os quatro botões para coletar os três círculos amarelos em 45 segundos; evite o círculo vermelho. Paredes bloqueiam o personagem. Reiniciar funciona durante a partida e depois de vitória/derrota. Não altere seus projetos existentes.

```csharp
using System;
using System.Numerics;
using System.Text.Json;
using Lunet;
using Lunet.Audio;
using Lunet.Graphics;
using Lunet.Pathfinding;
using Lunet.Physics;
using Lunet.UI;

public sealed class GateF4 : Game
{
    const float Unit = 32;
    readonly Camera2D camera = new() { Position = new(48, 48) };
    readonly Vector2[] targets = { new(272, 48), new(272, 304), new(48, 304) };
    readonly bool[] collected = new bool[3];
    readonly Fixture2D[] sensors = new Fixture2D[3];
    readonly Contact[] events = new Contact[16];
    readonly TouchButton left = new(new(16, 484, 72, 52));
    readonly TouchButton right = new(new(168, 484, 72, 52));
    readonly TouchButton up = new(new(92, 428, 72, 52));
    readonly TouchButton down = new(new(92, 540, 72, 52));
    readonly TouchButton restart = new(new(220, 556, 92, 48));
    readonly UiTheme theme = UiTheme.HighContrast;
    PhysicsWorld world = null!;
    RigidBody2D player = null!, hazard = null!;
    Fixture2D playerFixture = null!, hazardFixture = null!;
    TileMap map = null!;
    Texture2D tiles = null!, sheet = null!, dot = null!;
    SpriteFont font = null!;
    SpriteBatch batch = null!;
    SpriteAnimator animator = null!;
    ParticleEmitter particles = null!;
    SoundEffect beep = null!;
    int score, status; // 0 jogando, 1 vitória, 2 derrota
    float elapsed;

    protected override void Initialize()
    {
        Configuration.VirtualWidth = 320;
        Configuration.VirtualHeight = 640;
    }

    protected override void LoadContent()
    {
        var cells = new int[10 * 12];
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 10; x++)
                cells[y * 10 + x] = x == 0 || x == 9 || y == 0 || y == 11
                    || (x == 5 && y >= 2 && y <= 9 && y != 6) ? 1 : 0;
        map = TileMap.Parse(JsonSerializer.Serialize(new {
            version = 1, texture = "Textures/wall.png", width = 10, height = 12,
            tileWidth = 32, tileHeight = 32,
            layers = new[] { new { name = "walls", collision = true, visible = true, tiles = cells } }
        }));
        // Exercita o contrato compartilhável com um futuro editor, sem I/O.
        map = TileMap.Parse(map.ToJson());
        tiles = Texture2D.CreateSolid(GraphicsDevice, 32, 32, new Color(60, 95, 140));
        dot = Texture2D.CreateCircle(GraphicsDevice, 16, Color.White);
        var pixels = new byte[32 * 16 * 4];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 32; x++)
            {
                int i = (y * 32 + x) * 4;
                pixels[i] = (byte)(x < 16 ? 80 : 180);
                pixels[i + 1] = 220; pixels[i + 2] = 255; pixels[i + 3] = 255;
            }
        sheet = Texture2D.FromPixels(GraphicsDevice, 32, 16, pixels, TextureFilter.Point);
        animator = new SpriteAnimator(SpriteAnimationClip.FromSheet(new SpriteSheet(sheet, 16, 16), new[] { 0, 1 }, 0.12));
        particles = new ParticleEmitter(dot, new ParticleSettings(0.8, 30, 90, startSize: 6, endSize: 0), 64);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        batch = new SpriteBatch(GraphicsDevice);
        beep = Content.LoadSound("Audio/beep.wav");
        Reset();
    }

    void Reset()
    {
        if (world != null) world.Dispose();
        world = new PhysicsWorld(Vector2.Zero);
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.IsBlocked(new GridPoint(x, y)))
                    world.CreateBody(PhysicsBodyType.Static, new(x + 0.5f, y + 0.5f)).AddFixture(Collider2D.Box(1, 1));
        player = world.CreateBody(PhysicsBodyType.Dynamic, new(1.5f, 1.5f));
        playerFixture = player.AddFixture(Collider2D.Circle(0.3f));
        hazard = world.CreateBody(PhysicsBodyType.Kinematic, new(2.5f, 8.5f));
        hazardFixture = hazard.AddFixture(Collider2D.Circle(0.35f), sensor: true);
        for (int i = 0; i < targets.Length; i++)
        {
            sensors[i] = world.CreateBody(PhysicsBodyType.Static, targets[i] / Unit)
                .AddFixture(Collider2D.Circle(0.4f), sensor: true);
            collected[i] = false;
        }
        score = status = 0; elapsed = 0;
        particles.Clear(); animator.Restart();
        camera.Position = new(48, 48);
    }

    protected override void Update(GameTime time)
    {
        left.Update(Input); right.Update(Input); up.Update(Input); down.Update(Input); restart.Update(Input);
        if (restart.WasClicked) { Reset(); return; }
        particles.Update(time.DeltaSeconds);
        animator.Update(time.DeltaSeconds);
        if (status != 0) return;
        elapsed += time.DeltaSeconds;
        Vector2 direction = new((right.IsPressed ? 1 : 0) - (left.IsPressed ? 1 : 0),
            (down.IsPressed ? 1 : 0) - (up.IsPressed ? 1 : 0));
        if (direction.LengthSquared() > 1) direction = Vector2.Normalize(direction);
        player.LinearVelocity = direction * 3.5f;
        hazard.LinearVelocity = new(MathF.Cos(elapsed * 1.5f) * 1.1f, 0);
        world.Step(time.DeltaSeconds);
        int count = world.CopySensorBegins(events);
        for (int e = 0; e < count; e++)
        {
            var hit = events[e];
            if (hit.A != playerFixture && hit.B != playerFixture) continue;
            var other = hit.A == playerFixture ? hit.B : hit.A;
            if (other == hazardFixture) { status = 2; break; }
            for (int i = 0; i < sensors.Length; i++)
                if (other == sensors[i] && !collected[i])
                {
                    collected[i] = true; score++;
                    particles.Burst(16, targets[i]);
                    beep.Play(0.5f, 0, 1, false);
                }
        }
        if (status == 0) status = score == 3 ? 1 : elapsed >= 45 ? 2 : 0;
        if (status != 0) player.LinearVelocity = Vector2.Zero;
        camera.Follow(player.Position * Unit, 4, time.DeltaSeconds);
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(theme.Background);
        // HUD não recebe a transformação da câmera.
        batch.Begin(camera, clip: new RectangleF(0, 0, 320, 392));
        map.Draw(batch, tiles, camera.GetWorldViewBounds(GraphicsDevice.ViewSize), Vector2.Zero, Color.White);
        for (int i = 0; i < targets.Length; i++)
            if (!collected[i]) batch.Draw(dot, targets[i] - new Vector2(8), Color.Yellow);
        batch.Draw(dot, hazard.Position * Unit - new Vector2(8), Color.Red);
        batch.Draw(sheet, player.Position * Unit - new Vector2(8), animator.Source, Color.White);
        particles.Draw(batch);
        batch.End();
        batch.Begin();
        batch.DrawString(font, "COLETE 3 / EVITE VERMELHO", new(8, 400), theme.Text);
        batch.DrawString(font, status == 1 ? "VITORIA" : status == 2 ? "DERROTA" : "EM JOGO", new(220, 432), theme.Text);
        batch.DrawString(font, "PONTOS " + score + "  TEMPO " + Math.Max(0, 45 - (int)elapsed), new(8, 616), theme.Text);
        left.Draw(batch, font, "<", theme.Button); right.Draw(batch, font, ">", theme.Button);
        up.Draw(batch, font, "^", theme.Button); down.Draw(batch, font, "v", theme.Button);
        restart.Draw(batch, font, "RESET", theme.Button);
        batch.End();
    }

    protected override void OnPause()
    {
        left.Cancel(); right.Cancel(); up.Cancel(); down.Cancel(); restart.Cancel();
    }

    protected override void UnloadContent()
    {
        world.Dispose(); tiles.Dispose(); sheet.Dispose(); dot.Dispose(); font.Dispose();
        // beep pertence ao ContentManager, liberado pelo host.
    }
}
```

Teste partida normal, vitória, derrota por tempo/obstáculo, reinício em cada estado, Pause/Step/Resume e Stop/Run. Ative Perf e registre 60 segundos com modelo/Android/versão e captura. O exemplo formata duas linhas de HUD por quadro, portanto não promete zero GC para o jogo; os pools e APIs do framework têm seus próprios testes. O contador de underruns e a conexão com Tile Studio continuam lacunas separadas, visíveis na auditoria.
