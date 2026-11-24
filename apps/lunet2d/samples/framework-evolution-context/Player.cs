using static Lunet.Context;

public sealed class Player
{
    public Vector2 Feet { get; private set; } = new(180, 280);
    private Texture2D texture = null!;
    public void Load() => texture = Texture2D.CreateCircle(Graphics, 32, Color.Yellow);
    public void Update(Camera2D camera)
    {
        if (Input.TryGetPointer(out var pointer)) Feet = camera.ScreenToWorld(pointer, Graphics.ViewSize);
    }
    public void Draw(SpriteBatch batch) => batch.Draw(texture, Feet - new Vector2(16, 32), Color.White, order: Feet.Y);
    public void Dispose() => texture.Dispose();
}
