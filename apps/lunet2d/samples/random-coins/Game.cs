public sealed class RandomCoins : Game
{
    private readonly record struct Coin(Vector2 Position, int Rarity);
    private static readonly double[] Weights = [70, 25, 5];
    private static readonly Color[] Palette = [Color.Yellow, Color.CornflowerBlue, Color.FromHex(0xD983FF)];
    private static readonly RectangleF NewSeed = new(16, 568, 158, 56);
    private static readonly RectangleF Repeat = new(186, 568, 158, 56);

    private readonly Coin[] _coins = new Coin[12];
    private readonly Vector2[] _scatter = new Vector2[90];
    private SpriteBatch _batch = null!;
    private SpriteFont _font = null!;
    private Texture2D _circle = null!;
    private RandomField _cells = null!;
    private ulong _seed;
    private string _seedLabel = "";

    protected override void Initialize()
    {
        Configuration.VirtualWidth = 360;
        Configuration.VirtualHeight = 640;
        Generate(Rng.Seed);
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _font = SpriteFont.CreateDefault(GraphicsDevice);
        _circle = Texture2D.CreateCircle(GraphicsDevice, 22, Color.White);
    }

    private void Generate(ulong seed)
    {
        _seed = seed;
        _seedLabel = $"Seed: {seed:X16}";
        var root = new RandomSource(seed);
        var positions = root.Fork("coin-positions");
        var rarities = root.Fork("coin-loot");
        var circle = root.Fork("circle");
        _cells = new RandomField(seed);
        for (int i = 0; i < _coins.Length; i++)
            _coins[i] = new Coin(positions.InsideRectangle(new RectangleF(30, 102, 300, 194)), rarities.NextWeightedIndex(Weights));
        for (int i = 0; i < _scatter.Length; i++)
            _scatter[i] = circle.InsideCircle(61);
    }

    protected override void Update(GameTime time)
    {
        foreach (var gesture in Input.Gestures)
        {
            if (gesture.Type != GestureType.Tap) continue;
            if (NewSeed.Contains(gesture.Position)) Generate(new RandomSource().Seed);
            else if (Repeat.Contains(gesture.Position)) Generate(_seed);
        }
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.FromHex(0x151925));
        _batch.Begin();
        _batch.DrawString(_font, "Moedas e sementes", new(16, 18), Color.White, 2);
        _batch.DrawString(_font, _seedLabel, new(16, 48), Color.White, 1.4f);
        _batch.DrawString(_font, "Comum 70% / Rara 25% / Lendaria 5%", new(16, 75), Color.White);
        foreach (var coin in _coins)
            _batch.Draw(_circle, coin.Position - new Vector2(11), Palette[coin.Rarity]);
        _batch.DrawString(_font, "Campo indexado", new(16, 322), Color.White);
        _batch.DrawString(_font, "Area do circulo", new(192, 322), Color.White);
        for (int y = 0; y < 9; y++)
        for (int x = 0; x < 9; x++)
        {
            var value = _cells.FloatAt(x - 4, y - 4);
            _batch.Draw(new RectangleF(18 + 14 * x, 354 + 14 * y, 12, 12), Palette[value < 0.33f ? 0 : value < 0.67f ? 1 : 2]);
        }
        foreach (var dot in _scatter)
            _batch.Draw(new RectangleF(262 + dot.X, 416 + dot.Y, 3, 3), Color.White);
        Button(NewSeed, "Nova seed");
        Button(Repeat, "Repetir seed");
        _batch.End();
    }

    private void Button(RectangleF area, string title)
    {
        _batch.Draw(area, Color.FromHex(0x283B80));
        _batch.DrawString(_font, title, area.Position + new Vector2(12, 20), Color.White, 1.4f);
    }

    protected override void UnloadContent()
    {
        _circle?.Dispose();
        _font?.Dispose();
    }
}
