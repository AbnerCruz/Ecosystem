using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class SmokeGame : Game
{
    private SpriteBatch _batch = null!;
    private Texture2D _dot = null!;
    private Vector2 _position;

    protected override void Initialize()
    {
        var rng = new RandomSource(42);
        if (rng.NextUInt64() != 0x15780b2e0c2ec716UL ||
            new RandomField(0).UInt64At(0, 0) != 0xe169c58d6627e8d5UL ||
            !object.ReferenceEquals(Rng, Context.Rng) || object.ReferenceEquals(Rng, VisualRng))
            throw new System.InvalidOperationException("A implementação de RNG divergiu no host do APK.");
        var state = rng.CaptureState();
        if (rng.NextUInt32() != RandomSource.FromState(state).NextUInt32())
            throw new System.InvalidOperationException("O estado de RNG não reproduziu a sequência no APK.");
    }

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
