using System.Numerics;
using Lunet;
using Lunet.Graphics;

public sealed class RegressionPortrait : Game
{
    private SpriteBatch batch = null!;
    private Texture2D marker = null!;

    protected override void Initialize()
    {
        Configuration.VirtualWidth = 360;
        Configuration.VirtualHeight = 640;
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        marker = Texture2D.CreateCircle(GraphicsDevice, 48, Color.Yellow);
        Log.Info("REGRESSION:portrait");
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);
        batch.Begin(sampler: SamplerState.PointClamp);
        batch.Draw(marker, new Vector2(156, 296), Color.White);
        batch.End();
    }

    protected override void UnloadContent() => marker?.Dispose();
}
