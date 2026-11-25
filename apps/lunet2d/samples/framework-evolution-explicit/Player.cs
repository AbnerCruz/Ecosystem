using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Input;

public sealed class Player
{
    public Vector2 Feet { get; private set; } = new(180, 280);
    private Texture2D texture = null!;
    public void Load(GraphicsDevice graphics) => texture = Texture2D.CreateCircle(graphics, 32, Color.Yellow);
    public void Update(InputState input, Camera2D camera, Vector2 viewSize)
    {
        if (input.TryGetPointer(out var pointer)) Feet = camera.ScreenToWorld(pointer, viewSize);
    }
    public void Draw(SpriteBatch batch) => batch.Draw(texture, Feet - new Vector2(16, 32), Color.White, order: Feet.Y);
    public void Dispose() => texture.Dispose();
}
