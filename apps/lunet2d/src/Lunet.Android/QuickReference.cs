namespace Lunet.Android;

/// <summary>Referência rápida offline da API (a documentação completa gerada virá na Fase 3).</summary>
internal static class QuickReference
{
    public const string Text = """
        Lunet — referência rápida

        public sealed class MeuJogo : Game
        {
            protected override void Initialize()    // ajuste Configuration (resolução virtual, updates/s)
            protected override void LoadContent()   // crie SpriteBatch e Texture2D
            protected override void Update(GameTime time)  // passo fixo (60/s); time.DeltaSeconds
            protected override void Draw(GameTime time)    // desenho
        }

        Game
          Configuration.VirtualWidth/VirtualHeight   resolução virtual (padrão 360×640)
          Configuration.UpdatesPerSecond             padrão 60
          GraphicsDevice.Clear(Color)
          Input.TryGetPointer(out Vector2)           posição do primeiro toque, em coordenadas virtuais
          Input.Touches                              todos os toques (Id, Phase, Position)
          Log.Info / Warning / Error(string)         aparece no Console

        Texture2D
          Texture2D.CreateSolid(device, w, h, cor)
          Texture2D.CreateCircle(device, diâmetro, cor)
          Texture2D.FromPixels(device, w, h, bytes RGBA)

        Content (menu ⋯ → Importar imagem PNG copia para Content/Textures)
          Content.LoadTexture("Textures/hero.png")   PNG 8/16 bits, sem entrelaçamento; cacheado
          Content.ReadText("Data/level.json")

        SpriteBatch
          Begin()
          Draw(textura, posição, cor)
          Draw(textura, destino, origem?, cor, rotação, centro)
          End()

        Cores: Color.White, Black, Red, Green, Blue, Yellow, CornflowerBlue, new Color(r,g,b,a)
        Matemática: System.Numerics (Vector2, Matrix3x2...) e RectangleF
        """;
}
