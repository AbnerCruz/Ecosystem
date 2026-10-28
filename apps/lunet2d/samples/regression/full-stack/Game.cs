using System.Numerics;
using Lunet;
using Lunet.Audio;
using Lunet.Graphics;
using Lunet.Physics;
using Lunet.UI;

public sealed class RegressionFullStack : Game
{
    private SpriteBatch batch = null!;
    private SpriteFont font = null!;
    private Texture2D tiles = null!;
    private TileMap map = null!;
    private SoundEffect trigger = null!;
    private PhysicsWorld physics = null!;
    private readonly TouchButton button = new(new RectangleF(20, 540, 140, 54));

    protected override void Initialize()
    {
        Configuration.VirtualWidth = 360;
        Configuration.VirtualHeight = 640;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        font = SpriteFont.CreateDefault(GraphicsDevice);
        tiles = Content.LoadTexture("Textures/tiles.png", TextureFilter.Point);
        map = Content.LoadTileMap("Data/arena.tilemap.json");
        trigger = Content.LoadSound("Audio/trigger.wav");

        physics = new PhysicsWorld(new Vector2(0, 9.8f));
        var floor = physics.CreateBody(PhysicsBodyType.Static, new Vector2(3f, 7f));
        floor.AddFixture(Collider2D.Box(8f, 0.5f));
        var crate = physics.CreateBody(PhysicsBodyType.Dynamic, new Vector2(3f, 2f));
        crate.AddFixture(Collider2D.Box(0.6f, 0.6f));

        Save.Save("launches", Save.Load("launches", 0) + 1);
        Log.Info("REGRESSION:full-stack");
    }

    protected override void Update(GameTime time)
    {
        physics.Step(time.DeltaSeconds);
        button.Update(Input);
        if (button.WasClicked)
        {
            trigger.Play();
            Save.Save("presses", Save.Load("presses", 0) + 1);
        }
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(16, 26, 40));
        batch.Begin(sampler: SamplerState.PointClamp);
        map.Draw(batch, tiles, new RectangleF(12, 80, 128, 128),
            new Vector2(12, 80), Color.White);
        batch.Draw(tiles, new RectangleF(188, 126, 96, 96),
            null, Color.White, 0f, Vector2.Zero);
        batch.DrawString(font, "Regression full stack", new Vector2(12, 28), Color.White);
        button.Draw(batch, font, "Sound + save", TouchButtonStyle.Default);
        batch.End();
    }

    protected override void UnloadContent()
    {
        physics?.Dispose();
        font?.Dispose();
    }
}
