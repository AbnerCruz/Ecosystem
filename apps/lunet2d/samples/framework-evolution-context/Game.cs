public sealed class ForestGame : Game
{
    private SpriteBatch batch = null!;
    private Texture2D trunk = null!;
    private Texture2D crown = null!;
    private SpriteFont font = null!;
    private readonly Camera2D camera = new() { Position = new Vector2(180, 320) };
    private readonly Player player = new();
    private readonly Vector2 treeFeet = new(180, 320);

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        trunk = Texture2D.CreateSolid(GraphicsDevice, 24, 80, new Color(120, 72, 40));
        crown = Texture2D.CreateCircle(GraphicsDevice, 96, new Color(40, 132, 65));
        font = SpriteFont.CreateDefault(GraphicsDevice);
        player.Load();
    }

    protected override void Update(GameTime time) => player.Update(camera);

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(38, 58, 44));
        batch.BeginSorted(camera);
        // Submission intentionally puts player first, irrespective of the visible order.
        player.Draw(batch);
        batch.SetOrder(treeFeet.Y);
        batch.Draw(trunk, treeFeet - new Vector2(12, 80), Color.White);
        batch.Draw(crown, treeFeet - new Vector2(48, 126), Color.White);
        batch.End();
        batch.Begin();
        batch.DrawString(font, "Toque para andar: atras / frente", new Vector2(8, 8), Color.White);
        batch.End();
    }

    protected override void UnloadContent()
    {
        player.Dispose();
        trunk.Dispose();
        crown.Dispose();
    }
}
