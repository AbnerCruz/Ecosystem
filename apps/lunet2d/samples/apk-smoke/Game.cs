using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class SmokeGame : Game
{
    private SpriteBatch _batch = null!;
    private Texture2D _dot = null!;
    private Vector2 _position;

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _dot = Texture2D.CreateCircle(GraphicsDevice, 32, Color.Yellow);
        _position = new Vector2(GraphicsDevice.VirtualWidth / 2f, GraphicsDevice.VirtualHeight / 2f);
        Log.Info("Standalone APK: o jogo, não a IDE, foi iniciado.");
    }

    protected override void Update(GameTime time)
    {
        if (Input.TryGetPointer(out var pointer))
            _position = Vector2.Lerp(_position, pointer, MathF.Min(1f, 9f * time.DeltaSeconds));
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _batch.Begin();
        _batch.Draw(_dot, _position - new Vector2(16, 16), Color.White);
        _batch.End();
    }

    protected override void UnloadContent() => _dot?.Dispose();
}
