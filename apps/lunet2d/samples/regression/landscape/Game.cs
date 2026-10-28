using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class RegressionLandscape : Game
{
    private SpriteBatch batch = null!;
    private Texture2D marker = null!;
    private Vector2 position = new(320, 180);

    protected override void Initialize()
    {
        Configuration.VirtualWidth = 640;
        Configuration.VirtualHeight = 360;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        marker = Texture2D.CreateCircle(GraphicsDevice, 32, Color.Yellow);
        Log.Info("REGRESSION:landscape");
    }

    protected override void Update(GameTime time)
    {
        if (Input.TryGetPointer(out var pointer))
            position = Vector2.Lerp(position, pointer, MathF.Min(1f, 8f * time.DeltaSeconds));
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(new Color(28, 38, 64));
        batch.Begin(sampler: SamplerState.PointClamp);
        batch.Draw(marker, position - new Vector2(16, 16), Color.White);
        batch.End();
    }

    protected override void UnloadContent() => marker?.Dispose();
}
